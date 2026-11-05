using Laytivo.Core;

namespace Laytivo.Tests;

internal static class TestData
{
    public static MonitorSnapshot Monitor(string id = "primary", int x = 0, int y = 0, int width = 1920,
        int height = 1080, double dpi = 1, bool primary = true) => new(id, "DISPLAY-" + id,
            new(x, y, width, height), new(x, y, width, height - 40), dpi, primary);
    public static WindowSnapshot Window(string id = "one", string title = "Document", string path = @"C:\Apps\Editor.exe",
        string monitor = "primary") => new(id, path, "Editor", "", @"C:\Apps", title, "EditorWindow", monitor,
            new(100, 100, 900, 600), SavedWindowState.Normal, true);
    public static AppConfiguration Config(params WindowSnapshot[] windows) => new(1, new(),
        new("default", "Standard-Desktop", DateTimeOffset.UtcNow, [Monitor()], windows));
}

internal sealed class FakeWindows : IWindowSystem
{
    public List<LiveWindow> Live { get; } = [];
    public List<(nint Handle, DesktopRect Bounds, SavedWindowState State)> Placements { get; } = [];
    public IReadOnlyList<MonitorSnapshot> Monitors { get; set; } = [TestData.Monitor()];
    public Func<IReadOnlyList<LiveWindow>>? Discover { get; set; }
    public nint? FailHandle { get; set; }
    public bool PlacementIsSnapped { get; set; }
    public IReadOnlyList<MonitorSnapshot> GetMonitors() => Monitors;
    public IReadOnlyList<LiveWindow> GetWindows() => Discover?.Invoke() ?? Live.ToArray();
    public WindowPlacementResult PlaceWindow(LiveWindow window, DesktopRect bounds, SavedWindowState state, DesktopRect? visibleBounds = null)
    {
        if (window.Handle == FailHandle) throw new InvalidOperationException("Zugriff verweigert");
        Placements.Add((window.Handle, bounds, state));
        return new(PlacementIsSnapped);
    }
}

internal sealed class FakeLauncher : IApplicationLauncher
{
    public List<WindowSnapshot> Launched { get; } = [];
    public Action<WindowSnapshot>? OnLaunch { get; set; }
    public void Launch(WindowSnapshot window) { Launched.Add(window); OnLaunch?.Invoke(window); }
}

internal sealed class FakeLog : ILocalLog
{
    public void Write(string action, object details) { }
}
