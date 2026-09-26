using Swipewalk.Core.Coverage;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class ScreenCoverageBuilderTests
{
    private static ScreenResult Screen(string screenId, IReadOnlyList<Finding>? findings = null, DateTimeOffset? rescannedAt = null) => new()
    {
        ScreenId = screenId,
        Platform = Platform.Android,
        ScreenName = "Home",
        Findings = findings ?? [],
        RescannedAt = rescannedAt,
    };

    private static Finding MissingNameFinding(FindingKind kind = FindingKind.WcagIssue) => new()
    {
        RuleId = "missing-name",
        Kind = kind,
        Message = "test",
        NodePath = "0",
        Role = "image",
        Criteria = [WcagCriteria.NonTextContent],
    };

    private static GuidedAnswer Answer(string screenId, string criterion, GuidedAnswerResult result,
        string? notApplicableReason = null, DateTimeOffset? answeredAt = null, IReadOnlyList<GuidedEvidence>? evidence = null) => new(
        ScreenId: screenId, CriterionNumber: criterion, Result: result, Note: null,
        NotApplicableReason: notApplicableReason, FastPassConfirmed: false, Tester: "me",
        AnsweredAt: answeredAt ?? DateTimeOffset.Now, Device: null, AssistiveTechnology: "None",
        AssistiveTechnologyVersion: null, Evidence: evidence ?? (result == GuidedAnswerResult.Pass ? [new GuidedEvidence("evidence")] : []));

    private static ScreenCriterionReport Row(IReadOnlyList<ScreenCriterionReport> rows, string number) =>
        Assert.Single(rows, r => r.Number == number);

    [Fact]
    public void NoAnswerAndNoRuleRan_IsNotTested()
    {
        var rows = ScreenCoverageBuilder.Build([Screen("s1")], []);

        // 1.2.1 (Audio-only/Video-only) is Manual with no rule -> NotTested, never PartlyCheckedAutomatically.
        Assert.Equal(ScreenCriterionStatus.NotTested, Row(rows, "1.2.1").Status);
    }

    [Fact]
    public void RuleRanOnThisScreen_AndNoAnswer_IsPartlyCheckedAutomatically_AndStaysActionable()
    {
        var finding = MissingNameFinding();
        var rows = ScreenCoverageBuilder.Build([Screen("s1", [finding])], []);

        var row = Row(rows, "1.1.1"); // missing-name is in ScreenActivityBuilder's AlwaysRun list
        Assert.Equal(ScreenCriterionStatus.PartlyCheckedAutomatically, row.Status);
        Assert.NotNull(row.AutomatedSummary);
        Assert.Contains("Partly checked automatically: 1 issue, 0 to review; manual check still needed.", row.AutomatedSummary);
    }

    [Fact]
    public void GuidedAnswer_TakesPriorityOverPartlyCheckedAutomatically_ButKeepsTheAutomatedSummary()
    {
        var finding = MissingNameFinding();
        var answer = Answer("s1", "1.1.1", GuidedAnswerResult.Pass);
        var rows = ScreenCoverageBuilder.Build([Screen("s1", [finding])], [answer]);

        var row = Row(rows, "1.1.1");
        Assert.Equal(ScreenCriterionStatus.GuidedChecked, row.Status);
        Assert.NotNull(row.AutomatedSummary); // shown alongside, never replaced
        Assert.Equal(answer, row.Answer);
    }

    [Fact]
    public void ConfirmedNotApplicable_ProducesNotApplicableHereWithReason()
    {
        var answer = Answer("s1", "1.3.1", GuidedAnswerResult.ConfirmedNotApplicable, notApplicableReason: "no structure on this screen");
        var rows = ScreenCoverageBuilder.Build([Screen("s1")], [answer]);

        var row = Row(rows, "1.3.1");
        Assert.Equal(ScreenCriterionStatus.NotApplicableHere, row.Status);
        Assert.Equal("no structure on this screen", row.NotApplicableReason);
    }

    [Fact]
    public void ProposedButNotConfirmed_IsCarriedEvenWhenStatusIsNotTested()
    {
        var screen = Screen("s1") with { ProposedNotApplicable = [new ProposedNotApplicable("1.3.1", "no headings found")] };
        var rows = ScreenCoverageBuilder.Build([screen], []);

        var row = Row(rows, "1.3.1");
        Assert.Equal(ScreenCriterionStatus.NotTested, row.Status);
        Assert.Single(row.ProposedButNotConfirmed);
    }

    [Fact]
    public void ProposedButNotConfirmed_IsClearedOnceConfirmed()
    {
        var screen = Screen("s1") with { ProposedNotApplicable = [new ProposedNotApplicable("1.3.1", "no headings found")] };
        var answer = Answer("s1", "1.3.1", GuidedAnswerResult.ConfirmedNotApplicable, notApplicableReason: "no headings found");
        var rows = ScreenCoverageBuilder.Build([screen], [answer]);

        Assert.Empty(Row(rows, "1.3.1").ProposedButNotConfirmed);
    }

    [Fact]
    public void OlderAnswers_AreKept_OldestFirst()
    {
        var older = Answer("s1", "1.3.1", GuidedAnswerResult.Fail, answeredAt: DateTimeOffset.Now.AddDays(-1));
        var newer = Answer("s1", "1.3.1", GuidedAnswerResult.Pass, answeredAt: DateTimeOffset.Now);
        var rows = ScreenCoverageBuilder.Build([Screen("s1")], [older, newer]);

        var row = Row(rows, "1.3.1");
        Assert.Equal(newer, row.Answer);
        Assert.Equal([older], row.OlderAnswers);
    }
}

public class ContradictionCheckerTests
{
    private static ScreenResult Screen(string screenId, IReadOnlyList<Finding>? findings = null) => new()
    {
        ScreenId = screenId, Platform = Platform.Android, ScreenName = "Home", Findings = findings ?? [],
    };

    private static Finding Finding(FindingKind kind, WcagCriterion criterion) => new()
    {
        RuleId = "missing-name", Kind = kind, Message = "test", NodePath = "0", Role = "image", Criteria = [criterion],
    };

    private static GuidedAnswer Answer(string screenId, string criterion, GuidedAnswerResult result,
        DateTimeOffset? answeredAt = null, string? evidence = "all images have good labels") => new(
        ScreenId: screenId, CriterionNumber: criterion, Result: result, Note: null,
        NotApplicableReason: result == GuidedAnswerResult.ConfirmedNotApplicable ? "reason" : null,
        FastPassConfirmed: false, Tester: "me", AnsweredAt: answeredAt ?? DateTimeOffset.Now, Device: null,
        AssistiveTechnology: "None", AssistiveTechnologyVersion: null,
        Evidence: result == GuidedAnswerResult.Pass ? [new GuidedEvidence(evidence!)] : []);

    [Fact]
    public void PassNextToAWcagIssueAndNeedsReview_IsFlagged_WithExactWording()
    {
        var screen = Screen("s1", [Finding(FindingKind.WcagIssue, WcagCriteria.NonTextContent), Finding(FindingKind.WcagIssue, WcagCriteria.NonTextContent), Finding(FindingKind.NeedsReview, WcagCriteria.NonTextContent)]);
        var answer = Answer("s1", "1.1.1", GuidedAnswerResult.Pass);

        var contradictions = ContradictionChecker.Find([screen], [answer]);

        var contradiction = Assert.Single(contradictions);
        Assert.Equal(2, contradiction.ConflictingWcagIssueCount);
        Assert.Equal(1, contradiction.ConflictingNeedsReviewCount);
        Assert.Equal(
            "The tester recorded a pass for 1.1.1 on 'Home', with this evidence: 'all images have good labels'. " +
            "Automated checks on this screen found 2 issues and 1 item to review citing 1.1.1. Check both.",
            contradiction.Description);
    }

    [Fact]
    public void FailOrInconclusive_NeverContradictsAFinding()
    {
        var screen = Screen("s1", [Finding(FindingKind.WcagIssue, WcagCriteria.NonTextContent)]);
        var fail = Answer("s1", "1.1.1", GuidedAnswerResult.Fail);
        var inconclusive = Answer("s1", "1.1.1", GuidedAnswerResult.Inconclusive);

        Assert.Empty(ContradictionChecker.Find([screen], [fail]));
        Assert.Empty(ContradictionChecker.Find([screen], [inconclusive]));
    }

    [Fact]
    public void PassWithNoConflictingFinding_IsNotFlagged()
    {
        var screen = Screen("s1");
        var answer = Answer("s1", "1.1.1", GuidedAnswerResult.Pass);

        Assert.Empty(ContradictionChecker.Find([screen], [answer]));
    }

    [Fact]
    public void ConfirmedNotApplicable_DisagreeingWithAnOlderPass_IsFlagged_AndBothAnswersRetrievable()
    {
        var screen = Screen("s1");
        var olderPass = Answer("s1", "1.3.1", GuidedAnswerResult.Pass, answeredAt: DateTimeOffset.Now.AddDays(-1));
        var newerNotApplicable = Answer("s1", "1.3.1", GuidedAnswerResult.ConfirmedNotApplicable, answeredAt: DateTimeOffset.Now);

        var contradictions = ContradictionChecker.Find([screen], [olderPass, newerNotApplicable]);

        Assert.Single(contradictions);
        var rows = ScreenCoverageBuilder.Build([screen], [olderPass, newerNotApplicable]);
        var row = Assert.Single(rows, r => r.Number == "1.3.1");
        Assert.True(row.Contradicted);
        Assert.Equal(newerNotApplicable, row.Answer);
        Assert.Equal([olderPass], row.OlderAnswers);
    }

    [Fact]
    public void TwoConsistentAnswersInARow_AreNotFlagged()
    {
        var screen = Screen("s1");
        var older = Answer("s1", "1.3.1", GuidedAnswerResult.Fail, answeredAt: DateTimeOffset.Now.AddDays(-1));
        var newer = Answer("s1", "1.3.1", GuidedAnswerResult.Pass, answeredAt: DateTimeOffset.Now);

        Assert.Empty(ContradictionChecker.Find([screen], [older, newer]));
    }

    [Fact]
    public void ASupersededOlderPass_IsNeverFlagged_OnlyTheCurrentAnswerIs()
    {
        // A Pass later replaced by a Fail must not keep showing up as a contradiction: only the CURRENT
        // (most recent) answer for a (screen, criterion) speaks for it.
        var screen = Screen("s1", [Finding(FindingKind.WcagIssue, WcagCriteria.NonTextContent)]);
        var olderPass = Answer("s1", "1.1.1", GuidedAnswerResult.Pass, answeredAt: DateTimeOffset.Now.AddDays(-1));
        var newerFail = new GuidedAnswer(
            ScreenId: "s1", CriterionNumber: "1.1.1", Result: GuidedAnswerResult.Fail, Note: null,
            NotApplicableReason: null, FastPassConfirmed: false, Tester: "me", AnsweredAt: DateTimeOffset.Now,
            Device: null, AssistiveTechnology: "None", AssistiveTechnologyVersion: null, Evidence: []);

        var contradictions = ContradictionChecker.Find([screen], [olderPass, newerFail]);

        Assert.Empty(contradictions);
    }

    [Fact]
    public void ConfirmedNotApplicable_NextToARealFinding_IsFlagged()
    {
        // Required: a manual answer that contradicts automated data is flagged, never silently preferred --
        // this applies to a confirmed not-applicable next to a real finding just as much as a Pass does.
        var screen = Screen("s1", [Finding(FindingKind.WcagIssue, WcagCriteria.NonTextContent)]);
        var answer = new GuidedAnswer(
            ScreenId: "s1", CriterionNumber: "1.1.1", Result: GuidedAnswerResult.ConfirmedNotApplicable, Note: null,
            NotApplicableReason: "no images on this screen", FastPassConfirmed: false, Tester: "me",
            AnsweredAt: DateTimeOffset.Now, Device: null, AssistiveTechnology: "None", AssistiveTechnologyVersion: null,
            Evidence: []);

        var contradiction = Assert.Single(ContradictionChecker.Find([screen], [answer]));

        Assert.Equal(1, contradiction.ConflictingWcagIssueCount);
        Assert.Contains("confirmed 1.1.1 does not apply", contradiction.Description);
        Assert.Contains("no images on this screen", contradiction.Description);
    }
}

public class GuidedCoverageGapChecksTests
{
    [Fact]
    public void ScreensWithNoGuidedAnswers_ListsOnlyScreensWithoutAnAnswer()
    {
        var report = new ScanReport
        {
            ToolVersion = "test",
            Screens =
            [
                new ScreenResult { ScreenId = "s1", Platform = Platform.Android, ScreenName = "Home", Findings = [] },
                new ScreenResult { ScreenId = "s2", Platform = Platform.Android, ScreenName = "Settings", Findings = [] },
            ],
            GuidedAnswers =
            [
                new GuidedAnswer("s1", "1.3.1", GuidedAnswerResult.Pass, null, null, false, "me", DateTimeOffset.Now, null, "None", null, [new GuidedEvidence("ok")]),
            ],
        };

        var missing = report.ScreensWithNoGuidedAnswers;

        var screen = Assert.Single(missing);
        Assert.Equal("s2", screen.ScreenId);
    }

    [Fact]
    public void AnswerBeforeARescan_ShowsTheRescanCaveat()
    {
        var rescannedAt = DateTimeOffset.Now;
        var answerBefore = new GuidedAnswer("s1", "1.3.1", GuidedAnswerResult.Pass, null, null, false, "me",
            rescannedAt.AddMinutes(-5), null, "None", null, [new GuidedEvidence("ok")]);
        var screen = new ScreenResult { ScreenId = "s1", Platform = Platform.Android, ScreenName = "Home", Findings = [], RescannedAt = rescannedAt };

        var caveat = screen.RescannedAt is { } r && answerBefore.AnsweredAt < r
            ? GuidedChecksDisplay.AnsweredBeforeRescan(r)
            : null;

        Assert.NotNull(caveat);
        Assert.Contains("recorded before this screen was rescanned on", caveat);
    }

    [Fact]
    public void ScanReport_GuidedAnswers_DefaultsToEmpty_SoAnOlderReportStillLoads()
    {
        var report = new ScanReport { ToolVersion = "test", Screens = [] };

        Assert.Empty(report.GuidedAnswers);
        Assert.Empty(report.ScreenCoverage);
        Assert.Empty(report.Contradictions);
    }
}
