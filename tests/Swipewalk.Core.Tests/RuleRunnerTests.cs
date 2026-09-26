using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class RuleRunnerTests
{
    private static readonly ScreenSnapshot Snapshot = new()
    {
        Platform = Platform.Android,
        ScreenName = "Checkout",
        ScreenshotPath = "checkout.png",
        Root = new AccessibilityNode { Role = "window", Children = [new AccessibilityNode { Role = "button" }] },
    };

    private sealed class FakeRule(string id, Func<ScreenSnapshot, IEnumerable<Finding>> evaluate) : IRule
    {
        public string Id => id;
        public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot) => evaluate(snapshot);
    }

    private static Finding ButtonFinding(FindingKind kind, params WcagCriterion[] criteria) => new()
    {
        RuleId = "fake",
        Kind = kind,
        Message = "Automated check found an issue.",
        Criteria = criteria,
        PlatformGuideline = kind == FindingKind.PlatformAdvisory ? "Android: touch targets at least 48×48 dp" : null,
        NodePath = "0",
        Role = "button",
    };

    [Fact]
    public void Run_CollectsFindingsFromAllRules_AndCopiesScreenInfo()
    {
        var runner = new RuleRunner(
        [
            new FakeRule("a", _ => [ButtonFinding(FindingKind.WcagIssue, WcagCriteria.NameRoleValue)]),
            new FakeRule("b", _ => [ButtonFinding(FindingKind.PlatformAdvisory)]),
            new FakeRule("c", _ => []),
        ]);

        var result = runner.Run(Snapshot);

        Assert.Equal(Platform.Android, result.Platform);
        Assert.Equal("Checkout", result.ScreenName);
        Assert.Equal("checkout.png", result.ScreenshotPath);
        Assert.Equal([FindingKind.WcagIssue, FindingKind.PlatformAdvisory], result.Findings.Select(f => f.Kind));
    }

    [Fact]
    public void Run_Throws_WhenWcagFindingHasNoCriterion()
    {
        var runner = new RuleRunner([new FakeRule("bad", _ => [ButtonFinding(FindingKind.WcagIssue)])]);

        var ex = Assert.Throws<InvalidOperationException>(() => runner.Run(Snapshot));
        Assert.Contains("bad", ex.Message);
    }

    [Fact]
    public void Run_Throws_WhenAdvisoryCitesWcagCriterion()
    {
        var runner = new RuleRunner(
            [new FakeRule("mixed", _ => [ButtonFinding(FindingKind.PlatformAdvisory, WcagCriteria.TargetSizeMinimum)])]);

        Assert.Throws<InvalidOperationException>(() => runner.Run(Snapshot));
    }

    // --- label-in-name / screen-reader-label-in-name: the two rules must never both report a finding for
    // the same 2.5.3 concern on the same node -- ScreenReaderLabelInNameRule defers to LabelInNameRule
    // (skips) rather than duplicating it, since only one of the two is reporting the tree-based concern. ---

    private static ScreenReaderCapture Capture(string spokenText, string path) => new(
        ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow,
        [new ScreenReaderCaptureItem(1, spokenText, null, null, null, null, null, null, null, path, MatchConfidence.Exact)],
        true, null);

    [Fact]
    public void ScreenReaderLabelInName_DefersToLabelInNameRule_OnTheSameNode()
    {
        // Visible text and label both on the node itself: LabelInNameRule already reports this exact 2.5.3
        // concern from the tree alone, so ScreenReaderLabelInNameRule skips it here (own Source, no merge,
        // no second finding) rather than reporting the same thing twice from different evidence.
        var button = new AccessibilityNode
        {
            Role = "button", IsInteractive = true, IsAccessible = true, VisibleText = "Pay", Label = "Submit",
            Bounds = new Bounds(0, 0, 100, 40),
        };
        var snapshot = new ScreenSnapshot
        {
            Platform = Platform.Android,
            ScreenName = "Screen",
            Root = new AccessibilityNode { Role = "window", Children = [button] },
            ScreenReaderCapture = Capture("Submit, Button", "0"),
        };
        var runner = new RuleRunner([new LabelInNameRule(), new ScreenReaderLabelInNameRule()]);

        var findings = runner.Run(snapshot).Findings;

        var finding = Assert.Single(findings, f => f.Criteria.Contains(WcagCriteria.LabelInName));
        Assert.Equal(FindingKind.WcagIssue, finding.Kind); // the tree-based finding, and only that one
        Assert.Equal(Finding.DefaultSource, finding.Source);
        Assert.Empty(finding.AlsoReportedBy);
    }

    [Fact]
    public void ScreenReaderLabelInName_ReportsIndependently_WhenLabelInNameNeverFiresThere()
    {
        // LabelInNameRule requires node.VisibleText, which is null here (the node's own text is null; its
        // visible text is only on a child -- the shape samples/NativeAndroid's Compose bug N5 had before
        // UiAutomatorParser.TryMergeDescendantName started merging it, 2026-09-26; see docs/case-study.md):
        // LabelInNameRule never runs on this node either way, so there is no duplicate to defer to, and
        // ScreenReaderLabelInNameRule's finding stands alone.
        var button = new AccessibilityNode
        {
            Role = "button", IsInteractive = true, IsAccessible = true, VisibleText = null, Label = null,
            Bounds = new Bounds(0, 0, 100, 40),
            Children = [new AccessibilityNode { Role = "text", VisibleText = "Pay", Bounds = new Bounds(0, 0, 50, 20) }],
        };
        var snapshot = new ScreenSnapshot
        {
            Platform = Platform.Android,
            ScreenName = "Screen",
            Root = new AccessibilityNode { Role = "window", Children = [button] },
            ScreenReaderCapture = Capture("Submit || Button", "0"),
        };
        var runner = new RuleRunner([new LabelInNameRule(), new ScreenReaderLabelInNameRule()]);

        var findings = runner.Run(snapshot).Findings;

        var finding = Assert.Single(findings, f => f.Criteria.Contains(WcagCriteria.LabelInName));
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal(Finding.DefaultSource, finding.Source);
        Assert.Empty(finding.AlsoReportedBy);
    }
}
