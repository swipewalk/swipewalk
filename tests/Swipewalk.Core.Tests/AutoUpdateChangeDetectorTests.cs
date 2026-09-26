using Swipewalk.Core.Model;

namespace Swipewalk.Core.Tests;

public class AutoUpdateChangeDetectorTests
{
    private static AccessibilityNode Text(string text, double x = 0) =>
        new() { Role = "text", VisibleText = text, Bounds = new Bounds(x, 0, 100, 20) };

    private static ScreenSnapshot Snapshot(AccessibilityNode child) => new()
    {
        Platform = Platform.Android,
        ScreenName = "Home",
        Root = new AccessibilityNode { Role = "window", Bounds = new Bounds(0, 0, 1080, 2400), Children = [child] },
    };

    [Fact]
    public void Diff_NoChange_Empty()
    {
        var before = Snapshot(Text("Slide 1"));
        var after = Snapshot(Text("Slide 1"));

        Assert.Empty(AutoUpdateChangeDetector.Diff(before, after));
    }

    [Fact]
    public void Diff_TextChanged_ReportedWithBeforeAndAfter()
    {
        var before = Snapshot(Text("Slide 1"));
        var after = Snapshot(Text("Slide 2"));

        var diff = Assert.Single(AutoUpdateChangeDetector.Diff(before, after));
        Assert.Contains("Slide 1", diff.What);
        Assert.Contains("Slide 2", diff.What);
    }

    [Fact]
    public void Diff_ElementDisappeared_Reported()
    {
        var before = Snapshot(Text("Banner"));
        var after = new ScreenSnapshot { Platform = Platform.Android, ScreenName = "Home", Root = new AccessibilityNode { Role = "window", Bounds = new Bounds(0, 0, 1080, 2400) } };

        var diff = Assert.Single(AutoUpdateChangeDetector.Diff(before, after));
        Assert.Equal("disappeared", diff.What);
    }

    [Fact]
    public void Diff_EmptyContainerAppearing_NotReported()
    {
        // A purely structural node with no visible text, value or label (most layout containers) shouldn't
        // register as a change just because its path shifted -- only content matters here.
        var before = Snapshot(Text("Slide 1"));
        var withEmptyContainer = new ScreenSnapshot
        {
            Platform = Platform.Android,
            ScreenName = "Home",
            Root = new AccessibilityNode
            {
                Role = "window",
                Bounds = new Bounds(0, 0, 1080, 2400),
                Children = [Text("Slide 1"), new AccessibilityNode { Role = "container" }],
            },
        };

        Assert.Empty(AutoUpdateChangeDetector.Diff(before, withEmptyContainer));
    }

    [Fact]
    public void Diff_TinyBoundsJitter_NotReported()
    {
        var before = Snapshot(Text("Slide 1", x: 0));
        var after = Snapshot(Text("Slide 1", x: 0.5)); // sub-pixel measurement noise

        Assert.Empty(AutoUpdateChangeDetector.Diff(before, after));
    }

    [Fact]
    public void Diff_RealMove_Reported()
    {
        var before = Snapshot(Text("Ticker", x: 0));
        var after = Snapshot(Text("Ticker", x: 200)); // scrolled well past the jitter threshold

        var diff = Assert.Single(AutoUpdateChangeDetector.Diff(before, after));
        Assert.Equal("moved", diff.What);
    }

    [Fact]
    public void IsSustained_FewerThanTwoExtraCaptures_NeverTrue()
    {
        var primary = Snapshot(Text("Slide 1"));
        var oneExtra = new[] { Snapshot(Text("Slide 2")) };

        Assert.False(AutoUpdateChangeDetector.IsSustained(primary, oneExtra, out var evidence));
        Assert.Empty(evidence);
    }

    [Fact]
    public void IsSustained_ChangedInBothConsecutiveWindows_True()
    {
        // A carousel: slide 1 -> slide 2 -> slide 3, still changing after the first interval.
        var primary = Snapshot(Text("Slide 1"));
        var captures = new[] { Snapshot(Text("Slide 2")), Snapshot(Text("Slide 3")) };

        Assert.True(AutoUpdateChangeDetector.IsSustained(primary, captures, out var evidence));
        Assert.NotEmpty(evidence);
    }

    [Fact]
    public void IsSustained_ChangedOnceThenSettled_False()
    {
        // A spinner: appears then disappears within the first interval, then nothing changes -- e.g. a
        // one-off load that finished, not sustained auto-updating content.
        var primary = Snapshot(Text("Loading..."));
        var captures = new[] { Snapshot(Text("Done")), Snapshot(Text("Done")) };

        Assert.False(AutoUpdateChangeDetector.IsSustained(primary, captures, out var evidence));
        Assert.Empty(evidence);
    }

    [Fact]
    public void IsSustained_NothingChanged_False()
    {
        var primary = Snapshot(Text("Static"));
        var captures = new[] { Snapshot(Text("Static")), Snapshot(Text("Static")) };

        Assert.False(AutoUpdateChangeDetector.IsSustained(primary, captures, out var evidence));
        Assert.Empty(evidence);
    }
}
