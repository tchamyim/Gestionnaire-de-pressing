using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pressings.Models;
using Pressings.Services;

namespace Pressings.Controllers;

[Authorize]
public sealed class DeliveriesController(AccessRepository repository) : Controller
{
    [Authorize(Policy = StaffPolicies.DeliveryWork)]
    public IActionResult Index()
    {
        var canManage = StaffPolicies.IsManagerOrAdmin(User);
        var canSeeAll = canManage || User.IsInRole("reception");
        ViewBag.CanManage = canManage;
        ViewBag.DeliveryStaff = canManage ? repository.GetActiveDeliveryUsers() : [];
        return View(repository.GetDeliveryMissions(CurrentUserId(), canSeeAll));
    }

    [Authorize(Policy = StaffPolicies.DeliveryManagement), HttpGet]
    public IActionResult CreateCollection(int? clientId)
    {
        PrepareCollection();
        var client = clientId.HasValue ? repository.GetClient(clientId.Value) : null;
        return View(new DeliveryMissionInput { Direction = "collecte", ClientId = client?.ClientId ?? 0, Address = client?.Address ?? "", ScheduledAt = DateTime.Today.AddDays(1).AddHours(10) });
    }

    [Authorize(Policy = StaffPolicies.DeliveryManagement), HttpPost, ValidateAntiForgeryToken]
    public IActionResult CreateCollection(DeliveryMissionInput input)
    {
        input.Direction = "collecte";
        input.OrderId = null;
        if (string.IsNullOrWhiteSpace(input.Address)) ModelState.AddModelError(nameof(input.Address), "L’adresse de collecte est nécessaire.");
        if (!ModelState.IsValid) { PrepareCollection(); return View(input); }
        try
        {
            repository.CreateDeliveryMission(input, CurrentUserId());
            TempData["Success"] = "Demande de collecte enregistrée. Attribuez-la à un livreur depuis les missions.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            PrepareCollection();
            return View(input);
        }
    }

    [Authorize(Policy = StaffPolicies.DeliveryManagement), HttpPost, ValidateAntiForgeryToken]
    public IActionResult CreateOrderDelivery(int orderId, string address, DateTime? scheduledAt, string? notes)
    {
        var order = repository.GetOrderDetails(orderId);
        if (order is null) return NotFound();
        var input = new DeliveryMissionInput
        {
            ClientId = order.Client.ClientId,
            OrderId = orderId,
            Direction = "livraison",
            Address = address?.Trim() ?? "",
            ScheduledAt = scheduledAt,
            Notes = notes
        };
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(input.Address))
        {
            TempData["Error"] = "Indiquez l’adresse de livraison.";
            return RedirectToAction("Details", "Orders", new { id = orderId });
        }
        try
        {
            repository.CreateDeliveryMission(input, CurrentUserId());
            TempData["Success"] = "Livraison créée. Vous pourrez l’attribuer depuis les missions.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = StaffPolicies.DeliveryManagement), HttpPost, ValidateAntiForgeryToken]
    public IActionResult Assign(int id, int employeeId)
    {
        try
        {
            repository.AssignDeliveryMission(id, employeeId, CurrentUserId());
            TempData["Success"] = "Mission attribuée au livreur.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = StaffPolicies.DeliveryWork), HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(int id, DeliveryUpdateInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vérifiez le statut et les détails de la mission.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            repository.UpdateDeliveryMission(id, input, CurrentUserId(), StaffPolicies.IsManagerOrAdmin(User));
            TempData["Success"] = "Mise à jour de la mission enregistrée.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    private void PrepareCollection()
    {
        ViewBag.Clients = repository.GetClients();
    }

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
