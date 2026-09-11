using Velopack;
using Velopack.Sources;

namespace Supershot;

/// <summary>Only this service contacts GitHub; screenshot data is never sent.</summary>
public sealed class UpdateService
{
    private readonly UpdateManager _manager = new(new GithubSource(
        "https://github.com/stvoorhees/supershot", null, false));
    private bool _busy;
    public string State { get; private set; } = "idle";
    public string Message { get; private set; } = "Updates are checked through GitHub.";
    public int Progress { get; private set; }
    public string Version => _manager.CurrentVersion?.ToString() ??
        typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.2.0";
    public event Action? Changed;

    private void Set(string state, string message, int progress = 0)
    {
        State = state; Message = message; Progress = progress; Changed?.Invoke();
    }

    public async Task CheckAsync()
    {
        if (_busy) return;
        if (!_manager.IsInstalled)
        {
            Set("unavailable", "Install Supershot using Setup.exe to receive updates.");
            return;
        }
        if (_manager.UpdatePendingRestart is { } pending)
        {
            Set("ready", $"Version {pending.Version} is ready to install.");
            return;
        }
        _busy = true;
        try
        {
            Set("checking", "Checking for updates…");
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null) { Set("current", "You’re up to date."); return; }
            Set("downloading", $"Downloading {update.TargetFullRelease.Version}…");
            await _manager.DownloadUpdatesAsync(update, p =>
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    Set("downloading", $"Downloading {update.TargetFullRelease.Version}…", p)));
            Set("ready", $"Version {update.TargetFullRelease.Version} is ready. Restart when you’re finished editing.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(ex);
            Set("error", "Couldn’t check or download the update. Check your connection and try again.");
        }
        finally { _busy = false; }
    }

    public void Install()
    {
        if (_busy || !_manager.IsInstalled || _manager.UpdatePendingRestart is not { } pending) return;
        // Explicit consent on every restart. Never use the timed/force-quit path while editing.
        if (System.Windows.MessageBox.Show(
            "Restart to install the update? Save or copy your current screenshot first. The editor session will close.",
            "Update Supershot", System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Information) != System.Windows.MessageBoxResult.OK) return;
        try { _manager.ApplyUpdatesAndRestart(pending); }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(ex);
            Set("error", "Couldn’t install the update. Quit Supershot and try again.");
        }
    }
}
