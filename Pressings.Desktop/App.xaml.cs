using System.Configuration;
using System.Data;
using System.Threading;
using System.Windows;

namespace Pressings.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private Mutex? _instanceMutex;
    private bool _ownsInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(initiallyOwned: true, name: @"Local\FreshCare.Desktop", createdNew: out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("Fresh & Care est déjà ouvert. Utilise la fenêtre déjà lancée.",
                "Fresh & Care", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _ownsInstanceMutex = true;
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsInstanceMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
