using PrintingSlips.Models;
using PrintingSlips.Interfaces;

namespace PrintingSlips.Services;

public class PrintingService(ITemplateProvider? templateProvider = null) : IXPrintingService
{
    private readonly ITemplateProvider? _templateProvider = templateProvider;

    public async Task<bool> PrintSlipAsync(Slip slip, PrinterDestination printer)
    {
        var width = printer.WidthMm > 0 ? printer.WidthMm : 80;
        var charsPerLine = width <= 60 ? 32 : 48;
        
        // Определяем профиль принтера
        var (encoding, codePage) = GetPrinterProfile(printer.Name);
        var fields = _templateProvider?.GetTemplateForPrinter(printer.Name);

        var builder = new EscPosBuilder(charsPerLine, encoding).Initialize(codePage);

        // Генерация байтов
        if (fields is { Count: > 0 })
        {
            // Здесь ваш метод сборки по шаблону (адаптированный под EscPosBuilder)
            // GenerateTemplateBuffer(slip, builder, fields); 
        }
        else if (width > 60)
        {
            GenerateKitchenSlip(slip, builder);
        }
        else
        {
            // GenerateSlip(slip, builder);
        }

        byte[] buffer = builder.Build();
        return await SendToDestinationAsync(printer, buffer, $"Slip_{slip.OrderId}");
    }

    private void GenerateKitchenSlip(Slip slip, EscPosBuilder builder)
    {
        var currentTime = DateTime.Now.ToString("HH:mm");
        var deliveryType = string.IsNullOrWhiteSpace(slip.DeliveryType) ? "Доставка" : slip.DeliveryType;

        builder.AlignCenter()
               .BoldOn()
               .PrintLine(currentTime)
               .PrintLine(deliveryType)
               .PrintLine($"ЗАКАЗ № {slip.OrderId}")
               .BoldOff();

        builder.AlignLeft()
               .PrintLine($"Оформлен: {slip.OrderDate?.ToString("HH:mm") ?? currentTime}")
               .PrintSeparator()
               .PrintLine("Название                 Кол-во")
               .PrintSeparator();

        foreach (var item in slip.Items)
        {
            builder.PrintLine(item.Name ?? "Товар")
                   .PrintLine($"{item.Quantity ?? 1}");
        }

        builder.PrintSeparator()
               .PrintLine($"Оплата: {slip.PaymentMethod ?? "Не указана"}")
               .PrintLine($"Итого: {slip.TotalAmount?.ToString("F2") ?? "0.00"}")
               .FeedLines(3)
               .CutPaper();
    }

    private async Task<bool> SendToDestinationAsync(PrinterDestination printer, byte[] buffer, string documentName)
    {
        try
        {
            if (string.Equals(printer.ConnectionType, "Network", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(printer.Host)) throw new Exception("Сетевой IP не указан.");
                await PrinterTransport.SendToNetworkAsync(printer.Host, printer.Port > 0 ? printer.Port : 9100, buffer);
            }
            else
            {
                // Запускаем синхронный вызов WinAPI в пуле потоков, чтобы не блокировать вызывающий поток
                await Task.Run(() => PrinterTransport.SendToSpooler(printer.Name, buffer, documentName));
            }
            return true;
        }
        catch (Exception ex)
        {
            // Логирование ошибки
            Console.WriteLine($"Ошибка печати: {ex.Message}");
            return false;
        }
    }

    private static (string EncodingName, byte CodePage) GetPrinterProfile(string printerName)
    {
        if (printerName.Contains("XP-N160II", StringComparison.OrdinalIgnoreCase)) return ("windows-1251", 23);
        if (printerName.Contains("XP-80T", StringComparison.OrdinalIgnoreCase)) return ("cp866", 17);
        return ("cp866", 17); // Значение по умолчанию теперь всегда имеет CodePage (17)
    }

    // Для PrintTestPageAsync логика такая же - используйте EscPosBuilder
    public async Task<bool> PrintTestPageAsync(PrinterDestination printer) => throw new NotImplementedException();
}