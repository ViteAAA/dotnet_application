using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using PrintingSlips.Models;
using PrintingSlips.Interfaces;

namespace PrintingSlips.Services;

public class PrintingService(ITemplateProvider? templateProvider = null) : IXPrintingService
{
    private readonly ITemplateProvider? _templateProvider = templateProvider;

    private static readonly byte[] EscInitialize = [0x1B, 0x40];
    private static readonly byte[] EscAlignCenter = [0x1B, 0x61, 0x01];
    private static readonly byte[] EscAlignLeft = [0x1B, 0x61, 0x00];
    private static readonly byte[] EscAlignRight = [0x1B, 0x61, 0x02];
    private static readonly byte[] EscBoldOn = [0x1B, 0x45, 0x01];
    private static readonly byte[] EscBoldOff = [0x1B, 0x45, 0x00];
    private static readonly byte[] EscCutPaper = [0x1D, 0x56, 0x42, 0x00];

    private sealed record PrinterTextProfile(Encoding Encoding, byte? Page, string Description);

    public async Task<bool> PrintSlipAsync(Slip slip, PrinterDestination printer)
    {
        var slipId = slip.OrderId;
        var dateStr = (slip.OrderDate ?? DateTime.Now).ToString("dd.MM.yyyy HH:mm");
        var width = printer.WidthMm > 0 ? printer.WidthMm : 80;
        var textProfile = GetPrinterTextProfile(printer.Name);
        var fields = _templateProvider?.GetTemplateForPrinter(printer.Name);

        GeneratePdfPreview(slip, slipId, dateStr, width, fields);

        var buffer = fields is { Count: > 0 }
            ? GenerateTemplateBuffer(slip, width, fields, textProfile)
            : width > 60
                ? GenerateKitchenSlipBuffer(slip, width, textProfile)
                : GenerateSlipBuffer(slip, width, textProfile);

        return await SendToDestinationAsync(printer, buffer, $"Slip_{slipId}");
        // return await Task.Run(() => true);
    }

    public async Task<bool> PrintTestPageAsync(PrinterDestination printer)
    {
        var width = printer.WidthMm > 0 ? printer.WidthMm : 80;
        var charsPerLine = GetCharsPerLine(width);
        var textProfile = GetPrinterTextProfile(printer.Name);
        var encoding = textProfile.Encoding;
        var bytes = new List<byte>();

        AddPrinterInitialization(bytes, textProfile);
        bytes.AddRange(EscAlignCenter);
        bytes.AddRange(EscBoldOn);
        bytes.AddRange(GetFontSizeCommand(2));
        AddKitchenLine(bytes, encoding, "TEST / ТЕСТ", charsPerLine);
        bytes.AddRange(EscBoldOff);
        bytes.AddRange(GetFontSizeCommand(1));
        AddKitchenLine(bytes, encoding, "XP-58C / XP-80C", charsPerLine);
        AddKitchenLine(bytes, encoding, $"Width {width}mm / {charsPerLine} chars", charsPerLine);
        AddKitchenSeparator(bytes, encoding, charsPerLine);

        bytes.AddRange(EscAlignLeft);
        AddKitchenLine(bytes, encoding, $"Encoding: {textProfile.Description}", charsPerLine);
        AddKitchenLine(bytes, encoding, "RU: Привет, заказ №123", charsPerLine);
        AddKitchenLine(bytes, encoding, "EN: Hello, order #123", charsPerLine);
        AddKitchenLine(bytes, encoding, new string('0', charsPerLine), charsPerLine);

        bytes.AddRange(GetFontSizeCommand(1));
        AddKitchenLine(bytes, encoding, "Font size 1 / размер 1", charsPerLine);
        bytes.AddRange(GetFontSizeCommand(2));
        AddKitchenLine(bytes, encoding, "Font size 2 / размер 2", charsPerLine);
        bytes.AddRange(GetFontSizeCommand(3));
        AddKitchenLine(bytes, encoding, "Font size 3 / размер 3", charsPerLine);
        bytes.AddRange(GetFontSizeCommand(1));
        bytes.AddRange(EscBoldOn);
        AddKitchenLine(bytes, encoding, "Bold / Жирный", charsPerLine);
        bytes.AddRange(EscBoldOff);
        AddKitchenLine(bytes, encoding, "Cutter test / Отрезчик", charsPerLine);
        bytes.AddRange(encoding.GetBytes("\n\n\n"));
        bytes.AddRange(EscCutPaper);

        return await SendToDestinationAsync(printer, [.. bytes], "Printer_Test");
    }

    private static int GetCharsPerLine(float width) => width <= 60 ? 32 : 48;

    private async Task<bool> SendToDestinationAsync(PrinterDestination printer, byte[] buffer, string documentName)
    {
        if (string.Equals(printer.ConnectionType, "Network", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(printer.Host))
                throw new Exception("Для сетевого принтера не указан IP-адрес.");

            return await SendBytesToNetworkAsync(printer.Host, printer.Port > 0 ? printer.Port : 9100, buffer);
        }

        return await Task.Run(() => SendBytesToPrinter(printer.Name, buffer, documentName));
    }

    private static byte[] GenerateKitchenSlipBuffer(Slip slip, float width, PrinterTextProfile textProfile)
    {
        var bytes = new List<byte>();
        var charsPerLine = GetCharsPerLine(width);
        var encoding = textProfile.Encoding;
        var currentTime = DateTime.Now.ToString("HH:mm");
        var orderTime = slip.OrderDate?.ToString("HH:mm") ?? currentTime;
        var deliveryType = string.IsNullOrWhiteSpace(slip.DeliveryType) ? "Доставка" : slip.DeliveryType;
        var customerName = string.IsNullOrWhiteSpace(slip.CustomerName) ? "Не указан" : slip.CustomerName;

        AddPrinterInitialization(bytes, textProfile);
        bytes.AddRange(EscAlignCenter);
        bytes.AddRange(EscBoldOn);
        AddKitchenLine(bytes, encoding, currentTime, charsPerLine);
        AddKitchenLine(bytes, encoding, deliveryType, charsPerLine);
        AddKitchenLine(bytes, encoding, $"ЗАКАЗ № {slip.OrderId}", charsPerLine);
        bytes.AddRange(EscBoldOff);

        bytes.AddRange(EscAlignLeft);
        AddKitchenLine(bytes, encoding, $"Оформлен: {orderTime}", charsPerLine);
        AddKitchenLine(bytes, encoding, $"Доставить к: {slip.DeliveryTime}", charsPerLine);
        AddKitchenSeparator(bytes, encoding, charsPerLine);
        AddKitchenLine(bytes, encoding, "Название                 Кол-во", charsPerLine);
        AddKitchenSeparator(bytes, encoding, charsPerLine);

        foreach (var item in slip.Items)
        {
            var itemName = item.Name ?? "Товар";
            AddKitchenLine(bytes, encoding, itemName, charsPerLine);
            AddKitchenLine(bytes, encoding, $"{item.Quantity ?? 1}", charsPerLine);
        }

        AddKitchenLine(bytes, encoding, deliveryType, charsPerLine);
        AddKitchenSeparator(bytes, encoding, charsPerLine);
        AddKitchenLine(bytes, encoding, $"Адрес: {slip.ShippingAddress ?? slip.BillingAddress1 ?? "Не указан"}", charsPerLine);
        AddKitchenLine(bytes, encoding, $"Получатель: {customerName}", charsPerLine);
        if (!string.IsNullOrWhiteSpace(slip.CustomerPhone))
            AddKitchenLine(bytes, encoding, $"Телефон: {slip.CustomerPhone}", charsPerLine);
        if (!string.IsNullOrWhiteSpace(slip.CustomerNote))
            AddKitchenLine(bytes, encoding, $"Комментарий: {slip.CustomerNote}", charsPerLine);

        AddKitchenLine(bytes, encoding, $"Оплата: {slip.PaymentMethod ?? "Не указана"}", charsPerLine);
        AddKitchenLine(bytes, encoding, $"Итого: {slip.TotalAmount?.ToString("F2") ?? "0.00"}", charsPerLine);
        AddKitchenLine(bytes, encoding, $"Номер заказа FN: {slip.OrderId}", charsPerLine);
        bytes.AddRange(encoding.GetBytes("\n\n\n"));
        bytes.AddRange(EscCutPaper);

        return [.. bytes];
    }

    private static void AddKitchenLine(List<byte> bytes, Encoding encoding, string text, int charsPerLine)
    {
        foreach (var line in WrapText(text ?? string.Empty, charsPerLine))
        {
            bytes.AddRange(encoding.GetBytes(line));
            bytes.Add((byte)'\n');
        }
    }

    private static void AddKitchenSeparator(List<byte> bytes, Encoding encoding, int charsPerLine)
    {
        bytes.AddRange(encoding.GetBytes(new string('-', charsPerLine)));
        bytes.Add((byte)'\n');
    }

    private static byte[] GenerateTemplateBuffer(Slip slip, float width, IReadOnlyList<ReceiptField> fields,
        PrinterTextProfile textProfile)
    {
        var bytes = new List<byte>();
        var charsPerLine = GetCharsPerLine(width);
        var encoding = textProfile.Encoding;

        AddPrinterInitialization(bytes, textProfile);
        foreach (var line in BuildTemplateLines(slip, fields))
        {
            AddField(bytes, encoding, line.Label, line.Value, line.Field, charsPerLine);
        }

        bytes.AddRange(EscAlignLeft);
        bytes.AddRange(encoding.GetBytes("\n\n\n"));
        bytes.AddRange(EscCutPaper);
        return [.. bytes];
    }

    private static IEnumerable<(string Label, string Value, ReceiptField Field)> BuildTemplateLines(
        Slip slip, IReadOnlyList<ReceiptField> fields)
    {
        foreach (var field in fields)
        {
            if (field.Key == "block.items")
            {
                foreach (var item in slip.Items)
                {
                    foreach (var child in field.ChildFields)
                    {
                        yield return (child.Name, ResolveItemValue(child.Key, item), child);
                    }
                }

                continue;
            }

            if (field.Key == "static.text")
            {
                yield return (string.Empty,
                    string.IsNullOrWhiteSpace(field.CustomText) ? field.Name : field.CustomText,
                    field);
                continue;
            }

            yield return (field.Name, ResolveOrderValue(field.Key, slip), field);
        }
    }

    private static void AddField(List<byte> bytes, Encoding encoding, string label, string value,
        ReceiptField field, int charsPerLine)
    {
        bytes.AddRange(EscAlignLeft);
        bytes.AddRange(GetFontSizeCommand(field.FontSize));
        bytes.AddRange(field.IsBold ? EscBoldOn : EscBoldOff);

        var text = string.IsNullOrWhiteSpace(label) ? value : $"{label}: {value}";
        foreach (var line in WrapText(text, charsPerLine))
        {
            bytes.AddRange(encoding.GetBytes(line));
            bytes.Add((byte)'\n');
        }
    }

    private static IEnumerable<string> WrapText(string text, int width)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return string.Empty;
            yield break;
        }

        for (var index = 0; index < text.Length; index += width)
        {
            yield return text.Substring(index, Math.Min(width, text.Length - index));
        }
    }

    private static byte[] GetFontSizeCommand(int fontSize)
    {
        var mode = Math.Clamp(fontSize, 1, 3) switch
        {
            2 => 0x10,
            3 => 0x30,
            _ => 0x00
        };

        return [0x1B, 0x21, (byte)mode];
    }

    private static PrinterTextProfile GetPrinterTextProfile(string printerName)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        if (printerName.Contains("XP-N160II", StringComparison.OrdinalIgnoreCase))
            return new PrinterTextProfile(Encoding.GetEncoding(1251), 23, "Windows-1251 (Page 23)");

        if (printerName.Contains("XP-80T", StringComparison.OrdinalIgnoreCase))
            return new PrinterTextProfile(Encoding.GetEncoding(866), 17, "CP866 (Page 17)");

        return new PrinterTextProfile(Encoding.GetEncoding(866), null, "CP866");
    }

    private static void AddPrinterInitialization(List<byte> bytes, PrinterTextProfile textProfile)
    {
        bytes.AddRange(EscInitialize);
        if (textProfile.Page is byte page)
            bytes.AddRange([0x1B, 0x74, page]);
    }

    private static string ResolveOrderValue(string key, Slip slip) => key switch
    {
        "order.number" => slip.OrderId,
        "order.status" => slip.OrderStatus ?? string.Empty,
        "order.date_created" => slip.OrderDate?.ToString("dd.MM.yyyy HH:mm") ?? string.Empty,
        "order.payment_method_title" => slip.PaymentMethod ?? string.Empty,
        "order.customer_note" => slip.CustomerNote ?? string.Empty,
        "order.currency" => slip.Currency ?? string.Empty,
        "billing.first_name" => slip.BillingFirstName ?? string.Empty,
        "billing.last_name" => slip.BillingLastName ?? string.Empty,
        "billing.phone" => slip.CustomerPhone ?? string.Empty,
        "billing.email" => slip.CustomerEmail ?? string.Empty,
        "billing.city" => slip.BillingCity ?? string.Empty,
        "billing.address_1" => slip.BillingAddress1 ?? string.Empty,
        "billing.postcode" => slip.BillingPostcode ?? string.Empty,
        "shipping.first_name" => slip.ShippingFirstName ?? string.Empty,
        "shipping.last_name" => slip.ShippingLastName ?? string.Empty,
        "shipping.city" => slip.ShippingCity ?? string.Empty,
        "shipping.address_1" => slip.ShippingAddress ?? string.Empty,
        "order.total" => slip.TotalAmount?.ToString("F2") ?? "0.00",
        "order.shipping_total" => slip.ShippingTotal?.ToString("F2") ?? "0.00",
        "order.discount_total" => slip.DiscountTotal?.ToString("F2") ?? "0.00",
        "order.shipping_method" => slip.ShippingMethod ?? slip.DeliveryType ?? string.Empty,
        "order.delivery_type" => slip.DeliveryType ?? string.Empty,
        "order.delivery_time" => slip.DeliveryTime ?? string.Empty,
        "order.delivered_at" => slip.DeliveredAt ?? string.Empty,
        "order.persons" => slip.PersonsCount ?? string.Empty,
        "order.manager" => slip.Manager ?? string.Empty,
        "order.seller_title" => slip.SellerTitle ?? string.Empty,
        "order.seller_name" => slip.SellerName ?? string.Empty,
        "order.seller_inn" => slip.SellerInn ?? string.Empty,
        "order.delivery_code" => slip.DeliveryCode ?? string.Empty,
        "order.sales_channel" => slip.SalesChannel ?? string.Empty,
        "order.sign_released" => slip.SignReleased ?? string.Empty,
        "order.sign_received" => slip.SignReceived ?? string.Empty,
        "order.currency_note" => slip.CurrencyNote ?? string.Empty,
        "order.footer" => slip.Footer ?? string.Empty,
        _ => string.Empty
    };

    private static string ResolveItemValue(string key, SlipItem item) => key switch
    {
        "item.name" => item.Name ?? string.Empty,
        "item.quantity" => (item.Quantity ?? 1).ToString(),
        "item.price" => item.Price?.ToString("F2") ?? "0.00",
        "item.subtotal" => item.TotalPrice?.ToString("F2") ?? "0.00",
        "item.total" => item.TotalPrice?.ToString("F2") ?? "0.00",
        "item.sku" => item.Sku ?? string.Empty,
        _ => string.Empty
    };

    private static byte[] GenerateSlipBuffer(Slip slip, float width, PrinterTextProfile textProfile)
    {
        var bytes = new List<byte>();
        int charsPerLine = GetCharsPerLine(width);
        string starsLine = new string('*', charsPerLine) + "\n";
        string separatorLine = new string('-', charsPerLine) + "\n";
        var encoding = textProfile.Encoding;

        AddPrinterInitialization(bytes, textProfile);
        bytes.AddRange(EscAlignCenter);
        bytes.AddRange(encoding.GetBytes(starsLine));
        bytes.AddRange(encoding.GetBytes($"{slip.SellerTitle}\n"));
        if (!string.IsNullOrWhiteSpace(slip.SellerName))
            bytes.AddRange(encoding.GetBytes($"{slip.SellerName}\n"));
        if (!string.IsNullOrWhiteSpace(slip.SellerInn))
            bytes.AddRange(encoding.GetBytes($"ИНН {slip.SellerInn}\n"));
        bytes.AddRange(encoding.GetBytes(starsLine));
        bytes.AddRange(encoding.GetBytes("\n"));

        bytes.AddRange(EscBoldOn);
        bytes.AddRange(encoding.GetBytes("НАКЛАДНАЯ\n"));
        bytes.AddRange(encoding.GetBytes($"Заказ № {slip.OrderId}\n\n"));
        bytes.AddRange(EscBoldOff);

        bytes.AddRange(EscAlignLeft);
        bytes.AddRange(encoding.GetBytes($"Грузоотправитель:\n{slip.SellerTitle}\n"));
        if (!string.IsNullOrWhiteSpace(slip.SellerInn))
            bytes.AddRange(encoding.GetBytes($"ИНН {slip.SellerInn}\n\n"));

        var recipientName = !string.IsNullOrWhiteSpace(slip.CustomerName) ? slip.CustomerName : "Покупатель";
        bytes.AddRange(encoding.GetBytes($"Грузополучатель:\n{recipientName}\n"));
        if (!string.IsNullOrWhiteSpace(slip.CustomerPhone))
            bytes.AddRange(encoding.GetBytes($"{slip.CustomerPhone}\n"));

        bytes.AddRange(encoding.GetBytes("\n"));
        bytes.AddRange(encoding.GetBytes($"Менеджер: {slip.Manager}\n"));

        var dateFormatted = slip.OrderDate?.ToString("HH:mm dd.MM.yyyy") ?? DateTime.Now.ToString("HH:mm dd.MM.yyyy");
        bytes.AddRange(encoding.GetBytes($"Заказ принят:\n{dateFormatted}\n"));
        bytes.AddRange(encoding.GetBytes($"Накладная оформлена: {dateFormatted}\n"));
        bytes.AddRange(encoding.GetBytes($"Время доставки: {slip.DeliveryTime}\n"));
        bytes.AddRange(encoding.GetBytes($"Заказ доставлен: {slip.DeliveredAt}\n"));

        bytes.AddRange(encoding.GetBytes(separatorLine));
        bytes.AddRange(encoding.GetBytes($"Количество человек: {slip.PersonsCount}\n"));
        bytes.AddRange(encoding.GetBytes(separatorLine));

        if (charsPerLine == 48)
            bytes.AddRange(encoding.GetBytes("Наименование          К-во      Цена    Сумма\n"));
        else
            bytes.AddRange(encoding.GetBytes("Наименование     К-во   Цена   Сумма\n"));

        bytes.AddRange(encoding.GetBytes(separatorLine));

        if (slip.Items.Count > 0)
        {
            foreach (var item in slip.Items)
            {
                var name = item.Name ?? "Товар";
                bytes.AddRange(encoding.GetBytes($"{name}\n"));

                var qtyStr = $"{item.Quantity ?? 1} шт";
                var priceStr = $"{item.Price?.ToString("F2") ?? "0.00"}";
                var totalStr = $"{item.TotalPrice?.ToString("F2") ?? "0.00"}";

                string detailLine = charsPerLine == 48
                    ? string.Format("  {0,-15} {1,8} x {2,8} {3,10}", "", qtyStr, priceStr, totalStr)
                    : string.Format("  {0,-10} {1,5} x {2,6} {3,7}", "", qtyStr, priceStr, totalStr);

                bytes.AddRange(encoding.GetBytes($"{detailLine}\n"));
            }
            bytes.AddRange(encoding.GetBytes(separatorLine));
        }

        var totalAmount = slip.TotalAmount?.ToString("F2") ?? "0.00";
        bytes.AddRange(EscBoldOn);
        bytes.AddRange(encoding.GetBytes($"Итого к оплате:               {totalAmount}\n\n"));
        bytes.AddRange(EscBoldOff);

        var payMethod = !string.IsNullOrWhiteSpace(slip.PaymentMethod) ? slip.PaymentMethod : "Наличные";
        bytes.AddRange(encoding.GetBytes($"{payMethod,-28} {totalAmount}\n"));
        bytes.AddRange(encoding.GetBytes(separatorLine));

        bytes.AddRange(encoding.GetBytes($"{slip.SignReleased}\n\n"));
        bytes.AddRange(encoding.GetBytes($"{slip.SignReceived}\n"));
        bytes.AddRange(encoding.GetBytes(separatorLine));

        if (!string.IsNullOrWhiteSpace(slip.CustomerNote))
            bytes.AddRange(encoding.GetBytes($"Комментарий к заказу:\n{slip.CustomerNote}\n"));

        bytes.AddRange(encoding.GetBytes($"Способ оплаты: {payMethod}\n"));
        bytes.AddRange(encoding.GetBytes($"Количество персон: {slip.PersonsCount}\n"));
        bytes.AddRange(encoding.GetBytes($"Канал продаж: {slip.SalesChannel}\n"));
        bytes.AddRange(encoding.GetBytes($"Номер заказа FN: {slip.OrderId}\n"));
        bytes.AddRange(encoding.GetBytes(separatorLine));

        bytes.AddRange(EscAlignCenter);
        bytes.AddRange(EscBoldOn);
        bytes.AddRange(encoding.GetBytes($"{slip.DeliveryType}\n"));
        if (!string.IsNullOrWhiteSpace(slip.DeliveryCode))
            bytes.AddRange(encoding.GetBytes($"{slip.DeliveryCode}\n\n"));
        bytes.AddRange(EscBoldOff);

        bytes.AddRange(encoding.GetBytes($"{slip.CurrencyNote}\n\n"));
        bytes.AddRange(encoding.GetBytes($"{slip.Footer}\n\n\n"));
        bytes.AddRange(EscCutPaper);

        return [.. bytes];
    }

    private static void GeneratePdfPreview(Slip slip, string slipId, string dateStr, float width,
        IReadOnlyList<ReceiptField>? fields)
    {
        try
        {
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseSystemFonts = true;

            string pdfPath = Path.Combine(AppContext.BaseDirectory, $"check_{slipId}.pdf");
            float widthInPoints = width * 2.83465f;
            var baseSize = width <= 60 ? 7f : 8f;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.ContinuousSize(widthInPoints);
                    page.Margin(6);
                    page.DefaultTextStyle(x => x.FontFamily("Courier New").FontSize(baseSize));

                    page.Content().Column(col =>
                    {
                        col.Spacing(2);

                        if (fields is { Count: > 0 })
                        {
                            foreach (var line in BuildTemplateLines(slip, fields))
                            {
                                var text = string.IsNullOrWhiteSpace(line.Label)
                                    ? line.Value
                                    : $"{line.Label}: {line.Value}";
                                var fontSize = PdfFontSize(line.Field.FontSize, baseSize);
                                var item = col.Item().Text(text).FontSize(fontSize);
                                if (line.Field.IsBold)
                                    item.Bold();
                            }

                            return;
                        }

                        col.Item().AlignCenter().Text(new string('*', 32));
                        col.Item().AlignCenter().Text(slip.SellerTitle ?? string.Empty);
                        if (!string.IsNullOrWhiteSpace(slip.SellerName))
                            col.Item().AlignCenter().Text(slip.SellerName);
                        if (!string.IsNullOrWhiteSpace(slip.SellerInn))
                            col.Item().AlignCenter().Text($"ИНН {slip.SellerInn}");
                        col.Item().AlignCenter().Text(new string('*', 32));

                        col.Item().PaddingVertical(3).AlignCenter().Column(c =>
                        {
                            c.Item().AlignCenter().Text("НАКЛАДНАЯ").Bold().FontSize(10);
                            c.Item().AlignCenter().Text($"Заказ № {slipId}").Bold().FontSize(10);
                        });

                        col.Item().Text($"Грузоотправитель:\n{slip.SellerTitle}\nИНН {slip.SellerInn}");
                        col.Item().Text($"Грузополучатель:\n{(!string.IsNullOrWhiteSpace(slip.CustomerName) ? slip.CustomerName : "Покупатель")}");
                        if (!string.IsNullOrWhiteSpace(slip.CustomerPhone))
                            col.Item().Text(slip.CustomerPhone);

                        col.Item().Text($"Менеджер: {slip.Manager}");
                        col.Item().Text($"Заказ принят: {dateStr}");
                        col.Item().Text($"Накладная оформлена: {dateStr}");
                        col.Item().Text($"Время доставки: {slip.DeliveryTime}");
                        col.Item().Text($"Заказ доставлен: {slip.DeliveredAt}");

                        col.Item().LineHorizontal(0.5f);
                        col.Item().Text($"Количество человек: {slip.PersonsCount}");
                        col.Item().LineHorizontal(0.5f);

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
                                header.Cell().Text("Наименование").Bold();
                                header.Cell().AlignRight().Text("К-во").Bold();
                                header.Cell().AlignRight().Text("Цена").Bold();
                                header.Cell().AlignRight().Text("Сумма").Bold();
                            });

                            foreach (var item in slip.Items)
                            {
                                table.Cell().ColumnSpan(4).Text(item.Name ?? "Товар").Bold();
                                table.Cell().Text("");
                                table.Cell().AlignRight().Text($"{item.Quantity ?? 1} шт");
                                table.Cell().AlignRight().Text($"{item.Price?.ToString("F2") ?? "0.00"}");
                                table.Cell().AlignRight().Text($"{item.TotalPrice?.ToString("F2") ?? "0.00"}");
                            }
                        });

                        col.Item().LineHorizontal(0.5f);

                        var totalStr = slip.TotalAmount?.ToString("F2") ?? "0.00";
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Итого к оплате:").Bold();
                            row.RelativeItem().AlignRight().Text(totalStr).Bold();
                        });

                        var payMethod = !string.IsNullOrWhiteSpace(slip.PaymentMethod) ? slip.PaymentMethod : "Наличные";
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text(payMethod);
                            row.RelativeItem().AlignRight().Text(totalStr);
                        });

                        col.Item().LineHorizontal(0.5f);
                        col.Item().Text(slip.SignReleased ?? string.Empty);
                        col.Item().Text(slip.SignReceived ?? string.Empty);
                        col.Item().LineHorizontal(0.5f);

                        if (!string.IsNullOrWhiteSpace(slip.CustomerNote))
                            col.Item().Text($"Комментарий к заказу:\n{slip.CustomerNote}");

                        col.Item().Text($"Способ оплаты: {payMethod}");
                        col.Item().Text($"Количество персон: {slip.PersonsCount}");
                        col.Item().Text($"Канал продаж: {slip.SalesChannel}");
                        col.Item().Text($"Номер заказа FN: {slip.OrderId}");
                        col.Item().LineHorizontal(0.5f);
                        col.Item().PaddingTop(4).AlignCenter().Text(slip.DeliveryType ?? string.Empty).Bold().FontSize(12);
                        col.Item().AlignCenter().Text(slip.DeliveryCode ?? string.Empty).Bold().FontSize(14);
                        col.Item().PaddingTop(4).AlignCenter().Text(slip.CurrencyNote ?? string.Empty).FontSize(7);
                        col.Item().AlignCenter().Text(slip.Footer ?? string.Empty).FontSize(7);
                    });
                });
            }).GeneratePdf(pdfPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PDF] Ошибка создания: {ex.Message}");
        }
    }

    private static float PdfFontSize(int fontSize, float baseSize) => fontSize switch
    {
        2 => baseSize + 2,
        3 => baseSize + 5,
        _ => baseSize
    };

    private static async Task<bool> SendBytesToNetworkAsync(string host, int port, byte[] bytes)
    {
        using var client = new TcpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await client.ConnectAsync(host, port, cts.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(bytes, cts.Token);
        await stream.FlushAsync(cts.Token);
        return true;
    }

    #region WinAPI RAW Printing

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    private static bool SendBytesToPrinter(string printerName, byte[] bytes, string documentName)
    {
        IntPtr pBytes = Marshal.AllocCoTaskMem(bytes.Length);
        Marshal.Copy(bytes, 0, pBytes, bytes.Length);

        if (!OpenPrinter(printerName.Normalize(), out var hPrinter, IntPtr.Zero))
        {
            Marshal.FreeCoTaskMem(pBytes);
            throw new Exception($"Не удалось открыть принтер [{printerName}]. Проверьте подключение.");
        }

        var di = new DOCINFOA
        {
            pDocName = documentName,
            pDataType = "RAW"
        };

        bool success = false;
        if (StartDocPrinter(hPrinter, 1, di))
        {
            if (StartPagePrinter(hPrinter))
            {
                success = WritePrinter(hPrinter, pBytes, bytes.Length, out _);
                EndPagePrinter(hPrinter);
            }
            EndDocPrinter(hPrinter);
        }

        ClosePrinter(hPrinter);
        Marshal.FreeCoTaskMem(pBytes);

        if (!success)
        {
            throw new Exception($"Ошибка передачи байтов печати в спулер Windows (Код: {Marshal.GetLastWin32Error()}).");
        }

        return true;
    }

    #endregion
}
