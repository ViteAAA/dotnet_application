using System.Net.Sockets;
using System.Runtime.InteropServices;
using PrintingSlips.Interfaces;
using PrintingSlips.Models;

namespace PrintingSlips.Services;

/// <summary>
/// Отрефакторенный сервис печати. Отвечает ТОЛЬКО за маршрутизацию и отправку байтов
/// на принтер (WinAPI Spooler / TCP Socket) и сохранение PDF, делегируя верстку форм
/// строителю IPrintDocumentBuilder и экспортеру IPdfSlipExporter.
/// </summary>
public class PrintingService : IXPrintingService
{
    private readonly ITemplateProvider? _templateProvider;
    private readonly IPrintDocumentBuilder _documentBuilder;
    private readonly IPdfSlipExporter _pdfExporter;

    public PrintingService(
        ITemplateProvider? templateProvider = null,
        IPrintDocumentBuilder? documentBuilder = null,
        IPdfSlipExporter? pdfExporter = null)
    {
        _templateProvider = templateProvider;
        _documentBuilder = documentBuilder ?? new PrintDocumentBuilder();
        _pdfExporter = pdfExporter ?? new PdfSlipExporter();
    }

    /// <summary>
    /// Печатает чек на целевом принтере и генерирует соответствующую PDF копию
    /// </summary>
    public async Task<bool> PrintSlipAsync(Slip slip, PrinterDestination printer)
    {
        var width = printer.WidthMm > 0 ? printer.WidthMm : 80;
        var profile = _documentBuilder.ResolvePrinterProfile(printer.Name);
        var fields = _templateProvider?.GetTemplateForPrinter(printer.Name);

        // ТЗ: принтер 1 (~58 мм) = пречек/накладная; принтер 2 (~80 мм) = кухня.
        // Сохранённый шаблон (CustomTemplate) перекрывает встроенные формы.
        // Пустой шаблон → выбор по PaperWidthMm: ≤60 Invoice, >60 Kitchen.
        // FontSize полей — в CustomTemplate; DefaultFontSize принтера — для Invoice/Kitchen.
        var formType = fields is { Count: > 0 }
            ? PrintFormType.CustomTemplate
            : width > 60
                ? PrintFormType.KitchenSlip
                : PrintFormType.Invoice;

        // Автоматически экспортируем PDF для этой формы
        ExportPdfPreview(slip, formType, width, fields);

        var baseFont = Math.Clamp(printer.BaseFontSize, 1, 3);
        var result = _documentBuilder.BuildForm(formType, slip, width, profile, fields, baseFont);

        // Отправляем байты на оборудование
        return await SendToDestinationAsync(printer, result.EscPosBytes, $"Slip_{slip.OrderId}");
    }

    /// <summary>
    /// Печатает тестовую страницу и сохраняет ее PDF копию
    /// </summary>
    public async Task<bool> PrintTestPageAsync(PrinterDestination printer)
    {
        var width = printer.WidthMm > 0 ? printer.WidthMm : 80;
        var profile = _documentBuilder.ResolvePrinterProfile(printer.Name);

        // Экспорт PDF копии теста
        try
        {
            var pdfBytes = _pdfExporter.ExportTestPagePdf(width);
            var testPdfPath = Path.Combine(AppContext.BaseDirectory, $"test_page_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            File.WriteAllBytes(testPdfPath, pdfBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PDF Test] Ошибка сохранения: {ex.Message}");
        }

        // Формируем ESC/POS байты тестовой страницы
        var buffer = _documentBuilder.BuildTestPageEscPos(width, profile);

        return await SendToDestinationAsync(printer, buffer, "Printer_Test");
    }

    /// <summary>
    /// Прямой экспорт в PDF любой формы по запросу
    /// </summary>
    public string ExportFormToPdf(PrintFormType formType, Slip slip, float widthMm = 80, string? outputFolder = null)
    {
        var folder = outputFolder ?? Path.Combine(AppContext.BaseDirectory, "PdfExports");
        var fields = formType == PrintFormType.CustomTemplate && _templateProvider != null
            ? _templateProvider.GetTemplateForPrinter("Default")
            : null;

        return _pdfExporter.SavePdfToFile(folder, formType, slip, widthMm, fields);
    }

    private void ExportPdfPreview(Slip slip, PrintFormType formType, float width, IReadOnlyList<ReceiptField>? fields)
    {
        try
        {
            var pdfBytes = _pdfExporter.ExportFormPdf(formType, slip, width, fields);
            var path = Path.Combine(AppContext.BaseDirectory, $"check_{slip.OrderId}.pdf");
            File.WriteAllBytes(path, pdfBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PDF] Ошибка создания preview: {ex.Message}");
        }
    }

    private async Task<bool> SendToDestinationAsync(PrinterDestination printer, byte[] buffer, string documentName)
    {
        if (string.Equals(printer.ConnectionType, "Network", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(printer.Host))
                throw new Exception("Для сетевого принтера не указан IP-адрес.");

            return await SendBytesToNetworkAsync(printer.Host, printer.Port > 0 ? printer.Port : 9100, buffer);
        }

        return await Task.Run(() => SendBytesToPrinter(printer.Name, buffer, documentName));
    }

    private static async Task<bool> SendBytesToNetworkAsync(string host, int port, byte[] bytes)
    {
        using var client = new TcpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await client.ConnectAsync(host, port, cts.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(bytes, cts.Token);
        await stream.FlushAsync(cts.Token);
        return true;
    }

    #region WinAPI RAW Printing (winspool.drv)

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    private static bool SendBytesToPrinter(string printerName, byte[] bytes, string documentName)
    {
        IntPtr pBytes = Marshal.AllocCoTaskMem(bytes.Length);
        Marshal.Copy(bytes, 0, pBytes, bytes.Length);

        if (!OpenPrinter(printerName.Normalize(), out var hPrinter, IntPtr.Zero))
        {
            Marshal.FreeCoTaskMem(pBytes);
            throw new Exception($"Не удалось открыть принтер [{printerName}]. Проверьте подключение.");
        }

        var di = new DOCINFOA
        {
            pDocName = documentName,
            pDataType = "RAW"
        };

        bool success = false;
        if (StartDocPrinter(hPrinter, 1, di))
        {
            if (StartPagePrinter(hPrinter))
            {
                success = WritePrinter(hPrinter, pBytes, bytes.Length, out _);
                EndPagePrinter(hPrinter);
            }
            EndDocPrinter(hPrinter);
        }

        ClosePrinter(hPrinter);
        Marshal.FreeCoTaskMem(pBytes);

        if (!success)
        {
            throw new Exception($"Ошибка передачи байтов печати в спулер Windows (Код: {Marshal.GetLastWin32Error()}).");
        }

        return true;
    }

    #endregion
}