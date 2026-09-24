using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class ReportComparisonTests
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

    private static ScreenResult Screen(string name, params Finding[] findings) =>
        new() { Platform = Platform.Android, ScreenName = name, Findings = findings };

    private static ScanReport Report(params ScreenResult[] screens) => new() { ToolVersion = "test", Screens = screens };

    [Fact]
    public void Compare_SplitsFindingsIntoNewNoLongerFoundAndStillFound()
    {
        var earlier = Report(Screen("Home", F("missing-name", "0/1"), F("target-size", "0/2", "Help")));
        var later = Report(Screen("Home", F("missing-name", "0/1"), F("label-in-name", "0/3", "Submit")));

        var diff = ReportComparison.Compare(earlier, later);

        Assert.Equal("label-in-name", Assert.Single(diff.New).Finding.RuleId);
        Assert.Equal("target-size", Assert.Single(diff.NoLongerFound).Finding.RuleId);
        Assert.Equal("missing-name", Assert.Single(diff.StillFound).Finding.RuleId);
    }

    [Fact]
    public void Compare_MatchesMovedElementByLabel_AndRelabelledElementByPath()
    {
        var earlier = Report(Screen("Home", F("target-size", "0/2", "Help"), F("target-size", "0/5", "Close")));
        // "Help" moved in the tree; the element at 0/5 was relabelled.
        var later = Report(Screen("Home", F("target-size", "0/3", "Help"), F("target-size", "0/5", "Dismiss")));

        var diff = ReportComparison.Compare(earlier, later);

        Assert.Empty(diff.New);
        Assert.Empty(diff.NoLongerFound);
        Assert.Equal(2, diff.StillFound.Count);
    }

    [Fact]
    public void Compare_CountsRepeatedFindingsIndividually()
    {
        var earlier = Report(Screen("Home", F("missing-name", "0/1"), F("missing-name", "0/2"), F("missing-name", "0/3")));
        var later = Report(Screen("Home", F("missing-name", "0/1"), F("missing-name", "0/2")));

        var diff = ReportComparison.Compare(earlier, later);

        Assert.Equal("0/3", Assert.Single(diff.NoLongerFound).Finding.NodePath);
        Assert.Equal(2, diff.StillFound.Count);
    }

    [Fact]
    public void Compare_DoesNotReportFindingsOfScreensNotScannedAgainAsNoLongerFound()
    {
        var earlier = Report(Screen("Home", F("missing-name", "0/1")), Screen("Settings", F("missing-name", "0/4")));
        var later = Report(Screen("Home", F("missing-name", "0/1")), Screen("Profile", F("target-size", "0/2", "Edit")));

        var diff = ReportComparison.Compare(earlier, later);

        Assert.Empty(diff.NoLongerFound);
        Assert.Equal(["Settings"], diff.ScreensNotScannedAgain);
        Assert.Equal(["Profile"], diff.NewScreens);
        Assert.Equal("Profile", Assert.Single(diff.New).ScreenName);
    }

    [Fact]
    public void Compare_PutsFindingsFromAScreenNotScannedAgainInNotCheckedAgain_WithTheScreenNotScannedReason()
    {
        // Home is scanned in both runs; Settings (e.g. a terms-and-conditions or onboarding screen) only in the
        // earlier one — the person didn't reach it again this time.
        var earlier = Report(Screen("Home", F("missing-name", "0/1")), Screen("Settings", F("missing-name", "0/4"), F("target-size", "0/5", "Accept")));
        var later = Report(Screen("Home", F("missing-name", "0/1")));

        var diff = ReportComparison.Compare(earlier, later);

        Assert.Empty(diff.NoLongerFound);
        Assert.Equal(2, diff.NotCheckedAgain.Count);
        Assert.All(diff.NotCheckedAgain, sf => Assert.Equal("Settings", sf.ScreenName));
        Assert.All(diff.NotCheckedAgain, sf => Assert.Equal(ReportComparison.ScreenNotScannedAgainReason, sf.Reason));
        Assert.Equal(["Settings"], diff.ScreensNotScannedAgain);
    }

    [Fact]
    public void Compare_KeepsTheScreenNotScannedReasonApartFromTheCheckDidNotRunReason()
    {
        // Settings wasn't scanned again at all; Home was scanned again, but without a large-text capture, so its
        // text-resize finding is "not checked again" for a different reason.
        var earlier = Report(
            Screen("Home", F("text-resize", "0/4", "Late fees", "text")) with { LargeTextSetting = "200%" },
            Screen("Settings", F("missing-name", "0/1")));
        var later = Report(Screen("Home"));

        var diff = ReportComparison.Compare(earlier, later);

        Assert.Equal(2, diff.NotCheckedAgain.Count);
        var settings = diff.NotCheckedAgain.Single(sf => sf.ScreenName == "Settings");
        var home = diff.NotCheckedAgain.Single(sf => sf.ScreenName == "Home");
        Assert.Equal(ReportComparison.ScreenNotScannedAgainReason, settings.Reason);
        Assert.Equal(ReportComparison.CheckDidNotRunReason, home.Reason);
        Assert.NotEqual(settings.Reason, home.Reason);
    }

    [Fact]
    public void Compare_EveryEarlierFindingEndsUpInExactlyOneBucket()
    {
        var earlier = Report(
            Screen("Home", F("missing-name", "0/1"), F("target-size", "0/2", "Help")),
            Screen("Settings", F("missing-name", "0/4"), F("target-size", "0/5", "Accept")));
        var later = Report(Screen("Home", F("missing-name", "0/1")));

        var diff = ReportComparison.Compare(earlier, later);

        var earlierFindingPaths = earlier.Screens.SelectMany(s => s.Findings.Select(f => (s.ScreenName, f.NodePath))).ToList();
        var buckets = diff.NoLongerFound.Concat(diff.StillFound).Concat(diff.NotCheckedAgain)
            .Select(sf => (sf.ScreenName, sf.Finding.NodePath)).ToList();

        Assert.Equal(earlierFindingPaths.Count, buckets.Count);
        foreach (var path in earlierFindingPaths)
            Assert.Contains(path, buckets);
    }

    [Fact]
    public void Compare_ScreenOnlyInTheLaterRunIsNew_NotAWorseningLabel()
    {
        var earlier = Report(Screen("Home", F("missing-name", "0/1")));
        var later = Report(Screen("Home", F("missing-name", "0/1")), Screen("Onboarding", F("target-size", "0/2", "Skip")));

        var diff = ReportComparison.Compare(earlier, later);

        Assert.Equal(["Onboarding"], diff.NewScreens);
        Assert.Equal("Onboarding", Assert.Single(diff.New).ScreenName);
        Assert.Empty(diff.NoLongerFound);
        Assert.Empty(diff.NotCheckedAgain);
    }

    [Fact]
    public void Describe_AppendsTheReason_WhenPresent()
    {
        var finding = F("missing-name", "0/4");
        var withReason = new ScreenFinding("Settings", finding, ReportComparison.ScreenNotScannedAgainReason);
        var withoutReason = new ScreenFinding("Settings", finding);

        Assert.EndsWith(" — " + ReportComparison.ScreenNotScannedAgainReason, ReportComparison.Describe(withReason));
        Assert.DoesNotContain("—", ReportComparison.Describe(withoutReason));
    }

    [Fact]
    public void Compare_KeepsLargeTextFindingsApart_WhenTheLaterRunHadNoLargeTextCapture()
    {
        var earlier = Report(Screen("Home", F("text-resize", "0/4", "Late fees", "text")) with { LargeTextSetting = "200%" });

        var withoutLarge = ReportComparison.Compare(earlier, Report(Screen("Home")));
        var withLarge = ReportComparison.Compare(earlier, Report(Screen("Home") with { LargeTextSetting = "200%" }));

        Assert.Empty(withoutLarge.NoLongerFound);
        Assert.Single(withoutLarge.NotCheckedAgain);
        Assert.Single(withLarge.NoLongerFound);
        Assert.Empty(withLarge.NotCheckedAgain);
    }

    [Fact]
    public void Compare_KeepsContrastFindingsApart_WhenTheLaterScreenshotWasMissingOrBlocked()
    {
        var earlier = Report(Screen("Home", F("text-contrast", "0/4", "Payments", "text")) with { ScreenshotPath = "a.png" });
        var blocked = F("text-contrast", "", role: "screen");

        var noScreenshot = ReportComparison.Compare(earlier, Report(Screen("Home")));
        var blockedScreenshot = ReportComparison.Compare(earlier, Report(Screen("Home", blocked) with { ScreenshotPath = "b.png" }));
        var measured = ReportComparison.Compare(earlier, Report(Screen("Home") with { ScreenshotPath = "b.png" }));

        Assert.Single(noScreenshot.NotCheckedAgain);
        Assert.Empty(noScreenshot.NoLongerFound);
        Assert.Single(blockedScreenshot.NotCheckedAgain);
        Assert.Single(measured.NoLongerFound);
    }

    [Fact]
    public void Compare_KeepsEngineFindingsApart_WhenTheLaterScanHadNoEngineFindings()
    {
        var earlier = Report(Screen("Home", F("engine:contrast", "0/4", "Payments", "text"), F("engine:trait", "0/5", "Pay")));

        var noEngine = ReportComparison.Compare(earlier, Report(Screen("Home")));
        var engineRan = ReportComparison.Compare(earlier, Report(Screen("Home", F("engine:trait", "0/5", "Pay"))));

        Assert.Equal(2, noEngine.NotCheckedAgain.Count);
        Assert.Empty(noEngine.NoLongerFound);
        Assert.Equal("engine:contrast", Assert.Single(engineRan.NoLongerFound).Finding.RuleId);
    }

    [Fact]
    public void Compare_WorksOnReportsReadBackFromJson()
    {
        var report = Report(Screen("Home", F("missing-name", "0/1"), F("target-size", "0/2", "Help")));

        var diff = ReportComparison.Compare(JsonReport.Deserialize(JsonReport.Serialize(report))!, report);

        Assert.Equal(2, diff.StillFound.Count);
        Assert.Empty(diff.New);
    }

    [Fact]
    public void LargeTextSkippedReason_RoundTripsThroughJson()
    {
        var report = Report(Screen("Home") with { LargeTextSkippedReason = "the app wasn't in front after the text size changed; check large text by hand" });

        var roundTripped = JsonReport.Deserialize(JsonReport.Serialize(report))!;

        Assert.Equal("the app wasn't in front after the text size changed; check large text by hand", roundTripped.Screens[0].LargeTextSkippedReason);
    }

    [Fact]
    public void LargeTextSkippedReason_OmittedFromJson_WhenNull()
    {
        // Old results.json files have no such property; keep new ones backward-compatible by omitting it
        // (rather than writing "largeTextSkippedReason": null) when large text was never attempted or was captured.
        var report = Report(Screen("Home"));

        var json = JsonReport.Serialize(report);
        var roundTripped = JsonReport.Deserialize(json)!;

        Assert.DoesNotContain("largeTextSkippedReason", json);
        Assert.Null(roundTripped.Screens[0].LargeTextSkippedReason);
    }

    [Fact]
    public void LargeTextRestartCaptured_RoundTripsThroughJson()
    {
        var report = Report(Screen("Home") with { LargeTextMethod = "system setting", LargeTextRestartCaptured = true });

        var roundTripped = JsonReport.Deserialize(JsonReport.Serialize(report))!;

        Assert.True(roundTripped.Screens[0].LargeTextRestartCaptured);
    }

    [Fact]
    public void LargeTextRestartCaptured_OmittedFromJson_WhenNull()
    {
        // Old results.json files have no such property; keep new ones backward-compatible by omitting it
        // (rather than writing "largeTextRestartCaptured": null) when large text was never attempted, was
        // captured live without a restart escalation, or predates this field.
        var report = Report(Screen("Home"));

        var json = JsonReport.Serialize(report);
        var roundTripped = JsonReport.Deserialize(json)!;

        Assert.DoesNotContain("largeTextRestartCaptured", json);
        Assert.Null(roundTripped.Screens[0].LargeTextRestartCaptured);
    }

    [Fact]
    public void Describe_NamesScreenElementAndCriteria()
    {
        var text = ReportComparison.Describe(new ScreenFinding("Home", F("target-size", "0/2", "Help")));

        Assert.Equal("Home · button \"Help\": Automated check found an issue. (WCAG 4.1.2)", text);
    }

    [Fact]
    public void Describe_NoCriteria_SaysNoWcagCriterionMapped()
    {
        var finding = F("identifier-name", "0/2", "Help") with { Criteria = [] };

        var text = ReportComparison.Describe(new ScreenFinding("Home", finding));

        Assert.Equal("Home · button \"Help\": Automated check found an issue. (No WCAG criterion mapped)", text);
    }

    [Fact]
    public void Describe_PlatformAdvisoryWithNoCriteria_OmitsWcagSuffix()
    {
        var finding = F("target-size", "0/2", "Help") with { Kind = FindingKind.PlatformAdvisory, Criteria = [] };

        var text = ReportComparison.Describe(new ScreenFinding("Home", finding));

        Assert.Equal("Home · button \"Help\": Automated check found an issue.", text);
    }
}
