using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;

namespace Supershot;

/// <summary>
/// Frameless window hosting the web editor in WebView2. All UI is the local web app;
/// this class only bridges: it hands the capture to the page and services the page's
/// requests (save/copy/open/window controls) with native code. No network access.
/// </summary>
public partial class EditorWindow : Window
{
    private bool _ready;
    private string? _pending;
    private object? _pointer;
    public bool HasImage { get; private set; }
    public bool HasUnsavedChanges { get; private set; }
    private UpdateService Updates => ((App)System.Windows.Application.Current).Updates;

    public EditorWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => SetWindowAppearance(false);
        StateChanged += (_, _) =>
        {
            WindowFrame.BorderThickness = WindowState == WindowState.Maximized ? new Thickness(0) : new Thickness(1);
            SendWindowState();
        };
        var ico = Path.Combine(AppContext.BaseDirectory, "Supershot.ico");
        if (File.Exists(ico)) { try { Icon = BitmapFrame.Create(new Uri(ico)); } catch { } }
        Loaded += async (_, _) =>
        {
            try { await InitAsync(); }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); System.Windows.MessageBox.Show("The editor could not start. Install the Microsoft Edge WebView2 Runtime and reopen Supershot.", "Supershot"); }
        };
    }

    private void SetWindowAppearance(bool dark)
    {
        var border = dark ? System.Windows.Media.Color.FromRgb(74, 74, 74)
                          : System.Windows.Media.Color.FromRgb(213, 212, 208);
        WindowFrame.BorderBrush = new SolidColorBrush(border);
        Background = new SolidColorBrush(dark ? System.Windows.Media.Color.FromRgb(33, 33, 33)
                                             : System.Windows.Media.Color.FromRgb(250, 249, 246));
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        int corners = 2; // DWMWCP_ROUND; Windows retains square corners when maximized/snapped.
        int darkMode = dark ? 1 : 0;
        int borderColor = border.R | (border.G << 8) | (border.B << 16);
        DwmSetWindowAttribute(handle, 33, ref corners, sizeof(int));
        DwmSetWindowAttribute(handle, 20, ref darkMode, sizeof(int));
        DwmSetWindowAttribute(handle, 34, ref borderColor, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private async Task InitAsync()
    {
        var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SupershotData", "WebView2");
        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
        await Web.EnsureCoreWebView2Async(environment);
        var core = Web.CoreWebView2;

        // Serve the bundled editor from a virtual https origin (a secure context, so
        // clipboard/canvas APIs work), read-only, no network.
        var editorDir = Path.Combine(AppContext.BaseDirectory, "editor");
        core.SetVirtualHostNameToFolderMapping("supershot.editor", editorDir,
            CoreWebView2HostResourceAccessKind.Allow);

        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsNonClientRegionSupportEnabled = true;
        core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Auto;

        core.NavigationStarting += (_, e) => { if (e.Uri != "https://supershot.editor/index.html") e.Cancel = true; };
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.WebMessageReceived += OnWebMessage;
        Updates.Changed += SendUpdate;
        core.Navigate("https://supershot.editor/index.html");
    }

    /// <summary>Queue an image (data URL); delivered once the page reports ready.</summary>
    public void SetPendingImage(string dataUrl, object? pointer = null)
    {
        _pending = dataUrl; _pointer = pointer; HasImage = true; HasUnsavedChanges = true;
        if (_ready) PostImage(dataUrl);
    }

    private void PostImage(string dataUrl)
    {
        var msg = JsonSerializer.Serialize(new { type = "image", data = dataUrl, cursor = _pointer });
        Web.CoreWebView2.PostWebMessageAsJson(msg);
    }

    private async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (e.Source != "https://supershot.editor/index.html") return;
        JsonElement root;
        try { using var doc = JsonDocument.Parse(e.WebMessageAsJson); root = doc.RootElement.Clone(); }
        catch { return; }

        if (root.ValueKind != JsonValueKind.Object) return;
        var type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
        string Str(string k) => root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

        try
        {
        switch (type)
        {
            case "ready":
                _ready = true;
                SendWindowState();
                SendSettings();
                SendUpdate();
                if (_pending is not null) PostImage(_pending);
                break;
            case "appearance": SetWindowAppearance(Str("value") == "dark"); break;
            case "max": WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; break;
            case "min": WindowState = WindowState.Minimized; break;
            case "close": Hide(); break;
            case "open": OpenImage(); break;
            case "hasImage": HasImage = true; break;
            case "documentState": HasUnsavedChanges = root.GetProperty("dirty").GetBoolean(); break;
            case "capture":
                if (Enum.TryParse<App.CaptureMode>(Str("mode"), out var mode)) ((App)System.Windows.Application.Current).StartCapture(mode);
                break;
            case "checkUpdate": await Updates.CheckAsync(); break;
            case "installUpdate": Updates.Install(); break;
            case "setAutoUpdate": AppSettings.Data.AutoUpdate = root.GetProperty("value").GetBoolean(); AppSettings.Save(); break;
            case "setIncludeCursor": AppSettings.Data.IncludeCursor = root.GetProperty("value").GetBoolean(); AppSettings.Save(); break;
            case "setCaptureDelay": AppSettings.Data.CaptureDelay = Math.Clamp(root.GetProperty("value").GetInt32(), 0, 10); AppSettings.Save(); break;
            case "save": ExportFinished(Str("requestId"), SavePng(Str("data"))); break;
            case "copy": CopyPng(Str("data")); ExportFinished(Str("requestId"), true); break;
            case "setAutoCopy":
                AppSettings.Data.AutoCopy = root.TryGetProperty("value", out var b) && b.ValueKind == JsonValueKind.True;
                AppSettings.Save();
                break;
            case "setHotkey": AppSettings.SetHotkey(Str("value")); SendSettings(); break;
            case "chooseSaveFolder": ChooseSaveFolder(); break;
            case "clearSaveFolder": AppSettings.Data.SaveFolder = ""; AppSettings.Save(); SendSettings(); break;
        }
        }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); Notify("That action couldn’t be completed. Please try again."); if (type is "copy" or "save") ExportFinished(Str("requestId"), false); }
    }

    private void ExportFinished(string requestId, bool success)
    {
        if (_ready) Web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "exportResult", requestId, success }));
    }

    public void Notify(string message)
    {
        if (_ready) Web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "notice", message }));
    }

    private void SendWindowState()
    {
        if (_ready) Web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(
            new { type = "windowState", maximized = WindowState == WindowState.Maximized }));
    }

    private void SendUpdate()
    {
        if (!_ready) return;
        Web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new {
            type = "update", state = Updates.State, message = Updates.Message, progress = Updates.Progress, version = Updates.Version
        }));
    }

    private void SendSettings()
    {
        var d = AppSettings.Data;
        Web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(
            new { type = "settings", value = new { hotkeyAvailable = ((App)System.Windows.Application.Current).HotkeyAvailable, autoCopy = d.AutoCopy, hotkey = d.Hotkey, saveFolder = d.SaveFolder, autoUpdate = d.AutoUpdate, includeCursor = d.IncludeCursor, captureDelay = d.CaptureDelay } }));
    }

    private void ChooseSaveFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a folder to save screenshots" };
        if (dlg.ShowDialog() == true) { AppSettings.Data.SaveFolder = dlg.FolderName; AppSettings.Save(); SendSettings(); }
    }

    private void OpenImage()
    {
        if (HasUnsavedChanges && System.Windows.MessageBox.Show("Replace the current screenshot? Save or copy it first.", "Open image", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
        if (dlg.ShowDialog() != true) return;
        var bytes = File.ReadAllBytes(dlg.FileName);
        var mime = Path.GetExtension(dlg.FileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "image/png",
        };
        SetPendingImage($"data:{mime};base64,{Convert.ToBase64String(bytes)}");
    }

    private static byte[] Decode(string dataUrl)
    {
        var i = dataUrl.IndexOf(',');
        return Convert.FromBase64String(i >= 0 ? dataUrl[(i + 1)..] : dataUrl);
    }

    private bool SavePng(string dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl)) return false;
        var bytes = Decode(dataUrl);
        var name = $"Supershot {DateTime.Now:yyyy-MM-dd HH.mm.ss.fff}.png"; // unique by default

        // If a default folder is set, save straight there; otherwise ask.
        var folder = AppSettings.Data.SaveFolder;
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) { File.WriteAllBytes(Path.Combine(folder, name), bytes); Notify("Screenshot saved."); return true; }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = name, Filter = "PNG image|*.png", DefaultExt = ".png",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };
        if (dlg.ShowDialog() == true) { File.WriteAllBytes(dlg.FileName, bytes); Notify("Screenshot saved."); return true; }
        Notify("Save cancelled.");
        return false;
    }

    private void CopyPng(string dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl)) return;
        using var ms = new MemoryStream(Decode(dataUrl));
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        System.Windows.Clipboard.SetImage(bmp);
        Notify("Copied to clipboard.");
    }
}
