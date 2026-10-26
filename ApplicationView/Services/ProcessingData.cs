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

    /// <summary>
    /// Загружает сохранённый шаблон принтера.
    /// Если файла нет — пустой список: PrintingService тогда печатает
    /// встроенную форму по ширине ленты (≤60 мм накладная/пречек, >60 мм кухня).
    /// Порядок полей, FontSize (1–3) и IsBold применяются только к сохранённому CustomTemplate.
    /// </summary>
    public static List<ReceiptField> LoadTemplate(string printerName)
    {
        var filePath = GetTemplatePath(printerName);
        if (!File.Exists(filePath))
            return [];

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<ReceiptField>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Дефолт для редактора настроек: 58 мм → пречек/накладная, 80 мм → кухня.
    /// </summary>
    public static List<ReceiptField> GetDefaultTemplateForWidth(int widthMm) =>
        widthMm <= 60 ? GetDefaultInvoiceTemplate() : GetDefaultKitchenTemplate();

    public static void SaveTemplate(string printerName, IEnumerable<ReceiptField> fields)
    {
        var filePath = GetTemplatePath(printerName);
        var json = JsonSerializer.Serialize(fields, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public static List<ReceiptField> GetDefaultInvoiceTemplate()
    {
        // Пречек / накладная (XP-58 / ~58 мм). Подписи RU+EN — для ТЗ «в одном чеке».
        return
        [
            new ReceiptField("", "order.seller_title"),
            new ReceiptField("", "order.seller_name"),
            new ReceiptField("ИНН", "order.seller_inn"),
            new ReceiptField("НАКЛАДНАЯ / INVOICE", "static.text", "НАКЛАДНАЯ / INVOICE", isItemContainer: false)
            {
                IsBold = true,
                FontSize = 2
            },
            new ReceiptField("Заказ № / Order #", "order.number") { IsBold = true, FontSize = 2 },
            new ReceiptField("Грузоотправитель / Shipper", "order.seller_title"),
            new ReceiptField("Грузополучатель / Consignee", "shipping.first_name"),
            new ReceiptField("Фамилия получателя", "shipping.last_name"),
            new ReceiptField("Адрес / Address", "shipping.address_1"),
            new ReceiptField("Телефон / Phone", "billing.phone"),
            new ReceiptField("Менеджер / Manager", "order.manager"),
            new ReceiptField("Заказ принят / Accepted", "order.date_created"),
            new ReceiptField("Накладная оформлена / Issued", "order.date_created"),
            new ReceiptField("Время доставки / Deliver by", "order.delivery_time"),
            new ReceiptField("Заказ доставлен / Delivered", "order.delivered_at"),
            new ReceiptField("Кол-во человек / Persons", "order.persons"),
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
            new ReceiptField("Итого / Total", "order.total") { IsBold = true },
            new ReceiptField("Оплата / Payment", "order.payment_method_title"),
            new ReceiptField("Персон / Persons", "order.persons"),
            new ReceiptField("Канал / Channel", "order.sales_channel"),
            new ReceiptField("FN / Order FN", "order.number"),
            new ReceiptField("", "order.sign_released"),
            new ReceiptField("", "order.sign_received"),
            new ReceiptField("Комментарий / Note", "order.customer_note"),
            new ReceiptField("Доставка / Delivery", "order.delivery_type") { IsBold = true, FontSize = 2 },
            new ReceiptField("Код доставки", "order.delivery_code"),
            new ReceiptField("", "order.currency_note"),
            new ReceiptField("", "order.footer")
        ];
    }


    /// <summary>
    /// Кухонный чек по умолчанию (XP-80 / ~80 мм): время, тип доставки, позиции, адрес, оплата.
    /// </summary>
    public static List<ReceiptField> GetDefaultKitchenTemplate()
    {
        return
        [
            new ReceiptField("", "order.delivery_time") { IsBold = true, FontSize = 3 },
            new ReceiptField("Доставка / Delivery", "order.delivery_type") { IsBold = true, FontSize = 2 },
            new ReceiptField("ЗАКАЗ № / ORDER #", "order.number") { IsBold = true, FontSize = 2 },
            new ReceiptField("Оформлен / Placed", "order.date_created"),
            new ReceiptField("Доставить к / Deliver by", "order.delivery_time"),
            new ReceiptField("Позиции / Items", "block.items")
            {
                ChildFields = new ObservableCollection<ReceiptField>
                {
                    new ReceiptField("Название / Name", "item.name"),
                    new ReceiptField("Кол-во / Qty", "item.quantity")
                }
            },
            new ReceiptField("Адрес / Address", "shipping.address_1"),
            new ReceiptField("Ком. к заказу / Note", "order.customer_note"),
            new ReceiptField("Оплата / Payment", "order.payment_method_title"),
            new ReceiptField("Персон / Persons", "order.persons"),
            new ReceiptField("Канал / Channel", "order.sales_channel"),
            new ReceiptField("FN", "order.number")
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