using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace Supershot;

public partial class App : System.Windows.Application
{
    public enum CaptureMode { Region, Window, FullScreen }
    private WinForms.NotifyIcon? _tray;
    private HotKeyWindow? _hotkey;
    private EditorWindow? _editor;
    private bool _capturing;
    private bool _quitting;
    private readonly EventWaitHandle _activate = new(false, EventResetMode.AutoReset, @"Local\Supershot.Activate");
    private RegisteredWaitHandle? _activationWait;
    private DispatcherTimer? _updatesTimer;
    public bool HotkeyAvailable => _hotkey?.IsRegistered == true;
    public UpdateService Updates { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppSettings.Load();
        _tray = new WinForms.NotifyIcon { Icon = TrayIcon(), Visible = true, Text = "Supershot" };
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Capture region", null, (_, _) => StartCapture(CaptureMode.Region));
        menu.Items.Add("Capture window", null, (_, _) => StartCapture(CaptureMode.Window));
        menu.Items.Add("Capture display", null, (_, _) => StartCapture(CaptureMode.FullScreen));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Open editor", null, (_, _) => ShowEditor());
        menu.Items.Add("Check for updates…", null, async (_, _) => { ShowEditor(); await Updates.CheckAsync(); });
        menu.Items.Add("Quit Supershot", null, (_, _) => Quit());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowEditor();
        var (mods, vk) = AppSettings.ParseHotkey(AppSettings.Data.Hotkey);
        _hotkey = new HotKeyWindow(mods, vk);
        _hotkey.Pressed += () => StartCapture(CaptureMode.Region);
        AppSettings.HotkeyChanged += () =>
        {
            var (m, k) = AppSettings.ParseHotkey(AppSettings.Data.Hotkey);
            _hotkey?.Rebind(m, k);
            if (!HotkeyAvailable) _editor?.Notify("That shortcut is used by another app. Choose a different capture hotkey.");
        };
        _activationWait = ThreadPool.RegisterWaitForSingleObject(_activate,
            (_, _) => Dispatcher.BeginInvoke(() => ShowEditor()), null, Timeout.Infinite, false);
        ShowEditor();
        _updatesTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
        _updatesTimer.Tick += async (_, _) => { if (AppSettings.Data.AutoUpdate) await Updates.CheckAsync(); };
        _updatesTimer.Start();
        if (AppSettings.Data.AutoUpdate) _ = Updates.CheckAsync();
    }

    public async void StartCapture(CaptureMode mode)
    {
        if (_capturing) return;
        _capturing = true;
        bool visible = _editor?.IsVisible == true;
        try
        {
            var cursor = CursorSnapshot.Take();
            if (_editor?.HasUnsavedChanges == true && System.Windows.MessageBox.Show(
                "Start a new capture? Save or copy your current screenshot first.", "New capture",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            _editor?.Hide();
            await Task.Delay(160); // let the compositor remove our editor before selection/capture
            Int32Rect? rect = mode switch
            {
                CaptureMode.Window => RegionOverlay.SelectWindow(),
                CaptureMode.FullScreen => MonitorUnderCursor(),
                _ => RegionOverlay.SelectRegion(),
            };
            if (rect is null) { if (visible) ShowEditor(true); return; }
            int delay = Math.Clamp(AppSettings.Data.CaptureDelay, 0, 10);
            if (delay > 0)
            {
                if (!await CaptureCountdown.WaitAsync(delay)) { if (visible) ShowEditor(true); return; }
                await Task.Delay(160);
                cursor = CursorSnapshot.Take();
            }
            else await Task.Delay(120);
            var r = rect.Value;
            var data = ScreenCapture.CaptureDataUrl(r);
            object? pointer = cursor is null ? null : new
            {
                x = cursor.X - r.X, y = cursor.Y - r.Y, hotX = cursor.HotX, hotY = cursor.HotY,
                width = cursor.Width, height = cursor.Height, data = cursor.Data,
            };
            ShowEditor(true);
            _editor!.SetPendingImage(data, pointer);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(ex);
            ShowEditor(true);
            _editor?.Notify("Couldn’t capture that area. Try a smaller region or another display.");
        }
        finally { _capturing = false; }
    }

    private static Int32Rect MonitorUnderCursor()
    {
        var b = WinForms.Screen.FromPoint(WinForms.Cursor.Position).Bounds;
        return new Int32Rect(b.X, b.Y, b.Width, b.Height);
    }

    private void ShowEditor(bool afterCapture = false)
    {
        if (_quitting || (_capturing && !afterCapture)) return;
        if (_editor is null)
        {
            _editor = new EditorWindow();
            _editor.Closing += (_, args) => { if (!_quitting) { args.Cancel = true; _editor.Hide(); } };
        }
        _editor.Show();
        if (_editor.WindowState == WindowState.Minimized) _editor.WindowState = WindowState.Normal;
        _editor.Activate();
    }

    private void Quit()
    {
        if (_editor?.HasUnsavedChanges == true && System.Windows.MessageBox.Show(
            "Quit Supershot? Save or copy your current screenshot first.", "Quit Supershot",
            MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        _quitting = true; Shutdown();
    }

    private static Icon TrayIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Tray.ico");
        return File.Exists(path) ? new Icon(path, 32, 32) : (Icon)SystemIcons.Application.Clone();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _updatesTimer?.Stop(); _activationWait?.Unregister(null); _activate.Dispose(); _hotkey?.Dispose();
        if (_tray is not null) { _tray.Visible = false; _tray.Icon?.Dispose(); _tray.Dispose(); }
        base.OnExit(e);
    }
}
