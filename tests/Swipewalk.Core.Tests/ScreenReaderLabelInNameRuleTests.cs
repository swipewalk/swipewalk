using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class ScreenReaderLabelInNameRuleTests
{
    private static AccessibilityNode Button(
        string? visibleText, string? label, IReadOnlyList<AccessibilityNode>? children = null,
        bool isInteractive = true, bool isAccessible = true, bool isEnabled = true, Bounds? bounds = null) => new()
    {
        Role = "button",
        VisibleText = visibleText,
        Label = label,
        IsInteractive = isInteractive,
        IsAccessible = isAccessible,
        IsEnabled = isEnabled,
        Bounds = bounds ?? new Bounds(0, 0, 100, 40),
        Children = children ?? [],
    };

    /// <summary>A plain, non-focusable descendant carrying its own text and/or label -- the shape a Compose
    /// button's children have in the real captured tree (tests/Swipewalk.Core.Tests/Fixtures/NativeAndroid.Compose/uiautomator.xml,
    /// node index 9): a non-zero size is needed, since <see cref="AccessibilityNode.Bounds"/> defaults to
    /// zero and a zero-size descendant's text/label is deliberately not treated as real.</summary>
    private static AccessibilityNode Descendant(
        string? visibleText = null, string? label = null, bool isInteractive = false, bool isFocusable = false,
        bool isAccessible = true) => new()
    {
        Role = "text",
        VisibleText = visibleText,
        Label = label,
        IsInteractive = isInteractive,
        IsFocusable = isFocusable,
        IsAccessible = isAccessible,
        Bounds = new Bounds(0, 0, 50, 20),
    };

    private static ScreenReaderCaptureItem Item(
        string spokenText, string path, MatchConfidence confidence = MatchConfidence.Exact) =>
        new(1, spokenText, null, null, null, null, null, null, null, path, confidence);

    private static ScreenSnapshot Snapshot(AccessibilityNode root, ScreenReaderCapture? capture) => new()
    {
        Platform = Platform.Android,
        ScreenName = "Screen",
        Root = new AccessibilityNode { Role = "window", Children = [root] },
        ScreenReaderCapture = capture,
    };

    private static ScreenReaderCapture Capture(
        IReadOnlyList<ScreenReaderCaptureItem> items, bool complete = true, string? language = null,
        ScreenReaderSource source = ScreenReaderSource.TalkBack) =>
        new(source, "17.0.1", DateTimeOffset.UtcNow, items, complete, complete ? null : "device disconnected", language);

    private static List<Finding> Evaluate(AccessibilityNode root, ScreenReaderCapture? capture) =>
        new ScreenReaderLabelInNameRule().Evaluate(Snapshot(root, capture)).ToList();

    // --- The real samples/NativeAndroid Compose evidence (docs/case-study.md, bug N5; the real fixture is
    // tests/Swipewalk.Core.Tests/Fixtures/NativeAndroid.Compose/uiautomator.xml, node index 9): the clickable
    // node's own content-desc and text are BOTH empty. Its non-focusable children are a View with
    // content-desc "Submit" and a TextView with text "Pay" -- two different descendants each carrying a name
    // or text, so UiAutomatorParser.TryMergeSingleDescendantName declines to merge either onto the clickable
    // node (see android-compose-merged-name in docs/limitations.md): the clickable node's own Label and
    // VisibleText both stay null. TalkBack (16.0.0, emulator) announced the button as "Submit || Pay || Button".

    private static AccessibilityNode N5Button(string visibleChildText = "Pay") =>
        Button(visibleText: null, label: null, children:
        [
            Descendant(label: "Submit"),
            Descendant(visibleText: visibleChildText),
        ]);

    [Fact]
    public void N5Fixture_SpokenNameContainsVisibleTextAsWholeWord_NoFinding()
    {
        // TalkBack's real capture said "Submit || Pay || Button" for this button. Merged (see
        // ScreenReaderCaptureComparer.ActualNameAndRole), the spoken NAME (role word "Button" stripped) is
        // "Submit Pay", which contains "Pay" as its own whole word -- so, per this rule's evidence (what a
        // real screen reader actually said), the visible text IS in the name a screen-reader user hears.
        // Deliberately does NOT fire: docs/case-study.md is explicit that this only shows what TalkBack
        // said, not whether Voice Access activates the button by saying "Pay" specifically -- a stricter
        // check would need to guess at speech-input matching this rule has no evidence for.
        var findings = Evaluate(N5Button(), Capture([Item("Submit || Pay || Button", "0")]));

        Assert.Empty(findings);
    }

    [Fact]
    public void DescendantVisibleTextMissingFromSpokenName_ReportsNeedsReview()
    {
        // Same shape as N5, but the captured name genuinely never includes "Pay" (unlike the real N5
        // capture above) -- the case this rule exists to catch, which the tree-only label-in-name rule
        // structurally cannot see (LabelInNameRule requires node.VisibleText, which is null on this
        // clickable node either way -- see android-compose-merged-name in docs/limitations.md).
        var findings = Evaluate(N5Button(), Capture([Item("Submit || Button", "0")]));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.LabelInName], finding.Criteria);
        Assert.Equal("screen-reader-label-in-name", finding.RuleId);
        Assert.Equal(Finding.DefaultSource, finding.Source);
        Assert.Contains("Pay", finding.Message);
        Assert.Equal("Pay", finding.Details["visibleText"]);
        Assert.Equal("Submit", finding.Details["spokenName"]);
    }

    [Fact]
    public void RoleWordCase_PayButton_NoFinding()
    {
        // The ordinary, non-merged case: visible text on the node itself, TalkBack speaking the name
        // followed by its role word ("Pay, Button") -- ParseAnnouncement strips "Button" before comparing.
        var button = Button(visibleText: "Pay", label: "Pay");
        var findings = Evaluate(button, Capture([Item("Pay, Button", "0")]));

        Assert.Empty(findings);
    }

    [Fact]
    public void OwnVisibleTextMismatch_WithATreeLabel_DefersToLabelInNameRule_NoFinding()
    {
        // Visible text and a mismatching label both on the node itself: LabelInNameRule already reports
        // this exact 2.5.3 concern from the tree alone, so this rule skips it here rather than duplicating
        // it (see RuleRunnerTests for the two rules run together).
        var button = Button(visibleText: "Pay", label: "Submit");
        var findings = Evaluate(button, Capture([Item("Submit, Button", "0")]));

        Assert.Empty(findings);
    }

    [Fact]
    public void OwnVisibleTextNoLabel_SpokenNameDiffers_ReportsNeedsReview()
    {
        // No Label at all: LabelInNameRule never runs on this node either way (it requires a Label), so
        // there is no tree-based finding to defer to, and a genuine spoken-name mismatch is reported
        // independently.
        var button = Button(visibleText: "Pay", label: null);
        var findings = Evaluate(button, Capture([Item("Submit, Button", "0")]));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.LabelInName], finding.Criteria);
    }

    [Fact]
    public void TreeSaysNameContainsVisibleText_ButCaptureDisagrees_ReportsIndependently()
    {
        // The tree's own Label ("Pay ticket") contains the visible text "Pay", so LabelInNameRule reports
        // nothing here -- this isn't a duplicate, so a genuine disagreement in what TalkBack actually said
        // is still worth surfacing on its own (real evidence contradicting the tree, not a guess).
        var button = Button(visibleText: "Pay", label: "Pay ticket");
        var findings = Evaluate(button, Capture([Item("Something else, Button", "0")]));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
    }

    [Fact]
    public void NonEnglishCapture_UntranslatedRoleWordBesideAMatchingName_NoFinding()
    {
        // TalkBack speaks its OWN role word in the device's language but never translates the app's own
        // accessible name (verified across five languages -- see docs/case-study.md, "Real TalkBack
        // capture, in five languages"). "Botón" (Spanish "Button") isn't in the comparer's English-only
        // role-word vocabulary, so it stays attached to the name rather than being stripped -- the
        // whole-word match must still find "Guardar" inside "Guardar, Botón".
        var button = Button(visibleText: "Guardar", label: "Guardar");
        var findings = Evaluate(button, Capture([Item("Guardar, Botón", "0")], language: "es-ES"));

        Assert.Empty(findings);
    }

    [Fact]
    public void NonTalkBackSource_ProducesNoFindings()
    {
        // Restricted to TalkBack for now: ScreenReaderCaptureComparer.NamesMatch's whole-word matching
        // (which this rule needs, since its own "predicted" side -- the visible text -- is never empty) is
        // only established for TalkBack; for any other source it falls back to exact equality only, which
        // would wrongly flag a real name like "Submit ticket" against visible text "Submit". No collector
        // produces a non-TalkBack capture today, so this only guards against that happening incorrectly
        // once one does.
        var button = Button(visibleText: "Pay", label: null);
        var item = new ScreenReaderCaptureItem(1, null, "Submit", null, null, null, null, null, null, "0", MatchConfidence.Exact);
        var capture = Capture([item], source: ScreenReaderSource.AccessibilityInspector);

        Assert.Empty(Evaluate(button, capture));
    }

    [Fact]
    public void NestedFocusableDescendantWithText_IsNotBorrowed_NoFinding()
    {
        // A nested, separately-focusable/interactive control (for example a "Delete" button inside a
        // clickable card) is its own screen-reader stop -- TalkBack focuses it on its own and doesn't fold
        // its text into the outer node's announcement, so this rule must not borrow that text as if it were
        // the outer node's own visible text (mirrors the same safety check
        // UiAutomatorParser.TryMergeSingleDescendantName uses for a related but different problem).
        var button = Button(visibleText: null, label: null, children:
        [
            new AccessibilityNode
            {
                Role = "button", VisibleText = "Delete", IsInteractive = true, IsAccessible = true,
                Bounds = new Bounds(0, 0, 50, 20),
            },
        ]);
        var findings = Evaluate(button, Capture([Item("Something, Button", "0")]));

        Assert.Empty(findings);
    }

    [Fact]
    public void NoCapture_ProducesNoFindings()
    {
        Assert.Empty(Evaluate(Button(visibleText: "Pay", label: "Submit"), null));
    }

    [Fact]
    public void EmptyCapture_ProducesNoFindings()
    {
        Assert.Empty(Evaluate(Button(visibleText: "Pay", label: "Submit"), Capture([])));
    }

    [Fact]
    public void IncompleteCapture_ProducesNoFindings()
    {
        // The capture stopped early (device disconnected, walk timed out, order wrapped -- see
        // ScreenReaderCapture.Complete): too unreliable a basis for a per-element citation, even for an
        // item it did capture before stopping.
        var button = Button(visibleText: "Pay", label: null);
        var findings = Evaluate(button, Capture([Item("Submit, Button", "0")], complete: false));

        Assert.Empty(findings);
    }

    [Fact]
    public void ElementNotWalkedByCapture_ProducesNoFindings()
    {
        var button = Button(visibleText: "Pay", label: null);
        // The capture has an item, but for a different node path entirely -- this button was never reached.
        var findings = Evaluate(button, Capture([Item("Something else", "9")]));

        Assert.Empty(findings);
    }

    [Fact]
    public void WeakMatchConfidence_ProducesNoFindings()
    {
        var button = Button(visibleText: "Pay", label: null);
        var findings = Evaluate(button, Capture([Item("Submit, Button", "0", MatchConfidence.Weak)]));

        Assert.Empty(findings);
    }

    [Fact]
    public void HiddenFromAccessibilityTree_IsExcluded()
    {
        var button = Button(visibleText: "Pay", label: "Submit", isAccessible: false);
        var findings = Evaluate(button, Capture([Item("Submit, Button", "0")]));

        Assert.Empty(findings);
    }

    [Fact]
    public void DisabledNode_IsStillFlagged()
    {
        // Same reasoning as LabelInNameRule.DisabledNode_IsStillFlagged: a disabled control's name doesn't
        // change just because it's temporarily unusable. No tree Label here (see
        // OwnVisibleTextNoLabel_SpokenNameDiffers_ReportsNeedsReview for why), so there's nothing to defer to.
        var button = Button(visibleText: "Pay", label: null, isEnabled: false);
        var findings = Evaluate(button, Capture([Item("Submit, Button", "0")]));

        Assert.Single(findings);
    }

    [Fact]
    public void ZeroSizeBounds_IsNotFlagged()
    {
        var button = Button(visibleText: "Pay", label: "Submit", bounds: new Bounds(0, 0, 0, 0));
        var findings = Evaluate(button, Capture([Item("Submit, Button", "0")]));

        Assert.Empty(findings);
    }

    [Fact]
    public void NonInteractiveElement_IsIgnored()
    {
        var button = Button(visibleText: "Pay", label: "Submit", isInteractive: false);
        var findings = Evaluate(button, Capture([Item("Submit, Button", "0")]));

        Assert.Empty(findings);
    }

    [Fact]
    public void NoVisibleTextAnywhere_IsIgnored()
    {
        var button = Button(visibleText: null, label: "Submit");
        var findings = Evaluate(button, Capture([Item("Submit, Button", "0")]));

        Assert.Empty(findings);
    }

    [Fact]
    public void SeveralDistinctDescendantTexts_AreAmbiguous_NotGuessed()
    {
        // Two descendants with different, non-blank visible text: this rule requires exactly one distinct
        // candidate, the same "don't guess with more than one" reasoning
        // UiAutomatorParser.TryMergeSingleDescendantName uses for a related but different problem (merging a
        // NAME, not visible text -- see android-compose-merged-name in docs/limitations.md).
        var button = Button(visibleText: null, label: "Submit", children:
        [
            Descendant(visibleText: "Pay"),
            Descendant(visibleText: "Now"),
        ]);
        var findings = Evaluate(button, Capture([Item("Submit || Pay Now || Button", "0")]));

        Assert.Empty(findings);
    }

    [Fact]
    public void TextFields_AreIgnored()
    {
        var field = new AccessibilityNode
        {
            Role = "textfield",
            VisibleText = "Enter your email",
            Label = "Email",
            IsInteractive = true,
            IsAccessible = true,
            Bounds = new Bounds(0, 0, 100, 40),
        };
        var findings = Evaluate(field, Capture([Item("Email, Edit box", "0")]));

        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("X", "Close")]
    [InlineData("×", "Close")]
    public void SingleSymbolVisibleText_IsNotFlagged(string visible, string label)
    {
        var button = Button(visibleText: visible, label: label);
        var findings = Evaluate(button, Capture([Item($"{label}, Button", "0")]));

        Assert.Empty(findings);
    }
}
