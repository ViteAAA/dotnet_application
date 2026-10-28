using PrintingSlips.Models;

namespace PrintingSlips.Services;

/// <summary>
/// Текстовая (GDI) разметка форм для печати через очередь Windows.
/// Содержимое повторяет ESC/POS-формы, но шрифт и настройки задаёт драйвер/PrinterDestination.
/// </summary>
public partial class PrintDocumentBuilder
{
    public IReadOnlyList<PrintTextLine> BuildFormLines(
        PrintFormType formType,
        Slip slip,
        float widthMm,
        IReadOnlyList<ReceiptField>? templateFields = null)
    {
        return formType switch
        {
            PrintFormType.KitchenSlip => BuildKitchenLines(slip),
            PrintFormType.CustomTemplate when templateFields is { Count: > 0 } => BuildTemplateTextLines(slip, templateFields),
            PrintFormType.TestPage => BuildTestPageLines(widthMm),
            _ => BuildInvoiceLines(slip)
        };
    }

    private static void AddLine(List<PrintTextLine> lines, string? text, int size = 1, bool bold = false,
        PrintTextAlign align = PrintTextAlign.Left)
    {
        // Многострочный текст разбиваем, перенос по ширине делает GDI.
        foreach (var part in (text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
            lines.Add(new PrintTextLine { Text = part, FontSize = size, Bold = bold, Align = align });
    }

    private static void AddRowLine(List<PrintTextLine> lines, string left, string? right, int size = 1, bool bold = false) =>
        lines.Add(new PrintTextLine { Text = left, RightText = right ?? string.Empty, FontSize = size, Bold = bold });

    private static void AddSeparatorLine(List<PrintTextLine> lines) =>
        lines.Add(new PrintTextLine { IsSeparator = true });

    private static List<PrintTextLine> BuildInvoiceLines(Slip slip)
    {
        var l = new List<PrintTextLine>();
        AddSeparatorLine(l);
        AddLine(l, slip.SellerTitle);
        if (!string.IsNullOrWhiteSpace(slip.SellerName)) AddLine(l, slip.SellerName);
        if (!string.IsNullOrWhiteSpace(slip.SellerInn)) AddLine(l, $"ИНН {slip.SellerInn}");
        AddSeparatorLine(l);
        AddLine(l, "");

        AddLine(l, "НАКЛАДНАЯ / INVOICE", 2, true, PrintTextAlign.Center);
        AddLine(l, $"Заказ № / Order # {slip.OrderId}", 2, true, PrintTextAlign.Center);
        AddLine(l, "");

        AddLine(l, "Грузоотправитель:");
        AddLine(l, slip.SellerTitle);
        AddLine(l, $"ИНН {slip.SellerInn}");
        AddLine(l, "");
        AddLine(l, "Грузополучатель:");
        AddLine(l, !string.IsNullOrWhiteSpace(slip.CustomerName) ? slip.CustomerName : "Покупатель");
        if (!string.IsNullOrWhiteSpace(slip.CustomerPhone)) AddLine(l, slip.CustomerPhone);
        AddLine(l, "");
        AddLine(l, $"Менеджер: {slip.Manager}");

        var dateStr = (slip.OrderDate ?? DateTime.Now).ToString("HH:mm dd.MM.yyyy");
        AddRowLine(l, "Заказ принят:", dateStr);
        AddRowLine(l, "Накладная оформлена:", dateStr);
        if (!string.IsNullOrWhiteSpace(slip.DeliveryTime)) AddRowLine(l, "Время доставки:", slip.DeliveryTime);
        AddRowLine(l, "Заказ доставлен:", !string.IsNullOrWhiteSpace(slip.DeliveredAt) ? slip.DeliveredAt : "----------");
        AddLine(l, "");
        AddLine(l, $"Количество человек: {slip.PersonsCount}");
        AddSeparatorLine(l);

        AddRowLine(l, "Наименование", "К-во  Цена  Сумма", 1, true);
        AddSeparatorLine(l);
        if (slip.Items.Count > 0)
        {
            foreach (var item in slip.Items)
            {
                AddLine(l, item.Name ?? "Товар");
                AddRowLine(l, "",
                    $"{item.Quantity ?? 1} шт x {item.Price?.ToString("F2") ?? "0.00"}  {item.TotalPrice?.ToString("F2") ?? "0.00"}");
            }
            AddSeparatorLine(l);
        }

        var total = slip.TotalAmount?.ToString("F2") ?? "0.00";
        AddRowLine(l, "Итого к оплате:", total, 1, true);
        AddSeparatorLine(l);
        var payMethod = !string.IsNullOrWhiteSpace(slip.PaymentMethod) ? slip.PaymentMethod : "Наличные";
        AddRowLine(l, payMethod, total);
        AddLine(l, "");
        AddLine(l, slip.SignReleased);
        AddLine(l, "");
        AddLine(l, slip.SignReceived);
        AddLine(l, "");
        AddLine(l, "Комментарий к заказу:");
        if (!string.IsNullOrWhiteSpace(slip.CustomerNote)) AddLine(l, slip.CustomerNote);
        AddLine(l, $"Способ оплаты: {payMethod}");
        AddLine(l, $"Количество персон: {slip.PersonsCount}");
        AddLine(l, $"Канал продаж: {slip.SalesChannel}");
        AddLine(l, $"Номер заказа FN: {slip.OrderId}");
        AddSeparatorLine(l);

        AddLine(l, slip.DeliveryType, 2, true);
        AddLine(l, "");
        AddLine(l, slip.CurrencyNote, 1, false, PrintTextAlign.Center);
        AddLine(l, slip.Footer, 1, false, PrintTextAlign.Center);
        return l;
    }

    private static List<PrintTextLine> BuildKitchenLines(Slip slip)
    {
        var l = new List<PrintTextLine>();
        var orderTime = slip.OrderDate?.ToString("HH:mm") ?? DateTime.Now.ToString("HH:mm");
        var headlineTime = ExtractClock(slip.DeliveryTime) ?? orderTime;

        AddSeparatorLine(l);
        AddLine(l, headlineTime, 3, true, PrintTextAlign.Center);
        AddSeparatorLine(l);
        AddLine(l, slip.DeliveryType ?? "Доставка / Delivery", 2, true, PrintTextAlign.Center);
        AddLine(l, $"ЗАКАЗ № / ORDER # {slip.OrderId}", 2, true, PrintTextAlign.Center);
        AddLine(l, $"Оформлен / Placed: {orderTime}");
        if (!string.IsNullOrWhiteSpace(slip.DeliveryTime))
            AddLine(l, $"Доставить к / Deliver by: {slip.DeliveryTime}");
        AddSeparatorLine(l);
        AddRowLine(l, "Название / Name", "Кол-во", 1, true);
        foreach (var item in slip.Items)
        {
            AddSeparatorLine(l);
            AddRowLine(l, item.Name ?? "Товар / Item", $"{item.Quantity ?? 1}");
        }
        AddSeparatorLine(l);

        var address = slip.ShippingAddress ?? slip.BillingAddress1;
        if (!string.IsNullOrWhiteSpace(address)) AddLine(l, $"Адрес / Address: {address}");
        if (!string.IsNullOrWhiteSpace(slip.CustomerNote)) AddLine(l, $"Ком. к заказу / Note: {slip.CustomerNote}");
        AddLine(l, $"Оплата / Payment: {slip.PaymentMethod ?? "—"}");
        AddLine(l, $"Персон / Persons: {slip.PersonsCount ?? "1"}");
        AddLine(l, $"Канал / Channel: {slip.SalesChannel}");
        AddLine(l, $"FN: {slip.OrderId}");
        return l;
    }

    private static List<PrintTextLine> BuildTemplateTextLines(Slip slip, IReadOnlyList<ReceiptField> fields)
    {
        var l = new List<PrintTextLine>();
        foreach (var line in BuildTemplateLines(slip, fields))
        {
            var text = string.IsNullOrWhiteSpace(line.Label) ? line.Value : $"{line.Label}: {line.Value}";
            AddLine(l, text, Math.Clamp(line.Field.FontSize, 1, 3), line.Field.IsBold, line.Field.GetPrintAlign());
        }
        return l;
    }

    private static List<PrintTextLine> BuildTestPageLines(float widthMm)
    {
        var l = new List<PrintTextLine>();
        AddLine(l, "TEST / ТЕСТ", 2, true, PrintTextAlign.Center);
        AddLine(l, "XP-58C / XP-80C", 1, false, PrintTextAlign.Center);
        AddLine(l, $"Width {widthMm}mm (Windows queue / GDI)", 1, false, PrintTextAlign.Center);
        AddSeparatorLine(l);
        AddLine(l, "RU: Привет, заказ №123");
        AddLine(l, "EN: Hello, order #123");
        AddLine(l, "Font size 1 / размер 1", 1);
        AddLine(l, "Font size 2 / размер 2", 2);
        AddLine(l, "Font size 3 / размер 3", 3);
        AddLine(l, "Bold / Жирный", 1, true);
        AddRowLine(l, "Left / Слева", "Right / Справа");
        AddLine(l, "Cutter test / Отрезчик");
        return l;
    }
}
