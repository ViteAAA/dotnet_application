using System.Windows;
using PrintingSlips.Models;

namespace ApplicationView;

public partial class AddNetworkPrinterWindow : Window
{
    public string PrinterName { get; private set; } = string.Empty;
    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; } = 9100;
    public int PaperWidthMm { get; private set; } = 80;

    public AddNetworkPrinterWindow()
    {
        InitializeComponent();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtHost.Text))
        {
            MessageBox.Show("Укажите IP-адрес или имя хоста.", "Внимание",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Host = TxtHost.Text.Trim();
        Port = int.TryParse(TxtPort.Text, out var port) ? Math.Clamp(port, 1, 65535) : 9100;
        PaperWidthMm = (TxtWidth.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() == "58"
            ? 58
            : 80;
        PrinterName = string.IsNullOrWhiteSpace(TxtName.Text)
            ? $"NET {Host}:{Port}"
            : TxtName.Text.Trim();

        DialogResult = true;
    }
}
