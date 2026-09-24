using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class LargeTextLostContentRuleTests
{
    private static AccessibilityNode Text(string text) => new()
    {
        Role = "text",
        VisibleText = text,
        IsAccessible = true,
        Bounds = new Bounds(0, 0, 100, 20),
    };

    private static AccessibilityNode Button(string label, bool named = true) => new()
    {
        Role = "button",
        Label = named ? label : null,
        IsInteractive = true,
        IsAccessible = true,
        Bounds = new Bounds(0, 0, 100, 50),
    };

    private static AccessibilityNode Image(string label) => new()
    {
        Role = "image",
        Label = label,
        IsAccessible = true,
        Bounds = new Bounds(0, 0, 40, 40),
    };

    private static AccessibilityNode Container(bool scrollable, params AccessibilityNode[] children) => new()
    {
        Role = "group",
        IsScrollable = scrollable,
        IsAccessible = true,
        Bounds = new Bounds(0, 0, 300, 1000),
        Children = children,
    };

    private static AccessibilityNode Root(params AccessibilityNode[] children) => new()
    {
        Role = "window",
        IsAccessible = true,
        Bounds = new Bounds(0, 0, 300, 1000),
        Children = children,
    };

    private static ScreenSnapshot Snapshot(
        AccessibilityNode normalRoot, AccessibilityNode? largeRoot, double? scale = 2.0,
        string setting = "Android font scale 2.0", Platform platform = Platform.Android) => new()
    {
        Platform = platform,
        ScreenName = "Screen",
        Root = normalRoot,
        LargeTextSetting = setting,
        LargeTextScale = scale,
        LargeText = largeRoot is null
            ? null
            : new ScreenSnapshot { Platform = platform, ScreenName = "Screen", Root = largeRoot },
    };

    private static List<Finding> Evaluate(ScreenSnapshot snapshot) => new LargeTextLostContentRule().Evaluate(snapshot).ToList();

    [Fact]
    public void MissingElement_ScreenHasAScrollableContainerAtLargeSize_IsIgnored()
    {
        // The container was not scrollable at normal size either (nothing overflowed it yet) but becomes
        // scrollable once the larger text pushes content past its edge: this is the ordinary, working case,
        // so the missing element is treated as merely scrolled out of view, not lost.
        var normal = Root(Container(scrollable: false, Button("Pay"), Button("Save for later")));
        var large = Root(Container(scrollable: true, Button("Pay")));

        Assert.Empty(Evaluate(Snapshot(normal, large)));
    }

    [Fact]
    public void MissingElement_NoScrollableContainerAnywhereOnScreen_NeedsReview()
    {
        // Matches the Pixel 4a finding (2026-09-23): the container reports scrollable="false" both before
        // and after the text grows, so nothing on the screen can reveal the missing element.
        var normal = Root(Container(scrollable: false, Button("Pay"), Button("Save for later")));
        var large = Root(Container(scrollable: false, Button("Pay")));

        var finding = Assert.Single(Evaluate(Snapshot(normal, large)));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.ResizeText], finding.Criteria);
        Assert.Equal("screen", finding.Role);
        Assert.Contains("\"Save for later\"", finding.Message);
        Assert.Contains("reports itself as scrollable", finding.Message);
        Assert.Contains("bring it back into view", finding.Message);
        Assert.Contains("whether it can still be reached", finding.Message);
        Assert.Equal("largeText", finding.Details["screenshot"]);
    }

    [Fact]
    public void SingleMissingElement_MessageUsesSingularGrammarThroughout()
    {
        var normal = Root(Container(scrollable: false, Button("Pay"), Button("Save for later")));
        var large = Root(Container(scrollable: false, Button("Pay")));

        var finding = Assert.Single(Evaluate(Snapshot(normal, large)));

        Assert.Contains("1 element(s)", finding.Message);
        Assert.Contains("is missing", finding.Message);
        Assert.Contains("that it was renamed", finding.Message);
        Assert.DoesNotContain("it were renamed", finding.Message);
    }

    [Fact]
    public void MissingElement_NoContainerAtAll_NeedsReview()
    {
        var normal = Root(Button("Pay"), Button("Save for later"));
        var large = Root(Button("Pay"));

        var finding = Assert.Single(Evaluate(Snapshot(normal, large)));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Contains("\"Save for later\"", finding.Message);
    }

    [Fact]
    public void MissingElement_WholeOffScreenSubtreeGone_StillNeedsReview()
    {
        // Reproduces the exact shape of the BuggyApp bug beyond the single scroll-view case: an entire
        // off-screen container (not just a leaf) is absent from the large-text tree, and there's still no
        // scrollable node anywhere on the screen to reveal what it held.
        var normal = Root(Button("Pay"), Container(scrollable: false, Button("Email receipt")));
        var large = Root(Button("Pay"));

        var finding = Assert.Single(Evaluate(Snapshot(normal, large)));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Contains("\"Email receipt\"", finding.Message);
        Assert.Contains("reports itself as scrollable", finding.Message);
    }

    [Fact]
    public void MultipleMissingItems_AreGroupedIntoOneFinding()
    {
        var normal = Root(Container(scrollable: false, Button("Save for later"), Button("View payment history")));
        var large = Root(Container(scrollable: false));

        var finding = Assert.Single(Evaluate(Snapshot(normal, large)));

        Assert.Contains("2 element(s)", finding.Message);
        Assert.Contains("\"Save for later\"", finding.Message);
        Assert.Contains("\"View payment history\"", finding.Message);
        Assert.Contains("bring them back into view", finding.Message);
        Assert.Contains("whether they can still be reached", finding.Message);
        Assert.DoesNotContain("whether them", finding.Message);
    }

    [Fact]
    public void AtIosAx3_BeyondWcagScale_IsPlatformAdvisoryNotWcag()
    {
        var normal = Root(Container(scrollable: false, Button("Pay"), Button("Save for later")));
        var large = Root(Container(scrollable: false, Button("Pay")));

        var finding = Assert.Single(Evaluate(Snapshot(
            normal, large, scale: 2.35, setting: "iOS accessibility text size AX3 (about 235%)", platform: Platform.iOS)));

        Assert.Equal(FindingKind.PlatformAdvisory, finding.Kind);
        Assert.Empty(finding.Criteria);
        Assert.Equal(LargeTextLostContentRule.DynamicTypeGuideline, finding.PlatformGuideline);
        Assert.Contains("AX3", finding.Message);
        Assert.Contains("200%", finding.Message);
        // The setting is named once, not twice (an earlier draft said "At {setting} ... at {setting}").
        Assert.Equal(1, finding.Message.Split("AX3").Length - 1);
    }

    [Fact]
    public void UnknownScale_DefaultsToWcagIssue()
    {
        // Captures made before LargeTextScale was recorded: no evidence the tested scale went beyond 200%.
        var normal = Root(Container(scrollable: false, Button("Pay"), Button("Save for later")));
        var large = Root(Container(scrollable: false, Button("Pay")));

        var finding = Assert.Single(Evaluate(Snapshot(normal, large, scale: null)));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.ResizeText], finding.Criteria);
    }

    [Fact]
    public void NoLargeTextCapture_ProducesNothing()
    {
        var normal = Root(Button("Pay"));

        Assert.Empty(Evaluate(Snapshot(normal, null)));
    }

    [Fact]
    public void ElementPresentAtBothSizes_ProducesNothing()
    {
        var normal = Root(Button("Pay"), Button("Save for later"));
        var large = Root(Button("Pay"), Button("Save for later"));

        Assert.Empty(Evaluate(Snapshot(normal, large)));
    }

    [Fact]
    public void ElementWithNoAccessibleName_IsNotConsideredMissing()
    {
        // MissingNameRule already reports a nameless interactive control; without a name there's nothing
        // for this rule to say went missing.
        var normal = Root(Button("Save for later", named: false), Button("Pay"));
        var large = Root(Button("Pay"));

        Assert.Empty(Evaluate(Snapshot(normal, large)));
    }

    [Fact]
    public void NonInteractiveNonTextElement_IsNotConsideredMissing()
    {
        // Images, decorative or otherwise, are out of scope for this rule (interactive controls and text only).
        var normal = Root(Image("Logo"), Button("Pay"));
        var large = Root(Button("Pay"));

        Assert.Empty(Evaluate(Snapshot(normal, large)));
    }

    [Fact]
    public void InaccessibleElementAtNormalSize_IsNotConsideredMissing()
    {
        var hidden = Button("Save for later") with { IsAccessible = false };
        var normal = Root(hidden, Button("Pay"));
        var large = Root(Button("Pay"));

        Assert.Empty(Evaluate(Snapshot(normal, large)));
    }

    [Fact]
    public void MessageFlowsThroughRuleRunner()
    {
        var normal = Root(Container(scrollable: false, Button("Pay"), Button("Save for later")));
        var large = Root(Container(scrollable: false, Button("Pay")));
        var snapshot = Snapshot(normal, large);

        var result = new RuleRunner([new LargeTextLostContentRule()]).Run(snapshot);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("large-text-lost-content", finding.RuleId);
        Assert.Contains("\"Save for later\"", finding.Message);
    }
}
