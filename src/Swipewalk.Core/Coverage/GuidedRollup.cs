using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Coverage;

/// <summary>
/// One criterion's status across every screen in a run, computed as the worst case -- never averaged: a
/// single <see cref="GuidedAnswerResult.Fail"/> on any screen makes the whole run's roll-up
/// <see cref="Fail"/>, even if every other screen recorded a pass. A run-level
/// <see cref="ConfirmedNotApplicableOnEveryScreen"/> only shows once EVERY applicable screen confirmed
/// not-applicable; screens simply not checked yet keep the roll-up at <see cref="Incomplete"/> rather than
/// hiding the gap behind other screens' recorded passes. Named deliberately -- never plain "Pass"/"NotApplicable" --
/// so this can never be misread as an automated conformance verdict when it appears in results.json: it only
/// ever reflects what a tester recorded, per the hard "never claim compliance" rule.
/// </summary>
public enum RunCriterionRollupStatus
{
    Fail,
    Inconclusive,
    /// <summary>A recorded pass or confirmed-not-applicable answer disagrees with an automated finding, or
    /// with an earlier answer for the same (screen, criterion) -- see <see cref="ContradictionChecker"/>. This
    /// criterion is never rolled up as settled while that stands, even on screens where nothing else is wrong.</summary>
    Flagged,
    TesterRecordedPassOnEveryScreen,
    ConfirmedNotApplicableOnEveryScreen,
    Incomplete,
}

public sealed record GuidedCriterionRollup(string Number, string Name, WcagLevel Level, RunCriterionRollupStatus Status);

/// <summary>Builds the run-level "one status per criterion, worst case wins" roll-up shown on the
/// Results/Dashboard page. Pure function over <see cref="ScreenCoverageBuilder"/>'s own output.</summary>
public static class GuidedRollupBuilder
{
    public static IReadOnlyList<GuidedCriterionRollup> Build(IReadOnlyList<ScreenCriterionReport> screenCoverage)
    {
        var result = new List<GuidedCriterionRollup>();
        foreach (var group in screenCoverage.GroupBy(r => r.Number))
        {
            var rows = group.ToList();
            var first = rows[0];
            var status = Resolve(rows);
            result.Add(new GuidedCriterionRollup(first.Number, first.Name, first.Level, status));
        }
        return result;
    }

    private static RunCriterionRollupStatus Resolve(List<ScreenCriterionReport> rows)
    {
        if (rows.Any(r => r.Answer?.Result == GuidedAnswerResult.Fail))
            return RunCriterionRollupStatus.Fail;
        if (rows.Any(r => r.Answer?.Result == GuidedAnswerResult.Inconclusive))
            return RunCriterionRollupStatus.Inconclusive;
        // A live contradiction (a recorded pass/not-applicable disagreeing with an automated finding, or with
        // an earlier answer) gets its own status -- never rolled up as a clean pass while something about it
        // is still flagged, and never worded the same as "nobody's checked this yet" (Incomplete).
        if (rows.Any(r => r.Contradicted))
            return RunCriterionRollupStatus.Flagged;
        if (rows.All(r => r.Status == ScreenCriterionStatus.NotApplicableHere))
            return RunCriterionRollupStatus.ConfirmedNotApplicableOnEveryScreen;
        var everyRowPassedOrNotApplicable = rows.All(r =>
            r.Answer?.Result == GuidedAnswerResult.Pass || r.Status == ScreenCriterionStatus.NotApplicableHere);
        var atLeastOnePass = rows.Any(r => r.Answer?.Result == GuidedAnswerResult.Pass);
        return everyRowPassedOrNotApplicable && atLeastOnePass
            ? RunCriterionRollupStatus.TesterRecordedPassOnEveryScreen
            : RunCriterionRollupStatus.Incomplete;
    }
}
