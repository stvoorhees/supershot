using System.IO;
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

    public EditorWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitAsync();
    }

    private async Task InitAsync()
    {
        await Web.EnsureCoreWebView2Async();
        var core = Web.CoreWebView2;

        // Serve the bundled editor from a virtual https origin (a secure context, so
        // clipboard/canvas APIs work), read-only, no network.
        var editorDir = Path.Combine(AppContext.BaseDirectory, "editor");
        core.SetVirtualHostNameToFolderMapping("supershot.editor", editorDir,
            CoreWebView2HostResourceAccessKind.Allow);

        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDevToolsEnabled = false;

        core.WebMessageReceived += OnWebMessage;
        core.Navigate("https://supershot.editor/index.html");
    }

    /// <summary>Queue an image (data URL); delivered once the page reports ready.</summary>
    public void SetPendingImage(string dataUrl)
    {
        _pending = dataUrl;
        if (_ready) PostImage(dataUrl);
    }

    private void PostImage(string dataUrl)
    {
        var msg = JsonSerializer.Serialize(new { type = "image", data = dataUrl });
        Web.CoreWebView2.PostWebMessageAsJson(msg);
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string type, data;
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            type = doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            data = doc.RootElement.TryGetProperty("data", out var d) ? d.GetString() ?? "" : "";
        }
        catch { return; }

        switch (type)
        {
            case "ready":
                _ready = true;
                if (_pending is not null) PostImage(_pending);
                break;
            case "drag":
                try { DragMove(); } catch { /* only valid while the mouse button is down */ }
                break;
            case "min":
                WindowState = WindowState.Minimized;
                break;
            case "close":
                Hide();
                break;
            case "open":
                OpenImage();
                break;
            case "save":
                SavePng(data);
                break;
            case "copy":
                CopyPng(data);
                break;
        }
    }

    private void OpenImage()
    {
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
        PostImage($"data:{mime};base64,{Convert.ToBase64String(bytes)}");
    }

    private static byte[] Decode(string dataUrl)
    {
        var i = dataUrl.IndexOf(',');
        return Convert.FromBase64String(i >= 0 ? dataUrl[(i + 1)..] : dataUrl);
    }

    private void SavePng(string dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl)) return;
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = "supershot.png", Filter = "PNG image|*.png", DefaultExt = ".png" };
        if (dlg.ShowDialog() == true) File.WriteAllBytes(dlg.FileName, Decode(dataUrl));
    }

    private static void CopyPng(string dataUrl)
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
    }
}
