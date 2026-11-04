using Laytivo.Core;

namespace Laytivo.Tests;

public sealed class MatchingTests
{
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
