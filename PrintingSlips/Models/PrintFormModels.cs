namespace PrintingSlips.Models;

/// <summary>
/// Тип формируемой печатной формы
/// </summary>
public enum PrintFormType
{
    /// <summary> Полноформатная накладная с реквизитами, товарами и подписями </summary>
    Invoice = 0,

    /// <summary> Сервисный кухонный чек для поваров/сборщиков </summary>
    KitchenSlip = 1,

    /// <summary> Пользовательский шаблон по полям ReceiptField </summary>
    CustomTemplate = 2,

    /// <summary> Тестовая страница проверки шрифтов, кодировки и отрезчика </summary>
    TestPage = 3
}

/// <summary>
/// Профиль кодировки и кодовой страницы термопринтера
/// </summary>
public sealed record PrinterTextProfile(System.Text.Encoding Encoding, byte? Page, string Description);

/// <summary>
/// Результат формирования печатной формы
/// </summary>
public sealed class PrintFormResult
{
    public required PrintFormType FormType { get; init; }
    public required byte[] EscPosBytes { get; init; }
    public string? TextPreview { get; init; }
    public int CharsPerLine { get; init; }
    public float WidthMm { get; init; }
}