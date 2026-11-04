namespace Laytivo.Core;

public interface IWindowSystem
{
    IReadOnlyList<MonitorSnapshot> GetMonitors();
    IReadOnlyList<LiveWindow> GetWindows();
    WindowPlacementResult PlaceWindow(LiveWindow window, DesktopRect bounds, SavedWindowState state);
}

public interface IApplicationLauncher
{
    void Launch(WindowSnapshot window);
}

public interface IConfigurationStore
{
    AppConfiguration Load();
    void Save(AppConfiguration configuration);
}

public interface ILocalLog
{
    void Write(string action, object details);
}
