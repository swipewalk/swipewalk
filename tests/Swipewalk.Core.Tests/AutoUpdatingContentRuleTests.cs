using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class AutoUpdatingContentRuleTests
{
    private static AccessibilityNode Text(string text) => new() { Role = "text", VisibleText = text, Bounds = new Bounds(0, 0, 100, 20) };

    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static ScreenSnapshot Snapshot(
        string text, IReadOnlyList<ScreenSnapshot>? autoUpdateCaptures = null, double? intervalSeconds = null,
        DateTimeOffset? capturedAt = null) => new()
    {
        Platform = Platform.Android,
        ScreenName = "Home",
        Root = new AccessibilityNode { Role = "window", Bounds = new Bounds(0, 0, 1080, 2400), Children = [Text(text)] },
        AutoUpdateCaptures = autoUpdateCaptures ?? [],
        AutoUpdateIntervalSeconds = intervalSeconds,
        CapturedAt = capturedAt ?? Epoch,
    };

    private static List<Finding> Evaluate(ScreenSnapshot snapshot) => new AutoUpdatingContentRule().Evaluate(snapshot).ToList();

    [Fact]
    public void NoAutoUpdateCaptures_NoFinding()
    {
        Assert.Empty(Evaluate(Snapshot("Static")));
    }

    [Fact]
    public void SustainedChange_NeedsReviewCiting2_2_2()
    {
        var snapshot = Snapshot("Slide 1",
            [
                Snapshot("Slide 2", capturedAt: Epoch.AddSeconds(3)),
                Snapshot("Slide 3", capturedAt: Epoch.AddSeconds(6)),
            ],
            intervalSeconds: 3);

        var findings = Evaluate(snapshot);

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.PauseStopHide], finding.Criteria);
        // Real elapsed time (from CapturedAt), not the requested interval multiplied by the capture count --
        // a full capture itself takes time on top of the wait, especially on iOS (see the rule's remarks).
        Assert.Contains("6", finding.Message);
        Assert.Contains("pause, stop or hide", finding.Message);
        Assert.Contains("essential", finding.Message);
        Assert.Contains("shown alongside other content", finding.Message);
        Assert.Contains("ChangedElements", finding.Details.Keys);
    }

    [Fact]
    public void OneOffChangeThatSettled_NoFinding()
    {
        // A spinner or a one-off load: changed once, then settled -- not sustained (see
        // AutoUpdateChangeDetectorTests for the detector-level version of this).
        var snapshot = Snapshot("Loading...", [Snapshot("Done"), Snapshot("Done")], intervalSeconds: 3);

        Assert.Empty(Evaluate(snapshot));
    }

    [Fact]
    public void NothingChanged_NoFinding()
    {
        var snapshot = Snapshot("Static", [Snapshot("Static"), Snapshot("Static")], intervalSeconds: 3);

        Assert.Empty(Evaluate(snapshot));
    }

    [Fact]
    public void OnlyOneExtraCapture_NoFinding()
    {
        // Can't tell a settled one-off change from sustained auto-updating with only one extra capture.
        var snapshot = Snapshot("Slide 1", [Snapshot("Slide 2")], intervalSeconds: 3);

        Assert.Empty(Evaluate(snapshot));
    }
}
