using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class IdentifierNameRuleTests
{
    private static AccessibilityNode Node(
        string? label, string? automationId = null, string nativeType = "android.widget.Button",
        string? visibleText = null, bool isAccessible = true, bool isEnabled = true,
        Bounds? bounds = null) => new()
    {
        Role = "button",
        NativeType = nativeType,
        Label = label,
        VisibleText = visibleText,
        AutomationId = automationId,
        IsAccessible = isAccessible,
        IsEnabled = isEnabled,
        Bounds = bounds ?? new Bounds(0, 0, 10, 10),
    };

    private static ScreenSnapshot Snapshot(params AccessibilityNode[] children) => new()
    {
        Platform = Platform.Android,
        ScreenName = "Screen",
        Root = new AccessibilityNode { Role = "window", Children = children },
    };

    private static List<Finding> Evaluate(params AccessibilityNode[] children) =>
        new IdentifierNameRule().Evaluate(Snapshot(children)).ToList();

    [Theory]
    [InlineData("img_email_receipt")]
    [InlineData("btnSubmit")]
    public void LooksLikeIdentifier_FlagsDeveloperIdentifierLabels(string label)
    {
        // No visible text: the name is the text alternative of non-text content (1.1.1).
        var findings = Evaluate(Node(label));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.NonTextContent], finding.Criteria);
    }

    [Fact]
    public void IdentifierShownOnScreen_CitesHeadingsAndLabels()
    {
        var finding = Assert.Single(Evaluate(Node("btn_submit", visibleText: "btn_submit")));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.HeadingsAndLabels], finding.Criteria);
    }

    [Fact]
    public void HiddenIdentifierNextToDifferentVisibleText_NeedsReview()
    {
        var finding = Assert.Single(Evaluate(Node("btn_submit", visibleText: "Submit")));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Empty(finding.Criteria);
    }

    [Fact]
    public void LooksLikeIdentifier_TrueWhenNameEqualsAutomationId()
    {
        // "SaveButton" does not itself match the identifier patterns, but it is flagged when it
        // is identical to the element's AutomationId.
        var findings = Evaluate(Node("SaveButton", automationId: "SaveButton"));

        Assert.Single(findings);
    }

    [Theory]
    [InlineData("Help")]
    [InlineData("Submit")]
    [InlineData("iPhone")]
    [InlineData("Pay now")]
    [InlineData("OK")]
    public void LooksLikeIdentifier_DoesNotFlagOrdinaryLabels(string label)
    {
        var findings = Evaluate(Node(label));

        Assert.Empty(findings);
    }

    [Fact]
    public void PassingTree_NoIdentifierLikeLabels_ProducesNoFindings()
    {
        var findings = Evaluate(
            Node("Pay now", nativeType: "android.widget.Button"),
            Node("Email address", nativeType: "android.widget.EditText"));

        Assert.Empty(findings);
    }

    [Fact]
    public void ImageBasedControl_CitesNonTextContentInsteadOfHeadingsAndLabels()
    {
        var findings = Evaluate(Node("ic_close", nativeType: "android.widget.ImageButton"));

        var finding = Assert.Single(findings);
        Assert.Equal([WcagCriteria.NonTextContent], finding.Criteria);
    }

    [Fact]
    public void ImageBasedControl_TextFieldRole_CitesOnlyEmptyCriteria_NotNonTextContent()
    {
        // A textfield is not non-text content: an unnamed, empty textfield with an identifier-like
        // name has no WCAG criterion mapped (only 4.1.2 requires a name, which it already has).
        var textField = new AccessibilityNode
        {
            Role = "textfield",
            NativeType = "android.widget.EditText",
            Label = "et_email",
            IsAccessible = true,
            Bounds = new Bounds(0, 0, 200, 40),
        };
        var findings = new IdentifierNameRule().Evaluate(Snapshot(textField)).ToList();

        var finding = Assert.Single(findings);
        Assert.Empty(finding.Criteria);
    }

    [Fact]
    public void HiddenFromAccessibilityTree_IsExcluded()
    {
        var findings = Evaluate(Node("btnSubmit", isAccessible: false));

        Assert.Empty(findings);
    }

    [Fact]
    public void DisabledNode_IsStillFlagged()
    {
        // The rule does not filter by IsEnabled: a disabled control's label can still confuse a
        // screen reader user, and the control may become enabled later.
        var findings = Evaluate(Node("btnSubmit", isEnabled: false));

        Assert.Single(findings);
    }

    [Fact]
    public void ZeroSizeBounds_IsNotFlagged()
    {
        // Zero-size elements are off-screen or collapsed; all rules skip them consistently.
        var findings = Evaluate(Node("btnSubmit", bounds: new Bounds(0, 0, 0, 0)));

        Assert.Empty(findings);
    }

    [Fact]
    public void NoLabel_IsNotFlagged()
    {
        var findings = Evaluate(Node(null));

        Assert.Empty(findings);
    }

    [Fact]
    public void ReadableNameEqualToAutomationId_IsNotFlagged()
    {
        Assert.Empty(Evaluate(Node("Submit", automationId: "Submit")));
    }
}
