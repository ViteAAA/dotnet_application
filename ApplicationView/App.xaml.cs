using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;

namespace ApplicationView;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Перехват всех необработанных ошибок в приложении
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = (Exception)args.ExceptionObject;
            File.WriteAllText(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "critical_error.txt"), 
                $"[ERROR]: {ex.Message}\n\n[STACK]: {ex.StackTrace}\n\n[INNER]: {ex.InnerException?.Message}"
            );
        };

        base.OnStartup(e);
    }
}
