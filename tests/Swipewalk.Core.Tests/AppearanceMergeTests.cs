using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class AppearanceMergeTests
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
        var dark = Screen(F("text-contrast", "0/1"));
        var light = Screen(F("text-contrast", "0/1"));

        var merged = AppearanceMerge.Merge(dark, AppearanceLabels.Dark, light, AppearanceLabels.Light);

        var finding = Assert.Single(merged.Findings);
        Assert.Equal(AppearanceLabels.Both, finding.Appearance);
    }

    [Fact]
    public void Merge_TagsAFindingSeenInOnlyOneAppearance()
    {
        var dark = Screen(); // no findings in dark mode
        var light = Screen(F("text-contrast", "0/1", "Daily Forecasts"));

        var merged = AppearanceMerge.Merge(dark, AppearanceLabels.Dark, light, AppearanceLabels.Light);

        var finding = Assert.Single(merged.Findings);
        Assert.Equal(AppearanceLabels.Light, finding.Appearance);
    }

    [Fact]
    public void Merge_KeepsPrimaryScreenshotAndRecordsTheOtherOne()
    {
        var dark = Screen() with { ScreenshotPath = "dark.png", PixelScale = 2.0 };
        var light = Screen() with { ScreenshotPath = "light.png", PixelScale = 3.0 };

        var merged = AppearanceMerge.Merge(dark, AppearanceLabels.Dark, light, AppearanceLabels.Light);

        Assert.Equal("dark.png", merged.ScreenshotPath);
        Assert.Equal(2.0, merged.PixelScale);
        Assert.Equal("light.png", merged.OtherAppearanceScreenshotPath);
        Assert.Equal(3.0, merged.OtherAppearancePixelScale);
        Assert.Equal(AppearanceLabels.Dark, merged.Appearance);
        Assert.Equal(AppearanceLabels.Light, merged.OtherAppearance);
    }

    [Fact]
    public void Merge_MatchesAMovedElementByRoleAndLabel_NotAsTwoSeparateFindings()
    {
        // Same issue, but the element's tree path shifted between the two captures (e.g. a different node
        // order), the same fallback ReportComparison uses across two runs.
        var dark = Screen(F("target-size", "0/2", "Help"));
        var light = Screen(F("target-size", "0/5", "Help"));

        var merged = AppearanceMerge.Merge(dark, AppearanceLabels.Dark, light, AppearanceLabels.Light);

        var finding = Assert.Single(merged.Findings);
        Assert.Equal(AppearanceLabels.Both, finding.Appearance);
    }

    [Fact]
    public void Merge_CountsRepeatedFindingsIndividually()
    {
        var dark = Screen(F("missing-name", "0/1"), F("missing-name", "0/2"));
        var light = Screen(F("missing-name", "0/1"));

        var merged = AppearanceMerge.Merge(dark, AppearanceLabels.Dark, light, AppearanceLabels.Light);

        Assert.Equal(2, merged.Findings.Count);
        Assert.Contains(merged.Findings, f => f.NodePath == "0/1" && f.Appearance == AppearanceLabels.Both);
        Assert.Contains(merged.Findings, f => f.NodePath == "0/2" && f.Appearance == AppearanceLabels.Dark);
    }
}
