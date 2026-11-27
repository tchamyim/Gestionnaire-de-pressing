using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pressings.Models;
using Pressings.Services;

namespace Pressings.Controllers;

[Authorize(Roles = "staff")]
public sealed class TariffsController(AccessRepository repository) : Controller
{
    public IActionResult Index() => View(repository.GetTariffs());

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Save(TariffInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vérifiez le nom et les montants de la catégorie.";
            return RedirectToAction(nameof(Index));
        }
        repository.SaveTariff(input);
        TempData["Success"] = input.CategoryId == 0 ? "Catégorie tarifaire ajoutée." : "Tarif mis à jour.";
        return RedirectToAction(nameof(Index));
    }
}
