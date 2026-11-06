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
        Slip? slip,
        float widthMm,
        IReadOnlyList<ReceiptField>? templateFields = null)
    {
        return formType switch
        {
            PrintFormType.KitchenSlip => slip != null ? BuildKitchenLines(slip) : BuildTestPageLines(widthMm),
            PrintFormType.CustomTemplate when templateFields is { Count: > 0 } => slip != null ? BuildTemplateTextLines(slip, templateFields, widthMm) : BuildTestPageLines(widthMm),
            PrintFormType.TestPage => BuildTestPageLines(widthMm),
            _ => slip != null ? BuildInvoiceLines(slip) : BuildTestPageLines(widthMm)
        };
    }

    private static void AddLine(List<PrintTextLine> lines, string? text, int size = 1, bool bold = false,
        PrintTextAlign align = PrintTextAlign.Left, int spacingAfter = 0)
    {
        // Многострочный текст разбиваем, перенос по ширине делает GDI.
        var parts = (text ?? string.Empty).Replace("\r", string.Empty).Split('\n');
        for (int i = 0; i < parts.Length; i++)
        {
            // Отступ снизу применяется к последней строке разбитого абзаца
            var spacing = (i == parts.Length - 1) ? spacingAfter : 0;
            lines.Add(new PrintTextLine { Text = parts[i], FontSize = size, Bold = bold, Align = align, SpacingAfter = spacing });
        }
    }

    private static void AddRowLine(List<PrintTextLine> lines, string left, string? right, int size = 1, bool bold = false,
        int spacingAfter = 0, PrintTextAlign align = PrintTextAlign.Left) =>
        lines.Add(new PrintTextLine { Text = left, RightText = right ?? string.Empty, FontSize = size, Bold = bold, Align = align, SpacingAfter = spacingAfter });

    private static void AddSeparatorLine(List<PrintTextLine> lines, string style = "Dashed", int spacingAfter = 0) =>
        lines.Add(new PrintTextLine { IsSeparator = true, SeparatorStyle = style, SpacingAfter = spacingAfter, Bold = false});

    private static List<PrintTextLine> BuildInvoiceLines(Slip slip)
    {
        var l = new List<PrintTextLine>();
        AddSeparatorLine(l);
        AddLine(l, slip.SellerTitle);
        if (!string.IsNullOrWhiteSpace(slip.SellerName)) AddLine(l, slip.SellerName);
        if (!string.IsNullOrWhiteSpace(slip.SellerInn)) AddLine(l, $"ИНН {slip.SellerInn}");
        AddSeparatorLine(l);
        AddLine(l, "");

        AddLine(l, "НАКЛАДНАЯ", 2, false, PrintTextAlign.Center);
        AddLine(l, $"Заказ № {slip.OrderId}", 2, false, PrintTextAlign.Center);
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

        AddRowLine(l, "Наименование", "К-во  Цена  Сумма");
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
        AddRowLine(l, "Итого к оплате:", total);
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

    private static List<PrintTextLine> BuildTemplateTextLines(Slip slip, IReadOnlyList<ReceiptField> fields, float widthMm = 80)
    {
        var l = new List<PrintTextLine>();
        foreach (var field in fields)
        {
            if (field.CheckIsSeparator())
            {
                AddSeparatorLine(l, field.GetEffectiveSeparatorStyle(), field.LineSpacing);
                continue;
            }

            if (field.CheckIsSpacer())
            {
                AddLine(l, "", 1, false, PrintTextAlign.Left, Math.Max(4, field.LineSpacing));
                continue;
            }

            if (field.Key == "block.items")
            {
                // Вывод блока заказов в точности как в default шаблоне (накладная или кухня)
                var mode = field.ItemDisplayMode?.Trim().ToLowerInvariant() ?? "table";
                bool isKitchen = mode == "kitchen" || 
                    (field.ChildFields.Count == 2 && field.ChildFields.Any(c => c.Key == "item.quantity") && !field.ChildFields.Any(c => c.Key == "item.price"));

                if (isKitchen)
                {
                    AddRowLine(l, "Название / Name", "Кол-во", 1, true, field.LineSpacing);
                    foreach (var item in slip.Items)
                    {
                        AddSeparatorLine(l, "Dashed");
                        AddRowLine(l, item.Name ?? "Товар / Item", $"{item.Quantity ?? 1}", Math.Clamp(field.FontSize, 1, 3));
                    }
                    AddSeparatorLine(l);
                }
                else if (mode == "list")
                {
                    // Пользовательский список свойств
                    foreach (var item in slip.Items)
                    {
                        foreach (var child in field.ChildFields)
                        {
                            var childVal = ResolveItemValue(child.Key, item);
                            if (child.IsRow)
                            {
                                AddRowLine(l, child.Name, childVal, Math.Clamp(child.FontSize, 1, 3), child.IsBold,
                                    child.LineSpacing, child.GetPrintAlign());
                            }
                            else
                            {
                                var text = string.IsNullOrWhiteSpace(child.Name) ? childVal : $"{child.Name}: {childVal}";
                                AddLine(l, text, Math.Clamp(child.FontSize, 1, 3), child.IsBold,
                                    child.GetPrintAlign(), child.LineSpacing);
                            }
                        }
                        AddSeparatorLine(l, "Dashed");
                    }
                }
                else
                {
                    // Режим "Table" (по умолчанию, как в default invoice):
                    // Заголовок таблицы с разделителем
                    AddRowLine(l, "Наименование", "К-во  Цена  Сумма", 1, true);
                    AddSeparatorLine(l, "Dashed");
                    if (slip.Items.Count > 0)
                    {
                        foreach (var item in slip.Items)
                        {
                            AddLine(l, item.Name ?? "Товар", Math.Clamp(field.FontSize, 1, 3));
                            var qty = item.Quantity ?? 1;
                            var price = item.Price?.ToString("F2") ?? "0.00";
                            var total = item.TotalPrice?.ToString("F2") ?? (item.Subtotal?.ToString("F2") ?? "0.00");
                            AddRowLine(l, "", $"{qty} шт x {price}  {total}", 1, false);
                        }
                        AddSeparatorLine(l, "Dashed", field.LineSpacing);
                    }
                }
                continue;
            }

            if (field.Key == "static.text")
            {
                var text = string.IsNullOrWhiteSpace(field.CustomText) ? field.Name : field.CustomText;
                AddLine(l, text, Math.Clamp(field.FontSize, 1, 3), field.IsBold, field.GetPrintAlign(), field.LineSpacing);
                continue;
            }

            var val = ResolveOrderValue(field.Key, slip);
            if (field.IsRow)
            {
                AddRowLine(l, field.Name, val, Math.Clamp(field.FontSize, 1, 3), field.IsBold,
                    field.LineSpacing, field.GetPrintAlign());
            }
            else
            {
                var text = string.IsNullOrWhiteSpace(field.Name) ? val : $"{field.Name}: {val}";
                AddLine(l, text, Math.Clamp(field.FontSize, 1, 3), field.IsBold,
                    field.GetPrintAlign(), field.LineSpacing);
            }
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
