using System.Windows;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;

namespace Supershot;

/// <summary>A timer that does not steal focus and is gone before the pixels are sampled.</summary>
internal sealed class CaptureCountdown : Window
{
    private readonly CancellationTokenSource _cancel = new();
    private readonly System.Windows.Controls.TextBlock _label;

    private CaptureCountdown()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false;
        Topmost = true; ResizeMode = ResizeMode.NoResize; Width = 310; Height = 62;
        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
        Top = SystemParameters.WorkArea.Top + 28;
        var row = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _label = new System.Windows.Controls.TextBlock { Foreground = Brushes.White, FontSize = 13, Width = 200, VerticalAlignment = VerticalAlignment.Center };
        var cancel = new System.Windows.Controls.Button { Content = "Cancel", Padding = new Thickness(10, 5, 10, 5) };
        cancel.Click += (_, _) => _cancel.Cancel();
        row.Children.Add(_label); row.Children.Add(cancel);
        Content = new System.Windows.Controls.Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 39, 44)),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(16, 12, 16, 12), Child = row,
        };
    }

    public static async Task<bool> WaitAsync(int seconds)
    {
        var timer = new CaptureCountdown();
        using var escape = new HotKeyWindow(0, 0x1B);
        escape.Pressed += () => timer._cancel.Cancel();
        try
        {
            timer.Show();
            for (int remaining = seconds; remaining > 0; remaining--)
            {
                timer._label.Text = $"Capturing in {remaining}…  ·  Esc cancels";
                await Task.Delay(1000, timer._cancel.Token);
            }
            return true;
        }
        catch (OperationCanceledException) { return false; }
        finally { timer.Close(); timer._cancel.Dispose(); }
    }
}
