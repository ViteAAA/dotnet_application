using System.Text;
using PrintingSlips.Interfaces;
using PrintingSlips.Models;

namespace PrintingSlips.Services;

/// <summary>
/// Отдельный класс для формирования печатных форм (ESC/POS буферов и текстовой разметки).
/// Инкапсулирует форматирование накладных, кухонных чеков, шаблонов и тестовых страниц,
/// освобождая PrintingService от логики верстки документов.
/// </summary>
public class PrintDocumentBuilder : IPrintDocumentBuilder
{
    // Стандартные управляющие ESC/POS байты
    private static readonly byte[] EscInitialize = [0x1B, 0x40];
    private static readonly byte[] EscAlignCenter = [0x1B, 0x61, 0x01];
    private static readonly byte[] EscAlignLeft = [0x1B, 0x61, 0x00];
    private static readonly byte[] EscAlignRight = [0x1B, 0x61, 0x02];
    private static readonly byte[] EscBoldOn = [0x1B, 0x45, 0x01];
    private static readonly byte[] EscBoldOff = [0x1B, 0x45, 0x00];
    private static readonly byte[] EscCutPaper = [0x1D, 0x56, 0x42, 0x00];

    public PrintFormResult BuildForm(
        PrintFormType formType,
        Slip slip,
        float widthMm,
        PrinterTextProfile profile,
        IReadOnlyList<ReceiptField>? templateFields = null)
    {
        var chars = GetCharsPerLine(widthMm);
        byte[] bytes = formType switch
        {
            PrintFormType.KitchenSlip => BuildKitchenSlipEscPos(slip, widthMm, profile),
            PrintFormType.CustomTemplate when templateFields is { Count: > 0 } =>
                BuildTemplateEscPos(slip, widthMm, templateFields, profile),
            PrintFormType.TestPage => BuildTestPageEscPos(widthMm, profile),
            _ => BuildInvoiceEscPos(slip, widthMm, profile)
        };

        return new PrintFormResult
        {
            FormType = formType,
            EscPosBytes = bytes,
            CharsPerLine = chars,
            WidthMm = widthMm
        };
    }

    public int GetCharsPerLine(float widthMm) => widthMm <= 60 ? 32 : 48;

    public PrinterTextProfile ResolvePrinterProfile(string printerName)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        if (printerName.Contains("XP-N160II", StringComparison.OrdinalIgnoreCase))
            return new PrinterTextProfile(Encoding.GetEncoding(1251), 23, "Windows-1251 (Page 23)");

        if (printerName.Contains("XP-80T", StringComparison.OrdinalIgnoreCase))
            return new PrinterTextProfile(Encoding.GetEncoding(866), 17, "CP866 (Page 17)");

        return new PrinterTextProfile(Encoding.GetEncoding(866), null, "CP866");
    }
    
    private static void AddRow(List<byte> bytes, Encoding encoding, string left, string right, int charsPerLine)
    {
        if (left.Length + right.Length + 1 > charsPerLine)
        {
            bytes.AddRange(encoding.GetBytes(left + "\n"));
            bytes.AddRange(encoding.GetBytes(right + "\n"));
            return;
        }
        int spaces = charsPerLine - left.Length - right.Length;
        bytes.AddRange(encoding.GetBytes(left + new string(' ', Math.Max(0, spaces)) + right + "\n"));
    }

    #region Формирование накладной (Invoice)

    public byte[] BuildInvoiceEscPos(Slip slip, float widthMm, PrinterTextProfile profile)
    {
        var bytes = new List<byte>();
        int charsPerLine = GetCharsPerLine(widthMm);
        string starsLine = new string('*', charsPerLine) + "\n";
        string separatorLine = new string('-', charsPerLine) + "\n";
        var encoding = profile.Encoding;

        AddInitialization(bytes, profile);

        // Шапка с реквизитами (аналог col.Item().PaddingTop(10).AlignLeft().Text(new string('*', 32)))
        bytes.AddRange(EscAlignLeft);
        bytes.AddRange(encoding.GetBytes(starsLine));
        bytes.AddRange(encoding.GetBytes($"{slip.SellerTitle ?? string.Empty}\n"));
        if (!string.IsNullOrWhiteSpace(slip.SellerName))
            bytes.AddRange(encoding.GetBytes($"{slip.SellerName}\n"));
        if (!string.IsNullOrWhiteSpace(slip.SellerInn))
            bytes.AddRange(encoding.GetBytes($"ИНН {slip.SellerInn}\n"));
        bytes.AddRange(encoding.GetBytes(starsLine));
        bytes.AddRange(encoding.GetBytes("\n"));

        // Заголовок документа (НАКЛАДНАЯ / Заказ №)
        bytes.AddRange(EscAlignCenter);
        bytes.AddRange(GetFontSizeCommand(2)); // Увеличенный шрифт (примерно FontSize 10 в QuestPDF)
        bytes.AddRange(EscBoldOn);
        bytes.AddRange(encoding.GetBytes("НАКЛАДНАЯ / INVOICE\n"));
        bytes.AddRange(encoding.GetBytes($"Заказ № / Order # {slip.OrderId}\n\n"));
        bytes.AddRange(EscBoldOff);
        bytes.AddRange(GetFontSizeCommand(1));

        // Стороны и реквизиты
        bytes.AddRange(EscAlignLeft);
        bytes.AddRange(encoding.GetBytes($"Грузоотправитель:\n{slip.SellerTitle}\nИНН {slip.SellerInn}\n\n"));

        var recipientName = !string.IsNullOrWhiteSpace(slip.CustomerName) ? slip.CustomerName : "Покупатель";
        bytes.AddRange(encoding.GetBytes($"Грузополучатель:\n{recipientName}\n"));
        if (!string.IsNullOrWhiteSpace(slip.CustomerPhone))
            bytes.AddRange(encoding.GetBytes($"{slip.CustomerPhone}\n"));

        bytes.AddRange(encoding.GetBytes($"\nМенеджер: {slip.Manager}\n"));

        var dateStr = (slip.OrderDate ?? DateTime.Now).ToString("HH:mm dd.MM.yyyy");

        AddRow(bytes, encoding, "Заказ принят:", dateStr, charsPerLine);
        AddRow(bytes, encoding, "Накладная оформлена:", dateStr, charsPerLine);

        if (!string.IsNullOrWhiteSpace(slip.DeliveryTime))
        {
            AddRow(bytes, encoding, "Время доставки:", slip.DeliveryTime, charsPerLine);
        }

        var deliveredText = !string.IsNullOrWhiteSpace(slip.DeliveredAt) ? slip.DeliveredAt : "----------";
        AddRow(bytes, encoding, "Заказ доставлен:", deliveredText, charsPerLine);

        bytes.AddRange(encoding.GetBytes($"\nКоличество человек: {slip.PersonsCount}\n"));
        bytes.AddRange(encoding.GetBytes(separatorLine));

        // Таблица позиций
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

                var qtyStr = $"{item.Quantity ?? 1} шт x";
                var priceStr = $"{item.Price?.ToString("F2") ?? "0.00"}";
                var totalStr = $"{item.TotalPrice?.ToString("F2") ?? "0.00"}";

                string detailLine = charsPerLine == 48
                    ? string.Format("  {0,-15} {1,8} {2,8} {3,10}", "", qtyStr, priceStr, totalStr)
                    : string.Format("  {0,-10} {1,5} {2,6} {3,7}", "", qtyStr, priceStr, totalStr);

                bytes.AddRange(encoding.GetBytes($"{detailLine}\n"));
            }
            bytes.AddRange(encoding.GetBytes(separatorLine));
        }

        // Итоги и оплата
        var totalAmountStr = slip.TotalAmount?.ToString("F2") ?? "0.00";
        AddRow(bytes, encoding, "Итого к оплате:", totalAmountStr, charsPerLine);
        bytes.AddRange(encoding.GetBytes(separatorLine));

        var payMethod = !string.IsNullOrWhiteSpace(slip.PaymentMethod) ? slip.PaymentMethod : "Наличные";
        AddRow(bytes, encoding, payMethod, totalAmountStr, charsPerLine);
        bytes.AddRange(encoding.GetBytes("\n"));

        bytes.AddRange(encoding.GetBytes($"{slip.SignReleased ?? string.Empty}\n\n"));
        bytes.AddRange(encoding.GetBytes($"{slip.SignReceived ?? string.Empty}\n\n"));

        if (!string.IsNullOrWhiteSpace(slip.CustomerNote))
        {
            bytes.AddRange(encoding.GetBytes($"Комментарий к заказу:\n{slip.CustomerNote}\n"));
        }
        else
        {
            bytes.AddRange(encoding.GetBytes("Комментарий к заказу:\n"));
        }

        bytes.AddRange(encoding.GetBytes($"Способ оплаты: {payMethod}\n"));
        bytes.AddRange(encoding.GetBytes($"Количество персон: {slip.PersonsCount}\n"));
        bytes.AddRange(encoding.GetBytes($"Канал продаж: {slip.SalesChannel}\n"));
        bytes.AddRange(encoding.GetBytes($"Номер заказа FN: {slip.OrderId}\n"));
        
        bytes.AddRange(encoding.GetBytes(separatorLine));
        bytes.AddRange(encoding.GetBytes(separatorLine));
        bytes.AddRange(encoding.GetBytes(separatorLine));
        bytes.AddRange(encoding.GetBytes(separatorLine));

        // Доставка и подвал
        bytes.AddRange(EscAlignLeft);
        bytes.AddRange(EscBoldOn);
        bytes.AddRange(GetFontSizeCommand(2));
        bytes.AddRange(encoding.GetBytes($"{slip.DeliveryType ?? string.Empty}\n"));
        bytes.AddRange(EscBoldOff);
        bytes.AddRange(GetFontSizeCommand(1));

        bytes.AddRange(EscAlignCenter);
        bytes.AddRange(encoding.GetBytes($"\n{slip.CurrencyNote ?? string.Empty}\n"));
        bytes.AddRange(encoding.GetBytes($"{slip.Footer ?? string.Empty}\n\n\n"));
        bytes.AddRange(EscCutPaper);

        return [.. bytes];
    }

    #endregion

    #region Формирование кухонного чека (Kitchen Slip)

    public byte[] BuildKitchenSlipEscPos(Slip slip, float widthMm, PrinterTextProfile profile)
    {
        var bytes = new List<byte>();
        var charsPerLine = GetCharsPerLine(widthMm);
        var encoding = profile.Encoding;
        var orderTime = slip.OrderDate?.ToString("HH:mm") ?? DateTime.Now.ToString("HH:mm");
        // Крупное время сверху — «доставить к», как на образцах iiko; иначе время оформления.
        var headlineTime = ExtractClock(slip.DeliveryTime) ?? orderTime;

        AddInitialization(bytes, profile);

        AddSeparator(bytes, encoding, charsPerLine);

        bytes.AddRange(EscAlignCenter);
        bytes.AddRange(EscBoldOn);
        bytes.AddRange(GetFontSizeCommand(3));
        AddWrappedLine(bytes, encoding, headlineTime, charsPerLine);
        bytes.AddRange(GetFontSizeCommand(1));
        bytes.AddRange(EscBoldOff);

        AddSeparator(bytes, encoding, charsPerLine);

        bytes.AddRange(EscAlignCenter);
        bytes.AddRange(EscBoldOn);
        bytes.AddRange(GetFontSizeCommand(2));
        AddWrappedLine(bytes, encoding, slip.DeliveryType ?? "Доставка / Delivery", charsPerLine);
        AddWrappedLine(bytes, encoding, $"ЗАКАЗ № / ORDER # {slip.OrderId}", charsPerLine);
        bytes.AddRange(EscBoldOff);
        bytes.AddRange(GetFontSizeCommand(1));

        bytes.AddRange(EscAlignLeft);
        AddWrappedLine(bytes, encoding, $"Оформлен / Placed: {orderTime}", charsPerLine);
        if (!string.IsNullOrWhiteSpace(slip.DeliveryTime))
            AddWrappedLine(bytes, encoding, $"Доставить к / Deliver by: {slip.DeliveryTime}", charsPerLine);

        AddSeparator(bytes, encoding, charsPerLine);

        string title = "Название / Name";
        string qtyTitle = "Кол-во";
        string headerLine = title.Length + qtyTitle.Length < charsPerLine
            ? title + new string(' ', charsPerLine - title.Length - qtyTitle.Length) + qtyTitle
            : title + " " + qtyTitle;
        AddWrappedLine(bytes, encoding, headerLine, charsPerLine);

        foreach (var item in slip.Items)
        {
            AddSeparator(bytes, encoding, charsPerLine);

            var itemName = item.Name ?? "Товар / Item";
            var itemQty = $"{item.Quantity ?? 1}";
            string itemLine = itemName.Length + itemQty.Length < charsPerLine
                ? itemName + new string(' ', charsPerLine - itemName.Length - itemQty.Length) + itemQty
                : itemName + " " + itemQty;

            AddWrappedLine(bytes, encoding, itemLine, charsPerLine);
        }

        AddSeparator(bytes, encoding, charsPerLine);

        // Самовывоз: адрес можно скрыть; для доставки — как на образцах.
        var address = slip.ShippingAddress ?? slip.BillingAddress1;
        if (!string.IsNullOrWhiteSpace(address))
            AddWrappedLine(bytes, encoding, $"Адрес / Address: {address}", charsPerLine);

        if (!string.IsNullOrWhiteSpace(slip.CustomerNote))
            AddWrappedLine(bytes, encoding, $"Ком. к заказу / Note: {slip.CustomerNote}", charsPerLine);

        AddWrappedLine(bytes, encoding, $"Оплата / Payment: {slip.PaymentMethod ?? "—"}", charsPerLine);
        AddWrappedLine(bytes, encoding, $"Персон / Persons: {slip.PersonsCount ?? "1"}", charsPerLine);
        AddWrappedLine(bytes, encoding, $"Канал / Channel: {slip.SalesChannel}", charsPerLine);
        AddWrappedLine(bytes, encoding, $"FN: {slip.OrderId}", charsPerLine);

        bytes.AddRange(encoding.GetBytes("\n\n\n"));
        bytes.AddRange(EscCutPaper);

        return [.. bytes];
    }

    private static string? ExtractClock(string? deliveryTime)
    {
        if (string.IsNullOrWhiteSpace(deliveryTime))
            return null;

        // Ищем HH:mm в строке вида "10.09 13:42" или "13:42".
        for (var i = 0; i + 4 < deliveryTime.Length; i++)
        {
            if (char.IsDigit(deliveryTime[i]) &&
                char.IsDigit(deliveryTime[i + 1]) &&
                deliveryTime[i + 2] == ':' &&
                char.IsDigit(deliveryTime[i + 3]) &&
                char.IsDigit(deliveryTime[i + 4]))
            {
                return deliveryTime.Substring(i, 5);
            }
        }

        return null;
    }

    #endregion

    #region Формирование по пользовательскому шаблону (ReceiptField)

    public byte[] BuildTemplateEscPos(Slip slip, float widthMm, IReadOnlyList<ReceiptField> fields, PrinterTextProfile profile)
    {
        var bytes = new List<byte>();
        var charsPerLine = GetCharsPerLine(widthMm);
        var encoding = profile.Encoding;

        AddInitialization(bytes, profile);
        foreach (var line in BuildTemplateLines(slip, fields))
        {
            bytes.AddRange(EscAlignLeft);
            bytes.AddRange(GetFontSizeCommand(line.Field.FontSize));
            bytes.AddRange(line.Field.IsBold ? EscBoldOn : EscBoldOff);

            var text = string.IsNullOrWhiteSpace(line.Label) ? line.Value : $"{line.Label}: {line.Value}";
            AddWrappedLine(bytes, encoding, text, charsPerLine);
        }

        bytes.AddRange(EscAlignLeft);
        bytes.AddRange(encoding.GetBytes("\n\n\n"));
        bytes.AddRange(EscCutPaper);
        return [.. bytes];
    }

    public static IEnumerable<(string Label, string Value, ReceiptField Field)> BuildTemplateLines(
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

    #endregion

    #region Формирование тестовой страницы (Test Page)

    public byte[] BuildTestPageEscPos(float widthMm, PrinterTextProfile profile)
    {
        var charsPerLine = GetCharsPerLine(widthMm);
        var encoding = profile.Encoding;
        var bytes = new List<byte>();

        AddInitialization(bytes, profile);
        bytes.AddRange(EscAlignCenter);
        bytes.AddRange(EscBoldOn);
        bytes.AddRange(GetFontSizeCommand(2));
        AddWrappedLine(bytes, encoding, "TEST / ТЕСТ", charsPerLine);
        bytes.AddRange(EscBoldOff);
        bytes.AddRange(GetFontSizeCommand(1));
        AddWrappedLine(bytes, encoding, "XP-58C / XP-80C", charsPerLine);
        AddWrappedLine(bytes, encoding, $"Width {widthMm}mm / {charsPerLine} chars", charsPerLine);
        AddSeparator(bytes, encoding, charsPerLine);

        bytes.AddRange(EscAlignLeft);
        AddWrappedLine(bytes, encoding, $"Encoding: {profile.Description}", charsPerLine);
        AddWrappedLine(bytes, encoding, "RU: Привет, заказ №123", charsPerLine);
        AddWrappedLine(bytes, encoding, "EN: Hello, order #123", charsPerLine);
        AddWrappedLine(bytes, encoding, new string('0', charsPerLine), charsPerLine);

        bytes.AddRange(GetFontSizeCommand(1));
        AddWrappedLine(bytes, encoding, "Font size 1 / размер 1", charsPerLine);
        bytes.AddRange(GetFontSizeCommand(2));
        AddWrappedLine(bytes, encoding, "Font size 2 / размер 2", charsPerLine);
        bytes.AddRange(GetFontSizeCommand(3));
        AddWrappedLine(bytes, encoding, "Font size 3 / размер 3", charsPerLine);
        bytes.AddRange(GetFontSizeCommand(1));
        bytes.AddRange(EscBoldOn);
        AddWrappedLine(bytes, encoding, "Bold / Жирный", charsPerLine);
        bytes.AddRange(EscBoldOff);
        AddWrappedLine(bytes, encoding, "Cutter test / Отрезчик", charsPerLine);
        bytes.AddRange(encoding.GetBytes("\n\n\n"));
        bytes.AddRange(EscCutPaper);

        return [.. bytes];
    }

    #endregion

    #region Вспомогательные методы подстановки и форматирования

    public static string ResolveOrderValue(string key, Slip slip) => key switch
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

    public static string ResolveItemValue(string key, SlipItem item) => key switch
    {
        "item.name" => item.Name ?? string.Empty,
        "item.quantity" => (item.Quantity ?? 1).ToString(),
        "item.price" => item.Price?.ToString("F2") ?? "0.00",
        "item.subtotal" => item.TotalPrice?.ToString("F2") ?? "0.00",
        "item.total" => item.TotalPrice?.ToString("F2") ?? "0.00",
        "item.sku" => item.Sku ?? string.Empty,
        _ => string.Empty
    };

    public static IEnumerable<string> WrapText(string text, int width)
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

    private static void AddWrappedLine(List<byte> bytes, Encoding encoding, string text, int charsPerLine)
    {
        foreach (var line in WrapText(text ?? string.Empty, charsPerLine))
        {
            bytes.AddRange(encoding.GetBytes(line));
            bytes.Add((byte)'\n');
        }
    }

    private static void AddSeparator(List<byte> bytes, Encoding encoding, int charsPerLine)
    {
        bytes.AddRange(encoding.GetBytes(new string('-', charsPerLine)));
        bytes.Add((byte)'\n');
    }

    private static void AddInitialization(List<byte> bytes, PrinterTextProfile textProfile)
    {
        bytes.AddRange(EscInitialize);
        if (textProfile.Page is byte page)
            bytes.AddRange([0x1B, 0x74, page]);
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

    #endregion
}