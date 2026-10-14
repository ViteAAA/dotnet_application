using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ApplicationView.Models;
using PrintingSlips.Models;
using PrintingSlips.Services;
using ApplicationView.Services;

namespace ApplicationView;

public partial class MainWindow : Window
{
    private ObservableCollection<WooCommerceSite> Sites { get; set; } = [];
    private ObservableCollection<WooOrder> Orders { get; set; } = [];
    private ObservableCollection<PrinterSettingsItem> Printers { get; set; } = [];

    private readonly WooCommerceService _wooService = new();
    private readonly PrintingService _printingService = new(new Adapters.WpfTemplateProvider());
    private readonly HashSet<string> _handledOrderKeys = new(StringComparer.OrdinalIgnoreCase);
    
    private CancellationTokenSource? _pollCancellationTokenSource;
    private bool _suppressPrinterSave;

    public MainWindow()
    {
        InitializeComponent();

        HostsListView.ItemsSource = Sites;
        OrdersDataGrid.ItemsSource = Orders;
        LstPrinters.ItemsSource = Printers;

        LoadInstalledPrinters();
        LoadSitesFromStorage();
        LoadOrderHistory();

        BtnAdd.Click += BtnAdd_Click;
        BtnEdit.Click += BtnEdit_Click;
        BtnDelete.Click += BtnDelete_Click;
        BtnStartListen.Click += BtnStartListen_Click;
        BtnStopListen.Click += BtnStopListen_Click;
        BtnPrintSelected.Click += BtnPrintSelected_Click;
    }

    private void MainWindow_Closing(object sender, CancelEventArgs e)
    {
        SavePrintersToStorage();
        PersistOrders();
    }
    
    private void LoadSitesFromStorage()
    {
        var savedSites = ProcessingData.LoadSites();
        Sites.Clear();
        foreach (var site in savedSites)
        {
            Sites.Add(site);
        }
    }

    private void SaveSitesToStorage()
    {
        ProcessingData.SaveSites(Sites);
    }

    private static string OrderKey(string orderId, string siteUrl) => $"{siteUrl}|{orderId}";

    private void LoadOrderHistory()
    {
        Orders.Clear();
        _handledOrderKeys.Clear();

        foreach (var record in ProcessingData.LoadProcessedOrders()
                     .OrderByDescending(x => x.LastAttemptUtc))
        {
            var key = OrderKey(record.OrderId, record.SiteUrl);
            _handledOrderKeys.Add(key);

            Orders.Add(new WooOrder
            {
                OrderId = record.OrderId,
                OrderDate = record.OrderDate,
                SiteUrl = record.SiteUrl,
                CustomerName = record.CustomerName,
                TotalAmount = record.TotalAmount,
                Status = record.Status,
                PrintError = record.PrintError,
                SlipData = record.SlipData
            });
        }
    }

    private void PersistOrders()
    {
        var records = Orders.Select(o => new ProcessedOrderRecord
        {
            OrderId = o.OrderId,
            SiteUrl = o.SiteUrl,
            CustomerName = o.CustomerName,
            TotalAmount = o.TotalAmount,
            OrderDate = o.OrderDate,
            Status = o.Status,
            PrintError = o.PrintError,
            LastAttemptUtc = DateTime.UtcNow,
            SlipData = o.SlipData
        }).ToList();

        ProcessingData.SaveProcessedOrders(records);
    }

    private void HostsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HostsListView.SelectedItem is WooCommerceSite selectedSite)
        {
            TxtSiteUrl.Text = selectedSite.Url;
            TxtConsumerKey.Text = selectedSite.CustomerKey;
            TxtConsumerSecret.Text = selectedSite.SecretKey;
        }
    }

    private void ClearFormInputs()
    {
        TxtSiteUrl.Text = string.Empty;
        TxtConsumerKey.Text = string.Empty;
        TxtConsumerSecret.Text = string.Empty;
        HostsListView.SelectedItem = null;
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtSiteUrl.Text))
        {
            MessageBox.Show("Введите адрес сайта!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var newSite = new WooCommerceSite
        {
            Url = TxtSiteUrl.Text.Trim(),
            CustomerKey = TxtConsumerKey.Text.Trim(),
            SecretKey = TxtConsumerSecret.Text.Trim()
        };

        Sites.Add(newSite);
        SaveSitesToStorage();
        ClearFormInputs();
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (HostsListView.SelectedItem is WooCommerceSite selectedSite)
        {
            if (string.IsNullOrWhiteSpace(TxtSiteUrl.Text))
            {
                MessageBox.Show("Адрес сайта не может быть пустым!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            selectedSite.Url = TxtSiteUrl.Text.Trim();
            selectedSite.CustomerKey = TxtConsumerKey.Text.Trim();
            selectedSite.SecretKey = TxtConsumerSecret.Text.Trim();

            HostsListView.Items.Refresh();
            SaveSitesToStorage();
            ClearFormInputs();
        }
        else
        {
            MessageBox.Show("Выберите сайт из списка для изменения!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (HostsListView.SelectedItem is WooCommerceSite selectedSite)
        {
            Sites.Remove(selectedSite);
            SaveSitesToStorage();
            ClearFormInputs();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement { DataContext: WooCommerceSite host } } })
        {
            Sites.Remove(host);
            SaveSitesToStorage();
            ClearFormInputs();
        }
    }

    private void LoadInstalledPrinters()
    {
        _suppressPrinterSave = true;
        Printers.Clear();

        var saved = ProcessingData.LoadPrinterSettings();
        var savedByName = saved.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        try
        {
            using var printServer = new LocalPrintServer();
            var printQueues = printServer.GetPrintQueues([
                EnumeratedPrintQueueTypes.Local,
                EnumeratedPrintQueueTypes.Connections
            ]);

            var defaultQueue = LocalPrintServer.GetDefaultPrintQueue();

            foreach (var queue in printQueues)
            {
                savedByName.TryGetValue(queue.FullName, out var existing);
                bool isDefault = defaultQueue != null && queue.FullName == defaultQueue.FullName;

                AddPrinter(new PrinterSettingsItem
                {
                    Name = queue.FullName,
                    IsSelected = existing?.IsSelected ?? isDefault,
                    PaperWidthMm = existing?.PaperWidthMm > 0 ? existing.PaperWidthMm : 80,
                    ConnectionType = existing?.ConnectionType ?? "Windows",
                    Host = existing?.Host ?? string.Empty,
                    Port = existing?.Port > 0 ? existing.Port : 9100
                });
            }
        }
        catch
        {
            Console.WriteLine("Принтеры не найдены");
        }

        foreach (var extra in saved.Where(p =>
                     p.ConnectionType == "Network" &&
                     Printers.All(x => !string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase))))
        {
            AddPrinter(extra);
        }

        _suppressPrinterSave = false;
        SavePrintersToStorage();
    }

    private void AddPrinter(PrinterSettingsItem item)
    {
        item.PropertyChanged += Printer_PropertyChanged;
        Printers.Add(item);
    }

    private void Printer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressPrinterSave) return;
        SavePrintersToStorage();
    }

    private void SavePrintersToStorage()
    {
        ProcessingData.SavePrinterSettings(Printers);
    }

    private void BtnStartListen_Click(object sender, RoutedEventArgs e)
    {
        if (Sites.Count == 0)
        {
            MessageBox.Show("Добавьте хотя бы один сайт для опроса!", "Внимание", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_pollCancellationTokenSource != null) return;

        _pollCancellationTokenSource = new CancellationTokenSource();
        var token = _pollCancellationTokenSource.Token;

        LblStatus.Text = "Активен (Опрос...)";
        LblStatus.Foreground = Brushes.Green;

        Task.Run(() => StartPollingLoopAsync(token), token);
    }

    private void BtnStopListen_Click(object sender, RoutedEventArgs e)
    {
        _pollCancellationTokenSource?.Cancel();
        _pollCancellationTokenSource = null;

        LblStatus.Text = "Остановлен";
        LblStatus.Foreground = Brushes.Red;
    }

    private async Task StartPollingLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var currentSites = Dispatcher.Invoke(() => Sites.ToList());

            var pollingTasks = currentSites.Select(site => _wooService.GetProcessingOrdersAsync(site));
            var results = await Task.WhenAll(pollingTasks);

            var newSlips = results.SelectMany(x => x).ToList();

            if (newSlips.Count > 0)
            {
                var autoPrint = false;
                var toPrint = new List<Slip>();

                Dispatcher.Invoke(() =>
                {
                    autoPrint = ChkAutoPrint.IsChecked == true;
                    foreach (var slip in newSlips)
                    {
                        var siteUrl = slip.Content?.Replace("Источник: ", "") ?? "WooCommerce";
                        var key = OrderKey(slip.OrderId, siteUrl);
                        if (_handledOrderKeys.Contains(key) || Orders.Any(o => o.OrderId == slip.OrderId && o.SiteUrl == siteUrl))
                            continue;

                        _handledOrderKeys.Add(key);

                        var uiOrder = new WooOrder
                        {
                            OrderId = slip.OrderId,
                            OrderDate = (slip.OrderDate ?? DateTime.Now).ToString("HH:mm:ss"),
                            SiteUrl = siteUrl,
                            CustomerName = slip.CustomerName ?? "Не указан",
                            TotalAmount = slip.TotalAmount.HasValue ? $"{slip.TotalAmount:N2} руб." : "0.00 руб.",
                            Status = "Новый",
                            SlipData = slip
                        };

                        Orders.Insert(0, uiOrder);
                        if (autoPrint)
                            toPrint.Add(slip);
                    }

                    PersistOrders();
                });

                foreach (var slip in toPrint)
                    await PrintSlipInternalAsync(slip, isAutoPrint: true);
            }

            try
            {
                var pollMinutes = Dispatcher.Invoke(() =>
                    int.TryParse(TxtPollMinutes.Text, out var value) ? Math.Clamp(value, 1, 5) : 1);
                await Task.Delay(TimeSpan.FromMinutes(pollMinutes), token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async void BtnPrintSelected_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (OrdersDataGrid.SelectedItem is not WooOrder selectedOrder)
            {
                MessageBox.Show("Выберите заказ из таблицы, чтобы распечатать чек!", "Внимание", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selectedOrder.SlipData == null)
            {
                MessageBox.Show("Для выбранного заказа отсутствуют данные чека!", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await PrintSlipInternalAsync(selectedOrder.SlipData, isAutoPrint: false);
        }
        catch (Exception exc)
        {
            MessageBox.Show(exc.Message, "Ошибка печати", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task PrintSlipInternalAsync(Slip slip, bool isAutoPrint = false)
    {
        var selectedPrinters = Dispatcher.Invoke(() => 
            Printers.Where(p => p.IsSelected).ToList());

        if (selectedPrinters.Count == 0)
        {
            UpdateOrderPrintStatus(slip, false, "Принтеры не выбраны");
            if (!isAutoPrint)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы один принтер!", "Ошибка печати", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return;
        }

        ApplyCompanyDefaults(slip);

        var printTasks = selectedPrinters.Select(async printer =>
        {
            try
            {
                var destination = ToDestination(printer);
                var success = await _printingService.PrintSlipAsync(slip, destination);
                return (PrinterName: printer.Name, Success: success, ErrorMessage: (string?)null);
            }
            catch (Exception ex)
            {
                return (PrinterName: printer.Name, Success: false, ErrorMessage: ex.Message);
            }
        });

        var results = await Task.WhenAll(printTasks);
        var errors = results.Where(r => !r.Success).ToList();
        var success = errors.Count == 0;
        var errorText = success ? null : string.Join("; ", errors.Select(e => $"{e.PrinterName}: {e.ErrorMessage}"));

        UpdateOrderPrintStatus(slip, success, errorText);

        if (!success)
        {
            if (!isAutoPrint)
            {
                MessageBox.Show($"Возникли ошибки при печати чека №{slip.OrderId}:\n{errorText}", 
                    "Ошибка печати", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                Console.WriteLine($"[AutoPrint Error] Ошибка печати чека #{slip.OrderId}:\n{errorText}");
            }
        }
    }

    private void UpdateOrderPrintStatus(Slip slip, bool success, string? error)
    {
        Dispatcher.Invoke(() =>
        {
            var siteUrl = slip.Content?.Replace("Источник: ", "") ?? string.Empty;
            var order = Orders.FirstOrDefault(o => o.OrderId == slip.OrderId &&
                                                   (string.IsNullOrEmpty(siteUrl) || o.SiteUrl == siteUrl))
                        ?? Orders.FirstOrDefault(o => o.OrderId == slip.OrderId);

            if (order != null)
            {
                order.Status = success ? "Напечатан" : "Ошибка печати";
                order.PrintError = error;
                order.SlipData = slip;
            }

            PersistOrders();
        });

        ProcessingData.AppendPrintLog(success
            ? $"OK order #{slip.OrderId}"
            : $"FAIL order #{slip.OrderId} {error}");
    }

    private static PrinterDestination ToDestination(PrinterSettingsItem printer) => new()
    {
        Name = printer.Name,
        WidthMm = printer.PaperWidthMm,
        ConnectionType = printer.ConnectionType,
        Host = printer.Host,
        Port = printer.Port
    };

    private static void ApplyCompanyDefaults(Slip slip)
    {
        var company = ProcessingData.LoadCompanySettings();
        if (string.IsNullOrWhiteSpace(slip.SellerTitle)) slip.SellerTitle = company.SellerTitle;
        if (string.IsNullOrWhiteSpace(slip.SellerName)) slip.SellerName = company.SellerName;
        if (string.IsNullOrWhiteSpace(slip.SellerInn)) slip.SellerInn = company.Inn;
        if (string.IsNullOrWhiteSpace(slip.Manager)) slip.Manager = company.Manager;
        if (string.IsNullOrWhiteSpace(slip.SalesChannel)) slip.SalesChannel = company.SalesChannel;
        if (string.IsNullOrWhiteSpace(slip.SignReleased)) slip.SignReleased = company.SignReleased;
        if (string.IsNullOrWhiteSpace(slip.SignReceived)) slip.SignReceived = company.SignReceived;
        if (string.IsNullOrWhiteSpace(slip.Footer)) slip.Footer = company.Footer;
        if (string.IsNullOrWhiteSpace(slip.CurrencyNote)) slip.CurrencyNote = company.CurrencyNote;
        if (string.IsNullOrWhiteSpace(slip.PersonsCount)) slip.PersonsCount = "1";
        if (string.IsNullOrWhiteSpace(slip.DeliveryType)) slip.DeliveryType = slip.ShippingMethod ?? "Доставка";
    }

    private void BtnConfigureReceipt_Click(object sender, RoutedEventArgs e)
    {
        var defaultPrinter = Printers.FirstOrDefault(p => p.IsSelected)?.Name
                             ?? Printers.FirstOrDefault()?.Name;
        var window = new ReceiptSettingsWindow(defaultPrinter, Printers.Select(p => p.Name)) { Owner = this };
        window.ShowDialog();
    }

    private void BtnAddNetworkPrinter_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddNetworkPrinterWindow { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        AddPrinter(new PrinterSettingsItem
        {
            Name = dialog.PrinterName,
            IsSelected = true,
            PaperWidthMm = dialog.PaperWidthMm,
            ConnectionType = "Network",
            Host = dialog.Host,
            Port = dialog.Port
        });
        SavePrintersToStorage();
    }

    private async void BtnTestPrint_Click(object sender, RoutedEventArgs e)
    {
        var selected = Printers.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Выберите принтер для теста.", "Тест печати",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var errors = new List<string>();
        foreach (var printer in selected)
        {
            try
            {
                await _printingService.PrintTestPageAsync(ToDestination(printer));
            }
            catch (Exception ex)
            {
                errors.Add($"{printer.Name}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            MessageBox.Show(string.Join("\n", errors), "Тест печати",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            MessageBox.Show(
                "Тестовая страница отправлена.\nПроверьте русский/английский текст, CP866, размер шрифта и отрезчик на XP-58C/XP-80C.",
                "Тест печати", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnCompanySettings_Click(object sender, RoutedEventArgs e)
    {
        var window = new CompanySettingsWindow(ProcessingData.LoadCompanySettings()) { Owner = this };
        window.ShowDialog();
    }

    private void Properties_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement { DataContext: WooCommerceSite host } } })
        {
            MessageBox.Show($"URL: {host.Url}\nCustomerSecret: {host.SecretKey}\nCustomerKey: {host.CustomerKey}",
                "Свойства", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
