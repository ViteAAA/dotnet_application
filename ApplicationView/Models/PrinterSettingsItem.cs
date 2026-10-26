using CommunityToolkit.Mvvm.ComponentModel;

namespace ApplicationView.Models;

public partial class PrinterSettingsItem : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private int paperWidthMm = 80;
    /// <summary>Базовый размер ESC/POS шрифта для всего чека на этом принтере (1–3), если нет построчного CustomTemplate.</summary>
    [ObservableProperty] private int defaultFontSize = 1;
    [ObservableProperty] private string connectionType = "Windows";
    [ObservableProperty] private string host = string.Empty;
    [ObservableProperty] private int port = 9100;

    public string[] ConnectionTypes { get; } = ["Windows", "Usb", "Network"];

    public string ConnectionLabel => ConnectionType switch
    {
        "Network" => string.IsNullOrWhiteSpace(Host) ? "NET" : $"NET {Host}:{Port}",
        "Usb" => "USB",
        _ => "WIN"
    };

    partial void OnConnectionTypeChanged(string value) => OnPropertyChanged(nameof(ConnectionLabel));
    partial void OnHostChanged(string value) => OnPropertyChanged(nameof(ConnectionLabel));
    partial void OnPortChanged(int value) => OnPropertyChanged(nameof(ConnectionLabel));
}
