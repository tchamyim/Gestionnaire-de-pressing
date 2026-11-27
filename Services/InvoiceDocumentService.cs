using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Pressings.Models;

namespace Pressings.Services;

public static class InvoiceDocumentService
{
    public static byte[] Excel(OrderDetailViewModel order, byte[]? logoPng = null)
    {
        using var output = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(output, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new S.Workbook();
            var sheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var rows = new S.SheetData();
            sheetPart.Worksheet = new S.Worksheet(new S.Columns(
                new S.Column { Min = 1, Max = 1, Width = 26, CustomWidth = true },
                new S.Column { Min = 2, Max = 2, Width = 28, CustomWidth = true },
                new S.Column { Min = 3, Max = 3, Width = 18, CustomWidth = true },
                new S.Column { Min = 4, Max = 5, Width = 18, CustomWidth = true },
                new S.Column { Min = 6, Max = 6, Width = 18, CustomWidth = true },
                new S.Column { Min = 7, Max = 7, Width = 18, CustomWidth = true }), rows);
            workbookPart.Workbook.Append(new S.Sheets(new S.Sheet
            {
                Id = workbookPart.GetIdOfPart(sheetPart),
                SheetId = 1,
                Name = "Facture"
            }));
            workbookPart.AddNewPart<WorkbookStylesPart>().Stylesheet = CreateStylesheet();

            var rowNumber = 1U;
            AddTextRow(rows, ref rowNumber, ["FRESH & CARE"], 1);
            AddTextRow(rows, ref rowNumber, ["Le soin du linge, en toute simplicité."], 2);
            AddTextRow(rows, ref rowNumber, ["FACTURE DE PRESSING", $"Ticket : {order.Order.TicketNo}"], 1);
            AddTextRow(rows, ref rowNumber, ["Date", order.Order.CreatedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("fr-FR"))]);
            AddTextRow(rows, ref rowNumber, ["Client", $"{order.Client.FirstName} {order.Client.LastName}"]);
            AddTextRow(rows, ref rowNumber, ["Téléphone", order.Client.Phone]);
            rowNumber++;
            AddTextRow(rows, ref rowNumber, ["Article", "Description", "Service", "Dépôt", "Retrait prévu", "Statut", "Prix FCFA"], 3);

            foreach (var item in order.Items)
            {
                var description = string.IsNullOrWhiteSpace(item.Description) ? item.CategoryName : item.Description;
                var pickup = item.ExpectedPickupAt?.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-FR")) ?? "À définir";
                var service = item.IsExpress ? "Express" : "Standard";
                var row = new S.Row { RowIndex = rowNumber++ };
                var values = new object[] { item.CategoryName, description, service, item.DepositAt.ToString("dd/MM/yyyy"), pickup, item.ItemStatus, item.UnitPrice + item.ExpressFee };
                for (var index = 0; index < values.Length; index++)
                    row.Append(values[index] is decimal money
                        ? NumberCell(index + 1, row.RowIndex!.Value, money, 4)
                        : TextCell(index + 1, row.RowIndex!.Value, Convert.ToString(values[index], CultureInfo.GetCultureInfo("fr-FR")) ?? "", 0));
                rows.Append(row);
            }

            rowNumber++;
            AddTextRow(rows, ref rowNumber, ["TOTAL FACTURE", "", "", "", "", "", order.Order.Total], 4);
            AddTextRow(rows, ref rowNumber, ["Déjà réglé", "", "", "", "", "", order.Order.Paid], 4);
            AddTextRow(rows, ref rowNumber, ["RESTE À PAYER", "", "", "", "", "", order.Order.Remaining], 4);
            rowNumber++;
            AddTextRow(rows, ref rowNumber, ["PAIEMENTS REÇUS"], 3);
            AddTextRow(rows, ref rowNumber, ["Reçu", "Date", "Moyen", "Référence", "Montant FCFA"], 3);
            foreach (var payment in order.Payments)
            {
                var paymentRow = new S.Row { RowIndex = rowNumber++ };
                paymentRow.Append(TextCell(1, paymentRow.RowIndex!.Value, payment.ReceiptNo, 0));
                paymentRow.Append(TextCell(2, paymentRow.RowIndex.Value, payment.PaymentAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("fr-FR")), 0));
                paymentRow.Append(TextCell(3, paymentRow.RowIndex.Value, payment.PaymentMethod, 0));
                paymentRow.Append(TextCell(4, paymentRow.RowIndex.Value, payment.Reference, 0));
                paymentRow.Append(NumberCell(5, paymentRow.RowIndex.Value, payment.Amount, 4));
                rows.Append(paymentRow);
            }
            rowNumber++;
            AddTextRow(rows, ref rowNumber, ["◎  FRESH & CARE · PRESSING · YAOUNDÉ  ◎"], 5);
            AddTextRow(rows, ref rowNumber, ["Le soin du linge, en toute simplicité."], 2);
            AddExcelLogo(sheetPart, logoPng);
            sheetPart.Worksheet.Save();
            workbookPart.Workbook.Save();
        }
        return output.ToArray();
    }

    public static byte[] Word(OrderDetailViewModel order, byte[]? logoPng = null)
    {
        using var output = new MemoryStream();
        using (var document = WordprocessingDocument.Create(output, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            var body = new W.Body();
            if (logoPng is { Length: > 0 }) body.Append(LogoParagraph(mainPart, logoPng));
            else body.Append(Paragraph("FRESH & CARE", 30, true, "10344B", W.JustificationValues.Center));
            body.Append(Paragraph("PRESSING · BLANCHISSERIE · ENTRETIEN TEXTILE", 18, false, "718092", W.JustificationValues.Center));
            body.Append(Paragraph("Le soin du linge, en toute simplicité.", 21, false, "B68A34", W.JustificationValues.Center));
            body.Append(Paragraph("FACTURE DE PRESSING", 28, true, "10344B", W.JustificationValues.Center));
            body.Append(Paragraph($"Ticket : {order.Order.TicketNo}", 22, true, "2F78A8"));
            body.Append(Paragraph($"Date : {order.Order.CreatedAt:dd/MM/yyyy HH:mm}"));
            body.Append(Paragraph($"Client : {order.Client.FirstName} {order.Client.LastName}"));
            body.Append(Paragraph($"Téléphone : {order.Client.Phone}"));
            body.Append(BuildItemsTable(order));
            body.Append(Paragraph(" "));
            body.Append(Paragraph($"Total facture : {FormatMoney(order.Order.Total)} FCFA", 23, true, "10344B"));
            body.Append(Paragraph($"Déjà réglé : {FormatMoney(order.Order.Paid)} FCFA", 21, false, "263746"));
            body.Append(Paragraph($"Reste à payer : {FormatMoney(order.Order.Remaining)} FCFA", 23, true, "B47832"));
            if (order.Payments.Count > 0)
            {
                body.Append(Paragraph("PAIEMENTS REÇUS", 22, true, "10344B"));
                body.Append(BuildPaymentsTable(order));
            }
            body.Append(Paragraph("◎  FRESH & CARE  ◎", 24, true, "D6AA50", W.JustificationValues.Center));
            body.Append(Paragraph("PRESSING · YAOUNDÉ", 22, true, "10344B", W.JustificationValues.Center));
            body.Append(Paragraph("Merci de votre confiance.", 19, false, "718092", W.JustificationValues.Center));
            body.Append(new W.SectionProperties(new W.PageSize { Width = 11906, Height = 16838 }, new W.PageMargin { Top = 900, Right = 900, Bottom = 900, Left = 900 }));
            mainPart.Document = new W.Document(body);
            mainPart.Document.Save();
        }
        return output.ToArray();
    }

    private static S.Stylesheet CreateStylesheet()
    {
        var fonts = new S.Fonts(
            new S.Font(new S.FontSize { Val = 11 }, new S.Color { Rgb = "FF263746" }, new S.FontName { Val = "Aptos" }),
            new S.Font(new S.Bold(), new S.FontSize { Val = 18 }, new S.Color { Rgb = "FF10344B" }, new S.FontName { Val = "Aptos Display" }),
            new S.Font(new S.Bold(), new S.FontSize { Val = 11 }, new S.Color { Rgb = "FFFFFFFF" }, new S.FontName { Val = "Aptos" }));
        var fills = new S.Fills(
            new S.Fill(new S.PatternFill { PatternType = S.PatternValues.None }),
            new S.Fill(new S.PatternFill { PatternType = S.PatternValues.Gray125 }),
            new S.Fill(new S.PatternFill(new S.ForegroundColor { Rgb = "FF10344B" }) { PatternType = S.PatternValues.Solid }),
            new S.Fill(new S.PatternFill(new S.ForegroundColor { Rgb = "FFEAF4FB" }) { PatternType = S.PatternValues.Solid }));
        var borders = new S.Borders(new S.Border(), new S.Border(
            new S.LeftBorder { Style = S.BorderStyleValues.Thin, Color = new S.Color { Rgb = "FFDCE4EA" } },
            new S.RightBorder { Style = S.BorderStyleValues.Thin, Color = new S.Color { Rgb = "FFDCE4EA" } },
            new S.TopBorder { Style = S.BorderStyleValues.Thin, Color = new S.Color { Rgb = "FFDCE4EA" } },
            new S.BottomBorder { Style = S.BorderStyleValues.Thin, Color = new S.Color { Rgb = "FFDCE4EA" } },
            new S.DiagonalBorder()));
        var formats = new S.CellFormats(
            new S.CellFormat(),
            new S.CellFormat { FontId = 1, ApplyFont = true },
            new S.CellFormat { FontId = 0, ApplyFont = true },
            new S.CellFormat { FontId = 2, FillId = 2, BorderId = 1, ApplyFont = true, ApplyFill = true, ApplyBorder = true },
            new S.CellFormat { FontId = 1, FillId = 3, BorderId = 1, ApplyFont = true, ApplyFill = true, ApplyBorder = true, ApplyAlignment = true, Alignment = new S.Alignment { Horizontal = S.HorizontalAlignmentValues.Right } },
            new S.CellFormat { BorderId = 1, ApplyBorder = true },
            new S.CellFormat { FontId = 1, FillId = 3, BorderId = 1, ApplyFont = true, ApplyFill = true, ApplyBorder = true });
        return new S.Stylesheet(fonts, fills, borders, new S.CellStyleFormats(new S.CellFormat()), formats);
    }

    private static void AddTextRow(S.SheetData sheet, ref uint rowIndex, IReadOnlyList<object> values, uint style = 0)
    {
        var row = new S.Row { RowIndex = rowIndex++ };
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            row.Append(value is decimal money
                ? NumberCell(index + 1, row.RowIndex!.Value, money, 4)
                : TextCell(index + 1, row.RowIndex!.Value, Convert.ToString(value, CultureInfo.GetCultureInfo("fr-FR")) ?? "", style));
        }
        sheet.Append(row);
    }

    private static void AddExcelLogo(WorksheetPart worksheetPart, byte[]? logoPng)
    {
        if (logoPng is not { Length: > 0 }) return;
        var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
        var imagePart = drawingsPart.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(logoPng, writable: false)) imagePart.FeedData(stream);
        var relationshipId = drawingsPart.GetIdOfPart(imagePart);
        const long width = 1_280_000;
        const long height = 853_000;
        var picture = new Xdr.Picture(
            new Xdr.NonVisualPictureProperties(
                new Xdr.NonVisualDrawingProperties { Id = 1U, Name = "Fresh & Care" },
                new Xdr.NonVisualPictureDrawingProperties()),
            new Xdr.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
            new Xdr.ShapeProperties(
                new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        var anchor = new Xdr.OneCellAnchor(
            new Xdr.FromMarker(new Xdr.ColumnId("5"), new Xdr.ColumnOffset("0"), new Xdr.RowId("0"), new Xdr.RowOffset("0")),
            new Xdr.Extent { Cx = width, Cy = height }, picture, new Xdr.ClientData());
        drawingsPart.WorksheetDrawing = new Xdr.WorksheetDrawing(anchor);
        var worksheet = worksheetPart.Worksheet ?? throw new InvalidOperationException("La feuille de facture Excel est introuvable.");
        worksheet.Append(new S.Drawing { Id = worksheetPart.GetIdOfPart(drawingsPart) });
    }

    private static W.Paragraph LogoParagraph(MainDocumentPart mainPart, byte[] logoPng)
    {
        var imagePart = mainPart.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(logoPng, writable: false)) imagePart.FeedData(stream);
        var relationshipId = mainPart.GetIdOfPart(imagePart);
        const long width = 1_920_000;
        const long height = 1_280_000;
        var picture = new PIC.Picture(
            new PIC.NonVisualPictureProperties(
                new PIC.NonVisualDrawingProperties { Id = 1U, Name = "Fresh & Care" },
                new PIC.NonVisualPictureDrawingProperties()),
            new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
            new PIC.ShapeProperties(
                new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        var graphic = new A.Graphic(new A.GraphicData(picture)
        {
            Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture"
        });
        var inline = new DW.Inline(
            new DW.Extent { Cx = width, Cy = height },
            new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
            new DW.DocProperties { Id = 1U, Name = "Fresh & Care logo" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            graphic);
        return new W.Paragraph(
            new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Center }),
            new W.Run(new W.Drawing(inline)));
    }

    private static S.Cell TextCell(int column, uint row, string value, uint style) => new()
    {
        CellReference = CellName(column, row),
        DataType = S.CellValues.InlineString,
        StyleIndex = style,
        InlineString = new S.InlineString(new S.Text(value) { Space = SpaceProcessingModeValues.Preserve })
    };

    private static S.Cell NumberCell(int column, uint row, decimal value, uint style) => new()
    {
        CellReference = CellName(column, row),
        DataType = S.CellValues.Number,
        StyleIndex = style,
        CellValue = new S.CellValue(value.ToString(CultureInfo.InvariantCulture))
    };

    private static string CellName(int column, uint row)
    {
        var name = "";
        while (column > 0)
        {
            var remainder = (column - 1) % 26;
            name = (char)('A' + remainder) + name;
            column = (column - 1) / 26;
        }
        return name + row.ToString(CultureInfo.InvariantCulture);
    }

    private static W.Table BuildItemsTable(OrderDetailViewModel order)
    {
        var table = new W.Table();
        table.AppendChild(new W.TableProperties(
            new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct },
            new W.TableBorders(
                new W.TopBorder { Val = W.BorderValues.Single, Size = 6, Color = "DCE4EA" },
                new W.LeftBorder { Val = W.BorderValues.Single, Size = 6, Color = "DCE4EA" },
                new W.BottomBorder { Val = W.BorderValues.Single, Size = 6, Color = "DCE4EA" },
                new W.RightBorder { Val = W.BorderValues.Single, Size = 6, Color = "DCE4EA" },
                new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4, Color = "DCE4EA" },
                new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4, Color = "DCE4EA" })));
        table.Append(new W.TableGrid(
            new W.GridColumn { Width = "2500" },
            new W.GridColumn { Width = "1200" },
            new W.GridColumn { Width = "1500" },
            new W.GridColumn { Width = "1500" },
            new W.GridColumn { Width = "1800" }));
        table.Append(new W.TableRow(
            TableCell("Article", true), TableCell("Service", true), TableCell("Retrait", true), TableCell("Statut", true), TableCell("Prix FCFA", true)));
        foreach (var item in order.Items)
        {
            var desc = string.IsNullOrWhiteSpace(item.Description) ? item.CategoryName : $"{item.CategoryName} — {item.Description}";
            table.Append(new W.TableRow(
                TableCell(desc),
                TableCell(item.IsExpress ? "Express" : "Standard"),
                TableCell(item.ExpectedPickupAt?.ToString("dd/MM/yyyy") ?? "À définir"),
                TableCell(item.ItemStatus),
                TableCell(FormatMoney(item.UnitPrice + item.ExpressFee))));
        }
        return table;
    }

    private static W.Table BuildPaymentsTable(OrderDetailViewModel order)
    {
        var table = new W.Table();
        table.AppendChild(new W.TableProperties(
            new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct },
            new W.TableBorders(
                new W.TopBorder { Val = W.BorderValues.Single, Size = 6, Color = "DCE4EA" },
                new W.LeftBorder { Val = W.BorderValues.Single, Size = 6, Color = "DCE4EA" },
                new W.BottomBorder { Val = W.BorderValues.Single, Size = 6, Color = "DCE4EA" },
                new W.RightBorder { Val = W.BorderValues.Single, Size = 6, Color = "DCE4EA" },
                new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4, Color = "DCE4EA" },
                new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4, Color = "DCE4EA" })));
        table.Append(new W.TableGrid(
            new W.GridColumn { Width = "2000" }, new W.GridColumn { Width = "1800" },
            new W.GridColumn { Width = "1500" }, new W.GridColumn { Width = "2200" },
            new W.GridColumn { Width = "1500" }));
        table.Append(new W.TableRow(
            TableCell("Reçu", true), TableCell("Date", true), TableCell("Moyen", true),
            TableCell("Référence", true), TableCell("Montant FCFA", true)));
        foreach (var payment in order.Payments)
            table.Append(new W.TableRow(
                TableCell(payment.ReceiptNo),
                TableCell(payment.PaymentAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("fr-FR"))),
                TableCell(payment.PaymentMethod), TableCell(payment.Reference), TableCell(FormatMoney(payment.Amount))));
        return table;
    }

    private static W.TableCell TableCell(string text, bool header = false)
    {
        var color = header ? "FFFFFF" : "263746";
        var runProperties = new W.RunProperties();
        if (header) runProperties.Append(new W.Bold());
        runProperties.Append(new W.Color { Val = color }, new W.FontSize { Val = "18" });
        var paragraph = new W.Paragraph(new W.ParagraphProperties(new W.SpacingBetweenLines { Before = "80", After = "80" }),
            new W.Run(runProperties, new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        var fill = header ? "10344B" : "FFFFFF";
        return new W.TableCell(new W.TableCellProperties(new W.Shading { Fill = fill, Val = W.ShadingPatternValues.Clear }), paragraph);
    }

    private static W.Paragraph Paragraph(string text, int halfPointSize = 22, bool bold = false, string color = "263746", W.JustificationValues? alignment = null)
    {
        var properties = new W.ParagraphProperties();
        properties.Append(new W.SpacingBetweenLines { Before = "60", After = "100" });
        if (alignment.HasValue) properties.Append(new W.Justification { Val = alignment.Value });
        var runProperties = new W.RunProperties();
        if (bold) runProperties.Append(new W.Bold());
        runProperties.Append(new W.Color { Val = color }, new W.FontSize { Val = halfPointSize.ToString(CultureInfo.InvariantCulture) });
        return new W.Paragraph(properties, new W.Run(runProperties, new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static string FormatMoney(decimal value) => value.ToString("N0", CultureInfo.GetCultureInfo("fr-FR")).Replace('\u202F', ' ').Replace('\u00A0', ' ');
}
