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

public sealed class AccountController(AccessRepository repository, IConfiguration configuration, IAntiforgery antiforgery) : Controller
{
    private readonly PasswordHasher<AdminAccount> _hasher = new();

    [AllowAnonymous, HttpGet]
    public IActionResult Setup()
    {
        if (repository.AdminCount() > 0) return RedirectToAction(nameof(Login));
        return View(new SetupAdminInput { Email = configuration["InitialAdminEmail"] ?? "" });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("login")]
    public async Task<IActionResult> Setup(SetupAdminInput input)
    {
        if (repository.AdminCount() > 0) return RedirectToAction(nameof(Login));
        var minimumPasswordLength = repository.UsesSqlServer || repository.UsesMySql ? 12 : 6;
        if (input.Password.Length < minimumPasswordLength)
            ModelState.AddModelError(nameof(input.Password), $"Le mot de passe doit contenir au moins {minimumPasswordLength} caractères.");
        if (!ModelState.IsValid) return View(input);
        var account = new AdminAccount { Email = input.Email.Trim().ToLowerInvariant() };
        var hash = _hasher.HashPassword(account, input.Password);
        repository.CreateAdmin(account.Email, hash);
        account = repository.FindAdmin(account.Email)!;
        await SignInStaff(account);
        TempData["Success"] = "Compte gestionnaire créé. Bienvenue dans Fresh & Care.";
        return RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", User.IsInRole("client") ? "Portal" : "Dashboard");
        return repository.AdminCount() == 0
            ? RedirectToAction(nameof(Setup))
            : View(new AdminLoginInput { Email = configuration["InitialAdminEmail"] ?? "" });
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(AdminLoginInput input)
    {
        // A double submission may arrive after the first request has already signed the
        // user in. In that case, return to the dashboard before validating the now-stale
        // anonymous form token instead of replacing the working page with HTTP 400.
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", User.IsInRole("client") ? "Portal" : "Dashboard");

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
        return RedirectToAction("Index", "Dashboard");
    }

    [Authorize(Roles = "staff"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task SignInStaff(AdminAccount account)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim(ClaimTypes.Name, account.Email),
            new Claim(ClaimTypes.Email, account.Email),
            new Claim(ClaimTypes.Role, "staff")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }
}
