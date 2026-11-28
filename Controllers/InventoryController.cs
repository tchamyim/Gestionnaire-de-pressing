using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pressings.Models;
using Pressings.Services;

namespace Pressings.Controllers;

[Authorize(Policy = StaffPolicies.Inventory)]
public sealed class InventoryController(AccessRepository repository) : Controller
{
    public IActionResult Index() => View(repository.GetInventory());

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(InventoryItemInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vérifiez le nom, l’unité, le stock de départ et le seuil d’alerte.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            repository.CreateInventoryItem(input, CurrentUserId());
            TempData["Success"] = "Consommable ajouté au stock.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Movement(int id, InventoryMovementInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vérifiez le type et la quantité du mouvement.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            repository.RecordInventoryMovement(id, input, CurrentUserId());
            TempData["Success"] = input.Direction == "entrée" ? "Réapprovisionnement enregistré." : "Sortie de consommable enregistrée.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
