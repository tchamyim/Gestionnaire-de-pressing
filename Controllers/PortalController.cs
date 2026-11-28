using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pressings.Models;
using Pressings.Services;

namespace Pressings.Controllers;

public sealed class PortalController(AccessRepository repository, IAntiforgery antiforgery) : Controller
{
    private readonly PasswordHasher<PortalAccount> _hasher = new();

    [AllowAnonymous, HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", User.IsInRole("client") ? "Portal" : "Dashboard");
        return View(new PortalLoginInput());
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(PortalLoginInput input)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", User.IsInRole("client") ? "Portal" : "Dashboard");

        try { await antiforgery.ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return BadRequest(); }

        if (!ModelState.IsValid) return View(input);
        var account = repository.FindPortalAccountByPhone(input.Phone);
        if (account is null || string.IsNullOrWhiteSpace(account.PinHash) ||
            _hasher.VerifyHashedPassword(account, account.PinHash, input.Pin) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Numéro ou code client incorrect.");
            return View(input);
        }
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.ClientId.ToString()),
            new Claim(ClaimTypes.Name, $"{account.FirstName} {account.LastName}"),
            new Claim(ClaimTypes.Role, "client")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "client")]
    public IActionResult Index()
    {
        var id = CurrentClientId();
        var client = repository.GetClient(id);
        if (client is null) return NotFound();
        return View(new PortalHomeViewModel
        {
            Client = client,
            Balance = repository.GetClientBalance(id),
            Orders = repository.GetOrderSummaries(id),
            UnreadMessages = repository.GetUnreadMessageCount(id, "client")
        });
    }

    [Authorize(Roles = "client")]
    public IActionResult Conversation()
    {
        var id = CurrentClientId();
        var client = repository.GetClient(id);
        if (client is null) return NotFound();
        repository.MarkConversationRead(id, "client");
        return View(new ClientConversationViewModel
        {
            Client = client,
            IsStaffView = false,
            Messages = repository.GetClientMessages(id),
            Draft = new ClientMessageInput()
        });
    }

    [Authorize(Roles = "client"), HttpPost, ValidateAntiForgeryToken]
    public IActionResult Message(ClientMessageInput input)
    {
        var id = CurrentClientId();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Écrivez un message avant de l’envoyer.";
            return RedirectToAction(nameof(Conversation));
        }
        try
        {
            repository.SendClientMessage(id, "client", id, input);
            TempData["Success"] = "Votre message a bien été envoyé au pressing.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Conversation));
    }

    [Authorize(Roles = "client")]
    public IActionResult Order(int id)
    {
        var order = repository.GetOrderDetails(id);
        return order is null || order.Client.ClientId != CurrentClientId() ? NotFound() : View(order);
    }

    [Authorize(Roles = "client"), HttpPost, ValidateAntiForgeryToken]
    public IActionResult ChangePin(ChangePinInput input)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Le nouveau code doit contenir au moins 8 caractères."; return RedirectToAction(nameof(Index)); }
        var id = CurrentClientId();
        var client = repository.GetClient(id);
        var account = repository.FindPortalAccountByPhone(client?.Phone ?? "");
        if (account is null || string.IsNullOrWhiteSpace(account.PinHash) || _hasher.VerifyHashedPassword(account, account.PinHash, input.CurrentPin) == PasswordVerificationResult.Failed)
        {
            TempData["Error"] = "Le code actuel est incorrect.";
            return RedirectToAction(nameof(Index));
        }
        repository.ResetPortalPin(id, _hasher.HashPassword(account, input.NewPin));
        TempData["Success"] = "Votre code client a été modifié.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "client"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private int CurrentClientId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
