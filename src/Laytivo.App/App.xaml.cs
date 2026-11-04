using System.Security.Principal;
using System.Windows;
using Laytivo.Infrastructure;

namespace Laytivo.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private Mutex? _installerMutex;
    private bool _ownsMutex;
    private MainViewModel? _viewModel;
    private TrayService? _tray;
    private LocalLog? _log;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        _mutex = new Mutex(true, @"Local\Laytivo-" + sid, out _ownsMutex);
        if (!_ownsMutex) { Shutdown(); return; }
        _installerMutex = new Mutex(false, @"Local\Laytivo-Running");
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
            _viewModel = new(new ConfigurationStore(directory), new AutostartService(), new WindowsWindowSystem(), _log);
            var window = new MainWindow(_viewModel);
            MainWindow = window;
            void ShowSettings() { window.Show(); if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal; window.Activate(); }
            _tray = new(_viewModel, ShowSettings, async () => { ShowSettings(); await _viewModel.RefreshForCaptureAsync(); }, () => Shutdown());
            if (e.Args.Contains("--startup", StringComparer.OrdinalIgnoreCase)) _ = _viewModel.RestoreOnStartupAsync();
            else { ShowSettings(); _ = _viewModel.RefreshForCaptureAsync(); }
            _log.Write("started", new { background = e.Args.Contains("--startup", StringComparer.OrdinalIgnoreCase) });
        }
        catch (Exception ex)
        {
            _log.Write("startup-failed", new { error = ex.Message });
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        _tray?.Dispose();
        _log?.Write("stopped", new { e.ApplicationExitCode });
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _installerMutex?.Dispose();
        base.OnExit(e);
    }
}
