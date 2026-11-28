using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pressings.Models;
using Pressings.Services;

namespace Pressings.Controllers;

[Authorize(Policy = StaffPolicies.Employee)]
public sealed class IncidentsController(AccessRepository repository) : Controller
{
    [Authorize(Policy = StaffPolicies.IncidentManagement)]
    public IActionResult Index() => View(repository.GetOrderIncidents(CurrentUserId(), canManage: true));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Report(OrderIncidentInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Décrivez brièvement la réclamation ou l’incident.";
            return RedirectToAction("Details", "Orders", new { id = input.OrderId });
        }
        try
        {
            repository.CreateOrderIncident(input, CurrentUserId());
            TempData["Success"] = "Signalement envoyé au responsable.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Details", "Orders", new { id = input.OrderId });
    }

    [Authorize(Policy = StaffPolicies.IncidentManagement), HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(int id, OrderIncidentUpdateInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vérifiez le statut et la résolution de l’incident.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            repository.UpdateOrderIncident(id, input, CurrentUserId());
            TempData["Success"] = "Suivi de l’incident mis à jour.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
