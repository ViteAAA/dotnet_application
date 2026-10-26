namespace PrintingSlips.Models;

public class PrinterDestination
{
    public string Name { get; set; } = string.Empty;
    public float WidthMm { get; set; } = 80;
    public string ConnectionType { get; set; } = "Windows";
    public string? Host { get; set; }
    public int Port { get; set; } = 9100;
    /// <summary>FontFamily всего чека (PDF). ESC/POS размер — только FontSize полей шаблона.</summary>
    public string FontFamily { get; set; } = "Courier New";
}
