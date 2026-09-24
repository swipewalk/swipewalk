using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class AtfIssueRuleTests
{
    private static Finding Evaluate(string checkName) => Assert.Single(new AtfIssueRule().Evaluate(new ScreenSnapshot
    {
        Platform = Platform.Android,
        ScreenName = "Screen",
        Root = new AccessibilityNode { Role = "window" },
        AtfIssues = [new AtfIssue(checkName, "Issue", "0/1", "Pay", new Bounds(0, 0, 100, 40))],
    }));

    [Fact]
    public void TextContrastCheck_NeedsReviewUnderContrastMinimum()
    {
        var finding = Evaluate("TextContrastCheck");

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.ContrastMinimum], finding.Criteria);
        Assert.Equal(AtfIssueRule.EngineName, finding.Source);
        Assert.Equal("atf:TextContrastCheck", finding.RuleId);
    }

    [Fact]
    public void ImageContrastCheck_NeedsReviewUnderNonTextContrast()
    {
        Assert.Equal([WcagCriteria.NonTextContrast], Evaluate("ImageContrastCheck").Criteria);
    }

    [Fact]
    public void LinkPurposeUnclearCheck_NeedsReviewUnderLinkPurposeInContext()
    {
        Assert.Equal([WcagCriteria.LinkPurposeInContext], Evaluate("LinkPurposeUnclearCheck").Criteria);
    }

    [Fact]
    public void TraversalOrderCheck_NeedsReviewUnderMeaningfulSequenceAndFocusOrder()
    {
        Assert.Equal([WcagCriteria.MeaningfulSequence, WcagCriteria.FocusOrder], Evaluate("TraversalOrderCheck").Criteria);
    }

    [Fact]
    public void UnexposedTextCheck_NeedsReviewUnderNonTextContent()
    {
        Assert.Equal([WcagCriteria.NonTextContent], Evaluate("UnexposedTextCheck").Criteria);
    }

    [Fact]
    public void TextSizeCheck_NeedsReviewUnderResizeText()
    {
        Assert.Equal([WcagCriteria.ResizeText], Evaluate("TextSizeCheck").Criteria);
    }

    [Fact]
    public void TouchTargetSizeCheck_IsPlatformAdvisoryWithNoWcagCriteria()
    {
        var finding = Evaluate("TouchTargetSizeCheck");

        Assert.Equal(FindingKind.PlatformAdvisory, finding.Kind);
        Assert.Empty(finding.Criteria);
        Assert.NotNull(finding.PlatformGuideline);
    }

    [Theory]
    [InlineData("ClickableSpanCheck")]
    [InlineData("DuplicateClickableBoundsCheck")]
    [InlineData("DuplicateSpeakableTextCheck")]
    [InlineData("RedundantDescriptionCheck")]
    [InlineData("SomeFutureCheck")]
    public void UnmappedChecks_NeedReviewWithoutCriterion(string checkName)
    {
        var finding = Evaluate(checkName);

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Empty(finding.Criteria);
    }
}
