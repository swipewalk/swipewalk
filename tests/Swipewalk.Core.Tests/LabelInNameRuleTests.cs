using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class LabelInNameRuleTests
{
    private static AccessibilityNode Node(
        string? visibleText, string? label, string role = "button", bool isInteractive = true,
        bool isAccessible = true, bool isEnabled = true, Bounds? bounds = null) => new()
    {
        Role = role,
        VisibleText = visibleText,
        Label = label,
        IsInteractive = isInteractive,
        IsAccessible = isAccessible,
        IsEnabled = isEnabled,
        Bounds = bounds ?? new Bounds(0, 0, 100, 40),
    };

    private static List<Finding> Evaluate(AccessibilityNode node) => new LabelInNameRule().Evaluate(new ScreenSnapshot
    {
        Platform = Platform.Android,
        ScreenName = "Screen",
        Root = new AccessibilityNode { Role = "window", Children = [node] },
    }).ToList();

    [Fact]
    public void VisibleTextNotInName_ReportsWcagIssue()
    {
        var findings = Evaluate(Node(visibleText: "Pay", label: "Submit"));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.WcagIssue, finding.Kind);
        Assert.Equal([WcagCriteria.LabelInName], finding.Criteria);
    }

    [Fact]
    public void VisibleTextIsPrefixOfName_NoFinding()
    {
        var findings = Evaluate(Node(visibleText: "Pay", label: "Pay ticket"));

        Assert.Empty(findings);
    }

    [Fact]
    public void CaseAndPunctuationDifferences_AreIgnored()
    {
        var findings = Evaluate(Node(visibleText: "Pay!", label: "Pay Now"));

        Assert.Empty(findings);
    }

    [Fact]
    public void TextFields_AreIgnored()
    {
        var findings = Evaluate(Node(visibleText: "Enter your email", label: "Email", role: "textfield"));

        Assert.Empty(findings);
    }

    [Fact]
    public void NonInteractiveElement_IsIgnored()
    {
        var findings = Evaluate(Node(visibleText: "Pay", label: "Submit", isInteractive: false));

        Assert.Empty(findings);
    }

    [Fact]
    public void MissingLabelOrVisibleText_IsIgnored()
    {
        Assert.Empty(Evaluate(Node(visibleText: "Pay", label: null)));
        Assert.Empty(Evaluate(Node(visibleText: null, label: "Submit")));
    }

    [Fact]
    public void HiddenFromAccessibilityTree_IsExcluded()
    {
        var findings = Evaluate(Node(visibleText: "Pay", label: "Submit", isAccessible: false));

        Assert.Empty(findings);
    }

    [Fact]
    public void DisabledNode_IsStillFlagged()
    {
        // The rule does not filter by IsEnabled: it flags whenever visible text and accessible
        // name diverge, regardless of whether the control is currently enabled.
        var findings = Evaluate(Node(visibleText: "Pay", label: "Submit", isEnabled: false));

        Assert.Single(findings);
    }

    [Fact]
    public void ZeroSizeBounds_IsNotFlagged()
    {
        var findings = Evaluate(Node(visibleText: "Pay", label: "Submit", bounds: new Bounds(0, 0, 0, 0)));

        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("X", "Close")]
    [InlineData("×", "Close")]
    public void SingleSymbolVisibleText_IsNotFlagged(string visible, string label)
    {
        Assert.Empty(Evaluate(Node(visibleText: visible, label: label)));
    }

    [Fact]
    public void MergedComposeName_ContainsTheVisibleTextAsAWholeWord_NoFinding()
    {
        // The N5 shape after UiAutomatorParser.TryMergeDescendantName merges the Button's own semantics
        // content-desc ("Submit") and a separate visible-text child ("Pay") onto one clickable node:
        // Label becomes the full joined name ("Submit, Pay"), not just the content-desc part, precisely
        // so this never fires here -- matching the real TalkBack capture of this exact button announcing
        // both parts (see docs/case-study.md "Real TalkBack capture, in five languages": "Submit || Pay
        // || Button").
        var findings = Evaluate(Node(visibleText: "Pay", label: "Submit, Pay"));

        Assert.Empty(findings);
    }
}
