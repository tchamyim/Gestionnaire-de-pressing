using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pressings.Models;
using Pressings.Services;

namespace Pressings.Controllers;

[Authorize(Roles = "staff")]
public sealed class OrdersController(AccessRepository repository) : Controller
{
    public IActionResult Index(string? q) { ViewBag.Query = q ?? ""; return View(repository.SearchOrders(q)); }

    [HttpGet]
    public IActionResult Create()
    {
        FillOrderForm();
        return View(new NewOrderInput { Items = [NewBlankItem()] });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(NewOrderInput input, string? addLine)
    {
        if (string.Equals(addLine, "true", StringComparison.OrdinalIgnoreCase))
        {
            input.Items.Add(NewBlankItem());
            ModelState.Clear();
            FillOrderForm();
            return View(input);
        }
        var selectedItems = input.Items.Where(i => i.CategoryId > 0).ToList();
        if (selectedItems.Count == 0) ModelState.AddModelError(nameof(input.Items), "Ajoutez au moins un article.");
        if (!ModelState.IsValid) { FillOrderForm(); return View(input); }
        input.Items = selectedItems;
        try
        {
            var id = repository.CreateOrder(input, CurrentUserId());
            TempData["Success"] = "Dépôt enregistré et ticket unique généré.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            FillOrderForm();
            return View(input);
        }
    }

    public IActionResult Details(int id)
    {
        var order = repository.GetOrderDetails(id);
        return order is null ? NotFound() : View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult UpdateItem(int id, int itemId, UpdateItemInput input)
    {
        try
        {
            repository.UpdateItem(id, itemId, input, CurrentUserId());
            TempData["Success"] = "Statut de l’article enregistré.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult UpdateAllStatus(int id, string status, string? storageLocation)
    {
        try
        {
            repository.UpdateAllOrderItemsStatus(id, status, storageLocation, CurrentUserId());
            TempData["Success"] = $"Tous les articles restants ont été marqués « {status} ».";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public IActionResult ThermalTicket(int id)
    {
        var order = repository.GetOrderDetails(id);
        return order is null ? NotFound() : View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Pay(int id, PaymentInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Saisissez un montant valide.";
            return RedirectToAction(nameof(Details), new { id });
        }
        try
        {
            var paymentId = repository.RecordPayment(id, input, CurrentUserId());
            TempData["Success"] = "Paiement enregistré. Le solde de caisse a été recalculé.";
            TempData["LastPaymentId"] = paymentId;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private void FillOrderForm()
    {
        ViewBag.Clients = repository.GetClients();
        ViewBag.Tariffs = repository.GetTariffs(true);
    }

    private static OrderItemInput NewBlankItem() => new() { ExpectedPickup = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd") };

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
