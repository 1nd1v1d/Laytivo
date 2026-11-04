namespace Laytivo.Core;

public sealed class WorkspaceCaptureService(IWindowSystem windows)
{
    public Workspace Capture(IReadOnlyList<CaptureSelection> selection)
    {
        var selectedHandles = selection.Select(s => s.Window.Handle).ToHashSet();
        var monitors = windows.GetMonitors();
        if (monitors.Count == 0) throw new InvalidOperationException("Kein aktiver Monitor gefunden.");
        var selected = windows.GetWindows().Where(w => selectedHandles.Contains(w.Handle)).ToArray();
        if (selected.Length == 0) throw new InvalidOperationException("Keine ausgewählten Fenster mehr verfügbar. Bitte Liste aktualisieren.");
        if (selected.Length != selectedHandles.Count)
            throw new InvalidOperationException("Ein ausgewähltes Fenster wurde geschlossen. Bitte Liste aktualisieren und erneut speichern.");
        if (selected.Any(w => selection.First(s => s.Window.Handle == w.Handle).Window.ProcessId != w.ProcessId))
            throw new InvalidOperationException("Ein ausgewähltes Fenster wurde inzwischen ersetzt. Bitte Liste aktualisieren.");
        var workspace = new Workspace("default", "Standard-Desktop", DateTimeOffset.UtcNow, monitors,
            selected.Select(w =>
            {
                var options = selection.First(s => s.Window.Handle == w.Handle);
                return w.Window with
                {
                    Id = Guid.NewGuid().ToString("N"),
                    LaunchArguments = options.LaunchArguments,
                    WorkingDirectory = options.WorkingDirectory
                };
            }).ToArray());
        ConfigurationValidator.Validate(new(1, new(), workspace));
        return workspace;
    }
}

public sealed record CaptureSelection(LiveWindow Window, string LaunchArguments, string WorkingDirectory);
