using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class OrientationMergeTests
{
    private static Finding F(string rule, string path, string? label = null, string role = "button") => new()
    {
        RuleId = rule,
        Kind = FindingKind.WcagIssue,
        Message = "Automated check found an issue.",
        Criteria = [WcagCriteria.NameRoleValue],
        NodePath = path,
        Role = role,
        Label = label,
    };

    private static ScreenResult Screen(params Finding[] findings) =>
        new() { Platform = Platform.Android, ScreenName = "Home", Findings = findings, ScreenshotPath = "shot.png" };

    [Fact]
    public void Merge_TagsAFindingInBothCapturesAsBoth()
    {
        var portrait = Screen(F("text-contrast", "0/1"));
        var landscape = Screen(F("text-contrast", "0/1"));

        var merged = OrientationMerge.Merge(portrait, OrientationLabels.Portrait, landscape, OrientationLabels.Landscape);

        var finding = Assert.Single(merged.Findings);
        Assert.Equal(OrientationLabels.Both, finding.Orientation);
    }

    [Fact]
    public void Merge_TagsAFindingSeenInOnlyOneOrientation()
    {
        var portrait = Screen();
        var landscape = Screen(F("target-size", "0/1", "Save"));

        var merged = OrientationMerge.Merge(portrait, OrientationLabels.Portrait, landscape, OrientationLabels.Landscape);

        var finding = Assert.Single(merged.Findings);
        Assert.Equal(OrientationLabels.Landscape, finding.Orientation);
    }

    [Fact]
    public void Merge_KeepsPrimaryScreenshotAndRecordsTheOtherOne()
    {
        var portrait = Screen() with { ScreenshotPath = "portrait.png", PixelScale = 2.0 };
        var landscape = Screen() with { ScreenshotPath = "landscape.png", PixelScale = 3.0 };

        var merged = OrientationMerge.Merge(portrait, OrientationLabels.Portrait, landscape, OrientationLabels.Landscape);

        Assert.Equal("portrait.png", merged.ScreenshotPath);
        Assert.Equal(2.0, merged.PixelScale);
        Assert.Equal("landscape.png", merged.OtherOrientationScreenshotPath);
        Assert.Equal(3.0, merged.OtherOrientationPixelScale);
        Assert.Equal(OrientationLabels.Portrait, merged.Orientation);
        Assert.Equal(OrientationLabels.Landscape, merged.OtherOrientation);
        Assert.False(merged.OrientationUnchanged);
    }

    [Fact]
    public void Merge_MatchesAMovedElementByRoleAndLabel_NotAsTwoSeparateFindings()
    {
        // A rotated layout can shift tree paths without being a new issue -- the same fallback AppearanceMerge
        // and ReportComparison use across two captures.
        var portrait = Screen(F("target-size", "0/2", "Help"));
        var landscape = Screen(F("target-size", "0/5", "Help"));

        var merged = OrientationMerge.Merge(portrait, OrientationLabels.Portrait, landscape, OrientationLabels.Landscape);

        var finding = Assert.Single(merged.Findings);
        Assert.Equal(OrientationLabels.Both, finding.Orientation);
    }

    [Fact]
    public void Merge_CountsRepeatedFindingsIndividually()
    {
        var portrait = Screen(F("missing-name", "0/1"), F("missing-name", "0/2"));
        var landscape = Screen(F("missing-name", "0/1"));

        var merged = OrientationMerge.Merge(portrait, OrientationLabels.Portrait, landscape, OrientationLabels.Landscape);

        Assert.Equal(2, merged.Findings.Count);
        Assert.Contains(merged.Findings, f => f.NodePath == "0/1" && f.Orientation == OrientationLabels.Both);
        Assert.Contains(merged.Findings, f => f.NodePath == "0/2" && f.Orientation == OrientationLabels.Portrait);
    }

    [Fact]
    public void Merge_NeverTagsRuleIdsThatOnlyRanOnThePrimaryCapture()
    {
        // text-resize (and friends) depend on the large-text capture, which the orientation rescan's second
        // capture never has -- a finding from it says nothing about the other orientation, so it must not be
        // tagged as though it were compared.
        var portrait = Screen(F("text-resize", "0/1"));
        var landscape = Screen();

        var merged = OrientationMerge.Merge(portrait, OrientationLabels.Portrait, landscape, OrientationLabels.Landscape);

        var finding = Assert.Single(merged.Findings);
        Assert.Null(finding.Orientation);
    }
}
