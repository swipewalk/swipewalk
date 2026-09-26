using Swipewalk.Core.Coverage;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class CoverageReportTests
{
    private static Finding F(string rule, FindingKind kind, WcagCriterion criterion, string role = "button") => new()
    {
        RuleId = rule,
        Kind = kind,
        Message = "test",
        NodePath = "0",
        Role = role,
        Criteria = kind == FindingKind.PlatformAdvisory ? [] : [criterion],
        PlatformGuideline = kind == FindingKind.PlatformAdvisory ? "some platform guideline" : null,
    };

    private static ScreenResult Screen(
        Platform platform = Platform.Android, string? screenshotPath = null, string? largeTextSetting = null,
        IReadOnlyList<Finding>? findings = null, bool atfRan = false) => new()
    {
        Platform = platform,
        ScreenName = "Home",
        ScreenshotPath = screenshotPath,
        LargeTextSetting = largeTextSetting,
        Findings = findings ?? [],
        AtfRan = atfRan,
    };

    [Fact]
    public void Build_ReturnsExactlyFiftyFiveEntries_InCatalogOrder()
    {
        var entries = CoverageReport.Build([Screen()]);

        Assert.Equal(55, entries.Count);
        Assert.Equal(CoverageCatalog.All.Select(c => c.Criterion.Number), entries.Select(e => e.Number));
    }

    [Fact]
    public void ScanReport_Coverage_HasFiftyFiveEntries_AndRoundTripsThroughJson()
    {
        var report = new ScanReport { ToolVersion = "test", Screens = [Screen()] };

        Assert.Equal(55, report.Coverage.Count);

        var json = JsonReport.Serialize(report);
        Assert.Contains("\"coverage\"", json);

        var roundTripped = JsonReport.Deserialize(json)!;
        Assert.Equal(55, roundTripped.Coverage.Count);
    }

    [Fact]
    public void OldResultsJsonWithoutACoverageField_StillLoads_AndComputesCoverageFresh()
    {
        // Simulates a results.json written before this field existed: no "coverage" key at all.
        var old = """{"schemaVersion":"0.1","toolVersion":"test","screens":[{"platform":"android","screenName":"Home","findings":[]}]}""";

        var report = JsonReport.Deserialize(old);

        Assert.NotNull(report);
        Assert.Equal(55, report!.Coverage.Count);
    }

    [Fact]
    public void RunWithoutLargeText_ResizeTextIsNotTestedInThisRun_WithReason()
    {
        // No --large-text: LargeTextSetting and LargeTextSkippedReason are both null.
        var entries = CoverageReport.Build([Screen()]);

        var resizeText = Assert.Single(entries, e => e.Number == "1.4.4");
        Assert.Equal(CoverageRunStatus.NotTestedInThisRun, resizeText.RunStatus);
        Assert.Contains("large-text check not requested", resizeText.SkipReasons);
    }

    [Fact]
    public void AndroidRun_EngineListedAsNotRun_ButCriteriaStayPartlyAutomated_WhenOtherRulesRan()
    {
        var screen = Screen(
            platform: Platform.Android, screenshotPath: "shot.png", largeTextSetting: "font scale 2.0",
            findings: [F("missing-name", FindingKind.WcagIssue, WcagCriteria.NameRoleValue)]);

        var entries = CoverageReport.Build([screen]);

        foreach (var number in new[] { "1.4.3", "1.4.4", "4.1.2" })
        {
            var entry = Assert.Single(entries, e => e.Number == number);
            Assert.Equal(CoverageRunStatus.PartlyAutomated, entry.RunStatus);
            Assert.Contains("engine", entry.RulesThatDidNotRun);
        }
    }

    [Fact]
    public void PlatformAdvisoryFinding_IsNeverCountedInWcagIssueOrNeedsReviewCounts()
    {
        var screen = Screen(screenshotPath: "shot.png", findings: [F("target-size", FindingKind.PlatformAdvisory, WcagCriteria.TargetSizeMinimum)]);

        var entries = CoverageReport.Build([screen]);

        var targetSize = Assert.Single(entries, e => e.Number == "2.5.8");
        Assert.Equal(0, targetSize.WcagIssueCount);
        Assert.Equal(0, targetSize.NeedsReviewCount);
    }

    [Fact]
    public void RecordModeMultiScreenRun_CountsScreensRanOutOfScreensTotal()
    {
        var screens = new[]
        {
            Screen(screenshotPath: "a.png"),
            Screen(screenshotPath: "b.png"),
            Screen(screenshotPath: null),
        };

        var entries = CoverageReport.Build(screens);

        var contrast = Assert.Single(entries, e => e.Number == "1.4.3");
        Assert.Equal(CoverageRunStatus.PartlyAutomated, contrast.RunStatus);
        Assert.Equal(2, contrast.ScreensRan);
        Assert.Equal(3, contrast.ScreensTotal);
    }

    [Fact]
    public void Summary_MatchesTheDocumentedExample_WhenNothingWasSkipped()
    {
        // A screen with a screenshot, a large-text capture and a completed ATF harness run so nothing that
        // could run on this platform is skipped: every PartlyAutomated criterion whose mapped rule(s) can run
        // on Android stays PartlyAutomated instead of falling back to NotTestedInThisRun. This screen is
        // Android (Screen()'s default), so 1.4.10 Reflow -- whose only mapped rule, offscreen-unreachable,
        // runs on iOS only -- is the one exception: NotTestedInThisRun here, not a gap in this test's setup.
        var screen = Screen(screenshotPath: "a.png", largeTextSetting: "font scale 2.0", atfRan: true);

        var summary = CoverageReport.Summary(CoverageReport.Build([screen]));

        Assert.Equal(
            "WCAG 2.2 A/AA: 13 criteria partly checked by automation, 37 need a manual check, 4 usually out of scope, 1 not tested in this run.",
            summary);
    }

    [Fact]
    public void Summary_ContainsNoComplianceVerdict()
    {
        var summary = CoverageReport.Summary(CoverageReport.Build([Screen()]));

        foreach (var word in VerdictWords.All)
            Assert.DoesNotContain(word, summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IosScreenWithoutLargeTextCapture_LabelMentionsTheLargeTextRescanNotRun_WithReason()
    {
        // The Apple audit ran (iOS), so 1.4.4 is PartlyAutomated overall -- but the large-text rescan itself
        // didn't run, and that gap must not disappear behind the criterion-level status.
        var screen = Screen(platform: Platform.iOS, screenshotPath: "shot.png", largeTextSetting: null);

        var entries = CoverageReport.Build([screen]);

        var resizeText = Assert.Single(entries, e => e.Number == "1.4.4");
        Assert.Equal(CoverageRunStatus.PartlyAutomated, resizeText.RunStatus);
        Assert.Contains("Not run: large-text rescan (large-text check not requested).", resizeText.Label);
        // Never a raw rule id in the reader-facing label.
        Assert.DoesNotContain("text-resize", resizeText.Label);
    }

    [Fact]
    public void AndroidRun_EngineGapIsPhrasedAsIosOnly_NotAsAnAlarmingFailure()
    {
        var screen = Screen(platform: Platform.Android, screenshotPath: "shot.png", largeTextSetting: "font scale 2.0");

        var entries = CoverageReport.Build([screen]);

        foreach (var number in new[] { "1.4.3", "1.4.4", "4.1.2" })
        {
            var entry = Assert.Single(entries, e => e.Number == number);
            Assert.Contains("Apple accessibility audit (iOS only).", entry.Label);
            Assert.DoesNotContain("Not run: Apple accessibility audit", entry.Label);
            Assert.DoesNotContain("engine", entry.Label);
        }
    }

    [Fact]
    public void RuleRanOnSomeScreensButNotAll_LabelSaysHowManyOutOfHowMany()
    {
        var screens = new[]
        {
            Screen(screenshotPath: "a.png", largeTextSetting: "font scale 2.0"),
            Screen(screenshotPath: "b.png", largeTextSetting: null),
            Screen(screenshotPath: "c.png", largeTextSetting: null),
        };

        var entries = CoverageReport.Build(screens);

        var resizeText = Assert.Single(entries, e => e.Number == "1.4.4");
        Assert.Equal(CoverageRunStatus.PartlyAutomated, resizeText.RunStatus);
        Assert.Contains("large-text rescan ran on 1 of 3 screen(s).", resizeText.Label);
    }

    [Fact]
    public void RuleThatRanOnEveryScreen_IsNotMentionedInTheGapText()
    {
        // text-contrast ran on the only screen, so it needs no "ran on X of Y" or "Not run" mention.
        var screen = Screen(screenshotPath: "a.png", largeTextSetting: "font scale 2.0");

        var entries = CoverageReport.Build([screen]);

        var contrast = Assert.Single(entries, e => e.Number == "1.4.3");
        Assert.DoesNotContain("contrast measurement", contrast.Label);
    }
}
