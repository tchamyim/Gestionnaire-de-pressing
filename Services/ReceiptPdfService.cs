using System.Globalization;
using System.Text;
using Pressings.Models;

namespace Pressings.Services;

public static class ReceiptPdfService
{
    public static byte[] Invoice(OrderDetailViewModel order, byte[]? logoJpeg = null)
    {
        var lines = new List<(string Text, int Size, bool Bold)>
        {
            ("FRESH & CARE", 20, true),
            ("FACTURE DE PRESSING", 15, true),
            ($"Ticket : {order.Order.TicketNo}", 11, true),
            ($"Date de dépôt : {order.Order.CreatedAt:dd/MM/yyyy HH:mm}", 10, false),
            ($"Client : {order.Client.FirstName} {order.Client.LastName}", 10, false),
            ($"Téléphone : {order.Client.Phone}", 10, false),
            ("ARTICLES", 11, true)
        };
        foreach (var item in order.Items)
        {
            var description = string.IsNullOrWhiteSpace(item.Description) ? item.CategoryName : $"{item.CategoryName} - {item.Description}";
            var express = item.IsExpress ? " · EXPRESS" : "";
            lines.Add(($"• {description}{express} — {FormatMoney(item.UnitPrice + item.ExpressFee)} FCFA", 9, false));
            lines.Add(($"  Retrait prévu : {item.ExpectedPickupAt:dd/MM/yyyy} · Statut : {item.ItemStatus}", 8, false));
        }
        lines.Add(("", 9, false));
        lines.Add(($"TOTAL FACTURE : {FormatMoney(order.Order.Total)} FCFA", 12, true));
        lines.Add(($"Déjà réglé : {FormatMoney(order.Order.Paid)} FCFA", 10, false));
        lines.Add(($"RESTE À PAYER : {FormatMoney(order.Order.Remaining)} FCFA", 11, true));
        if (order.Payments.Count > 0)
        {
            lines.Add(("", 9, false));
            lines.Add(("PAIEMENTS REÇUS", 10, true));
            foreach (var payment in order.Payments)
                lines.Add(($"{payment.ReceiptNo} · {payment.PaymentAt:dd/MM/yyyy HH:mm} · {payment.PaymentMethod} · {FormatMoney(payment.Amount)} FCFA", 8, false));
        }
        lines.Add(("Merci de votre confiance !", 10, false));
        return Create(lines, logoJpeg);
    }

    public static byte[] Ticket(OrderDetailViewModel order)
    {
        var lines = new List<(string Text, int Size, bool Bold)>
        {
            ("FRESH & CARE", 20, true),
            ("PRESSING  ·  BLANCHISSERIE  ·  ENTRETIEN TEXTILE", 9, false),
            ("TICKET DE DÉPÔT", 15, true),
            ($"Ticket : {order.Order.TicketNo}", 11, true),
            ($"Date de dépôt : {order.Order.CreatedAt:dd/MM/yyyy HH:mm}", 10, false),
            ($"Client : {order.Client.FirstName} {order.Client.LastName}", 10, false),
            ($"Téléphone : {order.Client.Phone}", 10, false),
            ("ARTICLES", 11, true)
        };
        foreach (var item in order.Items)
        {
            var express = item.IsExpress ? " · EXPRESS" : "";
            var desc = string.IsNullOrWhiteSpace(item.Description) ? item.CategoryName : $"{item.CategoryName} - {item.Description}";
            lines.Add(($"• {desc}{express} - {FormatMoney(item.UnitPrice + item.ExpressFee)} FCFA", 9, false));
            lines.Add(($"  Statut : {item.ItemStatus} | Retrait prévu : {item.ExpectedPickupAt:dd/MM/yyyy}", 8, false));
        }
        lines.Add(("", 9, false));
        lines.Add(($"Total : {FormatMoney(order.Order.Total)} FCFA", 12, true));
        lines.Add(($"Déjà réglé : {FormatMoney(order.Order.Paid)} FCFA", 10, false));
        lines.Add(($"Reste à payer : {FormatMoney(order.Order.Remaining)} FCFA", 11, true));
        lines.Add(("Merci de votre confiance !", 10, false));
        return Create(lines);
    }

    public static byte[] Payment(OrderDetailViewModel order, PaymentViewModel payment)
    {
        var lines = new List<(string Text, int Size, bool Bold)>
        {
            ("FRESH & CARE", 20, true),
            ("REÇU DE PAIEMENT", 15, true),
            ($"Reçu : {payment.ReceiptNo}", 11, true),
            ($"Ticket : {order.Order.TicketNo}", 10, false),
            ($"Date : {payment.PaymentAt:dd/MM/yyyy HH:mm}", 10, false),
            ($"Client : {order.Client.FirstName} {order.Client.LastName}", 10, false),
            ($"Moyen de paiement : {payment.PaymentMethod}", 10, false),
            ($"Référence : {payment.Reference}", 9, false),
            ($"Montant reçu : {FormatMoney(payment.Amount)} FCFA", 14, true),
            ($"Total du ticket : {FormatMoney(order.Order.Total)} FCFA", 10, false),
            ($"Reste dû : {FormatMoney(order.Order.Remaining)} FCFA", 11, true),
            ("Merci de votre confiance !", 10, false)
        };
        return Create(lines);
    }

    private static byte[] Create(List<(string Text, int Size, bool Bold)> lines, byte[]? logoJpeg = null)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var content = new StringBuilder();
        var lineHeight = lines.Skip(1).Sum(line => line.Text.Length == 0 ? 8 : line.Size + 10);
        var pageHeight = logoJpeg is null ? Math.Max(470, lineHeight + 230) : Math.Max(590, lineHeight + 365);
        var y = pageHeight - 42;
        if (logoJpeg is not null)
        {
            content.Append("q 202 0 0 135 139 ").Append(pageHeight - 158).Append(" cm /Logo Do Q\n");
            y = pageHeight - 176;
        }
        else
        {
            content.Append("0.06 0.20 0.29 rg\nBT /F2 20 Tf 42 ").Append(y).Append(" Td ").Append(PdfText("FRESH & CARE")).Append(" Tj ET\n");
            y -= 20;
        }
        content.Append("0.84 0.67 0.31 RG 1.5 w 42 ").Append(y).Append(" m 438 ").Append(y).Append(" l S\n0.06 0.20 0.29 rg\n");
        y -= 27;
        foreach (var line in lines.Skip(1))
        {
            if (line.Text.Length == 0) { y -= 8; continue; }
            content.Append("BT /").Append(line.Bold ? "F2" : "F1").Append(' ').Append(line.Size.ToString(CultureInfo.InvariantCulture))
                .Append(" Tf 42 ").Append(y).Append(" Td ").Append(PdfText(line.Text)).Append(" Tj ET\n");
            y -= line.Size + 10;
        }
        content.Append("0.84 0.67 0.31 RG 1 w 42 128 m 438 128 l S\n")
            .Append("0.06 0.20 0.29 rg\nBT /F1 9 Tf 143 104 Td ")
            .Append(PdfText("Le soin du linge, en toute simplicité."))
            .Append(" Tj ET\n0.06 0.20 0.29 RG 2 w ")
            .Append(CirclePath(392, 64, 29)).Append(" S\n0.84 0.67 0.31 RG 1 w ")
            .Append(CirclePath(392, 64, 24)).Append(" S\n0.06 0.20 0.29 rg\n")
            .Append("BT /F1 4 Tf 376 78 Td ").Append(PdfText("FRESH & CARE")).Append(" Tj ET\n")
            .Append("BT /F2 8 Tf 371 62 Td ").Append(PdfText("PRESSING")).Append(" Tj ET\n")
            .Append("BT /F1 4 Tf 380 50 Td ").Append(PdfText("YAOUNDÉ")).Append(" Tj ET\n");
        var stream = Encoding.ASCII.GetBytes(content.ToString());
        var pageResources = logoJpeg is null
            ? "<< /Font << /F1 4 0 R /F2 5 0 R >> >>"
            : "<< /Font << /F1 4 0 R /F2 5 0 R >> /XObject << /Logo 7 0 R >> >>";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 480 {pageHeight}] /Resources {pageResources} /Contents 6 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
            $"<< /Length {stream.Length} >>\nstream\n{Encoding.ASCII.GetString(stream)}endstream"
        };
        using var output = new MemoryStream();
        WriteAscii(output, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(output.Position);
            WriteAscii(output, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        if (logoJpeg is not null)
        {
            offsets.Add(output.Position);
            WriteAscii(output, $"7 0 obj\n<< /Type /XObject /Subtype /Image /Width 1280 /Height 853 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {logoJpeg.Length} >>\nstream\n");
            output.Write(logoJpeg, 0, logoJpeg.Length);
            WriteAscii(output, "\nendstream\nendobj\n");
        }
        var objectCount = offsets.Count - 1;
        var xref = output.Position;
        WriteAscii(output, $"xref\n0 {objectCount + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) WriteAscii(output, $"{offset:0000000000} 00000 n \n");
        WriteAscii(output, $"trailer\n<< /Size {objectCount + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return output.ToArray();
    }

    private static string PdfText(string value)
    {
        var bytes = Encoding.GetEncoding(1252).GetBytes(value);
        var text = new StringBuilder("(");
        foreach (var b in bytes)
        {
            if (b is (byte)'(' or (byte)')' or (byte)'\\') text.Append('\\').Append((char)b);
            else if (b < 32 || b > 126) text.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
            else text.Append((char)b);
        }
        return text.Append(')').ToString();
    }

    private static string FormatMoney(decimal amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))
        .Replace('\u202F', ' ').Replace('\u00A0', ' ');

    private static string CirclePath(double cx, double cy, double radius)
    {
        var k = radius * 0.55228475;
        return FormattableString.Invariant($"{cx + radius:0.##} {cy:0.##} m {cx + radius:0.##} {cy + k:0.##} {cx + k:0.##} {cy + radius:0.##} {cx:0.##} {cy + radius:0.##} c {cx - k:0.##} {cy + radius:0.##} {cx - radius:0.##} {cy + k:0.##} {cx - radius:0.##} {cy:0.##} c {cx - radius:0.##} {cy - k:0.##} {cx - k:0.##} {cy - radius:0.##} {cx:0.##} {cy - radius:0.##} c {cx + k:0.##} {cy - radius:0.##} {cx + radius:0.##} {cy - k:0.##} {cx + radius:0.##} {cy:0.##} c");
    }

    private static void WriteAscii(Stream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }
}
