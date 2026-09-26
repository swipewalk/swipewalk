using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Coverage;

/// <summary>
/// The status Swipewalk shows for one WCAG criterion on one screen, combining what automation did with any
/// guided answer a tester recorded. Never "Automated" on its own: no criterion in this codebase has a fully
/// automated base status (see <see cref="CoverageBaseStatus.PartlyAutomated"/>'s own remarks), so
/// <see cref="CheckedAutomatically"/> is reserved for a status that doesn't exist yet.
/// </summary>
public enum ScreenCriterionStatus
{
    /// <summary>Reserved: no <see cref="CoverageBaseStatus"/> value for "fully automated" exists today. Never
    /// produced by <see cref="ScreenCoverageBuilder"/>.</summary>
    CheckedAutomatically,

    /// <summary>A mapped rule ran on this screen; the catalog's base status is <see cref="CoverageBaseStatus.PartlyAutomated"/>;
    /// a manual check is still needed. Stays on the actionable list -- automation running is not the same as a
    /// criterion being done.</summary>
    PartlyCheckedAutomatically,

    /// <summary>A tester recorded Pass, Fail or Inconclusive for this (screen, criterion).</summary>
    GuidedChecked,

    /// <summary>A tester confirmed a <see cref="ProposedNotApplicable"/> suggestion (or their own reason) for
    /// this (screen, criterion). Never automatic -- see <see cref="ApplicabilityRules"/>.</summary>
    NotApplicableHere,

    /// <summary>Nothing ran here and no tester answer exists yet. Shown prominently: never listed quietly
    /// alongside statuses that mean something was actually done.</summary>
    NotTested,
}

/// <summary>
/// One screen's status for one WCAG criterion, combining <see cref="ScreenActivityBuilder"/> (what ran),
/// <see cref="ApplicabilityRules"/> (what might not apply here) and any <see cref="GuidedAnswer"/>s. Built for
/// all ~55 criteria per screen -- nothing is silently dropped -- but the report/UI group these, not list them
/// flat: see the "short, relevant list" grouping in the report/CLI/desktop layers.
/// </summary>
/// <param name="ProposedButNotConfirmed">Shown even when <see cref="Status"/> isn't <see cref="ScreenCriterionStatus.NotApplicableHere"/>
/// yet -- a pending suggestion the tester hasn't acted on.</param>
/// <param name="NotApplicableReason">Set only for <see cref="ScreenCriterionStatus.NotApplicableHere"/>, from
/// the confirming answer.</param>
/// <param name="AutomatedSummary">Exact wording from <see cref="GuidedChecksDisplay.PartlyCheckedAutomatically"/>,
/// scoped to this screen's findings citing this criterion. Set whenever a mapped rule ran here, even when
/// <see cref="Status"/> is <see cref="ScreenCriterionStatus.GuidedChecked"/> -- the automated summary is shown
/// alongside a guided answer, never replaced by it, since a mismatch between the two is a contradiction, not
/// something to silently prefer one side of.</param>
/// <param name="Answer">The most recent answer for (ScreenId, CriterionNumber), when any exist.</param>
/// <param name="OlderAnswers">Every earlier answer for the same pair, oldest first -- kept, always rendered in
/// the report's detail view (collapsed by default, never dropped), even when it disagrees with the current one.</param>
/// <param name="Contradicted">True iff this row is also in <see cref="ContradictionChecker.Find"/>'s result.</param>
/// <param name="CapturedEvidenceSummary">A sentence naming real, real-device screen-reader evidence for THIS
/// criterion on THIS screen -- from <see cref="ScreenReader.ScreenReaderCoverageEvidence.Summarize"/>. Null
/// when this screen has no capture, or this criterion isn't one of the ones real capture evidence can speak
/// to (see that type's remarks for exactly which and why). Shown alongside <see cref="AutomatedSummary"/>,
/// never replacing it: <see cref="AutomatedSummary"/> already counts every rule's findings for this
/// criterion; this is specifically what a real device showed, naming the tool. Never implies a status
/// change on its own -- for 1.3.1 in particular this can be set while <see cref="Status"/> stays
/// <see cref="ScreenCriterionStatus.NotTested"/>, since that criterion's evidence is informational only (see
/// <see cref="ScreenReader.ScreenReaderCoverageEvidence"/>'s remarks).</param>
/// <param name="CapturedEvidenceSource">Which tool <see cref="CapturedEvidenceSummary"/> came from, for a UI
/// badge that distinguishes TalkBack's real speech from the Accessibility Inspector's walk (which is not
/// VoiceOver speech -- VoiceOver never runs for that route). Null exactly when
/// <see cref="CapturedEvidenceSummary"/> is null.</param>
public sealed record ScreenCriterionReport(
    string ScreenId,
    string ScreenName,
    string Number,
    string Name,
    WcagLevel Level,
    ScreenCriterionStatus Status,
    IReadOnlyList<ProposedNotApplicable> ProposedButNotConfirmed,
    string? NotApplicableReason,
    string? AutomatedSummary,
    GuidedAnswer? Answer,
    IReadOnlyList<GuidedAnswer> OlderAnswers,
    bool Contradicted,
    IReadOnlyList<string> SourceUrls,
    string? CapturedEvidenceSummary = null,
    ScreenReaderSource? CapturedEvidenceSource = null);

/// <summary>
/// Builds the per-screen WCAG criterion status list from a run's scanned screens and recorded guided answers.
/// Pure function, like <see cref="CoverageReport.Build"/>: no side effects, no I/O, and needs no persistence
/// of its own -- everything it can't recompute (the answers) is loaded separately from
/// <c>Engine.GuidedAnswerStore</c> and passed in.
/// </summary>
public static class ScreenCoverageBuilder
{
    /// <summary>True when <paramref name="coverageRuleId"/> (e.g. "engine") equals, or is the colon-prefix of,
    /// <paramref name="actualId"/> (e.g. "engine:hitRegion") -- same matching <see cref="CriterionCoverageExtensions"/>
    /// uses at run level, reused here scoped to one screen's own <see cref="ScreenActivity"/>.</summary>
    private static bool RuleRanHere(ScreenActivity activity, string coverageRuleId) =>
        activity.RanRuleIds.Any(id => id == coverageRuleId || id.StartsWith(coverageRuleId + ":", StringComparison.Ordinal));

    public static IReadOnlyList<ScreenCriterionReport> Build(IReadOnlyList<ScreenResult> screens, IReadOnlyList<GuidedAnswer> answers)
    {
        var contradictedKeys = ContradictionChecker.Find(screens, answers)
            .Select(c => (c.ScreenId, c.CriterionNumber))
            .ToHashSet();

        var reports = new List<ScreenCriterionReport>();
        foreach (var screen in screens)
        {
            var activity = ScreenActivityBuilder.For(screen);
            foreach (var coverage in CoverageCatalog.All)
            {
                reports.Add(BuildRow(screen, activity, coverage, answers, contradictedKeys));
            }
        }
        return reports;
    }

    private static ScreenCriterionReport BuildRow(
        ScreenResult screen, ScreenActivity activity, CriterionCoverage coverage,
        IReadOnlyList<GuidedAnswer> allAnswers, HashSet<(string, string)> contradictedKeys)
    {
        var number = coverage.Criterion.Number;
        var answersHere = allAnswers
            .Where(a => a.ScreenId == screen.ScreenId && a.CriterionNumber == number)
            .OrderByDescending(a => a.AnsweredAt)
            .ToList();
        var mostRecent = answersHere.FirstOrDefault();
        var olderAnswers = answersHere.Skip(1).OrderBy(a => a.AnsweredAt).ToList();

        var ranHere = coverage.RuleIds.Count > 0 && coverage.RuleIds.Any(id => RuleRanHere(activity, id));
        string? automatedSummary = null;
        if (coverage.BaseStatus == CoverageBaseStatus.PartlyAutomated && ranHere)
        {
            var findingsHere = screen.Findings.Where(f => f.Criteria.Contains(coverage.Criterion)).ToList();
            var wcagIssues = findingsHere.Count(f => f.Kind == FindingKind.WcagIssue);
            var needsReview = findingsHere.Count(f => f.Kind == FindingKind.NeedsReview);
            automatedSummary = GuidedChecksDisplay.PartlyCheckedAutomatically(wcagIssues, needsReview);
        }

        ScreenCriterionStatus status;
        string? notApplicableReason = null;
        if (mostRecent?.Result == GuidedAnswerResult.ConfirmedNotApplicable)
        {
            status = ScreenCriterionStatus.NotApplicableHere;
            notApplicableReason = mostRecent.NotApplicableReason;
        }
        else if (mostRecent is not null)
        {
            status = ScreenCriterionStatus.GuidedChecked;
        }
        else if (ranHere)
        {
            status = ScreenCriterionStatus.PartlyCheckedAutomatically;
        }
        else
        {
            status = ScreenCriterionStatus.NotTested;
        }

        var proposedButNotConfirmed = mostRecent?.Result == GuidedAnswerResult.ConfirmedNotApplicable
            ? []
            : screen.ProposedNotApplicable.Where(p => p.CriterionNumber == number).ToList();

        var (capturedEvidenceSummary, capturedEvidenceSource) =
            ScreenReader.ScreenReaderCoverageEvidence.Summarize(screen, coverage.Criterion);

        return new ScreenCriterionReport(
            screen.ScreenId, screen.ScreenName, number, coverage.Criterion.Name, coverage.Criterion.Level,
            status, proposedButNotConfirmed, notApplicableReason, automatedSummary,
            mostRecent, olderAnswers, contradictedKeys.Contains((screen.ScreenId, number)), coverage.SourceUrls,
            capturedEvidenceSummary, capturedEvidenceSource);
    }
}
