using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class TargetSizeRuleTests
{
    private static AccessibilityNode Target(
        double x, double y, double w, double h,
        bool isAccessible = true, bool isEnabled = true) => new()
    {
        Role = "button",
        IsInteractive = true,
        IsAccessible = isAccessible,
        IsEnabled = isEnabled,
        Bounds = new Bounds(x, y, w, h),
    };

    private static ScreenSnapshot Snapshot(Platform platform, params AccessibilityNode[] targets) => new()
    {
        Platform = platform,
        ScreenName = "Screen",
        Root = new AccessibilityNode { Role = "window", Children = targets },
    };

    private static List<Finding> Evaluate(ScreenSnapshot snapshot) => new TargetSizeRule().Evaluate(snapshot).ToList();

    [Fact]
    public void TwoAdjacentUndersizedTargets_BothReportWcagIssue()
    {
        // Two 20x20 targets touching edge-to-edge: a 24-diameter circle centered on either
        // overlaps the other, so the spacing exception does not apply.
        var a = Target(0, 0, 20, 20);
        var b = Target(20, 0, 20, 20);

        var findings = Evaluate(Snapshot(Platform.Android, a, b));

        Assert.Equal(2, findings.Count);
        Assert.All(findings, f =>
        {
            Assert.Equal(FindingKind.WcagIssue, f.Kind);
            Assert.Equal([WcagCriteria.TargetSizeMinimum], f.Criteria);
        });
    }

    [Fact]
    public void IsolatedUndersizedTarget_MeetsSpacingException_NoWcagIssue()
    {
        var lonely = Target(500, 500, 20, 20);

        var findings = Evaluate(Snapshot(Platform.Android, lonely));

        Assert.DoesNotContain(findings, f => f.Kind == FindingKind.WcagIssue);
        // Still below the Android platform guideline, reported separately as an advisory.
        Assert.Single(findings);
        Assert.Equal(FindingKind.PlatformAdvisory, findings[0].Kind);
    }

    [Fact]
    public void Android_44x44_ReportsPlatformAdvisoryOnly()
    {
        var target = Target(0, 0, 44, 44);

        var findings = Evaluate(Snapshot(Platform.Android, target));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.PlatformAdvisory, finding.Kind);
        Assert.Empty(finding.Criteria);
        Assert.Contains("48", finding.PlatformGuideline);
    }

    [Fact]
    public void iOS_44x44_ReportsNothing()
    {
        var target = Target(0, 0, 44, 44);

        var findings = Evaluate(Snapshot(Platform.iOS, target));

        Assert.Empty(findings);
    }

    [Fact]
    public void Android_48x48_ReportsNothing()
    {
        var target = Target(0, 0, 48, 48);

        var findings = Evaluate(Snapshot(Platform.Android, target));

        Assert.Empty(findings);
    }

    [Fact]
    public void UndersizedTarget_WithNoOwnLabel_UsesChildAccessibleNameAsFindingLabel()
    {
        // The icon-picker buttons in DeveloperBalance carry no content-desc themselves, but a
        // non-focusable child does (e.g. "Trophy Icon"); the finding should show that name instead of
        // leaving the label empty, matching what a screen reader announces for the button.
        var icon = new AccessibilityNode
        {
            Role = "group",
            IsInteractive = false,
            Label = "Trophy Icon",
            Bounds = new Bounds(2, 2, 20, 20),
        };
        var button = new AccessibilityNode
        {
            Role = "button",
            IsInteractive = true,
            Bounds = new Bounds(0, 0, 20, 20),
            Children = [icon],
        };

        var findings = Evaluate(Snapshot(Platform.Android, button));

        var finding = Assert.Single(findings);
        Assert.Equal("Trophy Icon", finding.Label);
    }

    [Fact]
    public void SmallTargetInsideLargerClickableContainer_NeedsReview()
    {
        // A clickable row (300x60) containing a 20x20 button: a near miss activates the row, which is
        // what 2.5.8 is about, but whether that is a failure needs a person to judge.
        var container = Target(0, 0, 300, 60);
        var inner = Target(10, 10, 20, 20);

        var findings = Evaluate(Snapshot(Platform.Android, container, inner));

        Assert.DoesNotContain(findings, f => f.Kind == FindingKind.WcagIssue);
        var review = Assert.Single(findings, f => f.Kind == FindingKind.NeedsReview);
        Assert.Equal([WcagCriteria.TargetSizeMinimum], review.Criteria);
    }

    [Fact]
    public void HiddenFromAccessibilityTree_IsExcludedFromEvaluation()
    {
        var hidden = Target(0, 0, 20, 20, isAccessible: false);

        var findings = Evaluate(Snapshot(Platform.Android, hidden));

        Assert.Empty(findings);
    }

    [Fact]
    public void DisabledUndersizedIsolatedTarget_IsStillEvaluated()
    {
        // The rule does not currently filter by IsEnabled; documents current behavior rather
        // than asserting it is the ideal outcome.
        var disabled = Target(500, 500, 20, 20, isEnabled: false);

        var findings = Evaluate(Snapshot(Platform.Android, disabled));

        Assert.DoesNotContain(findings, f => f.Kind == FindingKind.WcagIssue);
        Assert.Single(findings);
        Assert.Equal(FindingKind.PlatformAdvisory, findings[0].Kind);
    }

    [Fact]
    public void ZeroSizeBounds_HasNoArea_IsExcluded()
    {
        var zeroSize = Target(0, 0, 0, 0);

        var findings = Evaluate(Snapshot(Platform.Android, zeroSize));

        Assert.Empty(findings);
    }
}
