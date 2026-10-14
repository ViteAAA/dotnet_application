using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using PrintingSlips.Models;
using ApplicationView.Models;

namespace ApplicationView.Services;

public static class ProcessingData
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "PrintingSlips"
    );

    private static readonly string TemplatesFolder = Path.Combine(AppDataFolder, "Templates");
    private static readonly string SitesFilePath = Path.Combine(AppDataFolder, "sites.json");
    private static readonly string PrintersFilePath = Path.Combine(AppDataFolder, "printers.json");
    private static readonly string OrdersFilePath = Path.Combine(AppDataFolder, "processed_orders.json");
    private static readonly string CompanyFilePath = Path.Combine(AppDataFolder, "company.json");
    private static readonly string PrintLogFilePath = Path.Combine(AppDataFolder, "print_log.txt");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    static ProcessingData()
    {
        if (!Directory.Exists(AppDataFolder))
            Directory.CreateDirectory(AppDataFolder);

        if (!Directory.Exists(TemplatesFolder))
            Directory.CreateDirectory(TemplatesFolder);
    }

    // --- РАБОТА С ШАБЛОНАМИ ЧЕКОВ (ДЛЯ КАЖДОГО ПРИНТЕРА) ---

    private static string GetTemplatePath(string printerName)
    {
        string safeName = string.Join("_", printerName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(TemplatesFolder, $"{safeName}.json");
    }

    public static List<ReceiptField> LoadTemplate(string printerName)
    {
        var filePath = GetTemplatePath(printerName);
        if (!File.Exists(filePath))
        {
            return GetDefaultTemplate();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<ReceiptField>>(json) ?? GetDefaultTemplate();
        }
        catch
        {
            return GetDefaultTemplate();
        }
    }

    public static void SaveTemplate(string printerName, IEnumerable<ReceiptField> fields)
    {
        var filePath = GetTemplatePath(printerName);
        var json = JsonSerializer.Serialize(fields, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private static List<ReceiptField> GetDefaultTemplate()
    {
        return
        [
            new ReceiptField("", "order.seller_title"),
            new ReceiptField("", "order.seller_name"),
            new ReceiptField("ИНН", "order.seller_inn"),
            new ReceiptField("НАКЛАДНАЯ", "static.text", "НАКЛАДНАЯ", isItemContainer: false)
            {
                IsBold = true,
                FontSize = 2
            },
            new ReceiptField("Заказ №", "order.number") { IsBold = true, FontSize = 2 },
            new ReceiptField("Грузоотправитель", "order.seller_title"),
            new ReceiptField("Грузополучатель", "shipping.first_name"),
            new ReceiptField("Фамилия получателя", "shipping.last_name"),
            new ReceiptField("Адрес доставки", "shipping.address_1"),
            new ReceiptField("Телефон", "billing.phone"),
            new ReceiptField("Менеджер", "order.manager"),
            new ReceiptField("Заказ принят", "order.date_created"),
            new ReceiptField("Накладная оформлена", "order.date_created"),
            new ReceiptField("Время доставки", "order.delivery_time"),
            new ReceiptField("Заказ доставлен", "order.delivered_at"),
            new ReceiptField("Количество человек", "order.persons"),
            new ReceiptField("Список товаров", "block.items")
            {
                ChildFields = new ObservableCollection<ReceiptField>
                {
                    new ReceiptField("Наименование", "item.name"),
                    new ReceiptField("Кол-во", "item.quantity"),
                    new ReceiptField("Цена", "item.price"),
                    new ReceiptField("Сумма", "item.total")
                }
            },
            new ReceiptField("Итого к оплате", "order.total") { IsBold = true },
            new ReceiptField("Способ оплаты", "order.payment_method_title"),
            new ReceiptField("Количество персон", "order.persons"),
            new ReceiptField("Канал продаж", "order.sales_channel"),
            new ReceiptField("Номер заказа FN", "order.number"),
            new ReceiptField("", "order.sign_released"),
            new ReceiptField("", "order.sign_received"),
            new ReceiptField("Комментарий к заказу", "order.customer_note"),
            new ReceiptField("Доставка", "order.delivery_type") { IsBold = true, FontSize = 2 },
            new ReceiptField("Код доставки", "order.delivery_code"),
            new ReceiptField("", "order.currency_note"),
            new ReceiptField("", "order.footer")
        ];
    }

    // --- РАБОТА СО СПИСКОМ WOOCOMMERCE-САЙТОВ ---

    public static List<WooCommerceSite> LoadSites()
    {
        if (!File.Exists(SitesFilePath))
            return [];

        try
        {
            var json = File.ReadAllText(SitesFilePath);
            return JsonSerializer.Deserialize<List<WooCommerceSite>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void SaveSites(IEnumerable<WooCommerceSite> sites)
    {
        try
        {
            var json = JsonSerializer.Serialize(sites, JsonOptions);
            File.WriteAllText(SitesFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProcessingData Error] Не удалось сохранить сайты: {ex.Message}");
        }
    }

    // --- РАБОТА С НАСТРОЙКАМИ И ВЫБОРОМ ПРИНТЕРОВ ---

    public static List<PrinterSettingsItem> LoadPrinterSettings()
    {
        if (!File.Exists(PrintersFilePath))
            return [];

        try
        {
            var json = File.ReadAllText(PrintersFilePath);
            return JsonSerializer.Deserialize<List<PrinterSettingsItem>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void SavePrinterSettings(IEnumerable<PrinterSettingsItem> printers)
    {
        try
        {
            var json = JsonSerializer.Serialize(printers, JsonOptions);
            File.WriteAllText(PrintersFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProcessingData Error] Не удалось сохранить настройки принтеров: {ex.Message}");
        }
    }

    public static List<ProcessedOrderRecord> LoadProcessedOrders()
    {
        if (!File.Exists(OrdersFilePath))
            return [];

        try
        {
            var json = File.ReadAllText(OrdersFilePath);
            return JsonSerializer.Deserialize<List<ProcessedOrderRecord>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void SaveProcessedOrders(IEnumerable<ProcessedOrderRecord> orders)
    {
        try
        {
            var json = JsonSerializer.Serialize(orders, JsonOptions);
            File.WriteAllText(OrdersFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProcessingData Error] Не удалось сохранить историю заказов: {ex.Message}");
        }
    }

    public static CompanySettings LoadCompanySettings()
    {
        if (!File.Exists(CompanyFilePath))
            return new CompanySettings();

        try
        {
            var json = File.ReadAllText(CompanyFilePath);
            return JsonSerializer.Deserialize<CompanySettings>(json) ?? new CompanySettings();
        }
        catch
        {
            return new CompanySettings();
        }
    }

    public static void SaveCompanySettings(CompanySettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(CompanyFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProcessingData Error] Не удалось сохранить реквизиты: {ex.Message}");
        }
    }

    public static void AppendPrintLog(string message)
    {
        try
        {
            File.AppendAllText(PrintLogFilePath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
            // журнал не должен ломать печать
        }
    }
}