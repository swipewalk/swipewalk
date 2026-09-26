using Swipewalk.Core.Coverage;
using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class ApplicabilityRulesTests
{
    private static AccessibilityNode Root(params AccessibilityNode[] children) => new() { Role = "window", Children = children };

    private static List<string> ProposedNumbers(AccessibilityNode root) =>
        [.. ApplicabilityRules.Evaluate(root).Proposed.Select(p => p.CriterionNumber)];

    [Fact]
    public void EmptyScreen_ProposesTheNoInputAndNoInteractiveGroups()
    {
        var proposed = ProposedNumbers(Root());

        Assert.Contains("3.2.2", proposed);
        Assert.Contains("3.3.1", proposed);
        Assert.Contains("3.3.2", proposed);
        Assert.Contains("3.3.3", proposed);
        Assert.Contains("1.4.13", proposed);
        Assert.Contains("2.1.2", proposed);
        Assert.Contains("2.4.7", proposed);
        Assert.Contains("2.4.11", proposed);
        Assert.Contains("2.5.2", proposed);
        Assert.Contains("2.5.8", proposed);
        Assert.Contains("3.2.1", proposed);
        Assert.Contains("3.3.8", proposed);
        Assert.Contains("1.4.11", proposed); // no interactive node and no image node
        Assert.Contains("1.4.12", proposed); // no webview node
    }

    [Fact]
    public void ScreenWithATextField_DoesNotProposeTheNoInputGroup()
    {
        var root = Root(new AccessibilityNode { Role = "textfield", IsInteractive = true, IsFocusable = true });

        var proposed = ProposedNumbers(root);

        Assert.DoesNotContain("3.2.2", proposed);
        Assert.DoesNotContain("3.3.1", proposed);
        Assert.DoesNotContain("3.3.2", proposed);
        Assert.DoesNotContain("3.3.3", proposed);
        // The textfield is also interactive/focusable, so the "no interactive node" group doesn't fire either.
        Assert.DoesNotContain("2.5.8", proposed);
    }

    [Theory]
    [InlineData("android.widget.Spinner")]
    [InlineData("android.widget.DatePicker")]
    [InlineData("UIPickerView")]
    [InlineData("UISegmentedControl")]
    public void ScreenWithOnlyAPicker_DoesNotProposeTheNoInputGroup(string nativeType)
    {
        // Dropdowns/pickers commonly map to "button"/"group" with no Value set, so the role/Value signal alone
        // would wrongly propose N/A for a screen that clearly has an input control.
        var root = Root(new AccessibilityNode { Role = "group", NativeType = nativeType });

        Assert.DoesNotContain("3.3.1", ProposedNumbers(root));
    }

    [Fact]
    public void CustomControlWithAValueButNoNamedRole_DoesNotProposeTheNoInputGroup()
    {
        // The reviewer's required broadening: a custom control can expose a value/state without using one of
        // the named input roles.
        var root = Root(new AccessibilityNode { Role = "group", Value = "50%" });

        Assert.DoesNotContain("3.3.1", ProposedNumbers(root));
    }

    [Fact]
    public void ScreenWithAnInteractiveButton_DoesNotProposeTheNoInteractiveGroup()
    {
        var root = Root(new AccessibilityNode { Role = "button", IsInteractive = true });

        var proposed = ProposedNumbers(root);

        Assert.DoesNotContain("2.4.7", proposed);
        Assert.DoesNotContain("2.5.8", proposed);
        Assert.DoesNotContain("3.3.8", proposed);
        Assert.DoesNotContain("1.4.11", proposed); // interactive node present
    }

    [Fact]
    public void ScreenWithOnlyAnImage_DoesNotProposeNonTextContrast()
    {
        var root = Root(new AccessibilityNode { Role = "image" });

        Assert.DoesNotContain("1.4.11", ProposedNumbers(root));
    }

    [Fact]
    public void ScreenWithAWebView_DoesNotProposeTextSpacing()
    {
        var root = Root(new AccessibilityNode { Role = "webview" });

        Assert.DoesNotContain("1.4.12", ProposedNumbers(root));
    }

    [Fact]
    public void NameRoleValueAndKeyboard_AreNeverProposed_EvenOnACompletelyEmptyScreen()
    {
        // Required fix 3, by name: no tree signal, however empty, may ever propose N/A for these two.
        var proposed = ProposedNumbers(Root());

        Assert.DoesNotContain("4.1.2", proposed);
        Assert.DoesNotContain("2.1.1", proposed);
    }

    [Fact]
    public void NonTextContentImagesOfTextPointerGesturesAndDragging_AreNeverProposed_OnAnyFixture()
    {
        // Required fix 1: no safe tree signal exists for these four, ever, on any of the fixtures below.
        var fixtures = new[]
        {
            Root(),
            Root(new AccessibilityNode { Role = "button", IsInteractive = true }),
            Root(new AccessibilityNode { Role = "image" }),
            Root(new AccessibilityNode { Role = "textfield", IsInteractive = true }),
        };

        foreach (var fixture in fixtures)
        {
            var proposed = ProposedNumbers(fixture);
            Assert.DoesNotContain("1.1.1", proposed);
            Assert.DoesNotContain("1.4.5", proposed);
            Assert.DoesNotContain("2.5.1", proposed);
            Assert.DoesNotContain("2.5.7", proposed);
        }
    }

    [Fact]
    public void EveryProposedCriterionNumber_IsARealWcagCriterion()
    {
        var fixtures = new[] { Root(), Root(new AccessibilityNode { Role = "button", IsInteractive = true }) };
        var realNumbers = WcagCriteria.All.Select(c => c.Number).ToHashSet();

        foreach (var fixture in fixtures)
            foreach (var number in ProposedNumbers(fixture))
                Assert.Contains(number, realNumbers);
    }
}
