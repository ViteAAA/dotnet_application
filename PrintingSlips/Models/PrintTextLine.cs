namespace PrintingSlips.Models;

/// <summary>
/// Строка печатной формы для печати через очередь Windows (GDI).
/// </summary>
public sealed class PrintTextLine
{
    public string Text { get; init; } = string.Empty;

    /// <summary>Необязательный текст, прижатый к правому краю (строки «метка ... значение»).</summary>
    public string? RightText { get; init; }

    public int FontSize { get; init; } = 1; // 1..3 relative
    public bool Bold { get; init; }
    public PrintTextAlign Align { get; init; } = PrintTextAlign.Left;

    /// <summary>Горизонтальный разделитель вместо текста.</summary>
    public bool IsSeparator { get; init; }

    /// <summary>Стиль разделителя: "Dashed", "Solid", "Dotted", "Stars", "Double".</summary>
    public string SeparatorStyle { get; init; } = "Solid";

    /// <summary>Дополнительный вертикальный отступ после строки (межстрочный интервал в pt).</summary>
    public int SpacingAfter { get; init; } = 0;
}

public enum PrintTextAlign { Left, Center, Right }
