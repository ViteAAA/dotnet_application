using System.Globalization;
using System.Text.Json;
using PrintingSlips.Models;

namespace PrintingSlips.Services;

public class WooCommerceService
{
    private readonly HttpClient _client;

    private static readonly string[] DeliveryTimeKeys =
    [
        "delivery_time", "_delivery_time", "delivery_date", "_delivery_date",
        "delivery_datetime", "_delivery_datetime", "shipping_date", "_shipping_date",
        "wc_delivery_date", "_wc_delivery_date", "wc_delivery_time", "_wc_delivery_time",
        "jckwds_date", "jckwds_timeslot", "jckwds_date_text",
        "orddd_timestamp", "_orddd_timestamp", "e_deliverydate",
        "_billing_delivery_date", "_billing_delivery_time",
        "woofood_delivery_time", "_woofood_delivery_time"
    ];

    private static readonly string[] PersonsKeys =
    [
        "persons", "_persons", "number_of_persons", "_number_of_persons",
        "guests", "_guests", "number_of_guests", "_billing_persons",
        "billing_persons", "people", "persons_count"
    ];

    private static readonly string[] ManagerKeys =
    [
        "manager", "_manager", "order_manager", "_order_manager", "staff", "_staff"
    ];

    public WooCommerceService()
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("User-Agent", "C-Sharp-Receipt-Printer");
    }

    /// <summary>
    /// Опрашивает конкретный сайт и возвращает список чеков (Slip)
    /// </summary>
    public async Task<List<Slip>> GetProcessingOrdersAsync(WooCommerceSite site)
    {
        if (string.IsNullOrWhiteSpace(site.Url)) return [];

        var baseUrl = site.Url.TrimEnd('/');
        var requestUrl = $"{baseUrl}/wp-json/wc/v3/orders?status=processing&consumer_key={site.CustomerKey}&consumer_secret={site.SecretKey}";

        try
        {
            var response = await _client.GetAsync(requestUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Ошибка API {site.Url}]: {response.StatusCode}. {errorBody}");
                return [];
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var slips = new List<Slip>();

            foreach (var element in root.EnumerateArray())
            {
                slips.Add(ParseOrderToSlip(element, site.Url));
            }

            return slips;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Ошибка соединения с {site.Url}]: {ex.Message}");
            return [];
        }
    }

    private static Slip ParseOrderToSlip(JsonElement element, string siteUrl)
    {
        var orderId = element.GetProperty("id").GetInt64().ToString();
        var orderNumber = GetStringProperty(element, "number") ?? orderId;
        
        string? orderStatus = GetStringProperty(element, "status");
        string? currency = GetStringProperty(element, "currency");
        string? paymentMethod = GetStringProperty(element, "payment_method_title");
        string? customerNote = GetStringProperty(element, "customer_note");
        string? createdVia = GetStringProperty(element, "created_via");

        DateTime? orderDate = ParseDate(element, "date_created");
        var deliveredAt = FormatDate(ParseDate(element, "date_completed"));

        decimal? totalAmount = GetDecimalProperty(element, "total");
        decimal? shippingTotal = GetDecimalProperty(element, "shipping_total");
        decimal? discountTotal = GetDecimalProperty(element, "discount_total");

        string? billingFirstName = null;
        string? billingLastName = null;
        string? customerPhone = null;
        string? customerEmail = null;
        string? billingCity = null;
        string? billingAddress1 = null;
        string? billingPostcode = null;

        if (element.TryGetProperty("billing", out var billing))
        {
            billingFirstName = GetStringProperty(billing, "first_name");
            billingLastName = GetStringProperty(billing, "last_name");
            customerPhone = GetStringProperty(billing, "phone");
            customerEmail = GetStringProperty(billing, "email");
            billingCity = GetStringProperty(billing, "city");
            billingAddress1 = GetStringProperty(billing, "address_1");
            billingPostcode = GetStringProperty(billing, "postcode");
        }

        string? shippingFirstName = null;
        string? shippingLastName = null;
        string? shippingCity = null;
        string? shippingAddress = null;

        if (element.TryGetProperty("shipping", out var shipping))
        {
            shippingFirstName = GetStringProperty(shipping, "first_name");
            shippingLastName = GetStringProperty(shipping, "last_name");
            shippingCity = GetStringProperty(shipping, "city");
            var address1 = GetStringProperty(shipping, "address_1");
            var address2 = GetStringProperty(shipping, "address_2");
            shippingAddress = string.Join(", ", new[] { address1, address2 }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        var items = new List<SlipItem>();
        if (element.TryGetProperty("line_items", out var lineItems) && lineItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var itemEl in lineItems.EnumerateArray())
            {
                var name = GetStringProperty(itemEl, "name");
                var sku = GetStringProperty(itemEl, "sku");
                
                int? qty = itemEl.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number
                    ? q.GetInt32()
                    : null;

                decimal? price = GetDecimalProperty(itemEl, "price");
                decimal? total = GetDecimalProperty(itemEl, "total");

                items.Add(new SlipItem
                {
                    Name = name,
                    Sku = sku,
                    Quantity = qty,
                    Price = price,
                    TotalPrice = total
                });
            }
        }

        string? shippingMethod = null;
        string? deliveryCode = null;
        if (element.TryGetProperty("shipping_lines", out var shippingLines) &&
            shippingLines.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in shippingLines.EnumerateArray())
            {
                shippingMethod = GetStringProperty(line, "method_title") ?? shippingMethod;
                deliveryCode = GetStringProperty(line, "instance_id") ?? GetStringProperty(line, "method_id") ?? deliveryCode;

                if (line.TryGetProperty("meta_data", out var shipMeta))
                {
                    var fromShip = FindMetaValue(shipMeta, DeliveryTimeKeys);
                    // stored later
                    _ = fromShip;
                }
            }
        }

        JsonElement? meta = element.TryGetProperty("meta_data", out var metaEl) ? metaEl : null;
        var deliveryTime = meta.HasValue ? FindMetaValue(meta.Value, DeliveryTimeKeys) : null;
        if (string.IsNullOrWhiteSpace(deliveryTime) &&
            element.TryGetProperty("shipping_lines", out var sl2) && sl2.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in sl2.EnumerateArray())
            {
                if (line.TryGetProperty("meta_data", out var shipMeta))
                    deliveryTime = FindMetaValue(shipMeta, DeliveryTimeKeys) ?? deliveryTime;
            }
        }

        var persons = meta.HasValue ? FindMetaValue(meta.Value, PersonsKeys) : null;
        var manager = meta.HasValue ? FindMetaValue(meta.Value, ManagerKeys) : null;

        if (string.IsNullOrWhiteSpace(deliveryCode))
            deliveryCode = orderNumber.Length >= 2 ? orderNumber[^2..] : orderNumber;

        var deliveryType = InferDeliveryType(shippingMethod);

        return new Slip
        {
            OrderId = orderNumber,
            OrderStatus = orderStatus,
            OrderDate = orderDate,
            PaymentMethod = paymentMethod,
            CustomerNote = customerNote,
            Currency = currency,

            BillingFirstName = billingFirstName,
            BillingLastName = billingLastName,
            CustomerPhone = customerPhone,
            CustomerEmail = customerEmail,
            BillingCity = billingCity,
            BillingAddress1 = billingAddress1,
            BillingPostcode = billingPostcode,

            ShippingFirstName = shippingFirstName,
            ShippingLastName = shippingLastName,
            ShippingCity = shippingCity,
            ShippingAddress = shippingAddress,

            TotalAmount = totalAmount,
            ShippingTotal = shippingTotal,
            DiscountTotal = discountTotal,

            Items = items,
            Content = $"Источник: {siteUrl}",
            ShippingMethod = shippingMethod,
            DeliveryType = deliveryType,
            DeliveryTime = deliveryTime,
            DeliveredAt = deliveredAt,
            PersonsCount = persons,
            Manager = manager,
            DeliveryCode = deliveryCode,
            SalesChannel = MapSalesChannel(createdVia)
        };
    }

    private static string InferDeliveryType(string? shippingMethod)
    {
        if (string.IsNullOrWhiteSpace(shippingMethod))
            return "Доставка";

        var value = shippingMethod.ToLowerInvariant();
        if (value.Contains("local") || value.Contains("pickup") || value.Contains("самовыв") || value.Contains("самовывоз"))
            return shippingMethod;
        if (value.Contains("free") || value.Contains("flat") || value.Contains("courier") || value.Contains("достав"))
            return shippingMethod;

        return shippingMethod;
    }

    private static string MapSalesChannel(string? createdVia) => createdVia switch
    {
        "store-api" or "checkout" => "Сайт",
        "admin" => "Админ",
        "rest-api" => "API",
        "pos" => "POS",
        _ => string.IsNullOrWhiteSpace(createdVia) ? "Сайт" : createdVia
    };

    private static DateTime? ParseDate(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var dateProp) && dateProp.ValueKind == JsonValueKind.String)
        {
            var text = dateProp.GetString();
            if (!string.IsNullOrWhiteSpace(text) && DateTime.TryParse(text, out var parsedDate))
                return parsedDate;
        }

        return null;
    }

    private static string? FormatDate(DateTime? date) =>
        date?.ToString("dd.MM.yyyy HH:mm");

    private static string? FindMetaValue(JsonElement metaArray, IEnumerable<string> keys)
    {
        if (metaArray.ValueKind != JsonValueKind.Array)
            return null;

        var wanted = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        foreach (var item in metaArray.EnumerateArray())
        {
            var key = GetStringProperty(item, "key");
            if (string.IsNullOrWhiteSpace(key) || !wanted.Contains(key))
                continue;

            var value = MetaValueToString(item);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        foreach (var item in metaArray.EnumerateArray())
        {
            var key = GetStringProperty(item, "key") ?? string.Empty;
            var normalized = key.TrimStart('_').Replace('-', '_').ToLowerInvariant();
            if ((normalized.Contains("delivery") && (normalized.Contains("time") || normalized.Contains("date"))) ||
                (normalized.Contains("dostav") && (normalized.Contains("time") || normalized.Contains("date"))))
            {
                var value = MetaValueToString(item);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        return null;
    }

    private static string? MetaValueToString(JsonElement item)
    {
        if (!item.TryGetProperty("value", out var value))
            return GetStringProperty(item, "display_value");

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Object when value.TryGetProperty("label", out var label) => label.GetString(),
            JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(v => v.ToString())),
            _ => GetStringProperty(item, "display_value")
        };
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    private static decimal? GetDecimalProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
            {
                if (decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var valStr))
                    return valStr;
            }
            else if (prop.ValueKind == JsonValueKind.Number)
            {
                if (prop.TryGetDecimal(out var valNum))
                    return valNum;
            }
        }
        return null;
    }
}
