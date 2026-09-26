namespace Swipewalk.Core.Coverage;

/// <summary>
/// Fixed report/UI wording for guided checks. Never "Passed" on its own, never "compliant" or "accessible":
/// see <see cref="TesterFacingPass"/>/<see cref="ReportFacingPass"/>, which always spell out the evidence a
/// tester recorded rather than just naming the result.
/// </summary>
public static class GuidedChecksDisplay
{
    /// <summary>Exact wording, required by review: a per-screen automated summary, distinct from the run-level
    /// <see cref="CoverageDisplay.PartlyAutomated(int,int,System.Collections.Generic.IReadOnlyList{RuleRunSummary}?)"/>
    /// wording -- this one is scoped to a single screen's findings for one criterion.</summary>
    public static string PartlyCheckedAutomatically(int wcagIssueCount, int needsReviewCount) =>
        $"Partly checked automatically: {wcagIssueCount} issue{(wcagIssueCount == 1 ? "" : "s")}, " +
        $"{needsReviewCount} to review; manual check still needed.";

    /// <summary>Tester-facing wording shown immediately after saving a Pass (CLI/desktop): the evidence text
    /// is always quoted, never just the word "Pass".</summary>
    public static string TesterFacingPass(string evidenceDescription) =>
        $"You recorded a pass, with this evidence: {evidenceDescription}";

    /// <summary>Report-facing wording for a recorded Pass, shown in the per-screen detail view.</summary>
    public static string ReportFacingPass(string criterionNumber, string screenName, string evidenceDescription) =>
        $"The tester recorded a pass for {criterionNumber} on '{screenName}', with this evidence: '{evidenceDescription}'.";

    /// <summary>One count phrased as "N issue(s)" / "N item(s) to review", for the contradiction sentence.</summary>
    private static string Count(int n, string singular, string plural) => $"{n} {(n == 1 ? singular : plural)}";

    /// <summary>Plain-language word for a recorded result -- never the raw enum name (e.g. never
    /// "ConfirmedNotApplicable" in a sentence a tester reads).</summary>
    public static string ResultWord(GuidedAnswerResult result) => result switch
    {
        GuidedAnswerResult.Pass => "a pass",
        GuidedAnswerResult.Fail => "a fail",
        GuidedAnswerResult.Inconclusive => "an inconclusive result",
        GuidedAnswerResult.ConfirmedNotApplicable => "confirmed not applicable",
        _ => result.ToString(),
    };

    /// <summary>One compact line for an older/superseded answer (report's "Earlier answers" detail): result
    /// word, date, tester, and the evidence or reason if the answer has one -- never silently dropped, since
    /// an earlier answer is kept precisely so a later reader can see what changed and why.</summary>
    public static string AnswerOneLine(GuidedAnswer answer)
    {
        var detail = answer.Result switch
        {
            GuidedAnswerResult.Pass => string.Join("; ", answer.Evidence.Select(e => e.Description)),
            GuidedAnswerResult.ConfirmedNotApplicable => answer.NotApplicableReason,
            _ => answer.Note,
        };
        var prefix = $"{ResultWord(answer.Result)}, {answer.AnsweredAt:yyyy-MM-dd}, {answer.Tester}";
        return string.IsNullOrEmpty(detail) ? prefix : $"{prefix}: {detail}";
    }

    /// <summary>
    /// Exact contradiction sentence (required by review): quotes the tester's evidence verbatim, states both
    /// counts (WcagIssue vs NeedsReview) separately -- never collapsed into one number -- and ends with an
    /// explicit call to action.
    /// </summary>
    public static string Contradiction(
        string criterionNumber, string screenName, string evidenceDescription, int wcagIssueCount, int needsReviewCount) =>
        $"{ReportFacingPass(criterionNumber, screenName, evidenceDescription)} Automated checks on this screen found " +
        $"{Count(wcagIssueCount, "issue", "issues")} and {Count(needsReviewCount, "item", "items")} to review citing " +
        $"{criterionNumber}. Check both.";

    /// <summary>
    /// Contradiction sentence for an older answer disagreeing with the current one on the same (screen,
    /// criterion) -- required fix 4's "a tester answer on an N/A-here criterion is shown and flagged, never
    /// hidden", generalized to any disagreement between the current and an older recorded result.
    /// </summary>
    public static string AnswerDisagreement(string criterionNumber, string screenName, GuidedAnswer older, GuidedAnswer current) =>
        $"On '{screenName}', {criterionNumber} was recorded as {ResultWord(older.Result)} on {older.AnsweredAt:yyyy-MM-dd}, " +
        $"then recorded as {ResultWord(current.Result)} on {current.AnsweredAt:yyyy-MM-dd}. Check both.";

    /// <summary>A confirmed-not-applicable answer sitting next to a real automated finding for the same
    /// criterion -- as much a contradiction as a Pass next to a finding (required: a manual answer that
    /// contradicts automated data is flagged, never silently preferred).</summary>
    public static string NotApplicableContradiction(
        string criterionNumber, string screenName, string reason, int wcagIssueCount, int needsReviewCount) =>
        $"The tester confirmed {criterionNumber} does not apply on '{screenName}' ('{reason}'), but automated checks on " +
        $"this screen found {Count(wcagIssueCount, "issue", "issues")} and {Count(needsReviewCount, "item", "items")} " +
        $"to review citing {criterionNumber}. Check both.";

    /// <summary>Caveat shown when an existing answer predates a rescan of the same screen (gap check: "answers
    /// surviving a rescan") -- reuses <c>ScreenResult.RescannedAt</c>, never presented as if the answer still
    /// describes the current capture. A full sentence, not a fragment, so it reads the same whether it's
    /// appended after other text or shown on its own.</summary>
    public static string AnsweredBeforeRescan(DateTimeOffset rescannedAt) =>
        $"This answer was recorded before this screen was rescanned on {rescannedAt:yyyy-MM-dd}; check it still holds.";

    /// <summary>Gap check: screens recorded but with no guided checks started yet.</summary>
    public static string ScreensWithNoGuidedAnswers(int count, int total) =>
        $"{count} of {total} screens in this run have no guided checks started yet.";

    /// <summary>Fast-pass confirmation prompt (CLI and desktop share the exact wording).</summary>
    public const string FastPassPrompt = "That was fast -- are you sure you completed the steps?";

    /// <summary>Evidence prompt shown when the tester picks Pass (CLI's exact prompt text; the desktop app's
    /// required-evidence field uses the same phrase as a hint/placeholder).</summary>
    public const string EvidencePrompt = "Evidence (required for pass: what you saw or heard) > ";

    /// <summary>Reason prompt shown when the tester picks "confirm not applicable".</summary>
    public const string NotApplicableReasonPrompt = "Reason (required) > ";

    /// <summary>Short, reader-facing label for a screen's per-criterion status -- never "Automated" alone
    /// (see <see cref="ScreenCriterionStatus.PartlyCheckedAutomatically"/>'s own remarks: no criterion in this
    /// codebase is ever fully automated).</summary>
    public static string StatusLabel(ScreenCriterionStatus status) => status switch
    {
        ScreenCriterionStatus.PartlyCheckedAutomatically => "Partly checked automatically",
        ScreenCriterionStatus.GuidedChecked => "Guided check recorded",
        ScreenCriterionStatus.NotApplicableHere => "Confirmed not applicable here",
        ScreenCriterionStatus.NotTested => "Not tested",
        _ => status.ToString(),
    };

    /// <summary>Short, reader-facing label for a run-level roll-up status -- never a bare "Pass"/"Fail" word,
    /// per <see cref="RunCriterionRollupStatus"/>'s own remarks on avoiding a compliance-verdict reading.</summary>
    public static string RollupLabel(RunCriterionRollupStatus status) => status switch
    {
        RunCriterionRollupStatus.Fail => "Tester recorded a fail on at least one screen",
        RunCriterionRollupStatus.Inconclusive => "Tester recorded an inconclusive result on at least one screen",
        RunCriterionRollupStatus.Flagged => "Tester answers flagged against automated findings; check the Flagged list",
        RunCriterionRollupStatus.TesterRecordedPassOnEveryScreen => "Tester recorded a pass on every applicable screen",
        RunCriterionRollupStatus.ConfirmedNotApplicableOnEveryScreen => "Confirmed not applicable on every screen",
        RunCriterionRollupStatus.Incomplete => "Not fully checked yet",
        _ => status.ToString(),
    };
}
