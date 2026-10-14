using PrintingSlips.Services;
using PrintingSlips.Models;

namespace PrintingSlips;

public static class Printer
{
    

    public static async Task Printing(Slip slip, string selectedPrinter)
    {
        PrintingService printingService = new();

        // await printingService.PrintSlipAsync(slip, TODO, TODO);
    }
}

/*
Console.WriteLine("Запуск проверки заказов из другого проекта...");
var wcService = new WooCommerceService();

try
{
    var jsonOrders = await wcService.GetProcessingOrdersAsync();
    Console.WriteLine("Данные успешно получены в смежном проекте!");

    // ИСПРАВЛЕНО: Возвращаем строку в консольное приложение
    return jsonOrders; 
}
catch (Exception ex)
{
    return $"Произошла ошибка при вызове сервиса: {ex.Message}";
}
*/