using Velopack;

namespace Supershot;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        // Hooks must run before WPF, the single-instance mutex, or any normal app work.
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        using var instance = new Mutex(true, @"Local\Supershot.Desktop", out var first);
        if (!first)
        {
            using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Supershot.Activate");
            signal.Set();
            return;
        }
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
