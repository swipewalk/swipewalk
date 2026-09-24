using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class EngineIssueRuleTests
{
    private static Finding Evaluate(string type) => Assert.Single(new EngineIssueRule().Evaluate(new ScreenSnapshot
    {
        Platform = Platform.iOS,
        ScreenName = "Screen",
        Root = new AccessibilityNode { Role = "window" },
        EngineIssues = [new EngineIssue("Apple accessibility audit", type, "Issue", "0/1", "Pay", new Bounds(0, 0, 100, 40))],
    }));

    [Fact]
    public void Contrast_NeedsReviewUnderContrastMinimum()
    {
        var finding = Evaluate("contrast");

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.ContrastMinimum], finding.Criteria);
    }

    [Fact]
    public void Trait_NeedsReviewUnderNameRoleValue()
    {
        Assert.Equal([WcagCriteria.NameRoleValue], Evaluate("trait").Criteria);
    }

    [Theory]
    [InlineData("textClipped")]
    [InlineData("parentChild")]
    public void ClippedTextAndUnmappedTypes_NeedReviewWithoutCriterion(string type)
    {
        var finding = Evaluate(type);

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Empty(finding.Criteria);
    }

    [Fact]
    public void HitRegion_IsPlatformAdvisory()
    {
        var finding = Evaluate("hitRegion");

        Assert.Equal(FindingKind.PlatformAdvisory, finding.Kind);
        Assert.Empty(finding.Criteria);
    }
}
