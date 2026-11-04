using System.Drawing;
using System.Windows.Forms;

namespace Laytivo.App;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _trayIcon;
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripMenuItem _restore, _save, _startup, _launch;
    private readonly MainViewModel _viewModel;

    public TrayService(MainViewModel viewModel, Action showSettings, Func<Task> showCapture, Action exit)
    {
        _viewModel = viewModel;
        _restore = new("Gespeicherte Anordnung wiederherstellen", null, async (_, _) => await viewModel.RestoreAsync());
        _save = new("Aktuelle Anordnung speichern", null, async (_, _) => await showCapture());
        _startup = new("Mit Windows starten", null, (_, _) => viewModel.ToggleAutostart());
        _launch = new("Programme automatisch starten", null, (_, _) => viewModel.ToggleLaunch());
        _menu.Items.AddRange([new ToolStripMenuItem("Laytivo") { Enabled = false }, new ToolStripSeparator(),
            _restore, _save, new ToolStripMenuItem("Einstellungen öffnen", null, (_, _) => showSettings()),
            new ToolStripSeparator(), _startup, _launch, new ToolStripSeparator(),
            new ToolStripMenuItem("Laytivo beenden", null, (_, _) => exit())]);
        using var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Laytivo.ico")).Stream;
        using var sourceIcon = new Icon(stream, SystemInformation.SmallIconSize);
        _trayIcon = (Icon)sourceIcon.Clone();
        _icon = new NotifyIcon { Text = "Laytivo", Icon = _trayIcon, ContextMenuStrip = _menu, Visible = true };
        _icon.DoubleClick += (_, _) => showSettings();
        viewModel.StateChanged += OnStateChanged;
        _menu.Opening += (_, _) => Update();
        Update();
    }

    private void OnStateChanged(object? sender, EventArgs args) => Update();
    private void Update()
    {
        _restore.Enabled = _viewModel.RestoreCommand.CanExecute(null);
        _save.Enabled = !_viewModel.Busy && !_viewModel.ConfigurationBlocked;
        _startup.Enabled = _launch.Enabled = _save.Enabled;
        _startup.Checked = _viewModel.EffectiveSettings.StartWithWindows;
        _launch.Checked = _viewModel.EffectiveSettings.LaunchMissingApps;
    }
    public void Dispose()
    {
        _viewModel.StateChanged -= OnStateChanged;
        _icon.Visible = false;
        _icon.Dispose();
        _trayIcon.Dispose();
        _menu.Dispose();
    }
}
