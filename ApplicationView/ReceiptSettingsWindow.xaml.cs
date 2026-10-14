using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.Windows;
using ApplicationView.Models;
using ApplicationView.Services;
using PrintingSlips.Models;

namespace ApplicationView;

public partial class ReceiptSettingsWindow : Window
{
    public ObservableCollection<ReceiptField> ReceiptFields { get; set; } = [];
    public List<string> InstalledPrinters { get; set; } = [];
    public string SelectedPrinter { get; set; }

    // Полная карта полей WooCommerce JSON
    public Dictionary<string, string> MainWooCommerceOptions { get; } = new()
    {
        { "order.number", "[ID / Номер заказа]" },
        { "order.status", "[Статус заказа]" },
        { "order.date_created", "[Дата создания]" },
        { "order.payment_method_title", "[Способ оплаты]" },
        { "order.customer_note", "[Примечание покупателя]" },
        { "order.currency", "[Валюта]" },
        
        // Billing (Оплата/Контакты)
        { "billing.first_name", "[Имя покупателя]" },
        { "billing.last_name", "[Фамилия покупателя]" },
        { "billing.phone", "[Телефон]" },
        { "billing.email", "[Email]" },
        { "billing.city", "[Город оплаты]" },
        { "billing.address_1", "[Адрес оплаты 1]" },
        { "billing.postcode", "[Почтовый индекс]" },

        // Shipping (Доставка)
        { "shipping.first_name", "[Имя получателя]" },
        { "shipping.last_name", "[Фамилия получателя]" },
        { "shipping.city", "[Город доставки]" },
        { "shipping.address_1", "[Адрес доставки 1]" },

        // Суммы
        { "order.total", "[Итоговая сумма]" },
        { "order.shipping_total", "[Стоимость доставки]" },
        { "order.discount_total", "[Скидка]" },
        { "order.shipping_method", "[Способ / тип доставки]" },
        { "order.delivery_type", "[Тип доставки]" },
        { "order.delivery_time", "[Время доставки]" },
        { "order.delivered_at", "[Заказ доставлен]" },
        { "order.persons", "[Количество персон]" },
        { "order.manager", "[Менеджер]" },
        { "order.seller_title", "[Реквизиты продавца]" },
        { "order.seller_name", "[ФИО продавца]" },
        { "order.seller_inn", "[ИНН продавца]" },
        { "order.delivery_code", "[Код доставки]" },
        { "order.sales_channel", "[Канал продаж]" },
        { "order.sign_released", "[Подпись: груз сдал]" },
        { "order.sign_received", "[Подпись: груз принял]" },
        { "order.footer", "[Подвал чека]" },

        // Специальные блоки
        { "block.items", "📦 [Блок: Список товаров]" },
        { "static.text", "Текст (Статический)" }
    };

    // Опции полей позиции товара
    public Dictionary<string, string> ItemWooCommerceOptions { get; } = new()
    {
        { "item.name", "[Товар: Название]" },
        { "item.quantity", "[Товар: Количество]" },
        { "item.price", "[Товар: Цена]" },
        { "item.subtotal", "[Товар: Промежуточная сумма]" },
        { "item.total", "[Товар: Итоговая сумма]" },
        { "item.sku", "[Товар: Артикул (SKU)]" }
    };

    public ReceiptSettingsWindow(string? defaultPrinter = null, IEnumerable<string>? extraPrinters = null)
    {
        InitializeComponent();

        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            InstalledPrinters.Add(printer);
        }

        if (extraPrinters != null)
        {
            foreach (var printer in extraPrinters)
            {
                if (!string.IsNullOrWhiteSpace(printer) &&
                    InstalledPrinters.All(p => !string.Equals(p, printer, StringComparison.OrdinalIgnoreCase)))
                {
                    InstalledPrinters.Add(printer);
                }
            }
        }

        SelectedPrinter = defaultPrinter
                          ?? InstalledPrinters.FirstOrDefault()
                          ?? "DefaultPrinter";

        DataContext = this;
        LoadPrinterTemplate(SelectedPrinter);

        BtnCancel.Click += (s, e) => DialogResult = false;
    }

    private void LoadPrinterTemplate(string printerName)
    {
        var fields = ProcessingData.LoadTemplate(printerName);
        ReceiptFields.Clear();
        foreach (var f in fields)
            ReceiptFields.Add(f);
    }

    private void CmbPrinters_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(SelectedPrinter))
        {
            LoadPrinterTemplate(SelectedPrinter);
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(SelectedPrinter))
        {
            MessageBox.Show("Выберите принтер!");
            return;
        }

        // Сохраняем шаблон именно для выбранного принтера
        ProcessingData.SaveTemplate(SelectedPrinter, ReceiptFields);
        MessageBox.Show($"Шаблон для принтера '{SelectedPrinter}' успешно сохранен!");
        DialogResult = true;
    }

    private void BtnAddField_Click(object sender, RoutedEventArgs e)
    {
        Console.WriteLine("[CLICK] Add field click");
        var newField = new ReceiptField("Заголовок", "order.number");
        ReceiptFields.Add(newField);
    }

    private void BtnUp_Click(object sender, RoutedEventArgs e)
    {
        var idx = LstReceiptFields.SelectedIndex;
        if (idx > 0)
        {
            ReceiptFields.Move(idx, idx - 1);
            // Чтобы фокус/выделение в списке не сбрасывалось:
            LstReceiptFields.SelectedIndex = idx - 1; 
        }
    }

    private void BtnDown_Click(object sender, RoutedEventArgs e)
    {
        var idx = LstReceiptFields.SelectedIndex;
        if (idx >= 0 && idx < ReceiptFields.Count - 1)
        {
            ReceiptFields.Move(idx, idx + 1);
            // Чтобы фокус/выделение в списке сдвигалось вслед за элементом:
            LstReceiptFields.SelectedIndex = idx + 1; 
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        int idx = LstReceiptFields.SelectedIndex;
        if (idx >= 0) ReceiptFields.RemoveAt(idx);
    }

    private void BtnAddSubField_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ReceiptField parentField })
            parentField.ChildFields.Add(new ReceiptField("Параметр товара", "item.name"));
    }

    private void BtnRemoveSubField_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ReceiptField subField })
        {
            foreach (var parent in ReceiptFields)
            {
                if (parent.ChildFields.Remove(subField)) break;
            }
        }
    }
}