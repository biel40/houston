using System.Runtime.CompilerServices;

namespace Houston.App.Interop;

/// <summary>
/// Icon in the notification area while the app runs, with a native menu. Plain Shell_NotifyIcon instead
/// of WinForms' NotifyIcon, which would load all of WinForms into the host for one icon. Messages arrive
/// through the owner window's WndProc, which must forward them to <see cref="HandleMessage"/>.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const uint IconId = 1;
    private const int CallbackMessage = NativeMethods.WM_APP + 1;
    private const int CommandShow = 1;
    private const int CommandTopmost = 2;
    private const int CommandClose = 3;

    private readonly nint _hwnd;
    private readonly uint _taskbarCreated;
    private readonly nint _icon;
    private bool _disposed;

    public TrayIcon(nint hwnd, string iconPath)
    {
        _hwnd = hwnd;
        _taskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");

        // The app runs elevated and Explorer does not: UIPI drops Explorer's messages unless allowed.
        NativeMethods.ChangeWindowMessageFilterEx(hwnd, CallbackMessage, NativeMethods.MSGFLT_ALLOW, 0);
        NativeMethods.ChangeWindowMessageFilterEx(hwnd, _taskbarCreated, NativeMethods.MSGFLT_ALLOW, 0);

        _icon = NativeMethods.LoadImage(0, iconPath, NativeMethods.IMAGE_ICON,
            NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON),
            NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSMICON),
            NativeMethods.LR_LOADFROMFILE);
        Add();
    }

    /// <summary>Shown as the check mark of «Siempre visible».</summary>
    public bool Topmost { get; set; }

    public event Action? ShowRequested;
    public event Action? TopmostToggled;
    public event Action? CloseRequested;

    /// <summary>Returns true when the message was the icon's own and needs no further processing.</summary>
    public bool HandleMessage(int msg, nint lParam)
    {
        if (msg == CallbackMessage)
        {
            switch ((int)lParam)
            {
                case NativeMethods.WM_LBUTTONUP:
                    ShowRequested?.Invoke();
                    break;
                case NativeMethods.WM_RBUTTONUP:
                    ShowMenu();
                    break;
            }

            return true;
        }

        // Explorer restarted and forgot every icon.
        if (_taskbarCreated != 0 && (uint)msg == _taskbarCreated)
        {
            Add();
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var data = CreateData();
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, data);
        if (_icon != 0)
        {
            NativeMethods.DestroyIcon(_icon);
        }
    }

    private void Add() => NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, CreateData());

    private unsafe NativeMethods.NotifyIconData CreateData()
    {
        var data = new NativeMethods.NotifyIconData
        {
            Size = (uint)Unsafe.SizeOf<NativeMethods.NotifyIconData>(),
            Window = _hwnd,
            Id = IconId,
            Flags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            CallbackMessage = CallbackMessage,
            Icon = _icon,
        };

        const string tip = "Houston";
        for (var i = 0; i < tip.Length; i++)
        {
            data.Tip[i] = tip[i];
        }

        return data;
    }

    private void ShowMenu()
    {
        var menu = NativeMethods.CreatePopupMenu();
        if (menu == 0)
        {
            return;
        }

        try
        {
            NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, CommandShow, "Mostrar Houston");
            NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING | (Topmost ? NativeMethods.MF_CHECKED : 0),
                CommandTopmost, "Siempre visible");
            NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, 0, null);
            NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, CommandClose, "Cerrar Houston");

            NativeMethods.GetCursorPos(out var cursor);
            // Documented TrackPopupMenu quirk: without the foreground switch the menu does not close when
            // clicking elsewhere, and without the WM_NULL afterwards it can fail to show the next time.
            NativeMethods.SetForegroundWindow(_hwnd);
            var command = NativeMethods.TrackPopupMenuEx(menu,
                NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON, cursor.X, cursor.Y, _hwnd, 0);
            NativeMethods.PostMessage(_hwnd, NativeMethods.WM_NULL, 0, 0);

            switch (command)
            {
                case CommandShow:
                    ShowRequested?.Invoke();
                    break;
                case CommandTopmost:
                    TopmostToggled?.Invoke();
                    break;
                case CommandClose:
                    CloseRequested?.Invoke();
                    break;
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }
}
