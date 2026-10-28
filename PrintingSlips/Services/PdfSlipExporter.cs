using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using PrintingSlips.Interfaces;
using PrintingSlips.Models;

namespace PrintingSlips.Services;

/// <summary>
/// Сервис экспорта печатных форм в PDF формат на базе библиотеки QuestPDF.
/// Предоставляет экспорт в PDF для каждой создаваемой формы: накладная, кухонный чек,
/// динамический шаблон и тестовая страница.
/// </summary>
public class PdfSlipExporter : IPdfSlipExporter
{
    static PdfSlipExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
    }

    public byte[] ExportFormPdf(
        PrintFormType formType,
        Slip slip,
        float widthMm = 80,
        IReadOnlyList<ReceiptField>? fields = null,
        string fontFamily = "Courier New")
    {
        var family = string.IsNullOrWhiteSpace(fontFamily) ? "Courier New" : fontFamily;
        return formType switch
        {
            PrintFormType.KitchenSlip => ExportKitchenSlipPdf(slip, widthMm, family),
            PrintFormType.CustomTemplate when fields is { Count: > 0 } => ExportTemplatePdf(slip, widthMm, fields, family),
            PrintFormType.TestPage => ExportTestPagePdf(widthMm, family),
            _ => ExportInvoicePdf(slip, widthMm, family)
        };
    }

    public string SavePdfToFile(
        string outputDirectory,
        PrintFormType formType,
        Slip slip,
        float widthMm = 80,
        IReadOnlyList<ReceiptField>? fields = null)
    {
        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        var prefix = formType switch
        {
            PrintFormType.KitchenSlip => "kitchen",
            PrintFormType.CustomTemplate => "custom_template",
            PrintFormType.TestPage => "test_page",
            _ => "invoice"
        };

        var fileName = $"{prefix}_order_{slip.OrderId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        var fullPath = Path.Combine(outputDirectory, fileName);

        var pdfBytes = ExportFormPdf(formType, slip, widthMm, fields);
        File.WriteAllBytes(fullPath, pdfBytes);
        return fullPath;
    }

    #region Экспорт накладной (Invoice)

    [Obsolete("Obsolete")]
    public byte[] ExportInvoicePdf(Slip slip, float widthMm = 58, string fontFamily = "Courier New")
    {
        fontFamily = string.IsNullOrWhiteSpace(fontFamily) ? "Courier New" : fontFamily;
        float widthInPoints = widthMm * 2.83465f;
        var baseSize = widthMm <= 60 ? 7f : 8f;
        var dateStr = (slip.OrderDate ?? DateTime.Now).ToString("HH:mm dd.MM.yyyy");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(widthInPoints);
                page.Margin(6);
                page.DefaultTextStyle(x => x.FontFamily(fontFamily).FontSize(baseSize));

                page.Content().Column(col =>
                {
                    col.Spacing(0);

                    col.Item().PaddingTop(10).AlignLeft().Text(new string('*', 32));
                    col.Item().AlignLeft().Text(slip.SellerTitle ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(slip.SellerName))
                        col.Item().AlignLeft().Text(slip.SellerName);
                    if (!string.IsNullOrWhiteSpace(slip.SellerInn))
                        col.Item().AlignLeft().Text($"ИНН {slip.SellerInn}");
                    col.Item().AlignLeft().Text(new string('*', 32));

                    col.Item().PaddingVertical(3).AlignCenter().Column(c =>
                    {
                        c.Item().AlignCenter().Text("НАКЛАДНАЯ").FontSize(10);
                        c.Item().AlignCenter().Text($"Заказ № {slip.OrderId}").FontSize(10);
                    });

                    col.Item().Text($"Грузоотправитель:\n{slip.SellerTitle}\nИНН {slip.SellerInn}");
                    col.Item().Text($"Грузополучатель:\n{(!string.IsNullOrWhiteSpace(slip.CustomerName) ? slip.CustomerName : "Покупатель")}");
                    if (!string.IsNullOrWhiteSpace(slip.CustomerPhone))
                        col.Item().Text(slip.CustomerPhone);

                    col.Item().PaddingVertical(4).Text($"Менеджер: {slip.Manager}");
                    

                    col.Item().Row(row => {
                        row.RelativeItem(6).Text("Заказ принят:");
                        // ClampLines(1) принудительно оставляет текст в одну строку
                        row.RelativeItem(4).AlignRight().Text(dateStr);
                    });

                    col.Item().Row(row => {
                        row.RelativeItem(6).Text("Накладная оформлена:");
                        row.RelativeItem(4).AlignRight().Text(dateStr);
                    });

                    if (!string.IsNullOrWhiteSpace(slip.DeliveryTime))
                    {
                        col.Item().Row(row => {
                            row.RelativeItem(6).Text("Время доставки:");
                            row.RelativeItem(4).AlignRight().Text(slip.DeliveryTime);
                        });
                    }

                    col.Item().PaddingBottom(8).Row(row => {
                        row.RelativeItem(6).Text("Заказ доставлен:");
                        var deliveredText = !string.IsNullOrWhiteSpace(slip.DeliveredAt) ? slip.DeliveredAt : "----------";
                        row.RelativeItem(4).AlignRight().Text(deliveredText);
                    });

                    
                    col.Item().Text($"Количество человек: {slip.PersonsCount}");
                    

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });
                        
                        table.Header(header =>
                        {
                            // Убрали BorderBottom(1), оставили только BorderTop(1)
                            header.Cell().ColumnSpan(4).PaddingBottom(2).Column(column =>
                            {
                                column.Item().Text(new string('-', 36));
                                // Элемент 1: Строка с названиями колонок
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(3).Text("Наименование");
                                    row.RelativeItem().AlignRight().Text("К-во");
                                    row.RelativeItem().AlignRight().Text("Цена");
                                    row.RelativeItem().AlignRight().Text("Сумма");
                                });
                                column.Item().Text(new string('-', 36));
                                
                            });
                        });
                        
                        foreach (var item in slip.Items)
                        {
                            table.Cell().ColumnSpan(4).Text(item.Name ?? "Товар");
                            table.Cell().Text("");
                            table.Cell().AlignRight().Text($"{item.Quantity ?? 1} шт x");
                            table.Cell().AlignRight().Text($"{item.Price?.ToString("F2") ?? "0.00"}");
                            table.Cell().AlignRight().Text($"{item.TotalPrice?.ToString("F2") ?? "0.00"}");
                        }
                    });

                    col.Item().Text(new string('-', 36));

                    var totalStr = slip.TotalAmount?.ToString("F2") ?? "0.00";
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Итого к оплате:");
                        row.RelativeItem().AlignRight().Text(totalStr);
                    });
                    
                    col.Item().Text(new string('-', 36));

                    var payMethod = !string.IsNullOrWhiteSpace(slip.PaymentMethod) ? slip.PaymentMethod : "Наличные";
                    col.Item().PaddingBottom(20).Row(row =>
                    {
                        row.RelativeItem(6).Text(payMethod);
                        row.RelativeItem(4).AlignRight().Text(totalStr);
                    });
                    
                    col.Item().PaddingBottom(4).Text(slip.SignReleased ?? string.Empty);
                    col.Item().PaddingBottom(4).Text(slip.SignReceived ?? string.Empty);


                    if (!string.IsNullOrWhiteSpace(slip.CustomerNote))
                    {
                        col.Item().Text($"Комментарий к заказу:\n{slip.CustomerNote}");
                    }
                    else
                    {
                        col.Item().Text($"Комментарий к заказу:");
                    }

                    col.Item().Text($"Способ оплаты: {payMethod}");
                    col.Item().Text($"Количество персон: {slip.PersonsCount}");
                    col.Item().Text($"Канал продаж: {slip.SalesChannel}");
                    col.Item().Text($"Номер заказа FN: {slip.OrderId}");
                    col.Item().Text(new string('-', 36));
                    col.Item().Text(new string('-', 36));
                    col.Item().Text(new string('-', 36));
                    col.Item().Text(new string('-', 36));
                    col.Item().PaddingTop(4).AlignLeft().Text(slip.DeliveryType ?? string.Empty).Bold().FontSize(11);
                    col.Item().PaddingTop(3).AlignCenter().Text(slip.CurrencyNote ?? string.Empty).FontSize(7);
                    col.Item().AlignCenter().Text(slip.Footer ?? string.Empty).FontSize(7);
                });
            });
        });

        return doc.GeneratePdf();
    }

    #endregion

    #region Экспорт кухонного чека (Kitchen Slip)

    public byte[] ExportKitchenSlipPdf(Slip slip, float widthMm = 80, string fontFamily = "Courier New")
    {
        fontFamily = string.IsNullOrWhiteSpace(fontFamily) ? "Courier New" : fontFamily;
        float widthInPoints = widthMm * 2.83465f;
        var baseSize = widthMm <= 60 ? 7.5f : 8.5f;
        var currentTime = DateTime.Now.ToString("HH:mm");
        var orderTime = slip.OrderDate?.ToString("HH:mm") ?? currentTime;
        var minusTimes = 42;
        var minusTimesForLittleFont = 71;
        var subFontSize = 5;
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(widthInPoints);
                page.Margin(6);
                page.MarginTop(30);
                page.DefaultTextStyle(x => x.FontFamily(fontFamily).FontSize(baseSize));

                page.Content().Column(col =>
                {
                    col.Spacing(0);
                    col.Item().Text(new string('-', minusTimes));
                    col.Item().AlignCenter().Text(currentTime).FontSize(13);
                    col.Item().Text(new string('-', minusTimes));
                    col.Item().AlignCenter().Text(slip.DeliveryType ?? "ДОСТАВКА").FontSize(13);
                    col.Item().AlignCenter().Text($"ЗАКАЗ № {slip.OrderId}").FontSize(13);
                    
                    col.Item().PaddingVertical(10).Column(column =>
                    {
                        column.Item().Text($"Оформлен: {orderTime}");
                        if (!string.IsNullOrWhiteSpace(slip.DeliveryTime))
                            column.Item().Text($"Доставить к: {slip.DeliveryTime}");
                    });

                    col.Item().Text(new string('-', minusTimes));
                    col.Item().Row(r =>
                    {
                        r.RelativeItem(3).Text("Название");
                        r.RelativeItem().AlignRight().Text("Кол-во");
                    });

                    foreach (var item in slip.Items)
                    {
                        col.Item().Text(new string('-', minusTimes));
                        col.Item().Row(r =>
                        {
                            r.RelativeItem(3).Text(item.Name ?? "Товар");
                            r.RelativeItem().AlignRight().Text($"{item.Quantity ?? 1}").FontSize(baseSize + 1);
                        });
                    }

                    col.Item().PaddingTop(15).Text(new string('-', minusTimesForLittleFont)).FontSize(subFontSize);
                    col.Item().Text($"Адрес: {slip.ShippingAddress ?? slip.BillingAddress1 ?? "Не указан"}").FontSize(subFontSize);
                    if (!string.IsNullOrWhiteSpace(slip.CustomerNote))
                        col.Item().Text($"Ком. к заказу: {slip.CustomerNote}").FontSize(subFontSize);
                    var recipient = !string.IsNullOrWhiteSpace(slip.CustomerName) ? slip.CustomerName : "Покупатель";
                    col.Item().Text($"Способ оплаты: {slip.PaymentMethod ?? "Не указана"}").FontSize(subFontSize);
                    col.Item().Text($"Получатель: {recipient}").FontSize(subFontSize);
                    if (!string.IsNullOrWhiteSpace(slip.CustomerPhone))
                        col.Item().Text($"Телефон: {slip.CustomerPhone}").FontSize(subFontSize);

                    // col.Item().Text($"Итого: {slip.TotalAmount?.ToString("F2") ?? "0.00"} руб.").FontSize(subFontSize);
                    col.Item().Text($"Канал продаж: {slip.SalesChannel}").FontSize(subFontSize);
                    col.Item().Text($"Номер заказа FN: {slip.OrderId}").FontSize(subFontSize);
                });
            });
        });

        return doc.GeneratePdf();
    }

    #endregion

    #region Экспорт по динамическому шаблону (ReceiptField)

    public byte[] ExportTemplatePdf(Slip slip, float widthMm, IReadOnlyList<ReceiptField> fields, string fontFamily = "Courier New")
    {
        fontFamily = string.IsNullOrWhiteSpace(fontFamily) ? "Courier New" : fontFamily;
        float widthInPoints = widthMm * 2.83465f;
        var baseSize = widthMm <= 60 ? 7f : 8f;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(widthInPoints);
                page.Margin(6);
                page.DefaultTextStyle(x => x.FontFamily(fontFamily).FontSize(baseSize));

                page.Content().Column(col =>
                {
                    col.Spacing(2);

                    foreach (var line in PrintDocumentBuilder.BuildTemplateLines(slip, fields))
                    {
                        var text = string.IsNullOrWhiteSpace(line.Label)
                            ? line.Value
                            : $"{line.Label}: {line.Value}";

                        var fontSize = line.Field.FontSize switch
                        {
                            2 => baseSize + 2,
                            3 => baseSize + 5,
                            _ => baseSize
                        };

                        var cell = col.Item();
                        cell = line.Field.GetPrintAlign() switch
                        {
                            PrintTextAlign.Center => cell.AlignCenter(),
                            PrintTextAlign.Right => cell.AlignRight(),
                            _ => cell.AlignLeft()
                        };
                        var item = cell.Text(text).FontSize(fontSize);
                        if (line.Field.IsBold)
                            item.Bold();
                    }
                });
            });
        });

        return doc.GeneratePdf();
    }

    #endregion

    #region Экспорт тестовой страницы (Test Page)

    public byte[] ExportTestPagePdf(float widthMm = 80, string fontFamily = "Courier New")
    {
        fontFamily = string.IsNullOrWhiteSpace(fontFamily) ? "Courier New" : fontFamily;
        float widthInPoints = widthMm * 2.83465f;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(widthInPoints);
                page.Margin(6);
                page.DefaultTextStyle(x => x.FontFamily(fontFamily).FontSize(8f));

                page.Content().Column(col =>
                {
                    col.Spacing(2);

                    col.Item().AlignCenter().Text("TEST / ТЕСТ ПЕЧАТИ").Bold().FontSize(12);
                    col.Item().AlignCenter().Text("XP-58C / XP-80C");
                    col.Item().AlignCenter().Text($"Ширина ленты: {widthMm} мм");
                    col.Item().LineHorizontal(0.5f);

                    col.Item().Text("Кодировка: CP866 / CP1251");
                    col.Item().Text("RU: Тестовый заказ № 123");
                    col.Item().Text("EN: Test order # 123");
                    col.Item().Text(new string('0', (int)(widthMm <= 60 ? 32 : 48)));
                    col.Item().LineHorizontal(0.5f);

                    col.Item().Text("Размер шрифта 1 (Обычный)").FontSize(8);
                    col.Item().Text("Размер шрифта 2 (Увеличенный)").FontSize(10);
                    col.Item().Text("Размер шрифта 3 (Заголовок)").FontSize(12);
                    col.Item().Text("Bold / Жирный шрифт").Bold();
                    col.Item().Text("Тест отрезчика бумаги: OK");
                    col.Item().LineHorizontal(0.5f);
                    col.Item().AlignCenter().Text("ТЕСТ УСПЕШНО ЗАВЕРШЕН").Bold();
                });
            });
        });

        return doc.GeneratePdf();
    }

    #endregion
}