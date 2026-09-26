using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class ScreenReaderCaptureRuleTests
{
    private static AccessibilityNode Tree() => new()
    {
        Role = "window",
        Children = [new AccessibilityNode { Role = "button", IsInteractive = true, Label = "Terms" }],
    };

    private static ScreenSnapshot Snapshot(ScreenReaderCapture? capture) => new()
    {
        Platform = Platform.iOS,
        ScreenName = "Screen",
        Root = Tree(),
        ScreenReaderCapture = capture,
    };

    [Fact]
    public void NoCapture_ProducesNoFindings()
    {
        Assert.Empty(new ScreenReaderCaptureRule().Evaluate(Snapshot(null)));
    }

    [Fact]
    public void EmptyCapture_ProducesNoFindings()
    {
        var capture = new ScreenReaderCapture(ScreenReaderSource.AccessibilityInspector, "Xcode 27.0", DateTimeOffset.UtcNow, [], true, null);

        Assert.Empty(new ScreenReaderCaptureRule().Evaluate(Snapshot(capture)));
    }

    [Fact]
    public void RoleMismatch_IsNeedsReviewUnderNameRoleValue()
    {
        var item = new ScreenReaderCaptureItem(1, null, "Terms", null, [], null, null, "Static Text", null, "0", MatchConfidence.Exact);
        var capture = new ScreenReaderCapture(ScreenReaderSource.AccessibilityInspector, "Xcode 27.0", DateTimeOffset.UtcNow, [item], true, null);

        var finding = Assert.Single(new ScreenReaderCaptureRule().Evaluate(Snapshot(capture)));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.NameRoleValue], finding.Criteria);
        Assert.Equal("screen-reader-capture:role", finding.RuleId);
        Assert.Equal("0", finding.NodePath);
        Assert.Equal("button", finding.Role);
        Assert.Equal("Terms", finding.Label);
    }

    [Fact]
    public void OrderMismatch_IsNeedsReviewUnderMeaningfulSequenceAndFocusOrder()
    {
        // Order is compared by rank among matched pairs, so this needs two matched stops that swap places
        // relative to each other -- a single matched pair always has equal rank on both sides.
        var snapshot = new ScreenSnapshot
        {
            Platform = Platform.iOS,
            ScreenName = "Screen",
            Root = new AccessibilityNode
            {
                Role = "window",
                Children =
                [
                    new AccessibilityNode { Role = "button", IsInteractive = true, Label = "Terms" },
                    new AccessibilityNode { Role = "button", IsInteractive = true, Label = "Help" },
                ],
            },
        };
        var items = new ScreenReaderCaptureItem[]
        {
            new(1, "Help, Button", null, null, null, null, null, null, null, "1", MatchConfidence.Exact),
            new(2, "Terms, Button", null, null, null, null, null, null, null, "0", MatchConfidence.Exact),
        };
        // VoiceOverCaptions, not TalkBack: a person-driven session's order is the reader's own real
        // navigation order, unlike TalkBack's capture here (see the next test) where the harness itself
        // forces focus in its own tree-walk order, so an order difference there says nothing about the app.
        var capture = new ScreenReaderCapture(ScreenReaderSource.VoiceOverCaptions, "17.0", DateTimeOffset.UtcNow, items, true, null);

        var findings = new ScreenReaderCaptureRule().Evaluate(snapshot with { ScreenReaderCapture = capture }).ToList();

        Assert.Equal(2, findings.Count);
        Assert.All(findings, f => Assert.Equal("screen-reader-capture:order", f.RuleId));
        Assert.All(findings, f => Assert.Equal([WcagCriteria.MeaningfulSequence, WcagCriteria.FocusOrder], f.Criteria));
    }

    [Fact]
    public void OrderMismatch_NeverReportedForTalkBack_BecauseTheOrderIsSwipewalksOwnWalkOrder()
    {
        // Same fixture as above, but from a TalkBack capture (harness/android's TalkBackCollector.kt
        // forces accessibility focus in Swipewalk's own tree-walk order, not TalkBack's real swipe order --
        // see ScreenReaderCaptureRule's remarks): an OrderMismatch here must not surface as a finding.
        var snapshot = new ScreenSnapshot
        {
            Platform = Platform.Android,
            ScreenName = "Screen",
            Root = new AccessibilityNode
            {
                Role = "window",
                Children =
                [
                    new AccessibilityNode { Role = "button", IsInteractive = true, Label = "Terms" },
                    new AccessibilityNode { Role = "button", IsInteractive = true, Label = "Help" },
                ],
            },
        };
        var items = new ScreenReaderCaptureItem[]
        {
            new(1, "Help, Button", null, null, null, null, null, null, null, "1", MatchConfidence.Exact),
            new(2, "Terms, Button", null, null, null, null, null, null, null, "0", MatchConfidence.Exact),
        };
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow, items, true, null);

        var findings = new ScreenReaderCaptureRule().Evaluate(snapshot with { ScreenReaderCapture = capture }).ToList();

        Assert.Empty(findings);
    }

    [Fact]
    public void OrderMismatch_NotYetReportedForAccessibilityInspector_UntilItsWalkIsVerifiedAnchoredToTheTop()
    {
        // Same fixture again, from an Accessibility Inspector capture: the walk starts wherever a person
        // clicked to set it up, not necessarily the top of the screen, and the Inspector's order was found
        // to be circular -- an unanchored starting point would make the whole sequence look rotated relative
        // to the predicted order, which this comparer's rank-based check can't tell apart from a genuine
        // difference (see ScreenReaderCaptureRule's remarks). Suppressed until verified on a real device.
        var snapshot = new ScreenSnapshot
        {
            Platform = Platform.iOS,
            ScreenName = "Screen",
            Root = new AccessibilityNode
            {
                Role = "window",
                Children =
                [
                    new AccessibilityNode { Role = "button", IsInteractive = true, Label = "Terms" },
                    new AccessibilityNode { Role = "button", IsInteractive = true, Label = "Help" },
                ],
            },
        };
        var items = new ScreenReaderCaptureItem[]
        {
            new(1, null, "Help", null, ["Button"], null, null, null, null, "1", MatchConfidence.Exact),
            new(2, null, "Terms", null, ["Button"], null, null, null, null, "0", MatchConfidence.Exact),
        };
        var capture = new ScreenReaderCapture(ScreenReaderSource.AccessibilityInspector, "26.0", DateTimeOffset.UtcNow, items, true, null);

        var findings = new ScreenReaderCaptureRule().Evaluate(snapshot with { ScreenReaderCapture = capture }).ToList();

        Assert.Empty(findings);
    }

    [Fact]
    public void UnmatchedItem_ProducesNoFinding()
    {
        // An empty tree (no predicted stops at all) so the only difference in play is the unmatched item
        // itself -- otherwise the tree's one predicted stop would also (correctly) produce a Missing finding.
        var snapshot = new ScreenSnapshot { Platform = Platform.iOS, ScreenName = "Screen", Root = new AccessibilityNode { Role = "window" } };
        var item = new ScreenReaderCaptureItem(1, "Something", null, null, null, null, null, null, null, null, MatchConfidence.None);
        var capture = new ScreenReaderCapture(ScreenReaderSource.VoiceOverCaptions, "iOS 18", DateTimeOffset.UtcNow, [item], true, null);

        Assert.Empty(new ScreenReaderCaptureRule().Evaluate(snapshot with { ScreenReaderCapture = capture }));
    }

    [Fact]
    public void MatchingCapture_ProducesNoFindings()
    {
        var item = new ScreenReaderCaptureItem(1, "Terms, Button", null, null, null, null, null, null, null, "0", MatchConfidence.Exact);
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow, [item], true, null);

        Assert.Empty(new ScreenReaderCaptureRule().Evaluate(Snapshot(capture)));
    }

    [Fact]
    public void NoFindingKind_IsEverWcagIssue()
    {
        // Every difference is NeedsReview -- see ScreenReaderCaptureRule's remarks on why none is WcagIssue.
        var item = new ScreenReaderCaptureItem(1, "Something completely different, Button", null, null, null, null, null, null, null, "0", MatchConfidence.Exact);
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow, [item], true, null);

        Assert.All(new ScreenReaderCaptureRule().Evaluate(Snapshot(capture)), f => Assert.Equal(FindingKind.NeedsReview, f.Kind));
    }

    [Fact]
    public void Missing_OnInteractiveElement_IsNameRoleValueOnly()
    {
        // The tree's one child is an interactive button ("Terms"); a capture with zero items (predicted has
        // a named stop, nothing was reported) makes it Missing -- but empty items short-circuit to no
        // findings (see EmptyCapture_ProducesNoFindings), so use an unrelated matched path instead so the
        // "Terms" stop is genuinely unreported by a non-empty, complete capture.
        var item = new ScreenReaderCaptureItem(1, "Something else", null, null, null, null, null, null, null, "0/9", MatchConfidence.Exact);
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow, [item], true, null);

        var findings = new ScreenReaderCaptureRule().Evaluate(Snapshot(capture)).ToList();

        var missing = Assert.Single(findings, f => f.RuleId == "screen-reader-capture:missing");
        Assert.Equal([WcagCriteria.NameRoleValue], missing.Criteria);
    }

    [Fact]
    public void Missing_OnNonInteractiveImageElement_IsNonTextContentOnly()
    {
        var snapshot = new ScreenSnapshot
        {
            Platform = Platform.iOS,
            ScreenName = "Screen",
            Root = new AccessibilityNode { Role = "window", Children = [new AccessibilityNode { Role = "image", Label = "City seal" }] },
        };
        var item = new ScreenReaderCaptureItem(1, "Something else", null, null, null, null, null, null, null, "0/9", MatchConfidence.Exact);
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow, [item], true, null);

        var findings = new ScreenReaderCaptureRule().Evaluate(snapshot with { ScreenReaderCapture = capture }).ToList();

        var missing = Assert.Single(findings, f => f.RuleId == "screen-reader-capture:missing");
        Assert.Equal([WcagCriteria.NonTextContent], missing.Criteria);
    }

    [Fact]
    public void Missing_OnInteractiveImageElement_IsNameRoleValueAndNonTextContent()
    {
        var snapshot = new ScreenSnapshot
        {
            Platform = Platform.iOS,
            ScreenName = "Screen",
            Root = new AccessibilityNode { Role = "window", Children = [new AccessibilityNode { Role = "image", IsInteractive = true, Label = "City seal" }] },
        };
        var item = new ScreenReaderCaptureItem(1, "Something else", null, null, null, null, null, null, null, "0/9", MatchConfidence.Exact);
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow, [item], true, null);

        var findings = new ScreenReaderCaptureRule().Evaluate(snapshot with { ScreenReaderCapture = capture }).ToList();

        var missing = Assert.Single(findings, f => f.RuleId == "screen-reader-capture:missing");
        Assert.Equal([WcagCriteria.NameRoleValue, WcagCriteria.NonTextContent], missing.Criteria);
    }

    [Fact]
    public void Missing_OnPlainStaticText_IsUnmappedNotGuessed()
    {
        var snapshot = new ScreenSnapshot
        {
            Platform = Platform.iOS,
            ScreenName = "Screen",
            Root = new AccessibilityNode { Role = "window", Children = [new AccessibilityNode { Role = "text", VisibleText = "Welcome" }] },
        };
        var item = new ScreenReaderCaptureItem(1, "Something else", null, null, null, null, null, null, null, "0/9", MatchConfidence.Exact);
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow, [item], true, null);

        var findings = new ScreenReaderCaptureRule().Evaluate(snapshot with { ScreenReaderCapture = capture }).ToList();

        var missing = Assert.Single(findings, f => f.RuleId == "screen-reader-capture:missing");
        Assert.Empty(missing.Criteria);
    }
}
