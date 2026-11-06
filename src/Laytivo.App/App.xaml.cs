using System.Security.Principal;
using System.Windows;
using Laytivo.Core;
using Laytivo.Infrastructure;

namespace Laytivo.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private Mutex? _installerMutex;
    private bool _ownsMutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private EventWaitHandle? _restoreEvent;
    private RegisteredWaitHandle? _restoreWait;
    private bool _startupRestoreScheduled;
    private MainViewModel? _viewModel;
    private TrayService? _tray;
    private GlobalHotkeyService? _hotkey;
    private LocalLog? _log;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Laytivo");
        var dataOption = Array.IndexOf(e.Args, "--data-directory");
        if (dataOption >= 0 && dataOption + 1 < e.Args.Length) directory = Path.GetFullPath(e.Args[dataOption + 1]);
        _log = new(Path.Combine(directory, "logs"));
        DispatcherUnhandledException += (_, args) =>
        {
            _log.Write("ui-error", new { error = args.Exception.Message });
            args.Handled = true;
        };
        try
        {
            var background = e.Args.Contains("--startup", StringComparer.OrdinalIgnoreCase);
            _log.Write("startup-requested", new
            {
                background,
                executable = Environment.ProcessPath,
                version = typeof(App).Assembly.GetName().Version?.ToString()
            });
            if (e.Args.Contains("--enable-autostart", StringComparer.OrdinalIgnoreCase))
            {
                new AutostartService().SetEnabled(true);
                _log.Write("autostart-enabled", new { executable = Environment.ProcessPath });
                Shutdown();
                return;
            }
            var sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
            _showEvent = new(false, EventResetMode.AutoReset, @"Local\Laytivo-Show-" + sid);
            _restoreEvent = new(false, EventResetMode.AutoReset, @"Local\Laytivo-Restore-" + sid);
            _mutex = new Mutex(true, @"Local\Laytivo-" + sid, out _ownsMutex);
            if (!_ownsMutex)
            {
                if (background) _restoreEvent.Set();
                else _showEvent.Set();
                _log.Write("existing-instance", new { background });
                Shutdown();
                return;
            }
            _installerMutex = new Mutex(false, @"Local\Laytivo-Running");
            _hotkey = new GlobalHotkeyService();
            var autostart = new AutostartService();
            try
            {
                if (autostart.MigrateLegacyEntry()) _log.Write("autostart-migrated-to-shortcut", new { });
            }
            catch (Exception ex) { _log.Write("autostart-migration-failed", new { error = ex.ToString() }); }
            _viewModel = new(new ConfigurationStore(directory), autostart, new WindowsWindowSystem(), _log, _hotkey);
            _hotkey.Pressed += async (_, _) =>
            {
                _log.Write("hotkey-restore-requested", new { shortcut = RestoreHotkey.Describe(_viewModel.EffectiveSettings) });
                await _viewModel.RestoreAsync();
            };
            var window = new MainWindow(_viewModel);
            MainWindow = window;
            void ShowSettings() { window.Show(); if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal; window.Activate(); }
            void RestoreOnLogin()
            {
                if (_startupRestoreScheduled) return;
                _startupRestoreScheduled = true;
                _log.Write("startup-restore-requested", new { });
                _ = _viewModel.RestoreOnStartupAsync();
            }
            _restoreWait = ThreadPool.RegisterWaitForSingleObject(_restoreEvent, (_, _) =>
            {
                if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(RestoreOnLogin);
            }, null, Timeout.Infinite, false);
            _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) =>
            {
                if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(() =>
                {
                    ShowSettings();
                    _log.Write("instance-activated", new { });
                });
            }, null, Timeout.Infinite, false);
            _tray = new(_viewModel, ShowSettings, async () => { ShowSettings(); await _viewModel.RefreshForCaptureAsync(); }, () => Shutdown());
            if (background) RestoreOnLogin();
            else { ShowSettings(); _ = _viewModel.RefreshForCaptureAsync(); }
            _log.Write("started", new { background = e.Args.Contains("--startup", StringComparer.OrdinalIgnoreCase) });
        }
        catch (Exception ex)
        {
            _log.Write("startup-failed", new { error = ex.ToString() });
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);
        _showEvent?.Dispose();
        _restoreWait?.Unregister(null);
        _restoreEvent?.Dispose();
        _hotkey?.Dispose();
        _viewModel?.Dispose();
        _tray?.Dispose();
        _log?.Write("stopped", new { e.ApplicationExitCode });
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _installerMutex?.Dispose();
        base.OnExit(e);
    }
}
