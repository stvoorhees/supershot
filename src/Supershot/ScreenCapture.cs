using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Int32Rect = System.Windows.Int32Rect; // System.Drawing owns Size/Bitmap here; only Int32Rect is WPF

namespace Supershot;

/// <summary>
/// Screen capture via GDI (BitBlt through Graphics.CopyFromScreen). Simple and reliable
/// for stills. Rect is in physical screen pixels. (A future hardening step is
/// Windows.Graphics.Capture, which also blacks out DRM/protected windows.)
/// </summary>
public static class ScreenCapture
{
    public static byte[] CapturePng(Int32Rect r)
    {
        int w = Math.Max(1, r.Width), h = Math.Max(1, r.Height);
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(r.X, r.Y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public static string CaptureDataUrl(Int32Rect r) =>
        "data:image/png;base64," + Convert.ToBase64String(CapturePng(r));
}
