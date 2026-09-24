using System.Net;
using Swipewalk.Collectors;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Standards;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class HtmlReportTests
{
    private static Finding F(FindingKind kind, IReadOnlyList<WcagCriterion> criteria, string rule = "identifier-name") => new()
    {
        RuleId = rule,
        Kind = kind,
        Message = "Automated check found something that needs review.",
        Criteria = criteria,
        RelevantStandards = KnownStandards.RelevantTo(new Finding
        {
            RuleId = rule, Kind = kind, Message = "", Criteria = criteria, NodePath = "", Role = "button",
        }),
        NodePath = "0/1",
        Role = "button",
        Label = "et_email",
    };

    private static ScanReport Report(params Finding[] findings) => new()
    {
        ToolVersion = "test",
        Screens = [new ScreenResult { Platform = Platform.Android, ScreenName = "Home", Findings = findings }],
    };

    [Fact]
    public void NeedsReviewFindingWithNoCriteria_ShowsNoWcagCriterionMappedChip()
    {
        var html = HtmlReport.Render(Report(F(FindingKind.NeedsReview, [])));

        Assert.Contains("No WCAG criterion mapped", html);
    }

    [Fact]
    public void PlatformAdvisoryWithNoCriteria_DoesNotShowNoWcagCriterionMappedChip()
    {
        // Platform advisories are not WCAG findings by design (they are already labeled "not WCAG"
        // in their group heading), so they should not carry the "no criterion mapped" chip.
        var html = HtmlReport.Render(Report(F(FindingKind.PlatformAdvisory, [], rule: "target-size")));

        Assert.DoesNotContain("No WCAG criterion mapped", html);
    }

    [Fact]
    public void WcagIssueWithCriteria_DoesNotShowNoWcagCriterionMappedChip()
    {
        var html = HtmlReport.Render(Report(F(FindingKind.WcagIssue, [WcagCriteria.NameRoleValue])));

        Assert.DoesNotContain("No WCAG criterion mapped", html);
    }

    [Fact]
    public void FindingWithNoCriteria_IsNotCountedUnderAnyStandard()
    {
        // With only an unmapped finding, every standard's "outside its WCAG basis" count must be 0:
        // a finding with no criterion is not judged against any standard's basis at all.
        var report = Report(F(FindingKind.NeedsReview, []));

        var html = HtmlReport.Render(report);

        Assert.Empty(report.Screens[0].Findings[0].RelevantStandards);
        // The rendered "Outside its WCAG basis" column for every standard row should read 0, not 1:
        // the sole finding on this report has no criterion, so it is excluded from every row's tally.
        var outsideBasisCount = System.Text.RegularExpressions.Regex.Matches(html, "<td>0</td></tr>").Count;
        Assert.Equal(KnownStandards.All.Count, outsideBasisCount);
        Assert.DoesNotContain("<td>1</td></tr>", html);
    }

    [Fact]
    public void ScreenWithLargeTextSkippedReason_ShowsSkipTextInsteadOfClaimingAnythingPassed()
    {
        var screen = new ScreenResult { Platform = Platform.Android, ScreenName = "Home", Findings = [], LargeTextSkippedReason = LargeTextCapture.NotInFront };
        var report = new ScanReport { ToolVersion = "test", Screens = [screen] };

        var html = HtmlReport.Render(report);

        Assert.Contains(
            $"Large-text check not done: {WebUtility.HtmlEncode(LargeTextCapture.NotInFront)}. Check this screen at 200% text size by hand " +
            "(iOS: Settings > Accessibility > Display &amp; Text Size > Larger Text; Android: Settings > Display > Font size).", html);
        // The wording must not imply anything passed or was verified.
        Assert.DoesNotContain("large text passed", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScreenWithNoLargeTextAttempt_ShowsNoSkipMessage()
    {
        // Large text was never requested for this screen: LargeTextSkippedReason and LargeTextScreenshotPath
        // are both null, so nothing should be said about it in the per-screen figure area.
        var screen = new ScreenResult { Platform = Platform.Android, ScreenName = "Home", Findings = [] };
        var report = new ScanReport { ToolVersion = "test", Screens = [screen] };

        var html = HtmlReport.Render(report);

        Assert.DoesNotContain("Large-text check not done", html);
    }

    [Fact]
    public void ScreenWithBaselineTextSizeNote_ShowsItNearTheLargeTextCaption()
    {
        var note = Swipewalk.Collectors.BaselineTextSize.Warning("font_scale 1.3");
        var screen = new ScreenResult
        {
            Platform = Platform.Android, ScreenName = "Home", Findings = [],
            BaselineTextSizeNote = note,
            LargeTextSkippedReason = Swipewalk.Collectors.BaselineTextSize.AlreadyEnlargedReason,
        };
        var report = new ScanReport { ToolVersion = "test", Screens = [screen] };

        var html = HtmlReport.Render(report);

        Assert.Contains(WebUtility.HtmlEncode(note), html);
    }

    [Fact]
    public void ScreenWithNoBaselineTextSizeNote_ShowsNothingExtra()
    {
        var screen = new ScreenResult { Platform = Platform.Android, ScreenName = "Home", Findings = [] };
        var report = new ScanReport { ToolVersion = "test", Screens = [screen] };

        var html = HtmlReport.Render(report);

        Assert.DoesNotContain("baseline-note", html);
    }

    [Fact]
    public void ScreensScannedList_ShowsSkipReason_ForScreenWhereLargeTextWasNotChecked()
    {
        const string skipReason = "the app wasn't in front after the text size changed";
        var checkedScreen = new ScreenResult { Platform = Platform.Android, ScreenName = "Home", Findings = [], LargeTextScreenshotPath = "large.png", LargeTextSetting = "200%" };
        var skipped = new ScreenResult { Platform = Platform.Android, ScreenName = "Settings", Findings = [], LargeTextSkippedReason = skipReason };
        var report = new ScanReport { ToolVersion = "test", Screens = [checkedScreen, skipped] };

        var html = HtmlReport.Render(report);

        Assert.Contains("large text checked", html);
        Assert.Contains($"large text not checked: {WebUtility.HtmlEncode(skipReason)}", html);
    }

    [Fact]
    public void CoverageTable_PlatformAdvisoryOnlyCheckWithNoCriteria_ShowsNoneInsteadOfEmptyCell()
    {
        // e.g. text-resize-live: no WCAG criterion, platform advisory only. The criteria cell should say
        // so rather than rendering empty, which reads as a data-entry mistake rather than "by design".
        var html = HtmlReport.Render(Report());

        Assert.Contains("None (platform guideline only)", html);
    }

    [Fact]
    public void CoverageTable_CheckWithCriteria_ListsCriteriaNotNone()
    {
        var html = HtmlReport.Render(Report());

        Assert.Contains(WcagCriteria.NameRoleValue.ToString(), html);
    }

    [Fact]
    public void WcagCoverageSection_ContainsTheIntroSentence()
    {
        var html = HtmlReport.Render(Report());

        Assert.Contains(
            "Automated checks cover only part of WCAG. Every criterion below needs a person to check it; " +
            "statuses describe what this scan did, not whether the app meets the criterion.", html);
    }

    [Fact]
    public void WcagCoverageSection_ListsAllFiftyFiveCriteria()
    {
        var html = HtmlReport.Render(Report());

        Assert.Equal(55, System.Text.RegularExpressions.Regex.Matches(html, @"<td><a href=""https://www\.w3\.org/(WAI/WCAG22/Understanding|TR/wcag2ict-22)").Count);
    }

    [Fact]
    public void WcagCoverageSection_NotTestedGroup_AppearsBeforeOtherGroupsWithinTheFullTable()
    {
        // 1.4.4 is not tested in this run (no large-text capture); within the full 55-row table, its group
        // must still appear before the manual-check group.
        var html = HtmlReport.Render(Report());

        var fullTableStart = html.IndexOf("id=\"wcag-coverage-title\"", StringComparison.Ordinal);
        var notTested = html.IndexOf("Not tested in this run", fullTableStart, StringComparison.Ordinal);
        var manual = html.IndexOf("Needs a manual check", fullTableStart, StringComparison.Ordinal);
        Assert.True(fullTableStart >= 0 && notTested >= 0 && manual >= 0 && notTested < manual);
    }

    [Fact]
    public void FullWcagCoverageSection_IsAfterAllScreenSections_NotBetweenTopNoticesAndScreens()
    {
        // Moved so the 55-row table doesn't push the screens/findings far down the page.
        var report = Report(F(FindingKind.WcagIssue, [WcagCriteria.ContrastMinimum]));

        var html = HtmlReport.Render(report);

        var lastScreenIndex = html.LastIndexOf("class=\"screen\"", StringComparison.Ordinal);
        var fullTableIndex = html.IndexOf("id=\"wcag-coverage-title\"", StringComparison.Ordinal);
        var footerRuleTableIndex = html.IndexOf("Automated checks, by rule", StringComparison.Ordinal);
        Assert.True(lastScreenIndex >= 0);
        Assert.True(fullTableIndex > lastScreenIndex);
        Assert.True(fullTableIndex < footerRuleTableIndex);
        // Exactly one full 55-row table (not duplicated between top and bottom).
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "id=\"wcag-coverage-title\""));
    }

    [Fact]
    public void WcagGapsNotice_ListsOnlyNotTestedCriteria_NearTheTop_WhenThereAreGaps()
    {
        // Report()'s single screen has no screenshot and no large-text capture, so 1.4.3 and 1.4.4 are both
        // "Not tested in this run".
        var html = HtmlReport.Render(Report());

        var gapsIndex = html.IndexOf("id=\"wcag-gaps-title\"", StringComparison.Ordinal);
        var firstScreenIndex = html.IndexOf("class=\"screen\"", StringComparison.Ordinal);
        Assert.True(gapsIndex >= 0);
        Assert.True(firstScreenIndex < 0 || gapsIndex < firstScreenIndex);

        var gapsSectionEnd = html.IndexOf("</section>", gapsIndex, StringComparison.Ordinal);
        var gapsSection = html[gapsIndex..gapsSectionEnd];
        Assert.Contains("1.4.3 Contrast (Minimum) (AA)", gapsSection);
        Assert.Contains("1.4.4 Resize Text (AA)", gapsSection);
        // A criterion this run doesn't have a gap for (e.g. 4.1.2, which missing-name always runs) is not listed here.
        Assert.DoesNotContain("Name, Role, Value", gapsSection);
    }

    [Fact]
    public void WcagGapsNotice_RendersNothing_WhenThereAreNoGaps()
    {
        var screen = new ScreenResult
        {
            Platform = Platform.Android, ScreenName = "Home", Findings = [],
            ScreenshotPath = "a.png", LargeTextSetting = "font scale 2.0", AtfRan = true,
        };
        var report = new ScanReport { ToolVersion = "test", Screens = [screen] };

        var html = HtmlReport.Render(report);

        Assert.DoesNotContain("id=\"wcag-gaps-title\"", html);
    }

    [Fact]
    public void WcagCoverageSection_MultiScreenRun_ShowsScreensRanOutOfTotal()
    {
        var screens = new[]
        {
            new ScreenResult { Platform = Platform.Android, ScreenName = "Home", Findings = [], ScreenshotPath = "a.png" },
            new ScreenResult { Platform = Platform.Android, ScreenName = "Settings", Findings = [], ScreenshotPath = "b.png" },
        };
        var report = new ScanReport { ToolVersion = "test", Screens = screens };

        var html = HtmlReport.Render(report);

        Assert.Contains("ran on 2 of 2 screen(s)", html);
    }

    [Fact]
    public void SummaryLine_AppearsNearTheTopOfTheReport_AndLinksToTheFullCoverageSection()
    {
        var html = HtmlReport.Render(Report());

        Assert.Contains("WCAG 2.2 A/AA:", html);
        Assert.Contains("criteria partly checked by automation", html);
        Assert.Contains("<a href=\"#wcag-coverage-title\">See WCAG 2.2 coverage.</a>", html);
        // Appears before the first screen section, i.e. near the header counts, not buried at the bottom.
        var summaryIndex = html.IndexOf("WCAG 2.2 A/AA:", StringComparison.Ordinal);
        var firstScreenIndex = html.IndexOf("class=\"screen\"", StringComparison.Ordinal);
        Assert.True(summaryIndex >= 0 && (firstScreenIndex < 0 || summaryIndex < firstScreenIndex));
    }

    [Fact]
    public void Report_ContainsNoComplianceVerdictWord()
    {
        var report = Report(F(FindingKind.WcagIssue, [WcagCriteria.ContrastMinimum]));

        var html = HtmlReport.Render(report);

        foreach (var word in VerdictWords.All)
            Assert.DoesNotContain(word, html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScreenWithNoScreenReaderCapture_ShowsNoCapturedTab()
    {
        // No collector produces this evidence yet: the tab must simply not appear, rather than showing a
        // permanent "not captured" placeholder on every screen of every report.
        var report = Report();

        var html = HtmlReport.Render(report);

        Assert.DoesNotContain("Screen reader (captured)", html);
    }

    [Fact]
    public void ScreenWithEmptyScreenReaderCapture_ShowsNoCapturedTab()
    {
        // A capture with zero items (as opposed to no capture at all) is treated the same way: nothing to show.
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow, [], true, null);
        var screen = new ScreenResult { Platform = Platform.Android, ScreenName = "Home", Findings = [], ScreenReaderCapture = capture };
        var report = new ScanReport { ToolVersion = "test", Screens = [screen] };

        var html = HtmlReport.Render(report);

        Assert.DoesNotContain("Screen reader (captured)", html);
    }

    [Fact]
    public void ScreenWithTalkBackCapture_NamesTheToolAndShowsDifferences()
    {
        var item = new ScreenReaderCaptureItem(1, "Something else, Button", null, null, null, null, null, null, null, "0/1", MatchConfidence.Exact);
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow, [item], true, null);
        var screen = new ScreenResult
        {
            Platform = Platform.Android, ScreenName = "Home", Findings = [], ScreenReaderCapture = capture,
        };
        var report = new ScanReport { ToolVersion = "test", Screens = [screen] };

        var html = HtmlReport.Render(report);

        Assert.Contains("What TalkBack actually said", html);
        Assert.Contains("17.0.1", html);
        Assert.Contains("Covered the whole screen.", html);
        // A name mismatch between the predicted transcript (this screen has no findings/tree, so the
        // predicted transcript is empty) and the captured item shows as an Extra difference, since there is
        // no predicted stop at "0/1" to compare against.
        Assert.Contains("class=\"chip diff-extra\"", html);
    }

    [Fact]
    public void ScreenWithAccessibilityInspectorCapture_SaysItIsNotRecordedSpeech()
    {
        var item = new ScreenReaderCaptureItem(1, null, "Terms", null, [], null, null, "Static Text", null, "0", MatchConfidence.Exact);
        var capture = new ScreenReaderCapture(ScreenReaderSource.AccessibilityInspector, "Xcode 27.0", DateTimeOffset.UtcNow, [item], true, null);
        var screen = new ScreenResult
        {
            Platform = Platform.iOS, ScreenName = "Home", Findings = [], ScreenReaderCapture = capture,
        };
        var report = new ScanReport { ToolVersion = "test", Screens = [screen] };

        var html = HtmlReport.Render(report);

        Assert.Contains("Captured from Xcode&#39;s Accessibility Inspector", html);
        Assert.Contains("This is not recorded speech.", html);
    }

    [Fact]
    public void IncompleteCapture_ShowsTheNotCompleteReason()
    {
        var item = new ScreenReaderCaptureItem(1, "Something", null, null, null, null, null, null, null, "0", MatchConfidence.Exact);
        var capture = new ScreenReaderCapture(
            ScreenReaderSource.VoiceOverCaptions, "iOS 18", DateTimeOffset.UtcNow, [item], false, "device disconnected mid-capture");
        var screen = new ScreenResult
        {
            Platform = Platform.iOS, ScreenName = "Home", Findings = [], ScreenReaderCapture = capture,
        };
        var report = new ScanReport { ToolVersion = "test", Screens = [screen] };

        var html = HtmlReport.Render(report);

        Assert.Contains("Did not cover the whole screen: device disconnected mid-capture.", html);
    }
}
