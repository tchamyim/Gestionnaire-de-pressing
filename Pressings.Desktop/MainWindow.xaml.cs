using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Pressings.Desktop;

public partial class MainWindow : Window
{
    private const int FirstLocalPort = 5086;
    private const int LastLocalPort = 5095;
    private string _localUrl = "";
    private Process? _backend;
    private string? _projectRoot;
    private string _databasePath = "";
    private string _appUrl = "";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += (_, _) => StopBackend();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var onlineUrl = ReadOnlineUrl();
            if (onlineUrl is null)
            {
                _projectRoot = FindProjectRoot();
                _databasePath = ResolveDatabasePath(_projectRoot);
                _localUrl = ReserveLoopbackUrl();
                _appUrl = _localUrl;
                Log($"Démarrage local. Racine={_projectRoot ?? "publication"}; base={_databasePath}; URL={_localUrl}");
                StartBackend(_projectRoot, _databasePath);
                await WaitForBackend();
            }
            else
            {
                _appUrl = onlineUrl;
                Log($"Démarrage en ligne. Hôte={new Uri(_appUrl).Host}");
            }

            var webViewData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreshCare", "WebView2");
            Directory.CreateDirectory(webViewData);
            var webViewEnvironment = await CoreWebView2Environment.CreateAsync(userDataFolder: webViewData);
            await Browser.EnsureCoreWebView2Async(webViewEnvironment);
            if (onlineUrl is null) await ClearLocalAntiforgeryCookies();
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Browser.CoreWebView2.DownloadStarting += OnDownloadStarting;
            Browser.CoreWebView2.NavigationStarting += OnNavigationStarting;
            Browser.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            Browser.CoreWebView2.WebResourceResponseReceived += OnWebResourceResponseReceived;
            Browser.CoreWebView2.ProcessFailed += (_, args) => Log($"WebView2 arrêté. Type={args.ProcessFailedKind}; raison={args.Reason}");
            Browser.Source = new Uri(_appUrl);
        }
        catch (Exception ex)
        {
            Log("Erreur au démarrage : " + ex);
            MessageBox.Show(this,
                "Fresh & Care n’a pas pu démarrer. Vérifie que Microsoft Access et le pilote Access 64 bits sont installés, puis consulte le journal :\n" + LogPath + "\n\nDétail : " + ex.Message,
                "Démarrage impossible", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private static string ReserveLoopbackUrl()
    {
        for (var port = FirstLocalPort; port <= LastLocalPort; port++)
        {
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                return $"http://127.0.0.1:{port}";
            }
            catch (SocketException) { }
        }

        throw new InvalidOperationException(
            $"Les ports locaux {FirstLocalPort} à {LastLocalPort} sont tous utilisés. Ferme une autre instance de Fresh & Care puis réessaie.");
    }

    private static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreshCare", "logs", "desktop.log");

    private static void Log(string message)
    {
        try
        {
            var path = LogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}{Environment.NewLine}");
        }
        catch { }
    }

    private static string? ReadOnlineUrl()
    {
        var configured = Environment.GetEnvironmentVariable("FreshCare_OnlineUrl");
        if (string.IsNullOrWhiteSpace(configured))
        {
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(settingsPath))
            {
                using var settings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (settings.RootElement.TryGetProperty("OnlineUrl", out var value)) configured = value.GetString();
            }
        }
        if (string.IsNullOrWhiteSpace(configured)) return null;
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && IsPrivateNetworkHost(uri.Host))))
            throw new InvalidOperationException("OnlineUrl doit être une adresse HTTPS ou une adresse HTTP privée du réseau local.");
        return uri.ToString().TrimEnd('/');
    }

    private static bool IsPrivateNetworkHost(string host)
    {
        if (!IPAddress.TryParse(host, out var address))
        {
            var name = host.TrimEnd('.');
            return name.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith(".lan", StringComparison.OrdinalIgnoreCase) ||
                   (!name.Contains('.') && !string.IsNullOrWhiteSpace(name));
        }

        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal) return true;
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   (bytes[0] == 169 && bytes[1] == 254);
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6 && bytes.Length > 0 && (bytes[0] & 0xFE) == 0xFC;
    }

    private static string? FindProjectRoot()
    {
        var basePath = Path.GetFullPath(AppContext.BaseDirectory);
        if (File.Exists(Path.Combine(basePath, "backend", "Pressings.exe"))) return null;

        var directory = new DirectoryInfo(basePath);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pressings.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }
        return null;
    }

    private static string ResolveDatabasePath(string? projectRoot)
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreshCare");
        Directory.CreateDirectory(appData);
        if (!string.IsNullOrWhiteSpace(projectRoot))
            return Path.Combine(projectRoot, "Gestion pressing.accdb");

        var database = Path.Combine(appData, "Gestion pressing.accdb");
        if (!File.Exists(database))
        {
            var seed = Path.Combine(AppContext.BaseDirectory, "backend", "Gestion pressing.accdb");
            if (File.Exists(seed)) File.Copy(seed, database);
        }
        return database;
    }

    private async Task ClearLocalAntiforgeryCookies()
    {
        var cookies = await Browser.CoreWebView2.CookieManager.GetCookiesAsync(_localUrl);
        var antiforgeryCookies = cookies.Where(cookie =>
            cookie.Name.StartsWith("FreshCare.Antiforgery", StringComparison.OrdinalIgnoreCase) ||
            cookie.Name.StartsWith(".AspNetCore.Antiforgery.", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var cookie in antiforgeryCookies)
            Browser.CoreWebView2.CookieManager.DeleteCookie(cookie);
        Log($"Jetons anti-CSRF locaux renouvelés au démarrage. Cookies supprimés={antiforgeryCookies.Count}");
    }

    private void StartBackend(string? projectRoot, string databasePath)
    {
        var backendExe = Path.Combine(AppContext.BaseDirectory, "backend", "Pressings.exe");
        ProcessStartInfo start;
        if (File.Exists(backendExe))
        {
            start = new ProcessStartInfo(backendExe, "--urls " + _localUrl)
            {
                WorkingDirectory = Path.GetDirectoryName(backendExe)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }
        else if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            var project = Path.Combine(projectRoot, "Pressings.csproj");
            start = new ProcessStartInfo("dotnet", $"run --no-build --project \"{project}\" --no-launch-profile -- --urls {_localUrl}")
            {
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }
        else
        {
            throw new FileNotFoundException("Le moteur de l’application Fresh & Care est introuvable.");
        }

        start.Environment["DatabasePath"] = databasePath;
        string? antiforgeryCookieName = null;
        if (Uri.TryCreate(_localUrl, UriKind.Absolute, out var localUri))
        {
            antiforgeryCookieName = $"FreshCare.Antiforgery.{localUri.Port}";
            start.Environment["Antiforgery__CookieName"] = antiforgeryCookieName;
        }
        _backend = Process.Start(start) ?? throw new InvalidOperationException("Le serveur local n’a pas démarré.");
        Log($"Moteur lancé. PID={_backend.Id}; fichier={_backend.StartInfo.FileName}; cookie anti-CSRF={antiforgeryCookieName ?? "configuration web"}");
        _backend.OutputDataReceived += (_, args) => { if (args.Data is not null) Log("moteur: " + args.Data); };
        _backend.ErrorDataReceived += (_, args) => { if (args.Data is not null) Log("moteur erreur: " + args.Data); };
        _backend.BeginOutputReadLine();
        _backend.BeginErrorReadLine();
    }

    private async Task WaitForBackend()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var healthUrl = _localUrl + "/health";
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            if (_backend is { HasExited: true })
                throw new InvalidOperationException("Le moteur du pressing s’est arrêté au démarrage. Consulte le journal Fresh & Care.");
            try
            {
                using var response = await client.GetAsync(healthUrl);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log($"Moteur prêt. URL de contrôle={healthUrl}");
                    return;
                }
                if ((int)response.StatusCode >= 400)
                    throw new InvalidOperationException($"Le moteur répond HTTP {(int)response.StatusCode} sur /health.");
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(500);
        }
        throw new TimeoutException("Le moteur du pressing n’a pas répondu après 45 secondes.");
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        var url = Browser.Source?.GetLeftPart(UriPartial.Path) ?? "URL inconnue";
        Log($"Navigation terminée. Succès={e.IsSuccess}; HTTP={e.HttpStatusCode}; erreur={e.WebErrorStatus}; URL={url}");
        if (e.HttpStatusCode >= 400)
            Title = $"Fresh & Care — erreur HTTP {e.HttpStatusCode} (journal : {LogPath})";
    }

    private void OnWebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var requestUri)) return;
        if (requestUri.Host != "127.0.0.1" && requestUri.Host != "localhost" && requestUri.Host != new Uri(_appUrl).Host) return;
        if (e.Response.StatusCode < 400) return;
        Log($"Réponse HTTP {e.Response.StatusCode}. Méthode={e.Request.Method}; URL={requestUri.GetLeftPart(UriPartial.Path)}");
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        Log("Navigation demandée : " + e.Uri);
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) &&
            uri.Host != "127.0.0.1" && uri.Host != "localhost" &&
            uri.Host != new Uri(_appUrl).Host)
        {
            e.Cancel = true;
            try { Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Lien externe", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }

    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        var extension = Path.GetExtension(e.ResultFilePath).ToLowerInvariant();
        if (extension is not ".pdf" and not ".xlsx" and not ".docx") return;

        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FreshCare");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Path.GetFileName(e.ResultFilePath));
        e.ResultFilePath = path;
        e.Handled = true;
        e.DownloadOperation.StateChanged += (_, _) =>
        {
            if (e.DownloadOperation.State != CoreWebView2DownloadState.Completed) return;
            Dispatcher.Invoke(() =>
            {
                try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show(this, "Le fichier est enregistré dans Documents\\FreshCare, mais son application associée n’a pas pu s’ouvrir.\n\n" + ex.Message, "Document généré", MessageBoxButton.OK, MessageBoxImage.Information); }
            });
        };
    }

    private void StopBackend()
    {
        if (_backend is null) return;
        try
        {
            if (!_backend.HasExited)
            {
                Log($"Arrêt du moteur local. PID={_backend.Id}");
                _backend.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        _backend.Dispose();
        _backend = null;
    }
}
