using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Supershot;

/// <summary>
/// A message-only window that registers a system-wide hotkey via RegisterHotKey and
/// raises <see cref="Pressed"/> when it fires. RegisterHotKey (not a low-level keyboard
/// hook) is the enterprise-safe path: it doesn't look like a keylogger to EDR/AV.
/// </summary>
public sealed class HotKeyWindow : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const int HotKeyId = 0xB0B;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly HwndSource _src;

    public event Action? Pressed;

    public HotKeyWindow(uint modifiers, uint virtualKey)
    {
        _src = new HwndSource(new HwndSourceParameters("Supershot.HotKey")
        {
            Width = 0,
            Height = 0,
            ParentWindow = HwndMessage, // message-only window
            WindowStyle = 0,
        });
        _src.AddHook(WndProc);

        if (!RegisterHotKey(_src.Handle, HotKeyId, modifiers, virtualKey))
        {
            // Non-fatal: another app may own the combo. The tray menu still triggers capture.
            System.Diagnostics.Debug.WriteLine("Supershot: hotkey registration failed (combo in use?).");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && wParam.ToInt32() == HotKeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterHotKey(_src.Handle, HotKeyId);
        _src.RemoveHook(WndProc);
        _src.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
