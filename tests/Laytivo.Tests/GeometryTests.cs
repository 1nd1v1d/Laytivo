using Laytivo.Core;

namespace Laytivo.Tests;

public sealed class GeometryTests
{
    [Fact]
    public void UnchangedDisplayKeepsOuterResizeBordersAndSmallWindowsExactly()
    {
        var monitor = TestData.Monitor();
        foreach (var rect in new DesktopRect[] { new(-8, 0, 1936, 1048), new(50, 50, 100, 80) })
            Assert.Equal(rect, Geometry.Adapt(rect, monitor, monitor));
    }

    [Fact]
    public void SnapVisibleFrameRemainsFlushWithoutClippingInvisibleResizeBorders()
    {
        var monitor = TestData.Monitor();
        var entry = TestData.Window() with
        {
            IsSnapped = true,
            NormalBounds = new(-8, 0, 976, 1048),
            VisibleBounds = new(0, 0, 960, 1040)
        };
        Assert.Equal(entry.NormalBounds, Geometry.AdaptWindow(entry, monitor, monitor));
    }

    [Fact]
    public void SnappedWindowFallbackKeepsVisibleFrameInsideTargetWorkArea()
    {
        var source = TestData.Monitor("missing", -1920);
        var target = TestData.Monitor(width: 1280, height: 800, dpi: 1.5);
        var entry = TestData.Window() with
        {
            IsSnapped = true,
            NormalBounds = new(-1928, 0, 976, 1048),
            VisibleBounds = new(-1920, 0, 960, 1040)
        };
        var result = Geometry.AdaptWindow(entry, source, target);
        Assert.Equal(-12, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(760, result.Height - 12);
        Assert.Equal(1280, result.Width - 24);
    }

    [Fact]
    public void UnchangedDisplayPreservesBounds()
    {
        var monitor = TestData.Monitor();
        var rect = new DesktopRect(120, 75, 800, 600);
        Assert.Equal(rect, Geometry.Adapt(rect, monitor, monitor));
    }

    [Fact]
    public void NegativeCoordinatesRemainOnLeftMonitor()
    {
        var monitor = TestData.Monitor("left", -1920, -400);
        var rect = new DesktopRect(-1800, -300, 900, 600);
        Assert.Equal(rect, Geometry.Adapt(rect, monitor, monitor));
    }

    [Fact]
    public void DpiChangeScalesSizeAndKeepsWindowVisible()
    {
        var result = Geometry.Adapt(new(1800, 900, 1000, 800), TestData.Monitor(),
            TestData.Monitor(width: 2560, height: 1440, dpi: 1.5));
        Assert.Equal(1500, result.Width);
        Assert.Equal(1200, result.Height);
        Assert.InRange(result.X, 0, 2560 - result.Width);
        Assert.InRange(result.Y, 0, 1400 - result.Height);
    }

    [Theory]
    [InlineData(6000, 6000, 5000, 4000)]
    [InlineData(-6000, -6000, 900, 600)]
    [InlineData(0, 0, 10, 10)]
    public void FallbackClampsToWorkArea(int x, int y, int width, int height)
    {
        var target = TestData.Monitor(width: 800, height: 600);
        var result = Geometry.Adapt(new(x, y, width, height), TestData.Monitor("missing", -1920), target);
        Assert.InRange(result.X, 0, 800 - result.Width);
        Assert.InRange(result.Y, 0, 560 - result.Height);
    }

    [Fact]
    public void MonitorIdentityWinsOverDeviceNameAndPrimary()
    {
        var source = TestData.Monitor("stable");
        var target = source with { DeviceName = "renumbered" };
        Assert.Same(target, Geometry.SelectMonitor(source, [TestData.Monitor(), target]));
    }

    [Fact]
    public void MissingMonitorFallsBackToPrimary()
    {
        var primary = TestData.Monitor();
        Assert.Same(primary, Geometry.SelectMonitor(TestData.Monitor("missing"),
            [TestData.Monitor("secondary", primary: false), primary]));
    }

    [Fact]
    public void DeviceNameIsSecondaryFallback()
    {
        var target = TestData.Monitor("new") with { DeviceName = "old-name" };
        Assert.Same(target, Geometry.SelectMonitor(TestData.Monitor("old") with { DeviceName = "old-name" }, [TestData.Monitor(), target]));
    }

    [Fact]
    public void NoMonitorIsReported()
        => Assert.Throws<InvalidOperationException>(() => Geometry.SelectMonitor(TestData.Monitor(), []));
}
