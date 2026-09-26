using System.Net;
using Swipewalk.Core.Coverage;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class HtmlReportGuidedChecksTests
{
    private static Finding NonTextContentFinding() => new()
    {
        RuleId = "missing-name", Kind = FindingKind.WcagIssue, Message = "test", NodePath = "0", Role = "image",
        Criteria = [WcagCriteria.NonTextContent],
    };

    private static ScreenResult Screen(string screenId, string name, IReadOnlyList<Finding>? findings = null) => new()
    {
        ScreenId = screenId, Platform = Platform.Android, ScreenName = name, Findings = findings ?? [],
    };

    private static GuidedAnswer PassAnswer(string screenId, string criterion, string evidence, string tester = "me") => new(
        ScreenId: screenId, CriterionNumber: criterion, Result: GuidedAnswerResult.Pass, Note: null,
        NotApplicableReason: null, FastPassConfirmed: false, Tester: tester, AnsweredAt: DateTimeOffset.Now,
        Device: null, AssistiveTechnology: "None", AssistiveTechnologyVersion: null, Evidence: [new GuidedEvidence(evidence)]);

    [Fact]
    public void NoGuidedAnswers_RendersNoGuidedChecksSection()
    {
        var report = new ScanReport { ToolVersion = "test", Screens = [Screen("s1", "Home")] };

        var html = HtmlReport.Render(report);

        Assert.DoesNotContain("guided-checks-title", html);
        Assert.DoesNotContain("<h2 id=\"guided-checks-title\">Guided checks</h2>", html);
    }

    [Fact]
    public void PassWithNoConflict_ShowsExactTesterFacingWordingAndNoFlag()
    {
        var report = new ScanReport
        {
            ToolVersion = "test",
            Screens = [Screen("s1", "Home")],
            GuidedAnswers = [PassAnswer("s1", "1.3.1", "structure looks fine")],
        };

        var html = HtmlReport.Render(report);

        Assert.Contains(WebUtility.HtmlEncode("The tester recorded a pass for 1.3.1 on 'Home', with this evidence: 'structure looks fine'."), html);
        Assert.DoesNotContain("Flagged", html);
        Assert.DoesNotContain("disagree with automated findings", html);
    }

    [Fact]
    public void PassContradictingAFinding_IsFlaggedNearTheTopAndOnTheScreen()
    {
        var report = new ScanReport
        {
            ToolVersion = "test",
            Screens = [Screen("s1", "Home", [NonTextContentFinding()])],
            GuidedAnswers = [PassAnswer("s1", "1.1.1", "all images look labeled")],
        };

        var html = HtmlReport.Render(report);

        Assert.Contains("Flagged: guided answers that disagree with automated findings or with an earlier answer", html);
        Assert.Contains(WebUtility.HtmlEncode(
            "The tester recorded a pass for 1.1.1 on 'Home', with this evidence: 'all images look labeled'. " +
            "Automated checks on this screen found 1 issue and 0 items to review citing 1.1.1. Check both."),
            html);
        Assert.Contains("Flagged", html); // the per-screen guided card also shows the chip
    }

    [Fact]
    public void AnswerDisagreeingWithAnOlderOne_ShowsTheDisagreementAndTheEarlierAnswerDetails()
    {
        var older = PassAnswer("s1", "1.3.1", "looked fine", tester: "alex");
        var newer = new GuidedAnswer(
            ScreenId: "s1", CriterionNumber: "1.3.1", Result: GuidedAnswerResult.ConfirmedNotApplicable, Note: null,
            NotApplicableReason: "no headings on this screen after all", FastPassConfirmed: false, Tester: "sam",
            AnsweredAt: older.AnsweredAt.AddDays(1), Device: null, AssistiveTechnology: "None",
            AssistiveTechnologyVersion: null, Evidence: []);
        var report = new ScanReport { ToolVersion = "test", Screens = [Screen("s1", "Home")], GuidedAnswers = [older, newer] };

        var html = HtmlReport.Render(report);

        // The answer-vs-answer contradiction sentence (ContradictionChecker's second pass).
        Assert.Contains(WebUtility.HtmlEncode(
            $"On 'Home', 1.3.1 was recorded as a pass on {older.AnsweredAt:yyyy-MM-dd}, then recorded as confirmed not applicable on {newer.AnsweredAt:yyyy-MM-dd}. Check both."),
            html);
        // The older, superseded answer is never dropped: it shows in an "Earlier answers" detail.
        Assert.Contains("Earlier answers (1)", html);
        Assert.Contains(WebUtility.HtmlEncode(GuidedChecksDisplay.AnswerOneLine(older)), html);
    }

    [Fact]
    public void ScreenWithNoGuidedAnswers_IsListedInTheGapCheck()
    {
        var report = new ScanReport
        {
            ToolVersion = "test",
            Screens = [Screen("s1", "Home"), Screen("s2", "Settings")],
            GuidedAnswers = [PassAnswer("s1", "1.3.1", "fine")],
        };

        var html = HtmlReport.Render(report);

        Assert.Contains("1 of 2 screens in this run have no guided checks started yet", html);
        Assert.Contains("Settings", html);
    }

    [Fact]
    public void ConfirmedNotApplicable_ShowsReasonOnTheScreenTab()
    {
        var answer = new GuidedAnswer(
            ScreenId: "s1", CriterionNumber: "1.3.1", Result: GuidedAnswerResult.ConfirmedNotApplicable, Note: null,
            NotApplicableReason: "no headings or groups on this screen", FastPassConfirmed: false, Tester: "me",
            AnsweredAt: DateTimeOffset.Now, Device: null, AssistiveTechnology: "None", AssistiveTechnologyVersion: null,
            Evidence: []);
        var report = new ScanReport { ToolVersion = "test", Screens = [Screen("s1", "Home")], GuidedAnswers = [answer] };

        var html = HtmlReport.Render(report);

        Assert.Contains("no headings or groups on this screen", html);
        Assert.Contains("Confirmed not applicable here", html);
    }

    [Fact]
    public void ScreenWithOnlyCapturedEvidence_ShowsTheGuidedChecksTabWithNoTesterAnswerNeeded()
    {
        // A screen with real screen-reader evidence but no tester answer must still surface that evidence --
        // it isn't gated behind a guided answer or a confirmed-not-applicable status.
        var item = new ScreenReaderCaptureItem(1, "Submit, Button", null, null, null, null, null, null, null, "0", MatchConfidence.Exact);
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow, [item], Complete: true, NotCompleteReason: null);
        var screen = Screen("s1", "Home") with { ScreenReaderCapture = capture };
        var report = new ScanReport { ToolVersion = "test", Screens = [screen] };

        var html = HtmlReport.Render(report);

        Assert.Contains("Guided checks</button>", html);
        Assert.Contains("Captured evidence (TalkBack)", html);
        Assert.Contains("4.1.2 Name, Role, Value (A)", html);
    }

    [Fact]
    public void RollupRow_OnlyShownForCriteriaWithAnAnswer()
    {
        var report = new ScanReport
        {
            ToolVersion = "test",
            Screens = [Screen("s1", "Home")],
            GuidedAnswers = [PassAnswer("s1", "1.3.2", "order makes sense")],
        };

        var html = HtmlReport.Render(report);

        Assert.Contains("1.3.2 Meaningful Sequence", html);
        // 1.3.1 has no answer in this run, so it must not get a roll-up row even though it's a real criterion.
        Assert.DoesNotContain("1.3.1 Info and Relationships (A)</td>", html);
    }
}
