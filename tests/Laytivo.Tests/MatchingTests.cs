using Laytivo.Core;

namespace Laytivo.Tests;

public sealed class MatchingTests
{
    [Theory]
    [InlineData(@"C:\Users\Me\Discord\app-1.0.9266\Discord.exe", @"C:\Users\Me\Discord\app-1.0.9267\Discord.exe")]
    [InlineData(@"C:\Program Files\WindowsApps\OpenAI.Codex_26.1027.6604.0_x64__publisher\app\ChatGPT.exe", @"C:\Program Files\WindowsApps\OpenAI.Codex_26.1029.7803.0_x64__publisher\app\ChatGPT.exe")]
    public void UpdatedAppStillMatchesWithChangedTitle(string oldPath, string newPath)
    {
        var saved = TestData.Window(path: oldPath);
        Assert.Single(WindowMatchingService.Match([saved], [new(1, 1, saved with { ProcessPath = newPath, Title = "New title" })]));
    }

    [Theory]
    [InlineData(@"C:\Other\Discord\app-1.0.9267\Discord.exe")]
    [InlineData(@"C:\Users\Me\Other\app-1.0.9267\Discord.exe")]
    public void VersionMatchingKeepsInstallationRoot(string other)
    {
        var saved = TestData.Window(path: @"C:\Users\Me\Discord\app-1.0.9266\Discord.exe");
        Assert.Empty(WindowMatchingService.Match([saved], [new(1, 1, saved with { ProcessPath = other })]));
    }

    [Fact]
    public void StorePublisherRemainsPartOfIdentity()
    {
        var saved = TestData.Window(path: @"C:\Program Files\WindowsApps\App_1.2.3.4_x64__publisher1\App.exe");
        Assert.Empty(WindowMatchingService.Match([saved], [new(1, 1, saved with
        { ProcessPath = @"C:\Program Files\WindowsApps\App_2.3.4.5_x64__publisher2\App.exe" })]));
    }

    [Fact]
    public void ExactTitlesTakePriorityAndHandlesAreUsedOnce()
    {
        var a = TestData.Window("a", "A");
        var b = TestData.Window("b", "B");
        var result = WindowMatchingService.Match([a, b], [new(1, 1, b), new(2, 1, a)]);
        Assert.Equal((nint)2, result["a"].Handle);
        Assert.Equal((nint)1, result["b"].Handle);
    }

    [Fact]
    public void DocumentTitleCanChange()
    {
        var entry = TestData.Window();
        Assert.Single(WindowMatchingService.Match([entry], [new(1, 1, entry with { Title = "Changed document" })]));
    }

    [Fact]
    public void DifferentExecutableWithSameNameNeverMatches()
    {
        var entry = TestData.Window();
        Assert.Empty(WindowMatchingService.Match([entry], [new(1, 1, entry with { ProcessPath = @"D:\Other\Editor.exe" })]));
    }

    [Fact]
    public void InaccessiblePathRequiresTitleClassAndProcessName()
    {
        var entry = TestData.Window() with { ProcessPath = "" };
        Assert.Single(WindowMatchingService.Match([entry], [new(1, 1, entry)]));
        Assert.Empty(WindowMatchingService.Match([entry], [new(1, 1, entry with { Title = "Other" })]));
    }

    [Fact]
    public void DuplicateEntriesDoNotReuseAHandle()
    {
        var a = TestData.Window();
        Assert.Single(WindowMatchingService.Match([a, a with { Id = "two" }], [new(1, 1, a)]));
    }

    [Fact]
    public void WrongWindowClassIsNotReused()
    {
        var entry = TestData.Window();
        Assert.Empty(WindowMatchingService.Match([entry], [new(1, 1, entry with { WindowClass = "Splash" })]));
    }
}
