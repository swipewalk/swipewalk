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

    // --- WCAG 2.5.8's inline exception ("the target is in a sentence or its size is otherwise constrained
    // by the line-height of non-target text") is never granted automatically -- the tree can't tell a
    // target genuinely inline in running text from one that just happens to sit next to an unrelated text
    // label. When a target looks like a plain-text link beside other text and the spacing exception
    // doesn't already explain it, Swipewalk reports needs-review instead of a WCAG issue, so a person
    // confirms it. See TargetSizeRule.LooksLikeInlineTextLink.

    private static AccessibilityNode IosTextSibling(double x, double y, double w, double h, string text) => new()
    {
        Role = "text",
        VisibleText = text,
        Bounds = new Bounds(x, y, w, h),
    };

    /// <summary>An iOS "link element inside static text": a "button"-typed node (XcuiTreeParser's role for
    /// XCUITest's own button type) with no accessibility label of its own, wrapping one "text" child sized
    /// the same as the node -- see XcuiTreeParser.BuildNode.</summary>
    private static AccessibilityNode IosTextStyledLink(double x, double y, double w, double h, string text) => new()
    {
        Role = "button",
        IsInteractive = true,
        // Matches XcuiTreeParser.BuildNode: an interactive node with no own accessibility label gets its
        // VisibleText from concatenating its "text"-role children.
        VisibleText = text,
        Bounds = new Bounds(x, y, w, h),
        Children = [IosTextSibling(x, y, w, h, text)],
    };

    [Fact]
    public void iOS_RealBuggyAppTermsLink_SpacingExceptionAlreadyApplies_NoInlineWording()
    {
        // The full real row from samples/BuggyApp's "Terms" link (a MAUI Label with a TapGestureRecognizer,
        // next to a "Need help?" label and a 44x44 Help button) -- bounds taken from
        // tests/Swipewalk.Core.Tests/Fixtures/BuggyApp.iOS/tree.json. The Help button is far enough from
        // Terms (16pt center-to-edge, > the 12pt spacing-exception radius) that the spacing exception
        // already applies -- matching samples/BuggyApp/ground-truth.json's B10 ("because of the spacing
        // exception"), so this must stay a plain platform advisory with no mention of the inline exception,
        // never needs-review or a WCAG issue.
        var needHelpWrapper = new AccessibilityNode
        {
            Role = "group",
            Bounds = new Bounds(20, 505.67, 73.33, 19.33),
            Children = [IosTextSibling(20, 505.67, 73.33, 19.33, "Need help?")],
        };
        var help = new AccessibilityNode { Role = "button", IsInteractive = true, Label = "Help", Bounds = new Bounds(93.33, 493.33, 44, 44) };
        var terms = IosTextStyledLink(137.33, 507.83, 32.33, 15, "Terms");
        var row = new AccessibilityNode { Role = "group", Bounds = new Bounds(20, 493.33, 362, 44), Children = [needHelpWrapper, help, terms] };

        var findings = Evaluate(Snapshot(Platform.iOS, row));

        Assert.DoesNotContain(findings, f => f.Kind is FindingKind.WcagIssue or FindingKind.NeedsReview);
        var finding = Assert.Single(findings, f => f.Label == "Terms");
        Assert.Equal(FindingKind.PlatformAdvisory, finding.Kind);
        Assert.DoesNotContain("inline", finding.Message);
        Assert.Contains("44", finding.PlatformGuideline);
    }

    [Fact]
    public void iOS_TextStyledLinkBesideSiblingText_SpacingExceptionFails_NeedsReview()
    {
        // Same text-styled shape as the real "Terms" link, but placed close enough to another target that
        // the spacing exception fails -- Swipewalk can't grant the inline exception automatically, so this
        // is needs-review, not a silent exemption and not a confirmed WCAG issue.
        var needHelp = IosTextSibling(0, 0, 40, 20, "Need help?");
        var terms = IosTextStyledLink(40, 0, 20, 15, "Terms");
        var neighbor = Target(60, 0, 20, 20); // close enough to fail the spacing exception
        var row = new AccessibilityNode { Role = "group", Bounds = new Bounds(0, 0, 80, 20), Children = [needHelp, terms, neighbor] };

        var findings = Evaluate(Snapshot(Platform.iOS, row));

        Assert.DoesNotContain(findings, f => f.Kind == FindingKind.WcagIssue && f.Label == "Terms");
        var finding = Assert.Single(findings, f => f.Label == "Terms");
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.TargetSizeMinimum], finding.Criteria);
        Assert.Contains("inline exception may apply", finding.Message);
    }

    [Fact]
    public void Android_ClickableTextViewBesideSiblingText_SpacingExceptionFails_NeedsReview()
    {
        // Same pattern as UiAutomatorParser would report it: a clickable TextView (role "button",
        // NativeType "android.widget.TextView" -- see UiAutomatorParser.MapRole) next to a plain,
        // non-clickable TextView sibling ("text" role), close enough to another target to fail the spacing
        // exception. Real BuggyApp captures never expose this element as a target on Android at all (MAUI
        // doesn't mark the TapGestureRecognizer as clickable, a known miss -- see
        // samples/BuggyApp/ground-truth.json's "B10"), so this fixture is constructed to match what a
        // native clickable TextView link would look like, rather than taken from a capture.
        var needHelp = new AccessibilityNode { Role = "text", VisibleText = "Need help?", Bounds = new Bounds(0, 0, 40, 20) };
        var terms = new AccessibilityNode
        {
            Role = "button",
            NativeType = "android.widget.TextView",
            IsInteractive = true,
            VisibleText = "Terms",
            Bounds = new Bounds(40, 0, 20, 15),
        };
        var neighbor = Target(60, 0, 20, 20);
        var row = new AccessibilityNode { Role = "group", Bounds = new Bounds(0, 0, 80, 20), Children = [needHelp, terms, neighbor] };

        var findings = Evaluate(Snapshot(Platform.Android, row));

        Assert.DoesNotContain(findings, f => f.Kind == FindingKind.WcagIssue && f.Label == "Terms");
        var finding = Assert.Single(findings, f => f.Label == "Terms");
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Contains("inline exception may apply", finding.Message);
    }

    [Fact]
    public void Android_ClickableTextView_IsolatedAmongTargets_MergesWithAtfTouchTargetSizeCheck()
    {
        // The spacing exception passes trivially here (only one target in the whole tree), so this stays a
        // plain platform advisory with no inline wording (as for the real "Terms" link) -- and ATF's own
        // TouchTargetSizeCheck (48 dp) firing on the same element must not fight with, or duplicate, it:
        // RuleRunner.MergeEngineDuplicates folds them into one finding (same element, both platform
        // advisories) and records ATF in AlsoReportedBy.
        var needHelp = new AccessibilityNode { Role = "text", VisibleText = "Need help?", Bounds = new Bounds(20, 506, 73, 19) };
        var terms = new AccessibilityNode
        {
            Role = "button",
            NativeType = "android.widget.TextView",
            IsInteractive = true,
            VisibleText = "Terms",
            Bounds = new Bounds(137, 508, 32, 15),
        };
        var row = new AccessibilityNode { Role = "group", Bounds = new Bounds(20, 493, 362, 44), Children = [needHelp, terms] };
        var snapshot = new ScreenSnapshot
        {
            Platform = Platform.Android,
            ScreenName = "Screen",
            Root = new AccessibilityNode { Role = "window", Children = [row] },
            AtfIssues = [new AtfIssue("TouchTargetSizeCheck", "Touch target too small.", "0/1", "Terms", new Bounds(137, 508, 32, 15))],
        };

        var result = new RuleRunner([new TargetSizeRule(), new AtfIssueRule()]).Run(snapshot);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(FindingKind.PlatformAdvisory, finding.Kind);
        Assert.DoesNotContain("inline", finding.Message);
        Assert.Contains(AtfIssueRule.EngineName, finding.AlsoReportedBy);
    }

    [Fact]
    public void TextStyledTarget_WithNoSiblingText_DoesNotLookInline_StillReportsWcagIssue()
    {
        // The tap area is text-styled (a single matching-bounds text child), but nothing on the tree shows
        // it sitting beside other text on the same line -- conservative: this still reports a WCAG issue
        // (with the updated, honest wording) rather than needs-review, once the spacing exception also
        // fails (placed next to another target).
        var terms = IosTextStyledLink(0, 0, 20, 15, "Terms");
        var neighbor = Target(20, 0, 20, 20);

        var findings = Evaluate(Snapshot(Platform.iOS, terms, neighbor));

        var issue = Assert.Single(findings, f => f.Kind == FindingKind.WcagIssue && f.Label == "Terms");
        Assert.Contains("could not tell from the accessibility tree whether this target sits within a sentence", issue.Message);
    }

    [Fact]
    public void RealButton_BesideSiblingText_DoesNotLookLikeInlineTextLink()
    {
        // A real button (its accessible name set directly on itself, e.g. an accessibilityLabel/title --
        // not derived from a text child, and no matching-bounds text child at all) sitting beside sibling
        // text is not text-styled, so this must not be flagged as looking like an inline link just because
        // there happens to be text nearby.
        var label = IosTextSibling(0, 0, 40, 20, "Pay with card");
        var button = new AccessibilityNode { Role = "button", IsInteractive = true, Label = "Submit", Bounds = new Bounds(40, 0, 20, 15) };
        var row = new AccessibilityNode { Role = "group", Bounds = new Bounds(0, 0, 60, 20), Children = [label, button] };

        Assert.False(TargetSizeRule.LooksLikeInlineTextLink(row, button, "1"));
    }

    [Fact]
    public void InlineCandidate_HiddenFromAccessibilityTree_IsExcluded()
    {
        var needHelp = new AccessibilityNode { Role = "text", VisibleText = "Need help?", Bounds = new Bounds(0, 0, 40, 20) };
        var terms = new AccessibilityNode
        {
            Role = "button",
            NativeType = "android.widget.TextView",
            IsInteractive = true,
            IsAccessible = false,
            VisibleText = "Terms",
            Bounds = new Bounds(40, 0, 20, 15),
        };
        var neighbor = Target(60, 0, 20, 20);
        var row = new AccessibilityNode { Role = "group", Bounds = new Bounds(0, 0, 80, 20), Children = [needHelp, terms, neighbor] };

        var findings = Evaluate(Snapshot(Platform.Android, row));

        Assert.DoesNotContain(findings, f => f.Label == "Terms");
    }

    [Fact]
    public void InlineCandidate_Disabled_IsStillFlaggedForReview()
    {
        // Disabled doesn't exempt a target from evaluation (see DisabledUndersizedIsolatedTarget_IsStillEvaluated
        // above) -- confirmed here for the needs-review-inline path specifically: spacing fails (close to
        // "neighbor") and the target looks like a text-styled link beside "Need help?", so it is still
        // flagged for review even though disabled.
        var needHelp = new AccessibilityNode { Role = "text", VisibleText = "Need help?", Bounds = new Bounds(0, 0, 40, 20) };
        var terms = new AccessibilityNode
        {
            Role = "button",
            NativeType = "android.widget.TextView",
            IsInteractive = true,
            IsEnabled = false,
            VisibleText = "Terms",
            Bounds = new Bounds(40, 0, 20, 15),
        };
        var neighbor = Target(60, 0, 20, 20);
        var row = new AccessibilityNode { Role = "group", Bounds = new Bounds(0, 0, 80, 20), Children = [needHelp, terms, neighbor] };

        var findings = Evaluate(Snapshot(Platform.Android, row));

        var finding = Assert.Single(findings, f => f.Label == "Terms");
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Contains("inline exception may apply", finding.Message);
    }

    [Fact]
    public void InlineCandidate_ZeroSizeBounds_IsExcluded()
    {
        var needHelp = new AccessibilityNode { Role = "text", VisibleText = "Need help?", Bounds = new Bounds(0, 0, 40, 20) };
        var terms = new AccessibilityNode
        {
            Role = "button",
            NativeType = "android.widget.TextView",
            IsInteractive = true,
            VisibleText = "Terms",
            Bounds = new Bounds(40, 0, 0, 0),
        };
        var neighbor = Target(60, 0, 20, 20);
        var row = new AccessibilityNode { Role = "group", Bounds = new Bounds(0, 0, 80, 20), Children = [needHelp, terms, neighbor] };

        var findings = Evaluate(Snapshot(Platform.Android, row));

        Assert.DoesNotContain(findings, f => f.Label == "Terms");
    }
}
