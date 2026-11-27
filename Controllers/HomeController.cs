using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pressings.Models;
using Pressings.Services;

namespace Pressings.Controllers;

public sealed class HomeController(AccessRepository repository) : Controller
{
    [AllowAnonymous]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return User.IsInRole("client") ? RedirectToAction("Index", "Portal") : RedirectToAction("Index", "Dashboard");
        ViewBag.ManagerAccessAction = repository.AdminCount() == 0 ? "Setup" : "Login";
        return View();
    }

    [AllowAnonymous]
    public IActionResult Error() => View("~/Views/Shared/Error.cshtml", new ErrorViewModel
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });
}
