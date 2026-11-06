using System.Collections.ObjectModel;

namespace PrintingSlips.Models;

public class ReceiptField
{
    /// <summary>
    /// Отображаемый заголовок поля на чеке (например, "Покупатель", "Телефон", "Сумма")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Системный путь/ключ из JSON WooCommerce (например, "billing.first_name", "order.number", "order.total")
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Произвольный статический текст (используется, если Key равен "static.text")
    /// </summary>
    public string CustomText { get; set; } = string.Empty;

    public int FontSize { get; set; } = 1;

    public bool IsBold { get; set; }

    /// <summary>
    /// Горизонтальное выравнивание поля: "Left", "Center", "Right". По умолчанию "Left"
    /// (старые шаблоны без этого свойства загружаются с выравниванием слева).
    /// </summary>
    public string Align { get; set; } = "Left";

    public PrintTextAlign GetPrintAlign() => (Align ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "center" => PrintTextAlign.Center,
        "right" => PrintTextAlign.Right,
        _ => PrintTextAlign.Left
    };

    /// <summary>
    /// Флаг, указывающий, что данное поле является контейнером/блоком для списка товаров
    /// </summary>
    public bool IsItemContainer { get; set; } = false;

    /// <summary>
    /// Флаг, указывающий, что поле представляет собой горизонтальный разделитель (пунктир, линия, звездочки)
    /// </summary>
    public bool IsSeparator { get; set; }

    /// <summary>
    /// Стиль линии разделителя: "Dashed" (пунктир), "Solid" (сплошная), "Dotted" (точки), "Stars" (звездочки), "Double" (двойная)
    /// </summary>
    public string SeparatorStyle { get; set; } = "Dashed";

    /// <summary>
    /// Дополнительный отступ снизу после данной строки (межстрочный интервал в пунктах: 0, 2, 4, 6, 8, 10...)
    /// </summary>
    public int LineSpacing { get; set; } = 0;

    /// <summary>
    /// Выводить строку в формате 2 колонок (метка слева, значение прижато вправо), как в строках 'Заказ принят: 12:00' или 'Итого: 50.00'
    /// </summary>
    public bool IsRow { get; set; } = false;

    /// <summary>
    /// Режим форматирования списка позиций (для block.items):
    /// "Table" (как в накладной: таблица Наименование / К-во x Цена, Сумма),
    /// "Kitchen" (как на кухне: Название / Кол-во),
    /// "List" (отдельные строки по списку ChildFields)
    /// </summary>
    public string ItemDisplayMode { get; set; } = "Table";

    /// <summary>
    /// Вложенные поля позиции товара (используются, если IsItemContainer == true или Key == "block.items")
    /// </summary>
    public ObservableCollection<ReceiptField> ChildFields { get; set; } = [];

    public bool CheckIsSeparator() =>
        IsSeparator ||
        Key.StartsWith("separator.", StringComparison.OrdinalIgnoreCase);

    public string GetEffectiveSeparatorStyle()
    {
        if (Key.Equals("separator.dashed", StringComparison.OrdinalIgnoreCase)) return "Dashed";
        if (Key.Equals("separator.solid", StringComparison.OrdinalIgnoreCase)) return "Solid";
        if (Key.Equals("separator.dotted", StringComparison.OrdinalIgnoreCase)) return "Dotted";
        if (Key.Equals("separator.stars", StringComparison.OrdinalIgnoreCase)) return "Stars";
        if (Key.Equals("separator.double", StringComparison.OrdinalIgnoreCase)) return "Double";
        return string.IsNullOrWhiteSpace(SeparatorStyle) ? "Dashed" : SeparatorStyle;
    }

    public bool CheckIsSpacer() =>
        Key.Equals("space.empty", StringComparison.OrdinalIgnoreCase);

    // Беспараметрический конструктор обязателен для корректной JSON-десериализации
    public ReceiptField() { }

    /// <summary>
    /// Основной конструктор для создания стандартных динамических или статических полей
    /// </summary>
    public ReceiptField(string name, string key, string customText = "", bool isItemContainer = false)
    {
        Name = name;
        Key = key;
        CustomText = customText;
        IsItemContainer = isItemContainer || key == "block.items";
        if (key.StartsWith("separator.", StringComparison.OrdinalIgnoreCase))
        {
            IsSeparator = true;
            SeparatorStyle = GetEffectiveSeparatorStyle();
        }
    }
}