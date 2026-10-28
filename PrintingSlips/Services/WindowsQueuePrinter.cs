using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.Versioning;
using PrintingSlips.Models;

namespace PrintingSlips.Services;

/// <summary>
/// Печать через очередь Windows (GDI, System.Drawing.Printing.PrintDocument).
/// Задание проходит через драйвер принтера (XPrinter), поэтому применяются его настройки
/// (плотность, отрезка, поля и т.п.), а шрифт берётся из PrinterDestination.FontFamily.
/// Работает и для USB, и для сетевых (IP) принтеров, установленных в Windows как очередь.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsQueuePrinter
{
    private const string FallbackFont = "Courier New";

    public static void Print(string printerName, IReadOnlyList<PrintTextLine> lines, string? fontFamily,
        float widthMm, string documentName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new Exception("Не указано имя принтера (очередь Windows).");

        widthMm = widthMm > 0 ? widthMm : 80;
        var family = ResolveFontFamily(fontFamily);

        using var doc = new PrintDocument();
        doc.DocumentName = documentName;
        doc.PrintController = new StandardPrintController(); // без окна "Идёт печать"
        doc.PrinterSettings.PrinterName = printerName;

        if (!doc.PrinterSettings.IsValid)
            throw new Exception($"Принтер [{printerName}] не найден в Windows. Проверьте имя очереди печати.");

        var widthHundredths = (int)Math.Round(widthMm / 25.4f * 100f);

        // Предварительный расчёт высоты, чтобы весь чек поместился на одну «страницу» рулона.
        var heightHundredths = MeasureHeight(lines, family, widthMm, widthHundredths) + 50;
        heightHundredths = Math.Clamp(heightHundredths, 100, 20000); // до ~5 м

        doc.DefaultPageSettings.PaperSize = new PaperSize("Receipt", widthHundredths, heightHundredths);
        doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

        doc.PrintPage += (_, e) =>
        {
            var g = e.Graphics!;
            g.PageUnit = GraphicsUnit.Display; // 1/100 дюйма
            // Печатная область драйвера (учитывает аппаратные поля XPrinter).
            var area = e.MarginBounds.Width > 0 ? e.MarginBounds : e.PageBounds;
            float printableWidth = Math.Min(area.Width, widthHundredths);
            if (e.PageSettings.PrintableArea.Width > 0)
                printableWidth = Math.Min(printableWidth, e.PageSettings.PrintableArea.Width);
            float left = Math.Max(0, area.Left - e.PageSettings.HardMarginX);
            float y = 0;
            DrawLines(g, lines, family, widthMm, left, printableWidth, ref y);
            e.HasMorePages = false;
        };

        doc.Print();
    }

    private static void DrawLines(Graphics g, IReadOnlyList<PrintTextLine> lines, string family, float widthMm,
        float left, float width, ref float y)
    {
        foreach (var line in lines)
        {
            using var font = CreateFont(family, line, widthMm);
            if (line.IsSeparator)
            {
                var h = font.GetHeight(g);
                using var pen = new Pen(Color.Black, 1);
                g.DrawLine(pen, left, y + h / 2, left + width, y + h / 2);
                y += h;
                continue;
            }

            var rect = new RectangleF(left, y, width, 10000);
            if (line.RightText != null)
            {
                var rightSize = g.MeasureString(line.RightText, font);
                using var rf = new StringFormat { Alignment = StringAlignment.Far };
                var leftWidth = Math.Max(10, width - rightSize.Width - 4);
                var leftSize = g.MeasureString(line.Text, font, (int)leftWidth);
                if (string.IsNullOrEmpty(line.Text) || leftSize.Height <= font.GetHeight(g) * 1.2f)
                {
                    using var lf = new StringFormat();
                    g.DrawString(line.Text, font, Brushes.Black, new RectangleF(left, y, leftWidth, 10000), lf);
                    g.DrawString(line.RightText, font, Brushes.Black, rect, rf);
                    y += Math.Max(Math.Max(leftSize.Height, rightSize.Height), font.GetHeight(g));
                }
                else
                {
                    // Не помещается — левая часть отдельной строкой, правая ниже.
                    var sz = g.MeasureString(line.Text, font, (int)width);
                    g.DrawString(line.Text, font, Brushes.Black, rect);
                    y += sz.Height;
                    g.DrawString(line.RightText, font, Brushes.Black, new RectangleF(left, y, width, 10000), rf);
                    y += Math.Max(rightSize.Height, font.GetHeight(g));
                }
                continue;
            }

            using var sf = new StringFormat { Alignment = ToAlignment(line.Align) };
            var text = string.IsNullOrEmpty(line.Text) ? " " : line.Text;
            var size = g.MeasureString(text, font, (int)width, sf);
            g.DrawString(text, font, Brushes.Black, rect, sf);
            y += Math.Max(size.Height, font.GetHeight(g));
        }
    }

    private static int MeasureHeight(IReadOnlyList<PrintTextLine> lines, string family, float widthMm, int widthHundredths)
    {
        using var bmp = new Bitmap(1, 1);
        bmp.SetResolution(100, 100);
        using var g = Graphics.FromImage(bmp);
        g.PageUnit = GraphicsUnit.Display;
        float y = 0;
        DrawLines(g, lines, family, widthMm, 0, widthHundredths, ref y);
        return (int)Math.Ceiling(y * 1.05f);
    }

    private static Font CreateFont(string family, PrintTextLine line, float widthMm) =>
        new(family, MapSize(line.FontSize, widthMm), line.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);

    /// <summary>1 → ~7.5pt на 58 мм (~10pt на 80 мм), 2 → ×1.5, 3 → ×2.</summary>
    public static float MapSize(int fontSize, float widthMm)
    {
        var basePt = 7.5f * Math.Clamp(widthMm / 58f, 0.8f, 1.5f);
        return Math.Clamp(fontSize, 1, 3) switch
        {
            2 => basePt * 1.5f,
            3 => basePt * 2f,
            _ => basePt
        };
    }

    private static string ResolveFontFamily(string? fontFamily)
    {
        if (string.IsNullOrWhiteSpace(fontFamily)) return FallbackFont;
        try
        {
            using var ff = new FontFamily(fontFamily);
            return ff.Name;
        }
        catch (ArgumentException)
        {
            return FallbackFont; // шрифт не установлен
        }
    }

    private static StringAlignment ToAlignment(PrintTextAlign align) => align switch
    {
        PrintTextAlign.Center => StringAlignment.Center,
        PrintTextAlign.Right => StringAlignment.Far,
        _ => StringAlignment.Near
    };
}
