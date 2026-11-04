using Laytivo.Core;

namespace Laytivo.Tests;

public sealed class HotkeyConfigurationTests
{
    [Fact]
    public void DefaultShortcutIsEnabledControlAltR()
    {
        var settings = AppConfiguration.Empty.Settings;
        Assert.True(settings.RestoreHotkeyEnabled);
        Assert.Equal("Strg + Alt + R", RestoreHotkey.Describe(settings));
    }

    [Theory]
    [InlineData(0, 0x52)]
    [InlineData(8, 0x52)]
    [InlineData(3, 0x7b)]
    [InlineData(3, 0x0d)]
    public void InvalidOrReservedShortcutsCannotBePersisted(int modifiers, int key)
        => Assert.Throws<InvalidDataException>(() => ConfigurationValidator.Validate(new(1,
            new(RestoreHotkeyModifiers: modifiers, RestoreHotkeyKey: key), null)));
}
