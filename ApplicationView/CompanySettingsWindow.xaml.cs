using System.Windows;
using ApplicationView.Services;
using PrintingSlips.Models;

namespace ApplicationView;

public partial class CompanySettingsWindow : Window
{
    public CompanySettings Settings { get; }

    public CompanySettingsWindow(CompanySettings settings)
    {
        InitializeComponent();
        Settings = settings;
        DataContext = Settings;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        ProcessingData.SaveCompanySettings(Settings);
        DialogResult = true;
    }
}
