using CommunityToolkit.Mvvm.ComponentModel;
using PrintingSlips.Models;

namespace ApplicationView.Models;

public partial class WooOrder : ObservableObject
{
    [ObservableProperty] private string orderDate = string.Empty;
    [ObservableProperty] private string siteUrl = string.Empty;
    [ObservableProperty] private string orderId = string.Empty;
    [ObservableProperty] private string customerName = string.Empty;
    [ObservableProperty] private string totalAmount = string.Empty;
    [ObservableProperty] private string status = string.Empty;
    [ObservableProperty] private string? printError;
    [ObservableProperty] private Slip? slipData;
}
