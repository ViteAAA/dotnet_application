using PrintingSlips.Models;

namespace ApplicationView.Models;

public class ProcessedOrderRecord
{
    public string OrderId { get; set; } = string.Empty;
    public string SiteUrl { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string TotalAmount { get; set; } = string.Empty;
    public string OrderDate { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? PrintError { get; set; }
    public DateTime LastAttemptUtc { get; set; } = DateTime.UtcNow;
    public Slip? SlipData { get; set; }
}
