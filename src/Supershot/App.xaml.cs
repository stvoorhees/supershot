using System.Drawing;
using System.IO;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace Supershot;

/// <summary>
/// App lifetime: lives in the tray, registers a global hotkey, and orchestrates
/// capture -> editor. Nothing here touches the network; everything is local.
/// </summary>
public partial class App : System.Windows.Application
{
    public enum CaptureMode { Region, Window, FullScreen }

    private WinForms.NotifyIcon? _tray;
    private HotKeyWindow? _hotkey;
    private EditorWindow? _editor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppSettings.Load();

        _tray = new WinForms.NotifyIcon { Icon = TrayIcon(), Visible = true, Text = "Supershot" };
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Capture region", null, (_, _) => StartCapture(CaptureMode.Region));
        menu.Items.Add("Capture window", null, (_, _) => StartCapture(CaptureMode.Window));
        menu.Items.Add("Capture full screen", null, (_, _) => StartCapture(CaptureMode.FullScreen));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Open editor", null, (_, _) => ShowEditor(null));
        menu.Items.Add("Quit", null, (_, _) => Shutdown());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => StartCapture(CaptureMode.Region);

        var (mods, vk) = AppSettings.ParseHotkey(AppSettings.Data.Hotkey);
        _hotkey = new HotKeyWindow(mods, vk);
        _hotkey.Pressed += () => StartCapture(CaptureMode.Region);
        AppSettings.HotkeyChanged += () =>
        {
            var (m, k) = AppSettings.ParseHotkey(AppSettings.Data.Hotkey);
            _hotkey?.Rebind(m, k);
        };
    }

    /// <summary>Capture (region / window / full screen), then open the editor.</summary>
    private void StartCapture(CaptureMode mode)
    {
        _editor?.Hide(); // so the editor isn't part of the shot

        System.Windows.Int32Rect? rect = mode switch
        {
            CaptureMode.Window => RegionOverlay.SelectWindow(),
            CaptureMode.FullScreen => MonitorUnderCursor(),
            _ => RegionOverlay.SelectRegion(),
        };
        if (rect is null) return;

        ShowEditor(ScreenCapture.CaptureDataUrl(rect.Value));
    }

    private static System.Windows.Int32Rect MonitorUnderCursor()
    {
        var b = WinForms.Screen.FromPoint(WinForms.Cursor.Position).Bounds;
        return new System.Windows.Int32Rect(b.X, b.Y, b.Width, b.Height);
    }

    private void ShowEditor(string? dataUrl)
    {
        if (_editor is null)
        {
            _editor = new EditorWindow();
            _editor.Closing += (_, args) => { args.Cancel = true; _editor!.Hide(); }; // keep running in tray
        }
        if (dataUrl is not null) _editor.SetPendingImage(dataUrl);
        _editor.Show();
        _editor.WindowState = WindowState.Normal;
        _editor.Activate();
    }

    private static Icon TrayIcon()
    {
        var ico = Path.Combine(AppContext.BaseDirectory, "Supershot.ico");
        if (File.Exists(ico)) { try { return new Icon(ico, 32, 32); } catch { /* fall through */ } }

        // Fallback: generated gradient square if the icon asset is missing.
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                   new Rectangle(0, 0, 32, 32), ColorTranslator.FromHtml("#7c8cff"), ColorTranslator.FromHtml("#57e0d0"), 45f))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillRoundedRectangle(brush, new Rectangle(2, 2, 28, 28), 7);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        base.OnExit(e);
    }
}

internal static class GdiExtensions
{
    public static void FillRoundedRectangle(this Graphics g, System.Drawing.Brush brush, Rectangle r, int radius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
