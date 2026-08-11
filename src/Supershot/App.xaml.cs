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
    // Default capture hotkey: Ctrl + Shift + 2.  (MOD_CONTROL | MOD_SHIFT, VK '2')
    private const uint ModCtrl = 0x0002, ModShift = 0x0004;
    private const uint VkTwo = 0x32;

    private WinForms.NotifyIcon? _tray;
    private HotKeyWindow? _hotkey;
    private EditorWindow? _editor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _tray = new WinForms.NotifyIcon
        {
            Icon = TrayIcon(),
            Visible = true,
            Text = "Supershot — Ctrl+Shift+2 to capture",
        };
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Capture region\tCtrl+Shift+2", null, (_, _) => StartCapture());
        menu.Items.Add("Open editor", null, (_, _) => ShowEditor(null));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Shutdown());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => StartCapture();

        _hotkey = new HotKeyWindow(ModCtrl | ModShift, VkTwo);
        _hotkey.Pressed += StartCapture;
    }

    /// <summary>Dim the screen, let the user drag a region, capture it, open the editor.</summary>
    private void StartCapture()
    {
        // If the editor is up, hide it so it isn't part of the shot.
        _editor?.Hide();

        var rect = RegionOverlay.SelectRegion();
        if (rect is null) return;

        var dataUrl = ScreenCapture.CaptureDataUrl(rect.Value);
        ShowEditor(dataUrl);
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
