
namespace PrintingSlips.Models;

/// <summary>
/// Представляет данные чека (Slip), полностью соответствующие MainWooCommerceOptions
/// </summary>
public class Slip
{
    // --- ОСНОВНАЯ ИНФОРМАЦИЯ О ЗАКАЗЕ ---
    
    /// <summary> order.number - [ID / Номер заказа] </summary>
    public required string OrderId { get; set; }

    /// <summary> order.status - [Статус заказа] </summary>
    public string? OrderStatus { get; set; }

    /// <summary> order.date_created - [Дата создания] </summary>
    public DateTime? OrderDate { get; set; }

    /// <summary> order.payment_method_title - [Способ оплаты] </summary>
    public string? PaymentMethod { get; set; }

    /// <summary> order.customer_note - [Примечание покупателя] </summary>
    public string? CustomerNote { get; set; }

    /// <summary> order.currency - [Валюта] </summary>
    public string? Currency { get; set; }


    // --- BILLING (Оплата / Контакты) ---

    /// <summary> billing.first_name - [Имя покупателя] </summary>
    public string? BillingFirstName { get; set; }

    /// <summary> billing.last_name - [Фамилия покупателя] </summary>
    public string? BillingLastName { get; set; }

    /// <summary> billing.phone - [Телефон] </summary>
    public string? CustomerPhone { get; set; }

    /// <summary> billing.email - [Email] </summary>
    public string? CustomerEmail { get; set; }

    /// <summary> billing.city - [Город оплаты] </summary>
    public string? BillingCity { get; set; }

    /// <summary> billing.address_1 - [Адрес оплаты 1] </summary>
    public string? BillingAddress1 { get; set; }

    /// <summary> billing.postcode - [Почтовый индекс] </summary>
    public string? BillingPostcode { get; set; }


    // --- SHIPPING (Доставка) ---

    /// <summary> shipping.first_name - [Имя получателя] </summary>
    public string? ShippingFirstName { get; set; }

    /// <summary> shipping.last_name - [Фамилия получателя] </summary>
    public string? ShippingLastName { get; set; }

    /// <summary> shipping.city - [Город доставки] </summary>
    public string? ShippingCity { get; set; }

    /// <summary> shipping.address_1 - [Адрес доставки 1] </summary>
    public string? ShippingAddress { get; set; }


    // --- СУММЫИ И РАСЧЕТЫ ---

    /// <summary> order.total - [Итоговая сумма] </summary>
    public decimal? TotalAmount { get; set; }

    /// <summary> order.shipping_total - [Стоимость доставки] </summary>
    public decimal? ShippingTotal { get; set; }

    /// <summary> order.discount_total - [Скидка] </summary>
    public decimal? DiscountTotal { get; set; }


    // --- СПЕЦИАЛЬНЫЕ БЛОКИ И ДОПОЛНИТЕЛЬНО ---

    /// <summary> block.items - 📦 [Блок: Список товаров] </summary>
    public List<SlipItem> Items { get; set; } = [];

    /// <summary> static.text / Источник - Произвольный контент </summary>
    public string? Content { get; set; }

    public string? ShippingMethod { get; set; }
    public string? DeliveryType { get; set; }
    public string? DeliveryTime { get; set; }
    public string? DeliveredAt { get; set; }
    public string? PersonsCount { get; set; }
    public string? Manager { get; set; }
    public string? SellerTitle { get; set; }
    public string? SellerName { get; set; }
    public string? SellerInn { get; set; }
    public string? DeliveryCode { get; set; }
    public string? SalesChannel { get; set; }
    public string? SignReleased { get; set; }
    public string? SignReceived { get; set; }
    public string? Footer { get; set; }
    public string? CurrencyNote { get; set; }


    // --- УДОБНЫЕ ВСПОМОГАТЕЛЬНЫЕ СВОЙСТВА ДЛЯ UI ---

    /// <summary> Полное имя покупателя (Оплата) </summary>
    public string CustomerName => string.Join(" ", new[] { BillingFirstName, BillingLastName }
        .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

    /// <summary> Полное имя получателя (Доставка) </summary>
    public string ShippingCustomerName => string.Join(" ", new[] { ShippingFirstName, ShippingLastName }
        .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
}

/// <summary>
/// Представляет позицию товара в чеке, соответствующую ItemWooCommerceOptions
/// </summary>
public class SlipItem
{
    // item.name - [Товар: Название]
    public string? Name { get; set; }

    // item.quantity - [Товар: Количество]
    public int? Quantity { get; set; }

    // item.price - [Товар: Цена]
    public decimal? Price { get; set; }

    // item.total - [Товар: Сумма]
    public decimal? TotalPrice { get; set; }

    // item.sku - [Товар: Артикул (SKU)]
    public string? Sku { get; set; }
}
