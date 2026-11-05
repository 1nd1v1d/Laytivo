using Laytivo.Core;

namespace Laytivo.Tests;

public sealed class RestoreTests
{
    [Fact]
    public async Task SnappedGeometryUsesVisibleFrameAndReportsPreservedNativeState()
    {
        var entry = TestData.Window() with
        {
            IsSnapped = true,
            NormalBounds = new(-8, 0, 976, 1048),
            VisibleBounds = new(0, 0, 960, 1040)
        };
        var windows = new FakeWindows { PlacementIsSnapped = true };
        windows.Live.Add(new(1, 1, entry));
        var report = await Service(windows, new()).RestoreAsync(TestData.Config(entry));
        Assert.Equal(RestoreOutcome.Restored, Assert.Single(report.Results).Outcome);
        Assert.Contains("Andockung erhalten", report.Results[0].Message);
        Assert.All(windows.Placements, p => Assert.Equal(entry.NormalBounds, p.Bounds));
    }

    [Fact]
    public async Task MissingNativeSnapIsReportedAsPartialWhileOtherWindowsContinue()
    {
        var snapped = TestData.Window() with { IsSnapped = true };
        var normal = TestData.Window("two", "Other");
        var windows = new FakeWindows();
        windows.Live.AddRange([new(1, 1, snapped), new(2, 2, normal)]);
        var report = await Service(windows, new()).RestoreAsync(TestData.Config(snapped, normal));
        Assert.Equal(RestoreOutcome.PartiallyRestored, report.Results[0].Outcome);
        Assert.Contains("konnte nicht neu erzeugt", report.Results[0].Message);
        Assert.Equal(RestoreOutcome.Restored, report.Results[1].Outcome);
        Assert.Contains("1 teilweise wiederhergestellt", report.Summary);
        Assert.Equal(4, windows.Placements.Count);
    }

    private static WorkspaceRestoreService Service(FakeWindows windows, FakeLauncher launcher) =>
        new(windows, launcher, new FakeLog(), new(TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(2)));

    [Fact]
    public async Task ExistingWindowsAreRestoredWithoutLaunchingAndRemainIdempotent()
    {
        var entry = TestData.Window();
        var windows = new FakeWindows(); windows.Live.Add(new(1, 1, entry));
        var launcher = new FakeLauncher();
        var service = Service(windows, launcher);
        var config = TestData.Config(entry);
        var first = await service.RestoreAsync(config);
        var second = await service.RestoreAsync(config);
        Assert.All(first.Results.Concat(second.Results), r => Assert.Equal(RestoreOutcome.Restored, r.Outcome));
        Assert.Empty(launcher.Launched);
        Assert.All(windows.Placements, p => Assert.Equal(entry.NormalBounds, p.Bounds));
        Assert.Equal(entry.NormalBounds, config.Workspace!.Windows[0].NormalBounds);
    }

    [Fact]
    public async Task DisabledLaunchSkipsMissingWindows()
    {
        var launcher = new FakeLauncher();
        var config = TestData.Config(TestData.Window());
        var report = await Service(new(), launcher).RestoreAsync(config with { Settings = config.Settings with { LaunchMissingApps = false } });
        Assert.Equal(RestoreOutcome.Skipped, Assert.Single(report.Results).Outcome);
        Assert.Empty(launcher.Launched);
    }

    [Fact]
    public async Task MissingExecutableDoesNotBlockOtherWindows()
    {
        var existing = TestData.Window();
        var windows = new FakeWindows(); windows.Live.Add(new(1, 1, existing));
        var launcher = new FakeLauncher { OnLaunch = _ => throw new FileNotFoundException("EXE fehlt") };
        var report = await Service(windows, launcher).RestoreAsync(TestData.Config(existing,
            TestData.Window("missing", path: @"C:\Missing.exe")));
        Assert.Equal(RestoreOutcome.Restored, report.Results[0].Outcome);
        Assert.Equal(RestoreOutcome.Failed, report.Results[1].Outcome);
    }

    [Fact]
    public async Task DuplicateAppEntriesLaunchOnlyOnceAndTimeoutIsBounded()
    {
        var launcher = new FakeLauncher();
        var report = await Service(new(), launcher).RestoreAsync(TestData.Config(TestData.Window(), TestData.Window("two", "Other")));
        Assert.Single(launcher.Launched);
        Assert.Equal(2, report.Results.Count);
        Assert.All(report.Results, r => Assert.Equal(RestoreOutcome.Failed, r.Outcome));
    }

    [Fact]
    public async Task ExplicitDocumentArgumentsAllowSeparateStarts()
    {
        var launcher = new FakeLauncher();
        await Service(new(), launcher).RestoreAsync(TestData.Config(
            TestData.Window() with { LaunchArguments = "--profile A" }, TestData.Window("two") with { LaunchArguments = "--profile a" }));
        Assert.Equal(2, launcher.Launched.Count);
    }

    [Fact]
    public async Task LateWindowIsFoundAndPlaced()
    {
        var entry = TestData.Window();
        var windows = new FakeWindows();
        var launcher = new FakeLauncher { OnLaunch = _ => windows.Live.Add(new(2, 2, entry)) };
        var report = await Service(windows, launcher).RestoreAsync(TestData.Config(entry));
        Assert.Equal(RestoreOutcome.Restored, Assert.Single(report.Results).Outcome);
        Assert.NotEmpty(windows.Placements);
        Assert.All(windows.Placements, placement =>
        {
            Assert.Equal((nint)2, placement.Handle);
            Assert.Equal(entry.NormalBounds, placement.Bounds);
        });
    }

    [Fact]
    public async Task ExistingAppDoesNotStartAgainForAnUnmatchedSecondWindow()
    {
        var entry = TestData.Window();
        var windows = new FakeWindows(); windows.Live.Add(new(1, 1, entry));
        var launcher = new FakeLauncher();
        var report = await Service(windows, launcher).RestoreAsync(TestData.Config(entry, TestData.Window("two", "Other")));
        Assert.Empty(launcher.Launched);
        Assert.Equal(RestoreOutcome.Restored, report.Results[0].Outcome);
        Assert.Equal(RestoreOutcome.Failed, report.Results[1].Outcome);
    }

    [Fact]
    public async Task ConcurrentRestoreIsRejectedAndGateIsReleased()
    {
        var service = Service(new(), new());
        var config = TestData.Config(TestData.Window());
        var first = service.RestoreAsync(config);
        var second = await service.RestoreAsync(config);
        Assert.True(second.AlreadyRunning);
        await first;
        Assert.False((await service.RestoreAsync(AppConfiguration.Empty)).AlreadyRunning);
    }

    [Fact]
    public async Task CancellationReleasesGate()
    {
        var service = Service(new(), new());
        using var cts = new CancellationTokenSource();
        var restore = service.RestoreAsync(TestData.Config(TestData.Window()), cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => restore);
        Assert.False((await service.RestoreAsync(AppConfiguration.Empty)).AlreadyRunning);
    }

    [Fact]
    public async Task PlacementFailureDoesNotStopOtherWindowsAndMaximizedStateIsPreserved()
    {
        var a = TestData.Window();
        var b = TestData.Window("two", "Other") with { State = SavedWindowState.Maximized };
        var windows = new FakeWindows { FailHandle = 1 };
        windows.Live.AddRange([new(1, 1, a), new(2, 1, b)]);
        var report = await Service(windows, new()).RestoreAsync(TestData.Config(a, b));
        Assert.Equal(RestoreOutcome.Failed, report.Results[0].Outcome);
        Assert.Equal(RestoreOutcome.Restored, report.Results[1].Outcome);
        Assert.All(windows.Placements, p => Assert.Equal(SavedWindowState.Maximized, p.State));
    }

    [Fact]
    public async Task MissingMonitorFallbackDoesNotMutateProfile()
    {
        var entry = TestData.Window() with { NormalBounds = new(5000, 5000, 3000, 2000) };
        var windows = new FakeWindows { Monitors = [TestData.Monitor("fallback", width: 800, height: 600)] };
        windows.Live.Add(new(1, 1, entry));
        var config = TestData.Config(entry);
        await Service(windows, new()).RestoreAsync(config);
        Assert.Equal(entry, config.Workspace!.Windows[0]);
        Assert.All(windows.Placements, p => Assert.InRange(p.Bounds.X + p.Bounds.Width, 1, 800));
    }

    [Fact]
    public async Task EmptyProfileNeverStartsPrograms()
    {
        var launcher = new FakeLauncher();
        Assert.Empty((await Service(new(), launcher).RestoreAsync(AppConfiguration.Empty)).Results);
        Assert.Empty(launcher.Launched);
    }
}
