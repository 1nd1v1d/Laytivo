namespace Laytivo.Core;

public interface IGlobalHotkeyService
{
    event EventHandler? Pressed;
    bool IsRegistered { get; }
    void Configure(AppSettings settings);
}

public interface IAutostartService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

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
