using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pressings.Models;
using Pressings.Services;

namespace Pressings.Controllers;

public sealed class AccountController(AccessRepository repository, IConfiguration configuration, IAntiforgery antiforgery) : Controller
{
    private readonly PasswordHasher<AdminAccount> _hasher = new();

    [AllowAnonymous, HttpGet]
    public IActionResult Setup()
    {
        if (repository.AdminCount() > 0) return RedirectToAction(nameof(Login));
        return View(new SetupAdminInput { Email = configuration["InitialAdminEmail"] ?? "" });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Setup(SetupAdminInput input)
    {
        if (repository.AdminCount() > 0) return RedirectToAction(nameof(Login));
        var minimumPasswordLength = repository.UsesSqlServer || repository.UsesMySql ? 12 : 6;
        if (input.Password.Length < minimumPasswordLength)
            ModelState.AddModelError(nameof(input.Password), $"Le mot de passe doit contenir au moins {minimumPasswordLength} caractères.");
        if (!ModelState.IsValid) return View(input);
        var account = new AdminAccount { Email = input.Email.Trim().ToLowerInvariant(), DisplayName = string.IsNullOrWhiteSpace(input.DisplayName) ? "Administrateur général" : input.DisplayName.Trim() };
        var hash = _hasher.HashPassword(account, input.Password);
        repository.CreateAdmin(account.Email, account.DisplayName, hash);
        account = repository.FindAdmin(account.Email)!;
        await SignInStaff(account);
        TempData["Success"] = "Compte gestionnaire créé. Bienvenue dans Fresh & Care.";
        return RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction(User.IsInRole("client") ? "Index" : nameof(StaffHome), User.IsInRole("client") ? "Portal" : "Account");
        return repository.AdminCount() == 0
            ? RedirectToAction(nameof(Setup))
            : View(new AdminLoginInput { Email = configuration["InitialAdminEmail"] ?? "" });
    }

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(AdminLoginInput input)
    {
        // A double submission may arrive after the first request has already signed the
        // user in. In that case, return to the dashboard before validating the now-stale
        // anonymous form token instead of replacing the working page with HTTP 400.
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction(User.IsInRole("client") ? "Index" : nameof(StaffHome), User.IsInRole("client") ? "Portal" : "Account");

        try { await antiforgery.ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return BadRequest(); }

        if (!ModelState.IsValid) return View(input);
        var account = repository.FindAdmin(input.Email);
        if (account is null || _hasher.VerifyHashedPassword(account, account.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Adresse e-mail ou mot de passe incorrect.");
            return View(input);
        }
        await SignInStaff(account);
        return RedirectToAction(nameof(StaffHome));
    }

    [Authorize(Policy = StaffPolicies.Employee), HttpGet]
    public IActionResult StaffHome()
    {
        if (User.IsInRole("cashier") && !StaffPolicies.IsManagerOrAdmin(User)) return RedirectToAction("ClosingReport", "Dashboard");
        return RedirectToAction("Index", StaffPolicies.LandingController(User));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Denied() => View();

    [Authorize(Roles = "staff"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task SignInStaff(AdminAccount account)
    {
        account.RoleCodes = repository.GetAdminRoles(account.Id);
        var principal = StaffClaimsFactory.Create(account, account.RoleCodes);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }
}
