namespace Laytivo.Core;

public static class ConfigurationValidator
{
    public static void Validate(AppConfiguration config)
    {
        if (config.SchemaVersion != 1)
            throw new InvalidDataException($"Nicht unterstützte Konfigurationsversion {config.SchemaVersion}. Erwartet: 1.");
        if (config.Settings is null || config.Settings.StartupDelaySeconds is not (0 or 5 or 15))
            throw new InvalidDataException("Ungültige Einstellungen oder Startverzögerung.");
        if (!RestoreHotkey.IsValid(config.Settings.RestoreHotkeyModifiers, config.Settings.RestoreHotkeyKey))
            throw new InvalidDataException("Ungültiger Tastatur-Shortcut. Unterstützt sind zwei oder drei Modifikatortasten mit A–Z oder F1–F11.");
        if (config.Workspace is not { } workspace) return;
        if (string.IsNullOrWhiteSpace(workspace.Id) || string.IsNullOrWhiteSpace(workspace.Name)
            || workspace.Monitors is null || workspace.Windows is null
            || workspace.Monitors.Count is 0 or > 64 || workspace.Windows.Count > 1000)
            throw new InvalidDataException("Ungültiges Standardprofil.");
        var monitors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var monitor in workspace.Monitors)
        {
            if (monitor is null || string.IsNullOrWhiteSpace(monitor.Id) || !monitors.Add(monitor.Id)
                || monitor.Bounds is null || !monitor.Bounds.IsValid || monitor.WorkArea is null || !monitor.WorkArea.IsValid
                || !double.IsFinite(monitor.DpiScale) || monitor.DpiScale is < 0.5 or > 8)
                throw new InvalidDataException("Ungültige oder doppelte Monitorinformationen.");
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var window in workspace.Windows)
        {
            if (window is null || string.IsNullOrWhiteSpace(window.Id) || !ids.Add(window.Id)
                || window.ProcessPath is null || window.ProcessName is null || window.Title is null
                || window.LaunchArguments is null || window.WorkingDirectory is null || window.WindowClass is null
                || window.MonitorId is null || !monitors.Contains(window.MonitorId)
                || window.NormalBounds is null || !window.NormalBounds.IsValid || !Enum.IsDefined(window.State))
                throw new InvalidDataException("Ungültige Fensterinformationen im Standardprofil.");
            if (window.IsSnapped && window.State != SavedWindowState.Normal)
                throw new InvalidDataException("Ein maximiertes Fenster kann nicht gleichzeitig angedockt sein.");
            if (window.VisibleBounds is { } visible && (!visible.IsValid || window.State != SavedWindowState.Normal
                || visible.X < window.NormalBounds.X || visible.Y < window.NormalBounds.Y
                || visible.X + visible.Width > window.NormalBounds.X + window.NormalBounds.Width
                || visible.Y + visible.Height > window.NormalBounds.Y + window.NormalBounds.Height
                || window.NormalBounds.Width - visible.Width > 400 || window.NormalBounds.Height - visible.Height > 400))
                throw new InvalidDataException("Ungültige sichtbare Fenstergrenzen.");
        }
    }
}
