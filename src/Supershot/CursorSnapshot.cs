using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Supershot;

/// <summary>A copied cursor and its physical hotspot, captured before our selection UI appears.</summary>
public sealed record CursorSnapshot(int X, int Y, int HotX, int HotY, int Width, int Height, string Data)
{
    public static CursorSnapshot? Take()
    {
        var info = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref info) || (info.flags & 1) == 0) return null;
        var icon = CopyIcon(info.hCursor);
        if (icon == IntPtr.Zero) return null;
        ICONINFO details = default;
        try
        {
            if (!GetIconInfo(icon, out details)) return null;
            // The color bitmap carries the native size; monochrome masks are double-height.
            var handle = details.hbmColor != IntPtr.Zero ? details.hbmColor : details.hbmMask;
            if (handle == IntPtr.Zero) return null;
            if (GetObject(handle, Marshal.SizeOf<BITMAP>(), out var bitmap) == 0) return null;
            int w = Math.Clamp(bitmap.bmWidth, 1, 256);
            int h = Math.Clamp(details.hbmColor != IntPtr.Zero ? bitmap.bmHeight : bitmap.bmHeight / 2, 1, 256);
            // Rendering against black and white reconstructs alpha even for legacy
            // monochrome cursors, where DrawIconEx does not write an alpha channel.
            using var black = Render(icon, w, h, Color.Black);
            using var white = Render(icon, w, h, Color.White);
            using var image = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var b = black.GetPixel(x, y); var a = white.GetPixel(x, y);
                int alpha = Math.Clamp(255 - Math.Max(a.R - b.R, Math.Max(a.G - b.G, a.B - b.B)), 0, 255);
                if (alpha == 0) continue;
                image.SetPixel(x, y, Color.FromArgb(alpha, Math.Min(255, b.R * 255 / alpha),
                    Math.Min(255, b.G * 255 / alpha), Math.Min(255, b.B * 255 / alpha)));
            }
            using var ms = new MemoryStream(); image.Save(ms, ImageFormat.Png);
            return new(info.ptScreenPos.X, info.ptScreenPos.Y, (int)details.xHotspot,
                (int)details.yHotspot, w, h, "data:image/png;base64," + Convert.ToBase64String(ms.ToArray()));
        }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); return null; }
        finally
        {
            if (details.hbmColor != IntPtr.Zero) DeleteObject(details.hbmColor);
            if (details.hbmMask != IntPtr.Zero) DeleteObject(details.hbmMask);
            DestroyIcon(icon);
        }
    }

    private static Bitmap Render(IntPtr icon, int width, int height, Color background)
    {
        var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(background);
        var dc = graphics.GetHdc();
        try { DrawIconEx(dc, 0, 0, icon, width, height, 0, IntPtr.Zero, 3); }
        finally { graphics.ReleaseHdc(dc); }
        return image;
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct CURSORINFO { public int cbSize; public uint flags; public IntPtr hCursor; public POINT ptScreenPos; }
    [StructLayout(LayoutKind.Sequential)] private struct ICONINFO { public int fIcon; public uint xHotspot, yHotspot; public IntPtr hbmMask, hbmColor; }
    [StructLayout(LayoutKind.Sequential)] private struct BITMAP { public int bmType, bmWidth, bmHeight, bmWidthBytes; public ushort bmPlanes, bmBitsPixel; public IntPtr bmBits; }
    [DllImport("user32.dll")] private static extern bool GetCursorInfo(ref CURSORINFO info);
    [DllImport("user32.dll")] private static extern IntPtr CopyIcon(IntPtr icon);
    [DllImport("user32.dll")] private static extern bool GetIconInfo(IntPtr icon, out ICONINFO info);
    [DllImport("user32.dll")] private static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon, int w, int h, uint step, IntPtr brush, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll", EntryPoint="GetObjectW")] private static extern int GetObject(IntPtr obj, int size, out BITMAP bitmap);
}
