using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Houston.App.Interop;
using Houston.App.Sensors;
using Microsoft.Web.WebView2.Core;

namespace Houston.App;

public partial class MainWindow : Window
{
    private const string VirtualHost = "houston.local";
    private static readonly Uri ProductionUiUri = new($"https://{VirtualHost}/index.html");

    // Measured on this widget: software rendering plus folding the GPU and network services into the
    // browser process cut WebView2 from ~140 MB to ~60 MB across 5 processes instead of 7, with no CPU
    // penalty (the page is tiny and only animates for ~0.6 s per sample).
    private const string BrowserArguments =
        "--disable-gpu --in-process-gpu --enable-features=NetworkServiceInProcess2";

    private const double MinWidthDip = 200;
    private const double MaxWidthDip = 720;

    private readonly DispatcherTimer _outsideClickWatch = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly AppSettings _settings;
    private nint _hwnd;

    // Re-sent whenever the page announces it is ready (it may reload during development).
    private IReadOnlyList<string> _cardOrder;
    private IReadOnlyList<string> _hiddenCards;

    // Height / width of the UI at scale 1. Resizing keeps it, so the content scales without letterboxing.
    private double _aspect;
    private bool _sizeApplied;
    private CoreWebView2Environment? _environment;
    private UiBridge? _bridge;
    private TrayIcon? _tray;

    public MainWindow(AppSettings settings)
    {
        _settings = settings;
        _cardOrder = settings.CardOrder ?? [];
        _hiddenCards = settings.HiddenCards ?? [];
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        StateChanged += (_, _) => PostWindowState();
        _outsideClickWatch.Tick += OnOutsideClickWatchTick;
    }

    /// <summary>
    /// Raised after the user finishes dragging or resizing, with the top-left corner in physical pixels
    /// and the width in device-independent pixels.
    /// </summary>
    public event Action<int, int, double>? Placed;

    /// <summary>Raised when the user reorders, hides or shows a card: the card ids in order, and those hidden.</summary>
    public event Action<IReadOnlyList<string>, IReadOnlyList<string>>? LayoutChanged;

    /// <summary>Called from the sampling thread; serializes there and posts on the UI thread.</summary>
    public void ShowSnapshot(SensorSnapshot snapshot)
    {
        var json = UiBridge.SerializeSensors(snapshot);
        Dispatcher.BeginInvoke(() => _bridge?.PostJson(json));
    }

    /// <summary>
    /// Disposes the WebView2 and completes once its browser processes are gone (or the timeout elapses),
    /// at which point the user data folder is no longer locked.
    /// </summary>
    public async Task CloseWebViewAsync(TimeSpan timeout)
    {
        _outsideClickWatch.Stop();
        _bridge?.Dispose();
        _bridge = null;

        if (_environment is null)
        {
            WebView.Dispose();
            return;
        }

        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _environment.BrowserProcessExited += (_, _) => exited.TrySetResult();
        WebView.Dispose();
        await Task.WhenAny(exited.Task, Task.Delay(timeout));
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd).AddHook(WndProc);
        RestorePosition();

        _tray = new TrayIcon(_hwnd, Path.Combine(AppContext.BaseDirectory, "Assets", "houston.ico")) { Topmost = Topmost };
        _tray.ShowRequested += ShowFromTray;
        _tray.TopmostToggled += () => SetTopmost(!Topmost);
        // Deferred: the menu runs inside this window's WndProc, which must return before the window goes away.
        _tray.CloseRequested += () => Dispatcher.BeginInvoke(Close);
    }

    private void ShowFromTray()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void SetTopmost(bool topmost)
    {
        Topmost = topmost;
        if (_tray is not null)
        {
            _tray.Topmost = topmost;
        }

        PostWindowState();
    }

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void PostWindowState() => _bridge?.PostWindowState(Topmost, WindowState == WindowState.Maximized);

    private void RestorePosition()
    {
        if (_settings.WindowX is not { } x
            || _settings.WindowY is not { } y
            || !NativeMethods.GetWindowRect(_hwnd, out var current))
        {
            return;
        }

        var target = new NativeMethods.Rect
        {
            Left = x,
            Top = y,
            Right = x + (current.Right - current.Left),
            Bottom = y + (current.Bottom - current.Top),
        };

        // The monitor it was on may be gone: keep the default position rather than open off-screen.
        if (NativeMethods.MonitorFromRect(target, NativeMethods.MONITOR_DEFAULTTONULL) == 0)
        {
            return;
        }

        NativeMethods.SetWindowPos(_hwnd, 0, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (_tray?.HandleMessage(msg, lParam) == true)
        {
            handled = true;
            return 0;
        }

        if (msg == NativeMethods.WM_DESTROY)
        {
            _tray?.Dispose();
            _tray = null;
        }

        if (msg == NativeMethods.WM_GETMINMAXINFO)
        {
            FitMaximizedToWorkArea(lParam);
        }

        if (msg == NativeMethods.WM_SIZING && _aspect > 0)
        {
            var rect = Marshal.PtrToStructure<NativeMethods.Rect>(lParam);
            KeepAspect((int)wParam, ref rect);
            Marshal.StructureToPtr(rect, lParam, fDeleteOld: false);
            handled = true;
            return 1;
        }

        return 0;
    }

    // A borderless window maximizes over the whole monitor, taskbar included; limit it to the work area.
    // Not marked handled, so WPF still applies its own track sizes afterwards.
    private void FitMaximizedToWorkArea(nint lParam)
    {
        var monitor = NativeMethods.MonitorFromWindow(_hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MonitorInfo { Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (monitor == 0 || !NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var minMax = Marshal.PtrToStructure<NativeMethods.MinMaxInfo>(lParam);
        minMax.MaxPosition = new NativeMethods.Point
        {
            X = info.Work.Left - info.Monitor.Left,
            Y = info.Work.Top - info.Monitor.Top,
        };
        minMax.MaxSize = new NativeMethods.Point
        {
            X = info.Work.Right - info.Work.Left,
            Y = info.Work.Bottom - info.Work.Top,
        };
        Marshal.StructureToPtr(minMax, lParam, fDeleteOld: false);
    }

    // Applied while the user drags an edge: clamps the width and derives the height from the UI's aspect
    // ratio, anchoring the side opposite to the one being dragged.
    private void KeepAspect(int edge, ref NativeMethods.Rect rect)
    {
        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double width = rect.Right - rect.Left;
        double height = rect.Bottom - rect.Top;

        var onlyVertical = edge is NativeMethods.WMSZ_TOP or NativeMethods.WMSZ_BOTTOM;
        var corner = edge is NativeMethods.WMSZ_TOPLEFT or NativeMethods.WMSZ_TOPRIGHT
            or NativeMethods.WMSZ_BOTTOMLEFT or NativeMethods.WMSZ_BOTTOMRIGHT;
        if (onlyVertical)
        {
            width = height / _aspect;
        }
        else if (corner)
        {
            width = Math.Max(width, height / _aspect);
        }

        width = Math.Clamp(width, MinWidthDip * dpi, MaxWidthDip * dpi);
        height = width * _aspect;

        var w = (int)Math.Round(width);
        var h = (int)Math.Round(height);
        var fromLeft = edge is NativeMethods.WMSZ_LEFT or NativeMethods.WMSZ_TOPLEFT or NativeMethods.WMSZ_BOTTOMLEFT;
        var fromTop = edge is NativeMethods.WMSZ_TOP or NativeMethods.WMSZ_TOPLEFT or NativeMethods.WMSZ_TOPRIGHT;

        if (fromLeft)
        {
            rect.Left = rect.Right - w;
        }
        else
        {
            rect.Right = rect.Left + w;
        }

        if (fromTop)
        {
            rect.Top = rect.Bottom - h;
        }
        else
        {
            rect.Bottom = rect.Top + h;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _environment = await CoreWebView2Environment.CreateAsync(
            null, AppPaths.WebViewDataFolder, new CoreWebView2EnvironmentOptions(BrowserArguments));
        await WebView.EnsureCoreWebView2Async(_environment);

        var core = WebView.CoreWebView2;
        ConfigureSettings(core.Settings);
        core.SetVirtualHostNameToFolderMapping(
            VirtualHost, Path.Combine(AppContext.BaseDirectory, "wwwroot"), CoreWebView2HostResourceAccessKind.Deny);

        var uiUri = ResolveUiUri();
        var uiOrigin = uiUri.GetLeftPart(UriPartial.Authority);
        core.NavigationStarting += (_, args) =>
        {
            if (!args.Uri.StartsWith(uiOrigin + "/", StringComparison.OrdinalIgnoreCase))
            {
                args.Cancel = true;
            }
        };
        core.NewWindowRequested += (_, args) => args.Handled = true;

        _bridge = new UiBridge(core, uiUri);
        _bridge.Ready += () =>
        {
            PostWindowState();
            _bridge?.PostLayout(_cardOrder, _hiddenCards);
        };
        _bridge.LayoutChanged += (order, hidden) =>
        {
            _cardOrder = order;
            _hiddenCards = hidden;
            LayoutChanged?.Invoke(order, hidden);
        };
        _bridge.DragRequested += BeginDrag;
        _bridge.CloseRequested += Close;
        _bridge.MinimizeRequested += () => WindowState = WindowState.Minimized;
        _bridge.MaximizeToggleRequested += ToggleMaximize;
        _bridge.ResizeRequested += BeginResize;
        _bridge.TopmostRequested += SetTopmost;
        _bridge.MenuChanged += open => _outsideClickWatch.IsEnabled = open;
        _bridge.SizeRequested += OnNaturalSizeKnown;

        core.Navigate(uiUri.ToString());
    }

    // The UI owns the layout and reports its natural size; the window only keeps that aspect ratio.
    // The first report picks the saved width (or the natural one); later ones keep the current width.
    private void OnNaturalSizeKnown(double width, double height)
    {
        if (width < 1 || height < 1)
        {
            return;
        }

        _aspect = height / width;
        // Setting the size of a maximized window would change its restore bounds instead; the next
        // resize by the user re-applies the ratio.
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        var target = _sizeApplied ? Width : _settings.WindowWidth ?? width;
        _sizeApplied = true;
        Width = Math.Clamp(target, MinWidthDip, MaxWidthDip);
        Height = Width * _aspect;
    }

    private static void ConfigureSettings(CoreWebView2Settings settings)
    {
        settings.AreDefaultContextMenusEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsPinchZoomEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
#if DEBUG
        settings.AreDevToolsEnabled = true;
        settings.AreBrowserAcceleratorKeysEnabled = true;
#else
        settings.AreDevToolsEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
#endif
    }

    private static Uri ResolveUiUri()
    {
#if DEBUG
        // Hot reload: run `npm run dev` in ui/ and set HOUSTON_UI_URL=http://localhost:5173
        if (Uri.TryCreate(Environment.GetEnvironmentVariable("HOUSTON_UI_URL"), UriKind.Absolute, out var devUri))
        {
            return devUri;
        }
#endif
        return ProductionUiUri;
    }

    // The widget never activates, so neither it nor the page hears about clicks elsewhere on the desktop
    // (and WebView2 fires spurious mouseleave events in a layered window). While the context menu is
    // open, poll for a button press outside the window to dismiss it like a native menu.
    private void OnOutsideClickWatchTick(object? sender, EventArgs e)
    {
        var anyButtonDown = IsKeyDown(NativeMethods.VK_LBUTTON)
                            || IsKeyDown(NativeMethods.VK_RBUTTON)
                            || IsKeyDown(NativeMethods.VK_MBUTTON);
        if (!anyButtonDown
            || !NativeMethods.GetCursorPos(out var cursor)
            || !NativeMethods.GetWindowRect(_hwnd, out var bounds))
        {
            return;
        }

        var inside = cursor.X >= bounds.Left && cursor.X < bounds.Right
                     && cursor.Y >= bounds.Top && cursor.Y < bounds.Bottom;
        if (!inside)
        {
            _outsideClickWatch.Stop();
            _bridge?.PostDismissMenu();
        }
    }

    // High bit: down now. Low bit: pressed since the previous call, so short clicks between ticks count too.
    private static bool IsKeyDown(int virtualKey) => (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8001) != 0;

    // Hands the drag to the OS move loop; WPF never sees the press, the WebView2 child does.
    // SendMessage returns once the user releases the button, so the final position is known here.
    private void BeginDrag()
    {
        // A maximized window stays put, like one with a native title bar.
        if (WindowState == WindowState.Maximized)
        {
            return;
        }

        NativeMethods.GetWindowRect(_hwnd, out var before);
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(_hwnd, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, 0);

        if (NativeMethods.GetWindowRect(_hwnd, out var after)
            && (after.Left != before.Left || after.Top != before.Top))
        {
            Placed?.Invoke(after.Left, after.Top, Width);
        }
    }

    // Same hand-off as BeginDrag, for the system's sizing loop (SC_SIZE + the edge being grabbed).
    private void BeginResize(int edge)
    {
        if (WindowState == WindowState.Maximized)
        {
            return;
        }

        NativeMethods.GetWindowRect(_hwnd, out var before);
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(_hwnd, NativeMethods.WM_SYSCOMMAND, NativeMethods.SC_SIZE + edge, 0);

        if (NativeMethods.GetWindowRect(_hwnd, out var after)
            && (after.Right - after.Left != before.Right - before.Left
                || after.Left != before.Left
                || after.Top != before.Top))
        {
            Placed?.Invoke(after.Left, after.Top, Width);
        }
    }
}
