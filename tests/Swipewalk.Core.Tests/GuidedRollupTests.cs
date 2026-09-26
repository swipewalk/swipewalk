using Swipewalk.Core.Coverage;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class GuidedRollupTests
{
    private static GuidedAnswer Answer(GuidedAnswerResult result) => new(
        ScreenId: "ignored", CriterionNumber: "1.3.1", Result: result, Note: null,
        NotApplicableReason: result == GuidedAnswerResult.ConfirmedNotApplicable ? "reason" : null,
        FastPassConfirmed: false, Tester: "me", AnsweredAt: DateTimeOffset.Now, Device: null,
        AssistiveTechnology: "None", AssistiveTechnologyVersion: null,
        Evidence: result == GuidedAnswerResult.Pass ? [new GuidedEvidence("ok")] : []);

    private static ScreenCriterionReport Row(ScreenCriterionStatus status, GuidedAnswer? answer, bool contradicted = false) => new(
        ScreenId: Guid.NewGuid().ToString(), ScreenName: "Screen", Number: "1.3.1", Name: "Info and Relationships",
        Level: WcagLevel.A, Status: status, ProposedButNotConfirmed: [], NotApplicableReason: null,
        AutomatedSummary: null, Answer: answer, OlderAnswers: [], Contradicted: contradicted, SourceUrls: []);

    [Fact]
    public void AContradictedRow_NeverRollsUpAsAPass_EvenIfEveryScreenPassed()
    {
        var rows = new[] { Row(ScreenCriterionStatus.GuidedChecked, Answer(GuidedAnswerResult.Pass), contradicted: true) };

        // Not Incomplete either: a contradiction is a distinct, more specific status than "nobody's checked
        // this yet" -- see RunCriterionRollupStatus.Flagged.
        Assert.Equal(RunCriterionRollupStatus.Flagged, Assert.Single(GuidedRollupBuilder.Build(rows)).Status);
    }

    [Fact]
    public void OneFail_MakesTheWholeRunFail_EvenIfOtherScreensPassed()
    {
        var rows = new[]
        {
            Row(ScreenCriterionStatus.GuidedChecked, Answer(GuidedAnswerResult.Pass)),
            Row(ScreenCriterionStatus.GuidedChecked, Answer(GuidedAnswerResult.Fail)),
            Row(ScreenCriterionStatus.GuidedChecked, Answer(GuidedAnswerResult.Pass)),
        };

        var rollup = Assert.Single(GuidedRollupBuilder.Build(rows));

        Assert.Equal(RunCriterionRollupStatus.Fail, rollup.Status);
    }

    [Fact]
    public void NotApplicable_OnlyWhenEveryScreenConfirmedIt()
    {
        var allConfirmed = new[]
        {
            Row(ScreenCriterionStatus.NotApplicableHere, null),
            Row(ScreenCriterionStatus.NotApplicableHere, null),
        };
        var oneStillUnchecked = new[]
        {
            Row(ScreenCriterionStatus.NotApplicableHere, null),
            Row(ScreenCriterionStatus.NotTested, null),
        };

        Assert.Equal(RunCriterionRollupStatus.ConfirmedNotApplicableOnEveryScreen, Assert.Single(GuidedRollupBuilder.Build(allConfirmed)).Status);
        // A screen simply not checked yet must never be hidden behind another screen's confirmed N/A.
        Assert.Equal(RunCriterionRollupStatus.Incomplete, Assert.Single(GuidedRollupBuilder.Build(oneStillUnchecked)).Status);
    }

    [Fact]
    public void AllPass_IsPass()
    {
        var rows = new[]
        {
            Row(ScreenCriterionStatus.GuidedChecked, Answer(GuidedAnswerResult.Pass)),
            Row(ScreenCriterionStatus.NotApplicableHere, null),
        };

        Assert.Equal(RunCriterionRollupStatus.TesterRecordedPassOnEveryScreen, Assert.Single(GuidedRollupBuilder.Build(rows)).Status);
    }

    [Fact]
    public void NothingCheckedYet_IsIncomplete()
    {
        var rows = new[] { Row(ScreenCriterionStatus.NotTested, null) };

        Assert.Equal(RunCriterionRollupStatus.Incomplete, Assert.Single(GuidedRollupBuilder.Build(rows)).Status);
    }
}
