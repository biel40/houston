using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Houston.App.Sensors;

namespace Houston.App;

public partial class App : Application
{
    private const string InstanceMutexName = @"Local\Houston.SingleInstance";
    private static readonly TimeSpan BrowserExitTimeout = TimeSpan.FromSeconds(10);

    private readonly SensorService _sensors = new();
    private Mutex? _instanceMutex;
    private AppSettings _settings = new();
    private Task _pendingSave = Task.CompletedTask;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A second launch exits silently; the existing widget is already on screen.
        if (!TryAcquireSingleInstance())
        {
            Shutdown();
            return;
        }

        // WPF only paints a transparent background (WebView2 renders the UI), so skip Direct3D and keep
        // the graphics driver out of this process.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        _settings = SettingsStore.Load();
        var window = new MainWindow(_settings);
        window.Placed += OnWindowPlaced;
        window.Closed += OnMainWindowClosed;
        window.Show();

        _sensors.SnapshotReady += window.ShowSnapshot;
        _sensors.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private bool TryAcquireSingleInstance()
    {
        try
        {
            _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var createdNew);
            if (createdNew)
            {
                return true;
            }

            _instanceMutex.Dispose();
            _instanceMutex = null;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // Held by an elevated instance that this non-elevated process may not open.
            return false;
        }
    }

    private void OnWindowPlaced(int x, int y, double width)
    {
        _settings = _settings with { WindowX = x, WindowY = y, WindowWidth = width };
        _pendingSave = SettingsStore.SaveAsync(_settings);
    }

    // Shutdown is explicit so the process outlives the window until sensors and WebView2 are released.
    private async void OnMainWindowClosed(object? sender, EventArgs e)
    {
        await Task.WhenAll(
            _sensors.DisposeAsync().AsTask(),
            ((MainWindow)sender!).CloseWebViewAsync(BrowserExitTimeout),
            _pendingSave);
        TryDeleteWebViewDataFolder();
        Shutdown();
    }

    private static void TryDeleteWebViewDataFolder()
    {
        try
        {
            if (Directory.Exists(AppPaths.WebViewDataFolder))
            {
                Directory.Delete(AppPaths.WebViewDataFolder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still locked (browser exit timed out): the next run reuses the folder and retries.
        }
    }
}
