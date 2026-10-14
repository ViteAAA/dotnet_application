using PrintingSlips.Models;

namespace PrintingSlips.Interfaces;

public interface IXPrintingService
{
    Task<bool> PrintSlipAsync(Slip slip, PrinterDestination printer);
    Task<bool> PrintTestPageAsync(PrinterDestination printer);
}
