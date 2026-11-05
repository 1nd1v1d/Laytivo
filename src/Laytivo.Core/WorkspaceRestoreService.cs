using System.Diagnostics;

namespace Laytivo.Core;

public sealed record RestoreTiming(TimeSpan WindowTimeout, TimeSpan PollInterval, TimeSpan PlacementRetry)
{
    public static RestoreTiming Default => new(TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(750));
}

public sealed class WorkspaceRestoreService(IWindowSystem windows, IApplicationLauncher launcher, ILocalLog log,
    RestoreTiming? timing = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly RestoreTiming _timing = timing ?? RestoreTiming.Default;

    public async Task<RestoreReport> RestoreAsync(AppConfiguration config, CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        if (!await _gate.WaitAsync(0, cancellationToken)) return new(started, [], true);
        try
        {
            ConfigurationValidator.Validate(config);
            if (config.Workspace is not { } workspace) return new(started, []);
            var results = new Dictionary<string, WindowResult>();
            var monitors = windows.GetMonitors();
            var live = windows.GetWindows();
            var matches = WindowMatchingService.Match(workspace.Windows, live);
            var launched = new HashSet<string>(StringComparer.Ordinal);
            var pending = new List<WindowSnapshot>();
            foreach (var entry in workspace.Windows.Where(e => !matches.ContainsKey(e.Id)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!config.Settings.LaunchMissingApps)
                {
                    results[entry.Id] = Result(entry, RestoreOutcome.Skipped, "Fenster fehlt; automatischer Programmstart ist deaktiviert.");
                    continue;
                }
                if (!entry.AutoLaunchSupported || string.IsNullOrWhiteSpace(entry.ProcessPath))
                {
                    results[entry.Id] = Result(entry, RestoreOutcome.Skipped, "Keine unterstützten Startinformationen vorhanden.");
                    continue;
                }
                // Without explicit document arguments, never blindly launch an existing app again.
                if (string.IsNullOrWhiteSpace(entry.LaunchArguments)
                    && live.Any(w => string.Equals(w.Window.ProcessPath, entry.ProcessPath, StringComparison.OrdinalIgnoreCase)))
                {
                    pending.Add(entry);
                    continue;
                }
                var launchKey = entry.ProcessPath.ToUpperInvariant() + "\n" + entry.LaunchArguments;
                if (launched.Add(launchKey))
                {
                    try { launcher.Launch(entry); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        foreach (var sameApp in workspace.Windows.Where(e => !matches.ContainsKey(e.Id)
                            && string.Equals(e.ProcessPath, entry.ProcessPath, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(e.LaunchArguments, entry.LaunchArguments, StringComparison.Ordinal)))
                            results[sameApp.Id] = Result(sameApp, RestoreOutcome.Failed, $"Programmstart fehlgeschlagen: {ex.Message}");
                        continue;
                    }
                }
                if (!results.ContainsKey(entry.Id)) pending.Add(entry);
            }
            // Position existing windows immediately. A shared deadline bounds waiting for all missing windows.
            await PlaceAllAsync(workspace.Windows.Where(e => matches.ContainsKey(e.Id)).ToArray(), matches,
                workspace, monitors, results, cancellationToken);
            var wait = Stopwatch.StartNew();
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                live = windows.GetWindows();
                var available = live.Where(w => !matches.Values.Any(m => m.Handle == w.Handle)).ToArray();
                var found = WindowMatchingService.Match(pending, available);
                await PlaceAllAsync(pending.Where(e => found.ContainsKey(e.Id)).ToArray(), found,
                    workspace, monitors, results, cancellationToken);
                foreach (var pair in found) matches[pair.Key] = pair.Value;
                pending.RemoveAll(e => found.ContainsKey(e.Id) || results.ContainsKey(e.Id));
                if (pending.Count == 0 || wait.Elapsed >= _timing.WindowTimeout) break;
                await Task.Delay(_timing.PollInterval, cancellationToken);
            }
            foreach (var entry in pending)
                results[entry.Id] = Result(entry, RestoreOutcome.Failed, "Zeitüberschreitung: kein weiteres passendes Fenster. Zweitfenster benötigen ggf. eigene App-Startargumente.");
            // Apps can reposition themselves while other startup windows are still appearing.
            // Recheck existing matches against the final monitor topology after that shared wait.
            if (wait.Elapsed >= _timing.PlacementRetry)
                await PlaceAllAsync(workspace.Windows.Where(e => matches.ContainsKey(e.Id)).ToArray(), matches,
                    workspace, windows.GetMonitors(), results, cancellationToken);
            var report = new RestoreReport(started, workspace.Windows.Select(e => results[e.Id]).ToArray());
            log.Write("restore", report);
            return report;
        }
        finally { _gate.Release(); }
    }

    private async Task PlaceAllAsync(IReadOnlyList<WindowSnapshot> entries, Dictionary<string, LiveWindow> matches,
        Workspace workspace, IReadOnlyList<MonitorSnapshot> monitors, Dictionary<string, WindowResult> results, CancellationToken token)
    {
        if (entries.Count == 0) return;
        // Two bounded placement passes allow apps to finish their own startup positioning.
        for (var pass = 0; pass < 2; pass++)
        {
            if (pass > 0) await Task.Delay(_timing.PlacementRetry, token);
            foreach (var entry in entries)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var source = workspace.Monitors.First(m => m.Id == entry.MonitorId);
                    var target = Geometry.SelectMonitor(source, monitors);
                    var visible = entry.VisibleBounds is { } frame ? Geometry.Adapt(frame, source, target) : null;
                    var placed = windows.PlaceWindow(matches[entry.Id], Geometry.AdaptWindow(entry, source, target), entry.State, visible);
                    results[entry.Id] = entry.IsSnapped && !placed.IsSnapped
                        ? Result(entry, RestoreOutcome.PartiallyRestored,
                            "Position und Größe wiederhergestellt; Windows-Andockung/Snap-Gruppe konnte nicht neu erzeugt werden. Bei Bedarf manuell andocken.")
                        : Result(entry, RestoreOutcome.Restored, placed.IsSnapped
                            ? "Position wiederhergestellt; vorhandene Windows-Andockung erhalten."
                            : "Position und Fensterzustand angewendet.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    results[entry.Id] = Result(entry, RestoreOutcome.Failed, $"Fenster konnte nicht positioniert werden: {ex.Message}");
                }
            }
        }
    }

    private static WindowResult Result(WindowSnapshot entry, RestoreOutcome outcome, string message) =>
        new(entry.Id, string.IsNullOrWhiteSpace(entry.Title) ? entry.ProcessName : entry.Title, outcome, message);
}
