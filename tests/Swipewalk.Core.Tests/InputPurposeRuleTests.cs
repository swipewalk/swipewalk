using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class InputPurposeRuleTests
{
    private static AccessibilityNode Field(
        string? label = null, string? hint = null, string? automationId = null,
        bool isAccessible = true, bool isEnabled = true, Bounds? bounds = null) => new()
    {
        Role = "textfield",
        Label = label,
        Hint = hint,
        AutomationId = automationId,
        IsAccessible = isAccessible,
        IsEnabled = isEnabled,
        Bounds = bounds ?? new Bounds(0, 0, 200, 40),
    };

    private static ScreenSnapshot Snapshot(Platform platform, AccessibilityNode field) => new()
    {
        Platform = platform,
        ScreenName = "Screen",
        Root = new AccessibilityNode { Role = "window", Children = [field] },
    };

    private static List<Finding> Evaluate(Platform platform, AccessibilityNode field) =>
        new InputPurposeRule().Evaluate(Snapshot(platform, field)).ToList();

    [Theory]
    [InlineData("First name")]
    [InlineData("Email")]
    [InlineData("Email address")]
    [InlineData("Phone number")]
    [InlineData("Street address")]
    [InlineData("Username")]
    [InlineData("Password")]
    [InlineData("Date of birth")]
    public void CommonPersonalDataLabel_NeedsReviewUnderIdentifyInputPurpose(string label)
    {
        var finding = Assert.Single(Evaluate(Platform.Android, Field(label: label)));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.IdentifyInputPurpose], finding.Criteria);
    }

    [Fact]
    public void HintOnlyMatch_StillFlagged()
    {
        var finding = Assert.Single(Evaluate(Platform.iOS, Field(hint: "Enter your email")));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
    }

    [Fact]
    public void AutomationIdCamelCase_SplitAndMatched()
    {
        // "txtEmailAddress" -> "txt Email Address" -> matches the email pattern.
        var finding = Assert.Single(Evaluate(Platform.Android, Field(automationId: "txtEmailAddress")));

        Assert.Equal([WcagCriteria.IdentifyInputPurpose], finding.Criteria);
    }

    [Fact]
    public void UnrelatedField_NoFinding()
    {
        var findings = Evaluate(Platform.Android, Field(label: "Plate number", hint: "License plate"));

        Assert.Empty(findings);
    }

    [Fact]
    public void BareNameLabel_IsNotFlagged_TooGenericByDesign()
    {
        // "Name" alone is deliberately excluded (file name, team name, ...) -- see
        // KnownLimitations "input-purpose-heuristic".
        var findings = Evaluate(Platform.Android, Field(label: "Name"));

        Assert.Empty(findings);
    }

    [Fact]
    public void NoLabelHintOrAutomationId_NoFinding()
    {
        Assert.Empty(Evaluate(Platform.Android, Field()));
    }

    [Fact]
    public void HiddenFromAccessibilityTree_NotFlagged()
    {
        var findings = Evaluate(Platform.Android, Field(label: "Email address", isAccessible: false));

        Assert.Empty(findings);
    }

    [Fact]
    public void DisabledField_NotFlagged()
    {
        var findings = Evaluate(Platform.Android, Field(label: "Email address", isEnabled: false));

        Assert.Empty(findings);
    }

    [Fact]
    public void ZeroSizeBounds_NotFlagged()
    {
        var findings = Evaluate(Platform.Android, Field(label: "Email address", bounds: new Bounds(0, 0, 0, 0)));

        Assert.Empty(findings);
    }

    [Fact]
    public void NonTextFieldRole_NotFlagged()
    {
        var node = new AccessibilityNode { Role = "text", VisibleText = "Email address", Bounds = new Bounds(0, 0, 100, 20) };
        var findings = new InputPurposeRule().Evaluate(new ScreenSnapshot
        {
            Platform = Platform.Android,
            ScreenName = "Screen",
            Root = new AccessibilityNode { Role = "window", Children = [node] },
        }).ToList();

        Assert.Empty(findings);
    }

    [Fact]
    public void AndroidMessage_NamesAndroidLimitation()
    {
        var finding = Assert.Single(Evaluate(Platform.Android, Field(label: "Email address")));

        Assert.Contains("Android", finding.Message);
    }

    [Fact]
    public void IosMessage_NamesTextContentType()
    {
        var finding = Assert.Single(Evaluate(Platform.iOS, Field(label: "Email address")));

        Assert.Contains("textContentType", finding.Message);
    }
}
