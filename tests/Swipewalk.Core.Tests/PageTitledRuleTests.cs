using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class PageTitledRuleTests
{
    private static AccessibilityNode Node(string? paneTitle = null) => new()
    {
        Role = "text",
        VisibleText = "Content",
        PaneTitle = paneTitle,
        Bounds = new Bounds(0, 0, 100, 20),
    };

    private static ScreenSnapshot Snapshot(
        Platform platform = Platform.Android, bool atfRan = true, AccessibilityNode? child = null) => new()
    {
        Platform = platform,
        ScreenName = "Screen",
        AtfRan = atfRan,
        Root = new AccessibilityNode { Role = "window", Children = child is null ? [] : [child] },
    };

    private static List<Finding> Evaluate(ScreenSnapshot snapshot) => new PageTitledRule().Evaluate(snapshot).ToList();

    [Fact]
    public void NoPaneTitleAnywhereInTree_NeedsReviewUnderPageTitled()
    {
        var findings = Evaluate(Snapshot(child: Node()));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.PageTitled], finding.Criteria);
        Assert.Equal("window", finding.Role);
    }

    [Fact]
    public void PaneTitlePresentOnADescendant_NoFinding()
    {
        var findings = Evaluate(Snapshot(child: Node(paneTitle: "Pay a parking ticket")));

        Assert.Empty(findings);
    }

    [Fact]
    public void PaneTitlePresentOnRootItself_NoFinding()
    {
        var snapshot = new ScreenSnapshot
        {
            Platform = Platform.Android,
            ScreenName = "Screen",
            AtfRan = true,
            Root = new AccessibilityNode { Role = "window", PaneTitle = "Pay a parking ticket" },
        };

        Assert.Empty(Evaluate(snapshot));
    }

    [Fact]
    public void WhitespaceOnlyPaneTitle_StillTreatedAsNoTitle()
    {
        var findings = Evaluate(Snapshot(child: Node(paneTitle: "   ")));

        Assert.Single(findings);
    }

    [Fact]
    public void HarnessDidNotRun_NoFindingEvenWithNoPaneTitle()
    {
        // AtfRan false means Swipewalk never read pane titles for this capture at all -- reporting "no
        // title exposed" would describe something it never checked.
        var findings = Evaluate(Snapshot(atfRan: false, child: Node()));

        Assert.Empty(findings);
    }

    [Fact]
    public void IosPlatform_NeverEvaluated()
    {
        var findings = Evaluate(Snapshot(platform: Platform.iOS, child: Node()));

        Assert.Empty(findings);
    }
}
