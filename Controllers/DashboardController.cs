using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pressings.Models;
using Pressings.Services;

namespace Pressings.Controllers;

[Authorize(Roles = "staff")]
public sealed class DashboardController(AccessRepository repository) : Controller
{
    public IActionResult Index() => View(repository.GetDashboard());

    [HttpGet]
    public IActionResult ClosingReport(DateTime? date)
    {
        var report = repository.GetDailyClosingReport(date ?? DateTime.Today);
        return View(report);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Movement(CashMovementInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vérifiez le montant et le motif du mouvement de caisse.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            repository.AddCashMovement(input, CurrentUserId());
            TempData["Success"] = "Mouvement de caisse enregistré.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
