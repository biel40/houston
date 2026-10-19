using System.Runtime.InteropServices;

namespace Houston.App.Interop;

internal static partial class NativeMethods
{
    public const int WM_NCLBUTTONDOWN = 0x00A1;
    public const int HTCAPTION = 2;

    public const int WM_SYSCOMMAND = 0x0112;
    public const int SC_SIZE = 0xF000;

    public const int WM_SIZING = 0x0214;

    /// <summary>Which edge or corner is being dragged; the same values as the low bits of <c>SC_SIZE</c>.</summary>
    public const int WMSZ_LEFT = 1;
    public const int WMSZ_RIGHT = 2;
    public const int WMSZ_TOP = 3;
    public const int WMSZ_TOPLEFT = 4;
    public const int WMSZ_TOPRIGHT = 5;
    public const int WMSZ_BOTTOM = 6;
    public const int WMSZ_BOTTOMLEFT = 7;
    public const int WMSZ_BOTTOMRIGHT = 8;

    public const int VK_LBUTTON = 0x01;
    public const int VK_RBUTTON = 0x02;
    public const int VK_MBUTTON = 0x04;

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint MONITOR_DEFAULTTONULL = 0;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromRect(in Rect rect, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out Point point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(nint hwnd, out Rect rect);

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int virtualKey);

    public const uint PDH_FMT_DOUBLE = 0x00000200;
    public const uint PDH_FMT_NOCAP100 = 0x00008000;

    [StructLayout(LayoutKind.Sequential)]
    public struct PdhFmtCounterValue
    {
        public uint CStatus;
        public double DoubleValue;
    }

    [LibraryImport("pdh.dll", EntryPoint = "PdhOpenQueryW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint PdhOpenQuery(string? dataSource, nint userData, out nint query);

    // English counter paths work on every Windows display language (the localized ones do not).
    [LibraryImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint PdhAddEnglishCounter(nint query, string path, nint userData, out nint counter);

    [LibraryImport("pdh.dll")]
    public static partial uint PdhCollectQueryData(nint query);

    [LibraryImport("pdh.dll")]
    public static partial uint PdhGetFormattedCounterValue(nint counter, uint format, out uint type, out PdhFmtCounterValue value);

    [LibraryImport("pdh.dll")]
    public static partial uint PdhCloseQuery(nint query);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ReleaseCapture();

    // Maximizing a borderless window: without an explicit answer it would cover the taskbar.
    public const int WM_GETMINMAXINFO = 0x0024;
    public const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    // Notification area (tray) icon and its native menu.
    public const int WM_NULL = 0x0000;
    public const int WM_DESTROY = 0x0002;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_APP = 0x8000;

    public const uint NIM_ADD = 0;
    public const uint NIM_DELETE = 2;
    public const uint NIF_MESSAGE = 0x01;
    public const uint NIF_ICON = 0x02;
    public const uint NIF_TIP = 0x04;

    public const uint MSGFLT_ALLOW = 1;

    public const uint IMAGE_ICON = 1;
    public const uint LR_LOADFROMFILE = 0x10;
    public const int SM_CXSMICON = 49;
    public const int SM_CYSMICON = 50;

    public const uint MF_STRING = 0x0000;
    public const uint MF_CHECKED = 0x0008;
    public const uint MF_SEPARATOR = 0x0800;
    public const uint TPM_RIGHTBUTTON = 0x0002;
    public const uint TPM_RETURNCMD = 0x0100;

    // ushort rather than char buffers: char is not blittable for LibraryImport.
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NotifyIconData
    {
        public uint Size;
        public nint Window;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public nint Icon;
        public fixed ushort Tip[128];
        public uint State;
        public uint StateMask;
        public fixed ushort Info[256];
        public uint TimeoutOrVersion;
        public fixed ushort InfoTitle[64];
        public uint InfoFlags;
        public Guid Item;
        public nint BalloonIcon;
    }

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Shell_NotifyIcon(uint message, in NotifyIconData data);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterWindowMessage(string name);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ChangeWindowMessageFilterEx(nint hwnd, uint message, uint action, nint changeFilter);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint LoadImage(nint instance, string name, uint type, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint icon);

    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    public static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AppendMenu(nint menu, uint flags, nuint id, string? text);

    [LibraryImport("user32.dll")]
    public static partial int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint parameters);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyMenu(nint menu);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostMessage(nint hwnd, int msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    public static partial nint SendMessage(nint hwnd, int msg, nint wParam, nint lParam);
}
