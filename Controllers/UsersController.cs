using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pressings.Models;
using Pressings.Services;

namespace Pressings.Controllers;

[Authorize(Policy = StaffPolicies.UserAdministration)]
public sealed class UsersController(AccessRepository repository) : Controller
{
    private readonly PasswordHasher<AdminAccount> _hasher = new();

    public IActionResult Index() => View(repository.GetStaffUsers());

    [HttpGet]
    public IActionResult Create()
    {
        PrepareRoles();
        return View(new StaffUserInput { IsActive = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(StaffUserInput input)
    {
        input.RoleCodes = ValidRoles(input.RoleCodes);
        if (input.RoleCodes.Count == 0) ModelState.AddModelError(nameof(input.RoleCodes), "Choisissez au moins un rôle valide.");
        if (string.IsNullOrWhiteSpace(input.Password) || input.Password.Length < 12) ModelState.AddModelError(nameof(input.Password), "Choisissez un mot de passe d’au moins 12 caractères.");
        if (!ModelState.IsValid) { PrepareRoles(input.RoleCodes); return View(input); }
        try
        {
            var account = new AdminAccount { Email = input.Email.Trim().ToLowerInvariant(), DisplayName = input.DisplayName.Trim() };
            repository.CreateStaffUser(input, _hasher.HashPassword(account, input.Password!), CurrentUserId());
            TempData["Success"] = $"Compte créé pour {input.DisplayName}. Communiquez son mot de passe de façon privée.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            PrepareRoles(input.RoleCodes);
            return View(input);
        }
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var user = repository.GetStaffUser(id);
        if (user is null) return NotFound();
        PrepareRoles(user.RoleCodes);
        return View(new StaffUserInput { Id = user.Id, DisplayName = user.DisplayName, Email = user.Email, IsActive = user.IsActive, RoleCodes = user.RoleCodes });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Edit(int id, StaffUserInput input)
    {
        if (id != input.Id) return BadRequest();
        input.RoleCodes = ValidRoles(input.RoleCodes);
        if (input.RoleCodes.Count == 0) ModelState.AddModelError(nameof(input.RoleCodes), "Un compte actif doit avoir au moins un rôle.");
        if (!string.IsNullOrWhiteSpace(input.Password) && input.Password.Length < 12)
            ModelState.AddModelError(nameof(input.Password), "Le nouveau mot de passe doit contenir au moins 12 caractères.");
        var before = repository.GetStaffUser(id);
        if (before is null) return NotFound();
        var actorId = CurrentUserId();
        var removesLastAdmin = before.IsActive && before.RoleCodes.Contains("super_admin", StringComparer.Ordinal) &&
                              (!input.IsActive || !input.RoleCodes.Contains("super_admin", StringComparer.Ordinal));
        if (removesLastAdmin && repository.ActiveSuperAdminCount() <= 1)
            ModelState.AddModelError(nameof(input.RoleCodes), "Gardez au moins un administrateur général actif.");
        if (id == actorId && !input.IsActive)
            ModelState.AddModelError(nameof(input.IsActive), "Vous ne pouvez pas désactiver votre propre compte.");
        if (!ModelState.IsValid) { PrepareRoles(input.RoleCodes); return View(input); }

        try
        {
            repository.UpdateStaffUser(input, actorId);
            if (!string.IsNullOrWhiteSpace(input.Password))
            {
                var account = new AdminAccount { Id = id, Email = input.Email.Trim(), DisplayName = input.DisplayName.Trim() };
                repository.UpdateStaffPassword(id, _hasher.HashPassword(account, input.Password), actorId);
            }
            TempData["Success"] = "Compte, rôles et autorisations mis à jour.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            PrepareRoles(input.RoleCodes);
            return View(input);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SetActive(int id, bool active)
    {
        var user = repository.GetStaffUser(id);
        if (user is null) return NotFound();
        if (id == CurrentUserId() && !active)
        {
            TempData["Error"] = "Vous ne pouvez pas désactiver votre propre compte.";
            return RedirectToAction(nameof(Index));
        }
        if (!active && user.IsActive && user.RoleCodes.Contains("super_admin", StringComparer.Ordinal) && repository.ActiveSuperAdminCount() <= 1)
        {
            TempData["Error"] = "Le pressing doit garder au moins un administrateur général actif.";
            return RedirectToAction(nameof(Index));
        }
        repository.SetStaffActive(id, active, CurrentUserId());
        TempData["Success"] = active ? "Compte réactivé." : "Compte désactivé. La prochaine requête fermera aussi ses sessions actives.";
        return RedirectToAction(nameof(Index));
    }

    private void PrepareRoles(IEnumerable<string>? selected = null)
    {
        ViewBag.RoleCatalog = StaffRoleCatalog.All;
        ViewBag.SelectedRoles = (selected ?? []).ToHashSet(StringComparer.Ordinal);
    }

    private static List<string> ValidRoles(IEnumerable<string>? codes) => (codes ?? [])
        .Where(StaffRoleCatalog.IsKnown).Distinct(StringComparer.Ordinal).ToList();

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
