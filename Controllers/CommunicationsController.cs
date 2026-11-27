using System.Security.Claims;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pressings.Models;
using Pressings.Services;

namespace Pressings.Controllers;

[Authorize(Roles = "staff")]
public sealed class CommunicationsController(AccessRepository repository, ClientCommunicationService mailer) : Controller
{
    public IActionResult Index(string? q, bool unreadOnly = false)
    {
        var emailSettings = mailer.GetEffectiveSettings();
        return View(new CommunicationsInboxViewModel
        {
            EmailConfigured = mailer.IsConfigured(emailSettings),
            SearchQuery = q ?? "",
            UnreadOnly = unreadOnly,
            ReadyOrders = repository.GetReadyOrderNotices(),
            RecentMessages = repository.GetCommunicationHistory(),
            Conversations = repository.GetMessageThreads(q, unreadOnly),
            AllClients = repository.GetClients(),
            EmailSettings = emailSettings
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SaveEmailSettings(EmailSettingsInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vérifiez l'adresse d'envoi, le serveur SMTP et le port.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            mailer.SaveSettings(input);
            TempData["Success"] = mailer.EmailConfigured
                ? "Paramètres e-mail enregistrés. Vous pouvez envoyer un message ou lancer un test."
                : "Paramètres enregistrés, mais il manque encore les identifiants SMTP.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TestEmailSettings(string testRecipient, CancellationToken cancellationToken)
    {
        if (!MailAddress.TryCreate(testRecipient?.Trim(), out _))
        {
            TempData["Error"] = "Saisissez une adresse e-mail valide pour le test.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await mailer.SendEmailAsync(testRecipient.Trim(), "Test Fresh & Care — Configuration réussie",
                "Bonjour,\n\nCeci est un message de test confirmant que l'envoi d'e-mails depuis Fresh & Care fonctionne.\n\nFresh & Care Pressing",
                cancellationToken);
            TempData["Success"] = $"E-mail de test envoyé à {testRecipient.Trim()}.";
        }
        catch (Exception ex) { TempData["Error"] = "Le test d'envoi a échoué : " + ex.Message; }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendFreeEmail(FreeEmailInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Renseignez un destinataire, un objet et un message.";
            return RedirectToAction(nameof(Index));
        }

        if (!mailer.EmailConfigured)
        {
            TempData["Error"] = "Le serveur SMTP n'est pas encore configuré. Renseignez vos paramètres e-mail ci-dessous ou utilisez Gmail.";
            return RedirectToAction(nameof(Index));
        }

        var logId = repository.CreateCommunicationLog(input.ClientId ?? 0, 0, CurrentUserId(), input.Recipient, input.Subject, input.Body);
        try
        {
            await mailer.SendEmailAsync(input.Recipient, input.Subject, input.Body, cancellationToken);
            repository.UpdateCommunicationLog(logId, "envoyé");
            if (input.ClientId is > 0)
            {
                repository.SendClientMessage(input.ClientId.Value, "staff", CurrentUserId(), new ClientMessageInput
                {
                    Subject = input.Subject,
                    Body = input.Body
                });
            }
            TempData["Success"] = $"E-mail envoyé avec succès à {input.Recipient}.";
        }
        catch (Exception ex)
        {
            try { repository.UpdateCommunicationLog(logId, "échec", ex.Message); } catch { }
            TempData["Error"] = "Échec de l'envoi SMTP : " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Thread(int clientId)
    {
        var client = repository.GetClient(clientId);
        if (client is null) return NotFound();
        repository.MarkConversationRead(clientId, "staff");
        return View(new ClientConversationViewModel
        {
            Client = client,
            IsStaffView = true,
            Messages = repository.GetClientMessages(clientId),
            Draft = new ClientMessageInput()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int clientId, ClientMessageInput input, bool sendEmailCopy = false, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Saisissez un sujet et un message avant l’envoi.";
            return RedirectToAction(nameof(Thread), new { clientId });
        }
        try
        {
            repository.SendClientMessage(clientId, "staff", CurrentUserId(), input);
            var msg = "Votre message a été envoyé et est visible dans l’espace client.";
            if (sendEmailCopy && mailer.EmailConfigured)
            {
                var client = repository.GetClient(clientId);
                if (client is not null && !string.IsNullOrWhiteSpace(client.Email))
                {
                    var logId = repository.CreateCommunicationLog(clientId, 0, CurrentUserId(), client.Email, input.Subject, input.Body);
                    try
                    {
                        await mailer.SendEmailAsync(client.Email, input.Subject, input.Body, cancellationToken);
                        repository.UpdateCommunicationLog(logId, "envoyé");
                        msg += $" Une copie a aussi été envoyée par e-mail à {client.Email}.";
                    }
                    catch (Exception ex)
                    {
                        try { repository.UpdateCommunicationLog(logId, "échec", ex.Message); } catch { }
                        msg += " (La copie e-mail n'a pas pu partir : " + ex.Message + ")";
                    }
                }
            }
            TempData["Success"] = msg;
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Thread), new { clientId });
    }

    [HttpGet]
    public IActionResult Compose(int id)
    {
        var order = repository.GetOrderDetails(id);
        if (order is null) return NotFound();
        if (!order.Items.Any(item => item.ItemStatus == "prêt"))
        {
            TempData["Error"] = "Le message de disponibilité est réservé aux tickets qui ont au moins un article prêt.";
            return RedirectToAction("Details", "Orders", new { id });
        }

        var input = MakeReadyEmail(order);
        PrepareCompose(input, order.Client.FirstName + " " + order.Client.LastName);
        return View(input);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(ComposeEmailInput input, CancellationToken cancellationToken)
    {
        var order = repository.GetOrderDetails(input.OrderId);
        if (order is null) return NotFound();
        if (!order.Items.Any(item => item.ItemStatus == "prêt"))
        {
            TempData["Error"] = "Aucun article de ce ticket n’est encore prêt à être retiré.";
            return RedirectToAction("Details", "Orders", new { id = input.OrderId });
        }
        if (!ModelState.IsValid)
        {
            PrepareCompose(input, order.Client.FirstName + " " + order.Client.LastName);
            return View("Compose", input);
        }
        if (!mailer.EmailConfigured)
        {
            ModelState.AddModelError(string.Empty, "L’envoi direct n’est pas configuré. Utilisez le bouton de votre messagerie ou configurez le serveur SMTP.");
            PrepareCompose(input, order.Client.FirstName + " " + order.Client.LastName);
            return View("Compose", input);
        }

        var logId = repository.CreateCommunicationLog(order.Client.ClientId, order.Order.OrderId, CurrentUserId(), input.Recipient, input.Subject, input.Body);
        try
        {
            await mailer.SendEmailAsync(input.Recipient, input.Subject, input.Body, cancellationToken);
            repository.UpdateCommunicationLog(logId, "envoyé");
            TempData["Success"] = $"E-mail envoyé à {input.Recipient}.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            try { repository.UpdateCommunicationLog(logId, "échec", ex.Message); } catch { }
            ModelState.AddModelError(string.Empty, "L’e-mail n’a pas pu être envoyé. Vérifiez le SMTP ou ouvrez votre messagerie avec le brouillon préparé.");
            PrepareCompose(input, order.Client.FirstName + " " + order.Client.LastName);
            ViewBag.SendError = ex.Message;
            return View("Compose", input);
        }
    }

    [HttpGet]
    public IActionResult WhatsApp(int id)
    {
        var order = repository.GetOrderDetails(id);
        if (order is null) return NotFound();
        var readyItems = order.Items.Where(item => item.ItemStatus == "prêt").ToList();
        var message = readyItems.Count > 0
            ? $"Bonjour {order.Client.FirstName}, vos {readyItems.Count} article(s) du ticket {order.Order.TicketNo} sont prêts chez Fresh & Care. Vous pouvez passer les retirer. À bientôt !"
            : $"Bonjour {order.Client.FirstName}, nous vous contactons au sujet du ticket {order.Order.TicketNo} chez Fresh & Care. À bientôt !";
        var url = ClientCommunicationLinks.WhatsApp(
            string.IsNullOrWhiteSpace(order.Client.WhatsApp) ? order.Client.Phone : order.Client.WhatsApp, message);
        return url is null ? BadRequest("Numéro WhatsApp absent ou invalide.") : Redirect(url);
    }

    private ComposeEmailInput MakeReadyEmail(OrderDetailViewModel order)
    {
        var ready = order.Items.Where(item => item.ItemStatus == "prêt").ToList();
        var itemList = string.Join("\n", ready.Select(item => $"• {item.CategoryName}{(string.IsNullOrWhiteSpace(item.Description) ? "" : " — " + item.Description)}"));
        var subject = $"Vos vêtements sont prêts — ticket {order.Order.TicketNo}";
        var body = $"Bonjour {order.Client.FirstName},\n\nNous vous informons que vos articles sont prêts à être retirés chez Fresh & Care.\n\nTicket : {order.Order.TicketNo}\nArticles prêts :\n{itemList}\n\nReste à payer : {order.Order.Remaining:N0} FCFA\n\nVous pouvez passer les récupérer pendant nos heures d’ouverture.\n\nFresh & Care — Propreté, Élégance, Confiance\nYaoundé";
        return new ComposeEmailInput { OrderId = order.Order.OrderId, Recipient = order.Client.Email, Subject = subject, Body = body };
    }

    private void PrepareCompose(ComposeEmailInput input, string clientName)
    {
        ViewBag.ClientName = clientName;
        ViewBag.TicketNo = repository.GetOrderDetails(input.OrderId)?.Order.TicketNo ?? "—";
        ViewBag.EmailConfigured = mailer.EmailConfigured;
        ViewBag.MailtoUrl = ClientCommunicationLinks.Mailto(input.Recipient, input.Subject, input.Body);
        ViewBag.GmailUrl = ClientCommunicationLinks.GmailCompose(input.Recipient, input.Subject, input.Body);
    }

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
