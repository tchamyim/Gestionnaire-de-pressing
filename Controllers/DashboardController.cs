using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pressings.Models;
using Pressings.Services;

namespace Pressings.Controllers;

[Authorize]
public sealed class DashboardController(AccessRepository repository) : Controller
{
    [Authorize(Policy = StaffPolicies.Dashboard)]
    public IActionResult Index() => View(repository.GetDashboard());

    [Authorize(Policy = StaffPolicies.FinancialReports)]
    [HttpGet]
    public IActionResult ClosingReport(DateTime? date)
    {
        var report = repository.GetDailyClosingReport(date ?? DateTime.Today);
        return View(report);
    }

    [Authorize(Policy = StaffPolicies.FinancialReports)]
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Movement(CashMovementInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vérifiez le montant et le motif du mouvement de caisse.";
            return RedirectToAction(nameof(ClosingReport));
        }
        try
        {
            repository.AddCashMovement(input, CurrentUserId());
            TempData["Success"] = "Mouvement de caisse enregistré.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(ClosingReport));
    }

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
