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
    /// Вложенные поля позиции товара (используются, если IsItemContainer == true)
    /// </summary>
    public ObservableCollection<ReceiptField> ChildFields { get; set; } = [];

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
        IsItemContainer = isItemContainer;
    }
}