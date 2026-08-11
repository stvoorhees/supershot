using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
// disambiguate from the WinForms types pulled in by UseWindowsForms' implicit usings
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Supershot;

/// <summary>
/// Full-virtual-screen transparent overlay. The user drags a rectangle; we return it in
/// physical screen pixels (via PointToScreen, which is DPI- and position-correct) so the
/// GDI capture lines up. Returns null on cancel/empty selection.
/// </summary>
public partial class RegionOverlay : Window
{
    private Point _startDip;
    private bool _dragging;
    private Int32Rect? _result;

    private RegionOverlay()
    {
        InitializeComponent();

        // Cover the whole virtual desktop (these SystemParameters are in DIPs).
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Loaded += (_, _) =>
        {
            Dim.Width = Width; Dim.Height = Height;
            Canvas.SetLeft(Hint, (Width - Hint.ActualWidth) / 2);
            Canvas.SetTop(Hint, 40);
            Activate();
        };

        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { _result = null; DialogResult = false; } };
    }

    /// <summary>Show the overlay modally and return the selected region in physical pixels.</summary>
    public static Int32Rect? SelectRegion()
    {
        var overlay = new RegionOverlay();
        overlay.ShowDialog();
        return overlay._result;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _startDip = e.GetPosition(this);
        Hint.Visibility = Visibility.Collapsed;
        Selection.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetPosition(this);
        double x = Math.Min(p.X, _startDip.X), y = Math.Min(p.Y, _startDip.Y);
        double w = Math.Abs(p.X - _startDip.X), h = Math.Abs(p.Y - _startDip.Y);
        Canvas.SetLeft(Selection, x); Canvas.SetTop(Selection, y);
        Selection.Width = w; Selection.Height = h;
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();

        var endDip = e.GetPosition(this);
        // Convert both corners to physical screen pixels (DPI + multi-monitor aware).
        var a = PointToScreen(_startDip);
        var b = PointToScreen(endDip);
        int x = (int)Math.Round(Math.Min(a.X, b.X));
        int y = (int)Math.Round(Math.Min(a.Y, b.Y));
        int w = (int)Math.Round(Math.Abs(a.X - b.X));
        int h = (int)Math.Round(Math.Abs(a.Y - b.Y));

        _result = (w >= 4 && h >= 4) ? new Int32Rect(x, y, w, h) : null;
        DialogResult = _result is not null;
    }
}
