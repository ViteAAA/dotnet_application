using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace PrintingSlips.Services;

public static class PrinterTransport
{
    public static async Task SendToNetworkAsync(string host, int port, byte[] data, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(8));

        await client.ConnectAsync(host, port, cts.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(data, cts.Token);
        await stream.FlushAsync(cts.Token);
    }

    public static void SendToSpooler(string printerName, byte[] data, string documentName)
    {
        IntPtr hPrinter = IntPtr.Zero;
        IntPtr pBytes = Marshal.AllocCoTaskMem(data.Length);

        try
        {
            Marshal.Copy(data, 0, pBytes, data.Length);

            if (!OpenPrinter(printerName.Normalize(), out hPrinter, IntPtr.Zero))
                throw new InvalidOperationException($"Не удалось открыть принтер [{printerName}]. Ошибка: {Marshal.GetLastWin32Error()}");

            var di = new DOCINFOA { pDocName = documentName, pDataType = "RAW" };

            if (!StartDocPrinter(hPrinter, 1, di)) throw new InvalidOperationException("Ошибка StartDocPrinter.");
            try
            {
                if (!StartPagePrinter(hPrinter)) throw new InvalidOperationException("Ошибка StartPagePrinter.");
                try
                {
                    if (!WritePrinter(hPrinter, pBytes, data.Length, out _))
                        throw new InvalidOperationException("Ошибка WritePrinter.");
                }
                finally
                {
                    EndPagePrinter(hPrinter); // Гарантированно закрываем страницу
                }
            }
            finally
            {
                EndDocPrinter(hPrinter); // Гарантированно закрываем документ
            }
        }
        finally
        {
            if (hPrinter != IntPtr.Zero) ClosePrinter(hPrinter); // Гарантированно отпускаем принтер
            Marshal.FreeCoTaskMem(pBytes); // Гарантированно очищаем unmanaged-память
        }
    }

    #region WinAPI
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool OpenPrinter(string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);
    #endregion
}