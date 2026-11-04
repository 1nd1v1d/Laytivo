namespace Laytivo.Core;

public sealed record DesktopRect(int X, int Y, int Width, int Height)
{
    public bool IsValid => Width is > 0 and <= 100000 && Height is > 0 and <= 100000
        && X is >= -100000 and <= 100000 && Y is >= -100000 and <= 100000;
}

public sealed record MonitorSnapshot(string Id, string DeviceName, DesktopRect Bounds,
    DesktopRect WorkArea, double DpiScale, bool IsPrimary);

public enum SavedWindowState { Normal, Maximized }

public sealed record WindowSnapshot(string Id, string ProcessPath, string ProcessName,
    string LaunchArguments, string WorkingDirectory, string Title, string WindowClass,
    string MonitorId, DesktopRect NormalBounds, SavedWindowState State, bool AutoLaunchSupported,
    bool IsSnapped = false, DesktopRect? VisibleBounds = null);

public sealed record WindowPlacementResult(bool IsSnapped);

public sealed record LiveWindow(nint Handle, int ProcessId, WindowSnapshot Window);

public sealed record Workspace(string Id, string Name, DateTimeOffset SavedAtUtc,
    IReadOnlyList<MonitorSnapshot> Monitors, IReadOnlyList<WindowSnapshot> Windows);

public sealed record AppSettings(bool StartWithWindows = false, bool RestoreAfterLogin = true,
    bool LaunchMissingApps = true, int StartupDelaySeconds = 5);

public sealed record AppConfiguration(int SchemaVersion, AppSettings Settings, Workspace? Workspace)
{
    public static AppConfiguration Empty => new(1, new AppSettings(), null);
}

public enum RestoreOutcome { Restored, Skipped, Failed, PartiallyRestored }
public sealed record WindowResult(string WindowId, string Application, RestoreOutcome Outcome, string Message);
public sealed record RestoreReport(DateTimeOffset StartedAtUtc, IReadOnlyList<WindowResult> Results, bool AlreadyRunning = false)
{
    public string Summary => AlreadyRunning ? "Eine Wiederherstellung läuft bereits."
        : $"{Results.Count(x => x.Outcome == RestoreOutcome.Restored)} wiederhergestellt, "
        + $"{Results.Count(x => x.Outcome == RestoreOutcome.PartiallyRestored)} teilweise wiederhergestellt, "
        + $"{Results.Count(x => x.Outcome == RestoreOutcome.Skipped)} übersprungen, "
        + $"{Results.Count(x => x.Outcome == RestoreOutcome.Failed)} fehlgeschlagen.";
}
