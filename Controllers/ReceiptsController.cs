using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pressings.Services;

namespace Pressings.Controllers;

[Authorize]
public sealed class ReceiptsController(AccessRepository repository, IWebHostEnvironment environment) : Controller
{
    public IActionResult Ticket(int id)
    {
        var order = repository.GetOrderDetails(id);
        if (order is null || !CanReadClient(order.Client.ClientId, ticketDocument: true)) return NotFound();
        return File(ReceiptPdfService.Ticket(order), "application/pdf", $"ticket-{order.Order.TicketNo}.pdf");
    }

    public IActionResult InvoiceExcel(int id)
    {
        var order = repository.GetOrderDetails(id);
        if (order is null || !CanReadClient(order.Client.ClientId, financialDocument: true)) return NotFound();
        return File(InvoiceDocumentService.Excel(order, BrandLogoPng()),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"facture-{order.Order.TicketNo}.xlsx");
    }

    public IActionResult InvoiceWord(int id)
    {
        var order = repository.GetOrderDetails(id);
        if (order is null || !CanReadClient(order.Client.ClientId, financialDocument: true)) return NotFound();
        return File(InvoiceDocumentService.Word(order, BrandLogoPng()),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"facture-{order.Order.TicketNo}.docx");
    }

    public IActionResult InvoicePdf(int id)
    {
        var order = repository.GetOrderDetails(id);
        if (order is null || !CanReadClient(order.Client.ClientId, financialDocument: true)) return NotFound();
        return File(ReceiptPdfService.Invoice(order, BrandLogoJpeg()), "application/pdf", $"facture-{order.Order.TicketNo}.pdf");
    }

    public IActionResult Payment(int id)
    {
        var payment = repository.GetPayment(id);
        if (payment is null) return NotFound();
        var orderId = repository.PaymentOrderId(id);
        var order = repository.GetOrderDetails(orderId);
        if (order is null || !CanReadClient(order.Client.ClientId, financialDocument: true)) return NotFound();
        return File(ReceiptPdfService.Payment(order, payment), "application/pdf", $"recu-{payment.ReceiptNo}.pdf");
    }

    private bool CanReadClient(int clientId, bool financialDocument = false, bool ticketDocument = false)
    {
        if (User.IsInRole("staff"))
        {
            if (financialDocument) return StaffPolicies.CanSeeFinances(User);
            if (ticketDocument) return StaffPolicies.CanSeeFinances(User) || User.IsInRole("reception");
            return User.IsInRole("super_admin") || User.IsInRole("manager") || User.IsInRole("reception") || User.IsInRole("washer") || User.IsInRole("finisher") || User.IsInRole("quality") || User.IsInRole("cashier");
        }
        return User.IsInRole("client") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var current) && current == clientId;
    }

    private byte[]? BrandLogoPng() => ReadLogo("fresh-care-logo.png");
    private byte[]? BrandLogoJpeg() => ReadLogo("fresh-care-logo.jpg");

    private byte[]? ReadLogo(string fileName)
    {
        var path = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "img", fileName);
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null;
    }
}
