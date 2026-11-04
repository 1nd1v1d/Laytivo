using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Laytivo.Core;
using Laytivo.Infrastructure;

namespace Laytivo.App;

public sealed class WindowChoice(LiveWindow window, string monitorName) : INotifyPropertyChanged
{
    private bool _selected = true;
    public LiveWindow Window { get; } = window;
    public string LaunchArguments { get; set; } = window.Window.LaunchArguments;
    public string WorkingDirectory { get; set; } = window.Window.WorkingDirectory;
    public bool Selected { get => _selected; set { _selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); } }
    public string Title => Window.Window.Title;
    public string Application => string.IsNullOrEmpty(Window.Window.ProcessPath) ? Window.Window.ProcessName : Window.Window.ProcessPath;
    public string Monitor => monitorName;
    public string PlacementStatus => Window.Window.IsSnapped ? "Windows-angedockt" : Window.Window.State == SavedWindowState.Maximized ? "Maximiert" : "Normal";
    public string LaunchStatus => Window.Window.AutoLaunchSupported ? "EXE startbar · ohne Dokument-/Sitzungsgarantie" : "Nur Positionierung · automatischer Start nicht unterstützt";
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ConfigurationStore _store;
    private readonly AutostartService _autostart;
    private readonly IWindowSystem _windows;
    private readonly ILocalLog _log;
    private readonly WorkspaceCaptureService _capture;
    private readonly WorkspaceRestoreService _restore;
    private readonly CancellationTokenSource _lifetime = new();
    private AppConfiguration _configuration = AppConfiguration.Empty;
    private bool _busy, _configurationBlocked;
    private string _status = "Programme öffnen → Fenster manuell positionieren → Fensterliste aktualisieren und speichern.";

    public MainViewModel(ConfigurationStore store, AutostartService autostart, IWindowSystem windows, ILocalLog log)
    {
        _store = store; _autostart = autostart; _windows = windows; _log = log;
        _capture = new(windows);
        _restore = new(windows, new ApplicationLauncher(), log);
        RefreshCommand = new(() => RunAsync(RefreshAsync), () => !Busy);
        SaveCommand = new(() => RunAsync(SaveAsync), () => !Busy && !_configurationBlocked);
        RestoreCommand = new(RestoreAsync, () => !Busy && !_configurationBlocked && _configuration.Workspace is not null);
        ApplySettingsCommand = new(() => RunAsync(() => { ApplySettings(); return Task.CompletedTask; }), () => !Busy && !_configurationBlocked);
        ResetCommand = new(() => RunAsync(ResetAsync), () => !Busy && _configurationBlocked);
        try
        {
            _configuration = store.Load();
            // Read the OS state; external startup changes must not be hidden by stale JSON.
            var actualAutostart = autostart.IsEnabled();
            _configuration = _configuration with { Settings = _configuration.Settings with { StartWithWindows = actualAutostart } };
        }
        catch (Exception ex)
        {
            _configurationBlocked = true;
            Status = $"Konfiguration nicht geladen: {ex.Message} Die Originaldatei bleibt erhalten. „Fehlerhafte Konfiguration archivieren“ ermöglicht einen Neuanfang.";
            log.Write("configuration-load-failed", new { error = ex.Message });
        }
        LoadSettings();
        UpdateProfile();
    }

    public ObservableCollection<WindowChoice> Windows { get; } = [];
    public ObservableCollection<string> SavedWindows { get; } = [];
    public ObservableCollection<string> RestoreDetails { get; } = [];
    public int[] DelayOptions { get; } = [0, 5, 15];
    public bool StartWithWindows { get; set; }
    public bool RestoreAfterLogin { get; set; }
    public bool LaunchMissingApps { get; set; }
    public int StartupDelaySeconds { get; set; }
    public bool ConfigurationBlocked => _configurationBlocked;
    public bool Busy { get => _busy; private set { _busy = value; Changed(); Changed(nameof(IsIdle)); UpdateCommands(); } }
    public bool IsIdle => !Busy;
    public string Status { get => _status; private set { _status = value; Changed(); } }
    public string ProfileSummary { get; private set; } = "Noch keine Anordnung gespeichert.";
    public string LastRestore { get; private set; } = "Noch keine Wiederherstellung in dieser Sitzung.";
    public string DataDirectory => Path.GetDirectoryName(_store.FilePath)!;
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand RestoreCommand { get; }
    public AsyncCommand ApplySettingsCommand { get; }
    public AsyncCommand ResetCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? StateChanged;

    public Task RefreshForCaptureAsync() => RunAsync(RefreshAsync);

    private async Task RefreshAsync()
    {
        var live = await Task.Run(_windows.GetWindows, _lifetime.Token);
        var monitors = await Task.Run(_windows.GetMonitors, _lifetime.Token);
        Windows.Clear();
        foreach (var item in live)
        {
            var monitor = monitors.FirstOrDefault(m => m.Id == item.Window.MonitorId);
            Windows.Add(new(item, monitor is null ? "Monitor nicht verfügbar" : $"{monitor.DeviceName} · {monitor.Bounds.Width} × {monitor.Bounds.Height} · {monitor.DpiScale:P0}"));
        }
        Status = $"{Windows.Count} geeignete Fenster gefunden. Auswahl prüfen und „Auswahl als Anordnung speichern“ wählen.";
    }

    private async Task SaveAsync()
    {
        var selection = Windows.Where(x => x.Selected).Select(x => new CaptureSelection(x.Window, x.LaunchArguments, x.WorkingDirectory)).ToArray();
        if (selection.Length == 0) throw new InvalidOperationException("Bitte Fensterliste aktualisieren und mindestens ein Fenster auswählen.");
        var workspace = await Task.Run(() => _capture.Capture(selection), _lifetime.Token);
        var next = _configuration with { Workspace = workspace };
        _store.Save(next);
        _configuration = next;
        UpdateProfile();
        Status = $"{workspace.Windows.Count} Fenster als Standard-Desktop gespeichert. Die neue Anordnung ersetzt das bisherige Profil.";
        _log.Write("capture", new { workspace.SavedAtUtc, count = workspace.Windows.Count });
    }

    public async Task RestoreAsync()
    {
        if (Busy) { Status = "Ein Vorgang läuft bereits."; return; }
        if (_configurationBlocked) return;
        await RunAsync(async () =>
        {
            Status = "Anordnung wird wiederhergestellt … fehlende Fenster werden bis zu 20 Sekunden erwartet.";
            var config = _configuration;
            var report = await Task.Run(() => _restore.RestoreAsync(config, _lifetime.Token), _lifetime.Token);
            RestoreDetails.Clear();
            foreach (var result in report.Results) RestoreDetails.Add($"{result.Application}: {result.Message}");
            LastRestore = $"{report.StartedAtUtc.ToLocalTime():g} · {report.Summary}";
            Changed(nameof(LastRestore));
            Status = report.Summary;
        });
    }

    public async Task RestoreOnStartupAsync()
    {
        if (_configurationBlocked || !_configuration.Settings.RestoreAfterLogin || _configuration.Workspace is null) return;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_configuration.Settings.StartupDelaySeconds), _lifetime.Token);
            await RestoreAsync();
        }
        catch (OperationCanceledException) { }
    }

    private void ApplySettings()
    {
        var settings = new AppSettings(StartWithWindows, RestoreAfterLogin, LaunchMissingApps, StartupDelaySeconds);
        var previous = _configuration.Settings;
        var next = _configuration with { Settings = settings };
        ConfigurationValidator.Validate(next);
        _autostart.SetEnabled(settings.StartWithWindows);
        try { _store.Save(next); }
        catch
        {
            _autostart.SetEnabled(previous.StartWithWindows);
            throw;
        }
        _configuration = next;
        Status = "Einstellungen gespeichert; Windows-Autostart wurde im Benutzerkonto aktualisiert.";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleAutostart() => ApplyTraySetting(s => s with { StartWithWindows = !s.StartWithWindows });
    public void ToggleLaunch() => ApplyTraySetting(s => s with { LaunchMissingApps = !s.LaunchMissingApps });
    public AppSettings EffectiveSettings => _configuration.Settings;

    private void ApplyTraySetting(Func<AppSettings, AppSettings> change)
    {
        if (Busy || _configurationBlocked) return;
        var changed = change(_configuration.Settings);
        StartWithWindows = changed.StartWithWindows;
        RestoreAfterLogin = changed.RestoreAfterLogin;
        LaunchMissingApps = changed.LaunchMissingApps;
        StartupDelaySeconds = changed.StartupDelaySeconds;
        try { ApplySettings(); }
        catch (Exception ex) { Status = ex.Message; _log.Write("settings-failed", new { error = ex.Message }); }
        LoadSettings();
    }

    private Task ResetAsync()
    {
        var archived = _store.ArchiveInvalidConfiguration();
        _configuration = AppConfiguration.Empty with { Settings = new(StartWithWindows: _autostart.IsEnabled()) };
        _store.Save(_configuration);
        _configurationBlocked = false;
        Changed(nameof(ConfigurationBlocked));
        LoadSettings();
        UpdateProfile();
        Status = $"Bisherige Konfiguration archiviert: {archived}. Bitte neue Anordnung speichern.";
        return Task.CompletedTask;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (Busy) return;
        Busy = true;
        try { await action(); }
        catch (OperationCanceledException) { Status = "Vorgang beendet."; }
        catch (Exception ex) { Status = ex.Message; _log.Write("operation-failed", new { error = ex.Message }); }
        finally { Busy = false; StateChanged?.Invoke(this, EventArgs.Empty); }
    }

    private void LoadSettings()
    {
        var settings = _configuration.Settings;
        StartWithWindows = settings.StartWithWindows; RestoreAfterLogin = settings.RestoreAfterLogin;
        LaunchMissingApps = settings.LaunchMissingApps; StartupDelaySeconds = settings.StartupDelaySeconds;
        Changed(nameof(StartWithWindows)); Changed(nameof(RestoreAfterLogin)); Changed(nameof(LaunchMissingApps)); Changed(nameof(StartupDelaySeconds));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateProfile()
    {
        SavedWindows.Clear();
        if (_configuration.Workspace is { } workspace)
        {
            ProfileSummary = $"{workspace.Name} · {workspace.Windows.Count} Fenster · gespeichert {workspace.SavedAtUtc.ToLocalTime():g}";
            foreach (var window in workspace.Windows)
            {
                var monitor = workspace.Monitors.First(m => m.Id == window.MonitorId);
                SavedWindows.Add($"{window.Title} · {monitor.DeviceName} · {window.NormalBounds.Width} × {window.NormalBounds.Height} · "
                    + (window.IsSnapped ? "Windows-angedockt" : window.State == SavedWindowState.Maximized ? "maximiert" : "normal"));
            }
        }
        else ProfileSummary = "Noch keine Anordnung gespeichert.";
        Changed(nameof(ProfileSummary));
        UpdateCommands();
    }

    private void UpdateCommands()
    {
        RefreshCommand.RaiseCanExecuteChanged(); SaveCommand.RaiseCanExecuteChanged(); RestoreCommand.RaiseCanExecuteChanged();
        ApplySettingsCommand.RaiseCanExecuteChanged(); ResetCommand.RaiseCanExecuteChanged();
    }
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public void Dispose() { _lifetime.Cancel(); _lifetime.Dispose(); }
}
