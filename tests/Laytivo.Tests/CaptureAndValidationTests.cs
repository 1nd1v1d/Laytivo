using Laytivo.Core;

namespace Laytivo.Tests;

public sealed class CaptureAndValidationTests
{
    [Fact]
    public void InvalidSnapStateAndVisibleFrameAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => ConfigurationValidator.Validate(TestData.Config(
            TestData.Window() with { IsSnapped = true, State = SavedWindowState.Maximized })));
        Assert.Throws<InvalidDataException>(() => ConfigurationValidator.Validate(TestData.Config(
            TestData.Window() with { IsSnapped = true, VisibleBounds = new(0, 0, 2000, 2000) })));
    }

    [Fact]
    public void CaptureIncludesOnlySelectedWindowsAndFreshGeometry()
    {
        var entry = TestData.Window();
        var live = new LiveWindow(1, 10, entry);
        var windows = new FakeWindows();
        windows.Live.AddRange([live with { Window = entry with { NormalBounds = new(20, 30, 400, 300) } }, new(2, 20, TestData.Window("two"))]);
        var workspace = new WorkspaceCaptureService(windows).Capture([new(live, "document.txt", @"C:\Docs")]);
        var saved = Assert.Single(workspace.Windows);
        Assert.Equal(new DesktopRect(20, 30, 400, 300), saved.NormalBounds);
        Assert.Equal("document.txt", saved.LaunchArguments);
        Assert.Equal(@"C:\Docs", saved.WorkingDirectory);
    }

    [Fact]
    public void ClosedOrReusedHandleCannotBeSilentlySaved()
    {
        var window = new LiveWindow(1, 10, TestData.Window());
        var windows = new FakeWindows();
        var service = new WorkspaceCaptureService(windows);
        Assert.Throws<InvalidOperationException>(() => service.Capture([new(window, "", "")]));
        windows.Live.Add(window with { ProcessId = 99 });
        Assert.Throws<InvalidOperationException>(() => service.Capture([new(window, "", "")]));
    }

    [Fact]
    public void EmptySelectionCannotOverwriteProfile()
        => Assert.Throws<InvalidOperationException>(() => new WorkspaceCaptureService(new FakeWindows()).Capture([]));

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(99)]
    public void UnsupportedSchemaIsRejectedWithoutGuessingMigration(int version)
        => Assert.Throws<InvalidDataException>(() => ConfigurationValidator.Validate(AppConfiguration.Empty with { SchemaVersion = version }));

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(20)]
    public void UnsupportedDelayIsRejected(int delay)
        => Assert.Throws<InvalidDataException>(() => ConfigurationValidator.Validate(new(1, new(StartupDelaySeconds: delay), null)));

    [Fact]
    public void InvalidGeometryAndDuplicateIdsAreRejected()
    {
        var entry = TestData.Window();
        Assert.Throws<InvalidDataException>(() => ConfigurationValidator.Validate(TestData.Config(entry, entry)));
        Assert.Throws<InvalidDataException>(() => ConfigurationValidator.Validate(TestData.Config(entry with { NormalBounds = new(0, 0, -1, 100) })));
        Assert.Throws<InvalidDataException>(() => ConfigurationValidator.Validate(TestData.Config(entry with { MonitorId = "unknown" })));
    }
}
