using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class MissingNameRuleTests
{
    private static List<Finding> Evaluate(AccessibilityNode node) => new MissingNameRule().Evaluate(new ScreenSnapshot
    {
        Platform = Platform.Android,
        ScreenName = "Screen",
        Root = new AccessibilityNode { Role = "window", Children = [node] },
    }).ToList();

    [Fact]
    public void InteractiveElementWithoutName_ReportsWcagIssue()
    {
        var button = new AccessibilityNode
        {
            Role = "button",
            NativeType = "android.widget.Button",
            IsInteractive = true,
            Bounds = new Bounds(0, 0, 100, 40),
        };

        var findings = Evaluate(button);

        // No name and no visible text: the control's content is non-text, so 1.1.1 applies too.
        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.WcagIssue, finding.Kind);
        Assert.Equal([WcagCriteria.NonTextContent, WcagCriteria.NameRoleValue], finding.Criteria);
        Assert.Contains("Unlabeled, Button", finding.Message);
    }

    [Fact]
    public void InteractiveElementWithName_ProducesNoFinding()
    {
        var button = new AccessibilityNode
        {
            Role = "button",
            IsInteractive = true,
            Label = "Submit",
            Bounds = new Bounds(0, 0, 100, 40),
        };

        Assert.Empty(Evaluate(button));
    }

    [Fact]
    public void ImageBasedInteractiveElementWithoutName_CitesNonTextContentAndNameRoleValue()
    {
        var iconButton = new AccessibilityNode
        {
            Role = "button",
            NativeType = "android.widget.ImageButton",
            IsInteractive = true,
            Bounds = new Bounds(0, 0, 48, 48),
        };

        var finding = Assert.Single(Evaluate(iconButton));

        Assert.Equal([WcagCriteria.NonTextContent, WcagCriteria.NameRoleValue], finding.Criteria);
    }

    [Fact]
    public void EmptyUnnamedTextField_CitesOnlyNameRoleValue_NotNonTextContent()
    {
        // A text input is not non-text content: an unnamed, empty textfield is a WCAG issue under
        // 4.1.2 only, not also 1.1.1. Unlike a field with rendered text, there is nothing ambiguous
        // here (no value/name channel could be hiding a name), so this stays a WcagIssue, not NeedsReview.
        var textField = new AccessibilityNode
        {
            Role = "textfield",
            NativeType = "android.widget.EditText",
            IsInteractive = true,
            Bounds = new Bounds(0, 0, 200, 40),
        };

        var finding = Assert.Single(Evaluate(textField));

        Assert.Equal(FindingKind.WcagIssue, finding.Kind);
        Assert.Equal([WcagCriteria.NameRoleValue], finding.Criteria);
    }

    [Fact]
    public void NonInteractiveImageWithoutTextAlternative_NeedsReview()
    {
        var image = new AccessibilityNode
        {
            Role = "image",
            IsInteractive = false,
            Bounds = new Bounds(0, 0, 48, 48),
        };

        var finding = Assert.Single(Evaluate(image));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.NonTextContent], finding.Criteria);
    }

    [Fact]
    public void NonInteractiveNonImageElementWithoutName_ProducesNoFinding()
    {
        var group = new AccessibilityNode
        {
            Role = "group",
            IsInteractive = false,
            Bounds = new Bounds(0, 0, 100, 40),
        };

        Assert.Empty(Evaluate(group));
    }

    [Fact]
    public void HiddenFromAccessibilityTree_InteractiveElementWithoutName_ProducesNoFinding()
    {
        var hiddenButton = new AccessibilityNode
        {
            Role = "button",
            IsInteractive = true,
            IsAccessible = false,
            Bounds = new Bounds(0, 0, 100, 40),
        };

        Assert.Empty(Evaluate(hiddenButton));
    }

    [Fact]
    public void DisabledInteractiveElementWithoutName_IsStillFlagged()
    {
        // The rule does not filter by IsEnabled: a disabled button without a name is still
        // announced only by role, and it may be re-enabled later.
        var disabledButton = new AccessibilityNode
        {
            Role = "button",
            IsInteractive = true,
            IsEnabled = false,
            Bounds = new Bounds(0, 0, 100, 40),
        };

        var finding = Assert.Single(Evaluate(disabledButton));

        Assert.Contains("disabled", finding.Message);
    }

    [Fact]
    public void UnnamedImage_InsideLabeledFocusableAncestor_ProducesNoFinding()
    {
        // Mirrors a real MAUI menu row: the row (a focusable/clickable wrapper) has no content-desc of
        // its own, but a non-focusable child carries it, and the icon is a sibling with no label. A
        // screen reader focuses the row once and announces the child's content-desc; the icon is read
        // as part of that single stop, so it needs no text alternative of its own.
        var icon = new AccessibilityNode { Role = "image", IsInteractive = false, Bounds = new Bounds(0, 0, 24, 24) };
        var labeledChild = new AccessibilityNode
        {
            Role = "group",
            IsInteractive = false,
            Label = "Dashboard",
            Bounds = new Bounds(24, 0, 100, 24),
        };
        var row = new AccessibilityNode
        {
            Role = "group",
            IsInteractive = true,
            IsFocusable = true,
            Bounds = new Bounds(0, 0, 124, 24),
            Children = [icon, labeledChild],
        };

        Assert.Empty(Evaluate(row));
    }

    [Fact]
    public void UnnamedImage_InsideUnlabeledAncestor_IsStillFlagged()
    {
        var icon = new AccessibilityNode { Role = "image", IsInteractive = false, Bounds = new Bounds(0, 0, 24, 24) };
        var row = new AccessibilityNode
        {
            Role = "group",
            IsInteractive = true,
            IsFocusable = true,
            Bounds = new Bounds(0, 0, 24, 24),
            Children = [icon],
        };

        var finding = Assert.Single(Evaluate(row), f => f.Role == "image");

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
    }

    [Fact]
    public void UnnamedButton_InsideLabeledNonFocusableContainer_IsStillFlagged()
    {
        // The ancestor exception only applies to non-interactive elements that aren't separately
        // reachable. A clickable button is always its own screen-reader stop, labeled container or not.
        var button = new AccessibilityNode
        {
            Role = "button",
            IsInteractive = true,
            Bounds = new Bounds(0, 0, 100, 40),
        };
        var container = new AccessibilityNode
        {
            Role = "group",
            IsInteractive = false,
            IsFocusable = false,
            Label = "Actions",
            Bounds = new Bounds(0, 0, 100, 40),
            Children = [button],
        };

        var finding = Assert.Single(Evaluate(container));

        Assert.Equal(FindingKind.WcagIssue, finding.Kind);
        Assert.Equal("button", finding.Role);
    }

    [Fact]
    public void UnnamedFocusableImage_InsideLabeledFocusableAncestor_IsStillFlagged()
    {
        // An image that is itself a focus stop is separately reachable, so a labeled ancestor around
        // it does not excuse a missing text alternative.
        var icon = new AccessibilityNode
        {
            Role = "image",
            IsInteractive = false,
            IsFocusable = true,
            Bounds = new Bounds(0, 0, 24, 24),
        };
        var labeledChild = new AccessibilityNode
        {
            Role = "group",
            IsInteractive = false,
            Label = "Dashboard",
            Bounds = new Bounds(24, 0, 100, 24),
        };
        var row = new AccessibilityNode
        {
            Role = "group",
            IsInteractive = true,
            IsFocusable = true,
            Bounds = new Bounds(0, 0, 124, 24),
            Children = [icon, labeledChild],
        };

        var finding = Assert.Single(Evaluate(row), f => f.Role == "image");

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
    }

    [Fact]
    public void TextFieldWithValueButNoLabelOrHint_NeedsReview()
    {
        // TipCalc on Android: an EditText holding a typed value ("15") with no content-desc and no
        // hint. IsShowingHintText is left null here (the harness didn't run for this capture), so
        // uiautomator's ambiguity applies: the scanner can't tell an entered value with no name apart
        // from a name that reaches the field's rendered text some other way (see KnownLimitations
        // "android-edittext-text"). This is flagged for review under 4.1.2, not asserted as a WcagIssue --
        // a regression test that this fallback behavior is unchanged now that the harness signal exists.
        var field = new AccessibilityNode
        {
            Role = "textfield",
            NativeType = "android.widget.EditText",
            IsInteractive = true,
            VisibleText = "15",
            Value = "15",
            Bounds = new Bounds(0, 0, 200, 40),
        };

        var finding = Assert.Single(Evaluate(field));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.NameRoleValue], finding.Criteria);
        Assert.Contains("15, Edit box", finding.Message);
        Assert.Contains("Check with TalkBack.", finding.Message);
    }

    [Fact]
    public void TextFieldWithValueButNoLabelOrHint_OnIos_SuggestsAnyScreenReader()
    {
        // Same shape of finding as the Android case above, but on iOS: the message should not tell
        // an iOS/Windows user to check with TalkBack specifically.
        var field = new AccessibilityNode
        {
            Role = "textfield",
            NativeType = "UITextField",
            IsInteractive = true,
            VisibleText = "15",
            Value = "15",
            Bounds = new Bounds(0, 0, 200, 40),
        };
        var snapshot = new ScreenSnapshot
        {
            Platform = Platform.iOS,
            ScreenName = "Screen",
            Root = new AccessibilityNode { Role = "window", Children = [field] },
        };

        var finding = Assert.Single(new MissingNameRule().Evaluate(snapshot));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Contains("Check with a screen reader (TalkBack, VoiceOver or Narrator).", finding.Message);
        Assert.DoesNotContain("Check with TalkBack.", finding.Message);
    }

    [Fact]
    public void TextFieldShowingItsHint_WithHintResolvedByTheHarness_ProducesNoFinding()
    {
        // The common real case the Android harness fixes: uiautomator's own "hint" attribute was empty
        // (some devices omit it), but UiAutomatorParser.Convert already filled in node.Hint from the
        // harness's getHintText() before this rule ever runs (see its doc comment). A field with a real
        // hint has a name via ScreenReaderPredictor.AccessibleName's fallback, so it's not flagged at all
        // -- this was a false positive before that upstream fix existed.
        var field = new AccessibilityNode
        {
            Role = "textfield",
            NativeType = "android.widget.EditText",
            IsInteractive = true,
            VisibleText = "Email",
            Hint = "Email",
            IsShowingHintText = true,
            Bounds = new Bounds(0, 0, 200, 40),
        };

        Assert.Empty(Evaluate(field));
    }

    [Fact]
    public void TextFieldWithValue_HarnessConfirmsHintTextButHintTextUnavailable_IsNeedsReview()
    {
        // An inconsistent-data fallback, not the common case above: the harness says the visible text is
        // the hint (isShowingHintText() == true) but couldn't also recover the hint's own text (node.Hint
        // stays null) -- an unusual node, since a standard TextView sets both together. In the state
        // actually captured, TalkBack does speak the placeholder, so this isn't a certain "no accessible
        // name" -- only that the name would disappear once something is typed, which can't be observed
        // from one snapshot. NeedsReview, not a certain WcagIssue.
        var field = new AccessibilityNode
        {
            Role = "textfield",
            NativeType = "android.widget.EditText",
            IsInteractive = true,
            VisibleText = "Subtotal",
            Value = "Subtotal",
            IsShowingHintText = true,
            Bounds = new Bounds(0, 0, 200, 40),
        };

        var finding = Assert.Single(Evaluate(field));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.NameRoleValue], finding.Criteria);
        Assert.Contains("placeholder", finding.Message);
        Assert.Contains("Subtotal", finding.Message);
    }

    [Fact]
    public void TextFieldWithValue_HarnessConfirmsEnteredValue_IsNeedsReview()
    {
        // The Android harness ran and reported isShowingHintText() == false: the visible text is
        // definitely the entered value, not a hint. That doesn't prove the field has no name at all --
        // it could still be labelled by another element via android:labelFor, which the harness doesn't
        // read yet (see KnownLimitations "android-edittext-text") -- so this stays NeedsReview, not a
        // certain WcagIssue, with wording distinct from the placeholder case above.
        var field = new AccessibilityNode
        {
            Role = "textfield",
            NativeType = "android.widget.EditText",
            IsInteractive = true,
            VisibleText = "15",
            Value = "15",
            IsShowingHintText = false,
            Bounds = new Bounds(0, 0, 200, 40),
        };

        var finding = Assert.Single(Evaluate(field));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.NameRoleValue], finding.Criteria);
        Assert.Contains("entered value", finding.Message);
        Assert.Contains("labelFor", finding.Message);
        Assert.DoesNotContain("placeholder", finding.Message);
    }

    [Fact]
    public void TextFieldWithHintAndValue_ProducesNoFinding()
    {
        // TipCalc on Android: an EditText showing its placeholder ("Subtotal") through both the hint
        // and text attributes. The hint/placeholder counts as the accessible name.
        var field = new AccessibilityNode
        {
            Role = "textfield",
            NativeType = "android.widget.EditText",
            IsInteractive = true,
            VisibleText = "Subtotal",
            Value = "Subtotal",
            Hint = "Subtotal",
            Bounds = new Bounds(0, 0, 200, 40),
        };

        Assert.Empty(Evaluate(field));
    }

    [Fact]
    public void TextFieldWithLabel_OverridesValue_ProducesNoFinding()
    {
        // BuggyApp X1 (Android): an Entry named through SemanticProperties.Description ends up with an
        // explicit accessible name (content-desc), so it is unaffected by the value/text ambiguity.
        var field = new AccessibilityNode
        {
            Role = "textfield",
            NativeType = "android.widget.EditText",
            IsInteractive = true,
            Label = "Ticket number",
            VisibleText = "15",
            Value = "15",
            Bounds = new Bounds(0, 0, 200, 40),
        };

        Assert.Empty(Evaluate(field));
    }

    [Fact]
    public void TextFieldWithValue_HiddenFromAccessibilityTree_ProducesNoFinding()
    {
        var field = new AccessibilityNode
        {
            Role = "textfield",
            IsInteractive = true,
            IsAccessible = false,
            Value = "15",
            VisibleText = "15",
            Bounds = new Bounds(0, 0, 200, 40),
        };

        Assert.Empty(Evaluate(field));
    }

    [Fact]
    public void TextFieldWithValue_ZeroSizeBounds_IsExcluded()
    {
        var field = new AccessibilityNode
        {
            Role = "textfield",
            IsInteractive = true,
            Value = "15",
            VisibleText = "15",
            Bounds = new Bounds(0, 0, 0, 0),
        };

        Assert.Empty(Evaluate(field));
    }

    [Fact]
    public void TextFieldWithValue_Disabled_IsStillFlagged()
    {
        var field = new AccessibilityNode
        {
            Role = "textfield",
            IsInteractive = true,
            IsEnabled = false,
            Value = "15",
            VisibleText = "15",
            Bounds = new Bounds(0, 0, 200, 40),
        };

        var finding = Assert.Single(Evaluate(field));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Contains("disabled", finding.Message);
    }

    [Fact]
    public void UnnamedFocusableSlider_IsFlagged()
    {
        // TipCalc on Android: a SeekBar with no content-desc, reported clickable="false" (it is
        // dragged, not tapped) but focusable="true"; treated as interactive like iOS's "slider" role.
        var slider = new AccessibilityNode
        {
            Role = "slider",
            NativeType = "android.widget.SeekBar",
            IsInteractive = true,
            IsFocusable = true,
            Bounds = new Bounds(0, 0, 300, 40),
        };

        var finding = Assert.Single(Evaluate(slider));

        Assert.Equal(FindingKind.WcagIssue, finding.Kind);
        // A slider has no visible text of its own (unlike a text field, it isn't a text-entry
        // control), so it is non-text content under 1.1.1 as well as missing a name under 4.1.2.
        Assert.Equal([WcagCriteria.NonTextContent, WcagCriteria.NameRoleValue], finding.Criteria);
        Assert.Contains("Unlabeled, Slider", finding.Message);
    }

    [Fact]
    public void NamedSlider_ProducesNoFinding()
    {
        var slider = new AccessibilityNode
        {
            Role = "slider",
            IsInteractive = true,
            IsFocusable = true,
            Label = "Tip percent",
            Bounds = new Bounds(0, 0, 300, 40),
        };

        Assert.Empty(Evaluate(slider));
    }

    [Fact]
    public void ZeroSizeBounds_IsExcluded()
    {
        var zeroSizeButton = new AccessibilityNode
        {
            Role = "button",
            IsInteractive = true,
            Bounds = new Bounds(0, 0, 0, 0),
        };

        Assert.Empty(Evaluate(zeroSizeButton));
    }
}
