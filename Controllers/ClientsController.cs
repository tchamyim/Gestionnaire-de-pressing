using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pressings.Models;
using Pressings.Services;

namespace Pressings.Controllers;

[Authorize(Policy = StaffPolicies.ClientManagement)]
public sealed class ClientsController(AccessRepository repository) : Controller
{
    private readonly PasswordHasher<PortalAccount> _hasher = new();

    public IActionResult Index(string? q) { ViewBag.Query = q ?? ""; return View(repository.GetClients(q)); }

    [HttpGet]
    public IActionResult Create() => View(new ClientInput());

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(ClientInput input)
    {
        if (!ModelState.IsValid) return View(input);
        var client = new PortalAccount { FirstName = input.FirstName.Trim(), LastName = input.LastName.Trim(), Phone = input.Phone.Trim() };
        var pin = NewPin();
        var hash = _hasher.HashPassword(client, pin);
        var id = repository.CreateClient(input, hash);
        TempData["PortalPin"] = pin;
        TempData["Success"] = "Fiche client enregistrée. Remettez le code d’accès affiché ci-dessous au client.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public IActionResult Details(int id)
    {
        var client = repository.GetClient(id);
        if (client is null) return NotFound();
        client.Balance = repository.GetClientBalance(id);
        ViewBag.Orders = repository.GetOrderSummaries(id);
        ViewBag.History = repository.GetClientHistory(id);
        ViewBag.PortalPin = TempData["PortalPin"] as string;
        return View(client);
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var client = repository.GetClient(id);
        if (client is null) return NotFound();
        return View(new ClientInput { FirstName = client.FirstName, LastName = client.LastName, Phone = client.Phone, WhatsApp = client.WhatsApp, Email = client.Email, Address = client.Address });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Edit(int id, ClientInput input)
    {
        if (!ModelState.IsValid) return View(input);
        if (repository.GetClient(id) is null) return NotFound();
        repository.UpdateClient(id, input);
        TempData["Success"] = "Coordonnées client mises à jour.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ResetPin(int id)
    {
        var client = repository.GetClient(id);
        if (client is null) return NotFound();
        var account = new PortalAccount { ClientId = id, FirstName = client.FirstName, LastName = client.LastName, Phone = client.Phone };
        var pin = NewPin();
        repository.ResetPortalPin(id, _hasher.HashPassword(account, pin));
        TempData["PortalPin"] = pin;
        TempData["Success"] = "Nouveau code client généré. Il ne sera affiché qu’une seule fois.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private static string NewPin() => RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
}
