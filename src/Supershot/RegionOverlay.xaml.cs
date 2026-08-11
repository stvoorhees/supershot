using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
// disambiguate from the WinForms types pulled in by UseWindowsForms' implicit usings
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Supershot;

/// <summary>
/// Full-virtual-screen transparent overlay. In region mode the user drags a rectangle; in window
/// mode the window under the cursor is highlighted and a click captures it. Results are returned in
/// physical screen pixels (via PointToScreen / DWM frame bounds) so the GDI capture lines up.
/// </summary>
public partial class RegionOverlay : Window
{
    private Point _startDip;
    private bool _dragging;
    private bool _windowMode;
    private Int32Rect? _result;
    private Int32Rect? _hoverWindow; // physical bounds of the currently highlighted window

    private RegionOverlay(bool windowMode)
    {
        InitializeComponent();
        _windowMode = windowMode;

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Loaded += (_, _) =>
        {
            Dim.Width = Width; Dim.Height = Height;
            Canvas.SetLeft(Hint, (Width - Hint.ActualWidth) / 2);
            Canvas.SetTop(Hint, 40);
            if (_windowMode) Hint.Visibility = Visibility.Collapsed;
            Activate();
        };

        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { _result = null; DialogResult = false; } };
    }

    public static Int32Rect? SelectRegion()
    {
        var o = new RegionOverlay(false); o.ShowDialog(); return o._result;
    }

    public static Int32Rect? SelectWindow()
    {
        var o = new RegionOverlay(true); o.ShowDialog(); return o._result;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (_windowMode)
        {
            _result = _hoverWindow;                    // click captures the highlighted window
            if (_result is not null) DialogResult = true;
            return;
        }
        _dragging = true;
        _startDip = e.GetPosition(this);
        Hint.Visibility = Visibility.Collapsed;
        Selection.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_windowMode) { HighlightWindowUnderCursor(); return; }
        if (!_dragging) return;
        var p = e.GetPosition(this);
        double x = Math.Min(p.X, _startDip.X), y = Math.Min(p.Y, _startDip.Y);
        Canvas.SetLeft(Selection, x); Canvas.SetTop(Selection, y);
        Selection.Width = Math.Abs(p.X - _startDip.X); Selection.Height = Math.Abs(p.Y - _startDip.Y);
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (_windowMode || !_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        var a = PointToScreen(_startDip);
        var b = PointToScreen(e.GetPosition(this));
        int x = (int)Math.Round(Math.Min(a.X, b.X)), y = (int)Math.Round(Math.Min(a.Y, b.Y));
        int w = (int)Math.Round(Math.Abs(a.X - b.X)), h = (int)Math.Round(Math.Abs(a.Y - b.Y));
        _result = (w >= 4 && h >= 4) ? new Int32Rect(x, y, w, h) : null;
        DialogResult = _result is not null;
    }

    private void HighlightWindowUnderCursor()
    {
        _hoverWindow = WindowUnderCursor(new WindowInteropHelper(this).Handle);
        if (_hoverWindow is { } r)
        {
            var tl = PointFromScreen(new Point(r.X, r.Y));
            var br = PointFromScreen(new Point(r.X + r.Width, r.Y + r.Height));
            Selection.Visibility = Visibility.Visible;
            Canvas.SetLeft(Selection, tl.X); Canvas.SetTop(Selection, tl.Y);
            Selection.Width = Math.Max(0, br.X - tl.X); Selection.Height = Math.Max(0, br.Y - tl.Y);
        }
        else Selection.Visibility = Visibility.Collapsed;
    }

    // Topmost visible top-level window (excluding our overlay) whose frame contains the cursor.
    private static Int32Rect? WindowUnderCursor(IntPtr self)
    {
        GetCursorPos(out var cur);
        Int32Rect? found = null;
        EnumWindows((h, _) =>
        {
            if (h == self || !IsWindowVisible(h)) return true;
            if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            if (DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT r, Marshal.SizeOf<RECT>()) != 0)
                if (!GetWindowRect(h, out r)) return true;
            if (r.Right - r.Left <= 2 || r.Bottom - r.Top <= 2) return true;
            if (cur.X >= r.Left && cur.X < r.Right && cur.Y >= r.Top && cur.Y < r.Bottom)
            {
                found = new Int32Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
                return false; // enumeration is top-to-bottom, so first hit is the topmost window
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private const int DWMWA_CLOAKED = 14, DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT val, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int val, int size);
}
