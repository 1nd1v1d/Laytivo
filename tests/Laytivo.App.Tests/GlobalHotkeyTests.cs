using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Laytivo.App;
using Laytivo.Core;

namespace Laytivo.App.Tests;

public sealed class GlobalHotkeyTests
{
    [Fact]
    public void RegistrationIsExclusiveAndDisposeReleasesCombination() => OnSta(() =>
    {
        using var first = new GlobalHotkeyService();
        using var second = new GlobalHotkeyService();
        var settings = RegisterAvailable(first);
        Assert.True(first.IsRegistered);
        Assert.Throws<Win32Exception>(() => second.Configure(settings));
        first.Dispose();
        second.Configure(settings);
        Assert.True(second.IsRegistered);
    });

    [Fact]
    public void FailedChangeKeepsOldShortcutAndSuccessfulChangeReleasesIt() => OnSta(() =>
    {
        using var first = new GlobalHotkeyService();
        using var occupied = new GlobalHotkeyService();
        using var probe = new GlobalHotkeyService();
        var old = RegisterAvailable(first);
        var next = RegisterAvailable(occupied);
        Assert.Throws<Win32Exception>(() => first.Configure(next));
        Assert.Throws<Win32Exception>(() => probe.Configure(old));
        occupied.Dispose();
        first.Configure(next);
        probe.Configure(old);
        Assert.True(first.IsRegistered);
        Assert.True(probe.IsRegistered);
    });

    [Fact]
    public void NativeMessagesDispatchOnlyActiveCombinationAndDisabledShortcutIsIgnored() => OnSta(() =>
    {
        using var service = new GlobalHotkeyService();
        var settings = RegisterAvailable(service);
        var count = 0;
        service.Pressed += (_, _) => count++;
        var id = service.RegistrationId!.Value;
        var combination = (nint)(settings.RestoreHotkeyKey << 16 | settings.RestoreHotkeyModifiers);
        SendMessage(service.WindowHandle, 0x0312, id, combination);
        Assert.Equal(1, count);
        SendMessage(service.WindowHandle, 0x0312, id, combination | 0x4000);
        Assert.Equal(2, count);
        SendMessage(service.WindowHandle, 0x0312, id, (nint)(0x7b << 16 | settings.RestoreHotkeyModifiers));
        Assert.Equal(2, count);
        service.Configure(settings with { RestoreHotkeyEnabled = false });
        SendMessage(service.WindowHandle, 0x0312, id, combination);
        Assert.False(service.IsRegistered);
        Assert.Equal(2, count);
    });

    private static AppSettings RegisterAvailable(GlobalHotkeyService service)
    {
        foreach (var key in RestoreHotkey.Keys.Reverse())
        {
            var settings = new AppSettings(RestoreHotkeyModifiers: 7, RestoreHotkeyKey: key.Value);
            try { service.Configure(settings); return settings; }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1409) { }
        }
        throw new InvalidOperationException("Keine freie Tastenkombination für den nativen Hotkey-Test.");
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "STA-Hotkey-Test hat das Zeitlimit überschritten.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);
}
