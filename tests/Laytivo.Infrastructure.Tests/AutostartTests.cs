using Laytivo.Infrastructure;
using Microsoft.Win32;

namespace Laytivo.Infrastructure.Tests;

public sealed class AutostartTests : IDisposable
{
    private readonly string _root = @"Software\Laytivo.Tests\" + Guid.NewGuid().ToString("N");
    [Fact]
    public void DisabledWindowsEntryIsReportedAndExplicitEnableRepairsIt()
    {
        var service = new AutostartService(_root + @"\Run", _root + @"\Approved", @"C:\App Space\Laytivo.exe");
        service.SetEnabled(true);
        Assert.True(service.IsEnabled());
        using var run = Registry.CurrentUser.OpenSubKey(_root + @"\Run");
        Assert.Equal("\"C:\\App Space\\Laytivo.exe\" --startup", run!.GetValue("Laytivo"));
        using var approved = Registry.CurrentUser.CreateSubKey(_root + @"\Approved", true);
        approved.SetValue("Laytivo", new byte[] { 3, 0, 0, 0 }, RegistryValueKind.Binary);
        Assert.False(service.IsEnabled());
        service.SetEnabled(true);
        Assert.True(service.IsEnabled());
        service.SetEnabled(false);
        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void TooLongCommandDoesNotLeaveBrokenAutostartEntry()
    {
        var service = new AutostartService(_root + @"\Run", _root + @"\Approved", @"C:\" + new string('a', 260) + @"\Laytivo.exe");
        Assert.Throws<InvalidOperationException>(() => service.SetEnabled(true));
        Assert.False(service.IsEnabled());
    }

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_root, false);
}
