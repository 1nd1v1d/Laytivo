using Laytivo.Infrastructure;
using Microsoft.Win32;

namespace Laytivo.Infrastructure.Tests;

public sealed class AutostartTests : IDisposable
{
    private readonly string _root = @"Software\Laytivo.Tests\" + Guid.NewGuid().ToString("N");
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Laytivo.Autostart.Tests-" + Guid.NewGuid().ToString("N"));
    private string Executable => Path.Combine(_directory, "App Space", "Laytivo.exe");
    private string Shortcut => Path.Combine(_directory, "Startup", "Laytivo.lnk");
    private AutostartService Service => new(_root + @"\Run", _root + @"\Approved", Path.GetDirectoryName(Shortcut)!, Executable);
    public AutostartTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Executable)!);
        File.WriteAllText(Executable, "Test fixture; never executed.");
    }

    [Fact]
    public void ShortcutPersistsWithoutRunEntryAndCanBeDisabled()
    {
        Service.SetEnabled(true);
        Assert.True(File.Exists(Shortcut));
        Assert.True(Service.IsEnabled()); // a new service reads the saved shortcut
        using var run = Registry.CurrentUser.OpenSubKey(_root + @"\Run");
        Assert.Null(run?.GetValue("Laytivo"));
        Service.SetEnabled(false);
        Assert.False(File.Exists(Shortcut));
        Assert.False(Service.IsEnabled());
    }

    [Fact]
    public void WindowsDisabledShortcutIsRespectedAndExplicitEnableRepairsIt()
    {
        Service.SetEnabled(true);
        using var approved = Registry.CurrentUser.CreateSubKey(_root + @"\Approved\StartupFolder", true);
        approved.SetValue("Laytivo.lnk", new byte[] { 3, 0, 0, 0 }, RegistryValueKind.Binary);
        Assert.False(Service.IsEnabled());
        Assert.False(Service.MigrateLegacyEntry());
        Service.SetEnabled(true);
        Assert.True(Service.IsEnabled());
    }

    [Fact]
    public void EnabledLegacyEntryMigratesOnceAfterShortcutIsSaved()
    {
        using var run = Registry.CurrentUser.CreateSubKey(_root + @"\Run", true);
        run.SetValue("Laytivo", $"\"{Executable}\" --startup");
        Assert.True(Service.IsEnabled());
        Assert.True(Service.MigrateLegacyEntry());
        Assert.True(File.Exists(Shortcut));
        Assert.Null(run.GetValue("Laytivo"));
        Assert.True(Service.IsEnabled());
        Assert.False(Service.MigrateLegacyEntry());
    }

    [Fact]
    public void DisabledLegacyEntryIsNotAutomaticallyReenabled()
    {
        using var run = Registry.CurrentUser.CreateSubKey(_root + @"\Run", true);
        run.SetValue("Laytivo", $"\"{Executable}\" --startup");
        using var approved = Registry.CurrentUser.CreateSubKey(_root + @"\Approved\Run", true);
        approved.SetValue("Laytivo", new byte[] { 3, 0, 0, 0 }, RegistryValueKind.Binary);
        Assert.False(Service.MigrateLegacyEntry());
        Assert.False(Service.IsEnabled());
        Assert.False(File.Exists(Shortcut));
        Service.SetEnabled(true);
        Assert.True(Service.IsEnabled());
        Assert.Null(run.GetValue("Laytivo"));
    }

    [Fact]
    public void DisablingAnotherCopyPreservesForeignShortcutAndRunEntry()
    {
        Service.SetEnabled(true);
        var other = new AutostartService(_root + @"\Run", _root + @"\Approved", Path.GetDirectoryName(Shortcut)!, @"C:\Other\Laytivo.exe");
        using var run = Registry.CurrentUser.CreateSubKey(_root + @"\Run", true);
        run.SetValue("Laytivo", $"\"{Executable}\" --startup");
        other.SetEnabled(false);
        Assert.True(Service.IsEnabled());
        Assert.NotNull(run.GetValue("Laytivo"));
        Assert.False(other.IsEnabled());
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_root, false);
        var path = Path.GetFullPath(_directory);
        if (!path.StartsWith(Path.Combine(Path.GetTempPath(), "Laytivo.Autostart.Tests-"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ungültiges Testverzeichnis.");
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
