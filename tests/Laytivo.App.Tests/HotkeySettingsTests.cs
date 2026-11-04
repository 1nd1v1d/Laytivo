using System.IO;
using Laytivo.Core;
using Laytivo.Infrastructure;

namespace Laytivo.App.Tests;

public sealed class HotkeySettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Laytivo.App.Tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void StartupHotkeyConflictDoesNotBlockIndependentTraySettings()
    {
        var hotkey = new FakeHotkey { RejectDefault = true };
        var store = new ConfigurationStore(_directory);
        using var model = new MainViewModel(store, new FakeAutostart(), new EmptyWindows(), new FakeLog(), hotkey);
        model.ToggleLaunch();
        Assert.False(store.Load().Settings.LaunchMissingApps);
        Assert.False(hotkey.IsRegistered);
        Assert.Contains("belegt", model.HotkeyStatus);
    }

    [Fact]
    public async Task FailedSaveRestoresPreviouslyUnregisteredHotkeyAfterStartupConflict()
    {
        var hotkey = new FakeHotkey { RejectDefault = true };
        var store = new ConfigurationStore(_directory);
        using var model = new MainViewModel(store, new FakeAutostart(), new EmptyWindows(), new FakeLog(), hotkey);
        Assert.False(hotkey.IsRegistered);
        Assert.Contains("belegt", model.HotkeyStatus);
        // Block the atomic destination after initialization to exercise a real persistence failure.
        Directory.CreateDirectory(store.FilePath);
        model.RestoreHotkeyKey = 0x54;
        model.ApplySettingsCommand.Execute(null);
        while (model.Busy) await Task.Delay(10);
        Assert.False(hotkey.IsRegistered);
        Assert.Equal(0x52, model.EffectiveSettings.RestoreHotkeyKey);
        Assert.False(File.Exists(store.FilePath));
    }

    [Fact]
    public void TrayOptionKeepsSavedShortcutAndDisabledState()
    {
        var store = new ConfigurationStore(_directory);
        var settings = new AppSettings(RestoreHotkeyEnabled: false, RestoreHotkeyModifiers: 6, RestoreHotkeyKey: 0x54);
        store.Save(new(1, settings, null));
        using var model = new MainViewModel(store, new FakeAutostart(), new EmptyWindows(), new FakeLog(), new FakeHotkey());
        model.ToggleLaunch();
        var saved = store.Load().Settings;
        Assert.False(saved.RestoreHotkeyEnabled);
        Assert.Equal(6, saved.RestoreHotkeyModifiers);
        Assert.Equal(0x54, saved.RestoreHotkeyKey);
        Assert.False(saved.LaunchMissingApps);
    }

    [Fact]
    public async Task ShortcutWithoutProfileGivesInstructionAndDoesNotDiscoverOrLaunchWindows()
    {
        using var model = new MainViewModel(new(_directory), new FakeAutostart(), new EmptyWindows(), new FakeLog(), new FakeHotkey());
        await model.RestoreAsync();
        Assert.Contains("zuerst eine Fensteranordnung speichern", model.Status);
        Assert.False(model.Busy);
    }

    private sealed class FakeHotkey : IGlobalHotkeyService
    {
        public event EventHandler? Pressed { add { } remove { } }
        public bool IsRegistered { get; private set; }
        public bool RejectDefault { get; init; }
        public void Configure(AppSettings settings)
        {
            if (RejectDefault && settings.RestoreHotkeyEnabled && settings.RestoreHotkeyKey == 0x52)
                throw new InvalidOperationException("Shortcut ist belegt.");
            IsRegistered = settings.RestoreHotkeyEnabled;
        }
    }
    private sealed class FakeAutostart : IAutostartService
    {
        public bool IsEnabled() => false;
        public void SetEnabled(bool enabled) { }
    }
    private sealed class EmptyWindows : IWindowSystem
    {
        public IReadOnlyList<MonitorSnapshot> GetMonitors() => throw new InvalidOperationException("Keine Fensteroperation erwartet.");
        public IReadOnlyList<LiveWindow> GetWindows() => throw new InvalidOperationException("Keine Fensteroperation erwartet.");
        public WindowPlacementResult PlaceWindow(LiveWindow window, DesktopRect bounds, SavedWindowState state) => throw new InvalidOperationException();
    }
    private sealed class FakeLog : ILocalLog { public void Write(string action, object details) { } }
    public void Dispose()
    {
        var path = Path.GetFullPath(_directory);
        var allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Laytivo.App.Tests-"));
        if (!path.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Ungültiges Testverzeichnis.");
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
