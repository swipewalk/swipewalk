using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class OffscreenUnreachableRuleTests
{
    // iPhone SE (3rd gen) Simulator's point size, matching the real-device shape this bug was first found
    // on by eye and this rule's own iPhone SE (3rd gen) Simulator and physical-device verification
    // confirmed: BuggyApp's plain VerticalStackLayout (no ScrollView) doesn't fit at normal/100% text size
    // on this screen. Both dimensions are above WCAG 1.4.10 Reflow's own 320×256 reference size.
    private const double ScreenWidth = 375;
    private const double ScreenHeight = 667;

    private static AccessibilityNode Button(
        string label, Bounds bounds, bool accessible = true, bool enabled = true) => new()
    {
        Role = "button",
        Label = label,
        IsInteractive = true,
        IsAccessible = accessible,
        IsEnabled = enabled,
        Bounds = bounds,
    };

    private static AccessibilityNode Text(string text, Bounds bounds) => new()
    {
        Role = "text",
        VisibleText = text,
        IsAccessible = true,
        Bounds = bounds,
    };

    private static AccessibilityNode Container(bool scrollable, Bounds bounds, params AccessibilityNode[] children) => new()
    {
        Role = "group",
        IsScrollable = scrollable,
        IsAccessible = true,
        Bounds = bounds,
        Children = children,
    };

    private static AccessibilityNode Root(params AccessibilityNode[] children) => new()
    {
        Role = "window",
        IsAccessible = true,
        Bounds = new Bounds(0, 0, ScreenWidth, ScreenHeight),
        Children = children,
    };

    private static ScreenSnapshot Snapshot(AccessibilityNode root, Platform platform = Platform.iOS) => new()
    {
        Platform = platform,
        ScreenName = "Pay a parking ticket",
        Root = root,
    };

    private static List<Finding> Evaluate(AccessibilityNode root, Platform platform = Platform.iOS) =>
        new OffscreenUnreachableRule().Evaluate(Snapshot(root, platform)).ToList();

    [Fact]
    public void BuggyAppShapedScreen_BottomControlsBelowTheFold_NoScrollView_NeedsReview()
    {
        // Reproduces the physical iPhone SE finding (confirmed by an iPhone SE (3rd gen) Simulator capture
        // of BuggyApp): everything fits until the last two buttons, which sit entirely below the screen's
        // bottom edge, and the page has no ScrollView anywhere.
        var root = Root(
            Text("City of Exampleville", new Bounds(10, 10, 300, 40)),
            Button("Pay", new Bounds(20, 560, 335, 50)),
            Button("Save for later", new Bounds(20, 680, 335, 36)), // fully below the 667-tall screen
            Button("View payment history", new Bounds(20, 726, 335, 50))); // fully below too

        var finding = Assert.Single(Evaluate(root));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.Reflow], finding.Criteria);
        Assert.Equal("screen", finding.Role);
        Assert.Contains("\"Save for later\"", finding.Message);
        Assert.Contains("\"View payment history\"", finding.Message);
        Assert.DoesNotContain("\"Pay\"", finding.Message); // Pay is fully on-screen
        Assert.Contains("reports itself as scrollable", finding.Message);
        Assert.Contains("screen reader may still reach", finding.Message);
        Assert.Contains("375×667 pt", finding.Message); // the tested screen size is stated
        Assert.Contains("320×256 reference size", finding.Message);
        Assert.Contains("check by hand", finding.Message);
    }

    [Fact]
    public void SingleOffscreenElement_MessageUsesSingularGrammar()
    {
        var root = Root(Button("Save for later", new Bounds(20, 680, 335, 36)));

        var finding = Assert.Single(Evaluate(root));

        // "1 element(s) ... is" reads awkwardly; the noun itself must agree with the count too.
        Assert.Contains("1 element present", finding.Message);
        Assert.Contains(") is positioned", finding.Message);
        Assert.DoesNotContain("element(s)", finding.Message);
        Assert.Contains("it appears to be out of view", finding.Message);
        Assert.Contains("whether it can still be reached", finding.Message);
        Assert.DoesNotContain("whether they can", finding.Message);
    }

    [Fact]
    public void MultipleOffscreenElements_MessageUsesPluralGrammar()
    {
        var root = Root(
            Button("Save for later", new Bounds(20, 680, 335, 36)),
            Button("View payment history", new Bounds(20, 726, 335, 50)));

        var finding = Assert.Single(Evaluate(root));

        Assert.Contains("2 elements present", finding.Message);
        Assert.Contains(") are positioned", finding.Message);
        Assert.DoesNotContain("element(s)", finding.Message);
        Assert.Contains("they appear to be out of view", finding.Message);
        Assert.Contains("whether they can still be reached", finding.Message);
        Assert.Contains("\"Save for later\"", finding.Message);
        Assert.Contains("\"View payment history\"", finding.Message);
    }

    [Fact]
    public void OffscreenControlWithItsOwnChildTextLabel_IsReportedOnce()
    {
        // Mirrors a real control's tree shape: the interactive control itself is a candidate, and its own
        // visible text can also land on a separate child "text"-role node, which independently satisfies
        // IsCandidate. Report the control once, not once for itself and once for its label child (the
        // duplicate the iPhone SE Simulator verification actually found for "View payment history").
        var control = new AccessibilityNode
        {
            Role = "button",
            Label = "View payment history",
            IsInteractive = true,
            IsAccessible = true,
            Bounds = new Bounds(20, 726, 335, 50),
            Children = [Text("View payment history", new Bounds(20, 726, 335, 50))],
        };
        var root = Root(control);

        var finding = Assert.Single(Evaluate(root));

        Assert.Contains("1 element present", finding.Message);
        Assert.Equal(1, finding.Message.Split("\"View payment history\"").Length - 1);
    }

    [Fact]
    public void EverythingFitsOnScreen_ProducesNothing()
    {
        var root = Root(
            Text("City of Exampleville", new Bounds(10, 10, 300, 40)),
            Button("Pay", new Bounds(20, 560, 335, 50)));

        Assert.Empty(Evaluate(root));
    }

    [Fact]
    public void OffscreenElement_UnderAScrollableAncestor_IsIgnored()
    {
        // A vertical ScrollView holding the same content: the missing buttons could be scrolled to, so this
        // is the ordinary, working case, unlike the BuggyApp shape above.
        var root = Root(
            Container(scrollable: true, new Bounds(0, 0, ScreenWidth, ScreenHeight),
                Button("Save for later", new Bounds(20, 680, 335, 36)),
                Button("View payment history", new Bounds(20, 726, 335, 50))));

        Assert.Empty(Evaluate(root));
    }

    [Fact]
    public void OffscreenElement_UnderAHorizontallyScrollingPagerOrCarousel_IsIgnored()
    {
        // The model has no separate "horizontal" flag (see AccessibilityNode.IsScrollable's remarks): any
        // container reporting itself scrollable -- a pager included -- is treated as a way to reach its
        // off-screen children, whichever direction it actually scrolls in.
        var root = Root(
            Container(scrollable: true, new Bounds(0, 600, ScreenWidth, 200),
                Button("Slide 3 of 5", new Bounds(400, 620, 335, 50)))); // off the right/bottom edge

        Assert.Empty(Evaluate(root));
    }

    [Fact]
    public void ScrollableSibling_NotAnAncestor_DoesNotExcludeAnUnrelatedOffscreenElement()
    {
        // Only an ancestor's scroll position can bring a node into view: a scrollable region elsewhere on
        // the screen, that does not contain the off-screen element, cannot reach it.
        var root = Root(
            Container(scrollable: true, new Bounds(0, 0, ScreenWidth, 300), Button("In view", new Bounds(20, 20, 100, 40))),
            Button("Save for later", new Bounds(20, 680, 335, 36))); // sibling of the scrollable container, not inside it

        var finding = Assert.Single(Evaluate(root));
        Assert.Contains("\"Save for later\"", finding.Message);
    }

    [Fact]
    public void ContentInsideAWebView_IsTreatedAsReachableByScrolling()
    {
        // XCUITest maps a WKWebView to role "webview" but does not expose its inner DOM scroll view as its
        // own "scrollView" element (see XcuiTreeParser.MapRole), so this rule treats "webview" itself as
        // scroll-like rather than flag ordinary below-the-fold web content (e.g. a long Terms page) as
        // unreachable.
        var webView = new AccessibilityNode
        {
            Role = "webview",
            NativeType = "webView",
            IsAccessible = true,
            Bounds = new Bounds(0, 100, ScreenWidth, 400),
            Children = [Text("Terms text far down the page", new Bounds(20, 900, 335, 40))],
        };

        Assert.Empty(Evaluate(Root(webView)));
    }

    [Fact]
    public void ContentInsideAMap_IsTreatedAsReachableByPanning()
    {
        // A map's annotations can sit outside its currently visible region the same way a web view's DOM
        // content does; nothing maps to a dedicated "map" role in XcuiTreeParser.MapRole, so this is
        // recognized by NativeType instead.
        var map = new AccessibilityNode
        {
            Role = "group",
            NativeType = "map",
            IsAccessible = true,
            Bounds = new Bounds(0, 100, ScreenWidth, 400),
            Children = [Button("Pin: City Hall", new Bounds(20, 900, 200, 40))],
        };

        Assert.Empty(Evaluate(Root(map)));
    }

    [Fact]
    public void OffscreenElement_HiddenFromAssistiveTechnology_IsIgnored()
    {
        var root = Root(Button("Save for later", new Bounds(20, 680, 335, 36), accessible: false));

        Assert.Empty(Evaluate(root));
    }

    [Fact]
    public void OffscreenElement_Disabled_IsIgnored()
    {
        var root = Root(Button("Save for later", new Bounds(20, 680, 335, 36), enabled: false));

        Assert.Empty(Evaluate(root));
    }

    [Fact]
    public void OffscreenElement_ZeroSizeBounds_IsIgnored()
    {
        // A node with no area (e.g. a collapsed or not-yet-laid-out view) can't be "mostly outside" anything
        // meaningfully, and RuleFinding.HasArea already excludes it, the same as every other rule.
        var root = Root(Button("Save for later", new Bounds(20, 680, 0, 0)));

        Assert.Empty(Evaluate(root));
    }

    [Fact]
    public void ElementOnlyASliverOffTheBottomEdge_UnderHalfOutside_IsIgnored()
    {
        // Screen bottom edge is at 667. This button starts at 650 and is 30 pt tall (bottom at 680): 13 of
        // its 30 points (~43%) fall past the edge, under the 50% threshold.
        var root = Root(Button("Pay", new Bounds(20, 650, 335, 30)));

        Assert.Empty(Evaluate(root));
    }

    [Fact]
    public void ElementMostlyOffTheBottomEdge_OverHalfOutside_IsFlagged()
    {
        // Starts at 650 and is 40 pt tall (bottom at 690): 23 of its 40 points (~58%) fall past the edge,
        // over the 50% threshold.
        var root = Root(Button("Save for later", new Bounds(20, 650, 335, 40)));

        var finding = Assert.Single(Evaluate(root));
        Assert.Contains("\"Save for later\"", finding.Message);
    }

    [Fact]
    public void ElementMostlyOffTheLeftEdge_IsFlagged()
    {
        // x=-300, width=335 -> right edge at 35: about 89% of its area falls to the left of the screen.
        var root = Root(Button("Hidden menu item", new Bounds(-300, 100, 335, 50)));

        var finding = Assert.Single(Evaluate(root));
        Assert.Contains("\"Hidden menu item\"", finding.Message);
    }

    [Fact]
    public void UnnamedOffscreenButton_IsNotFlaggedByThisRule()
    {
        // No accessible name at all: MissingNameRule already reports this separately; without a name there's
        // nothing for this rule's message to identify.
        var root = Root(new AccessibilityNode
        {
            Role = "button",
            IsInteractive = true,
            IsAccessible = true,
            Bounds = new Bounds(20, 680, 335, 36),
        });

        Assert.Empty(Evaluate(root));
    }

    [Fact]
    public void PlainGroupOffscreen_WithNoInteractiveOrTextContent_IsNotACandidate()
    {
        // A bare container with no name and no visible text (e.g. a spacer or decorative wrapper) is not the
        // kind of "interactive or named content" this rule looks for.
        var root = Root(Container(scrollable: false, new Bounds(20, 680, 335, 36)));

        Assert.Empty(Evaluate(root));
    }

    [Fact]
    public void ZeroSizeScreenBounds_ProducesNothing()
    {
        // Defensive: a root with no usable bounds (e.g. an unset/placeholder capture) can't establish what
        // "on screen" even means, so the rule stays silent rather than guess.
        var root = new AccessibilityNode
        {
            Role = "window",
            IsAccessible = true,
            Bounds = new Bounds(0, 0, 0, 0),
            Children = [Button("Save for later", new Bounds(20, 680, 335, 36))],
        };

        Assert.Empty(Evaluate(root));
    }

    [Fact]
    public void ScreenSmallerThanWcagReferenceSize_IsPlatformAdvisoryNotWcag()
    {
        // WCAG 1.4.10 Reflow's own reference viewport is 320×256; a screen smaller than that is beyond what
        // the criterion was tested against, so this is a platform advisory, not a WCAG citation.
        var smallRoot = new AccessibilityNode
        {
            Role = "window",
            IsAccessible = true,
            Bounds = new Bounds(0, 0, 200, 300), // narrower than 320
            Children = [Button("Save for later", new Bounds(20, 290, 160, 40))], // ~75% below the bottom edge
        };

        var finding = Assert.Single(Evaluate(smallRoot));

        Assert.Equal(FindingKind.PlatformAdvisory, finding.Kind);
        Assert.Empty(finding.Criteria);
        Assert.Equal(OffscreenUnreachableRule.AdaptiveLayoutGuideline, finding.PlatformGuideline);
        Assert.Contains("smaller than the 320×256 reference size", finding.Message);
    }

    [Fact]
    public void AndroidPlatform_NeverEvaluated()
    {
        // Restricted to iOS: Android's uiautomator dump clips every node's reported bounds to the visible
        // screen (AOSP's AccessibilityNodeInfoDumper via getVisibleBoundsInScreen -- see the rule's own
        // remarks and KnownLimitations "offscreen-unreachable-android-gap"), so a real Android capture could
        // never actually have out-of-bounds coordinates for this rule to see. The rule reflects that by not
        // evaluating Android snapshots at all, regardless of what the (synthetic, here) bounds say.
        var root = Root(Button("Save for later", new Bounds(20, 680, 335, 36)));

        Assert.Empty(Evaluate(root, Platform.Android));
    }
}
