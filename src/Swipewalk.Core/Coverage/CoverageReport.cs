using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Coverage;

/// <summary>
/// One row of the run-level WCAG 2.2 A/AA coverage list included in every report (results.json "coverage",
/// and the HTML report's "WCAG 2.2 A/AA coverage" section): a criterion's static facts (see
/// <see cref="CoverageCatalog"/>) combined with what this run's automated checks actually did (see
/// <see cref="CriterionCoverageExtensions.ComputeRunStatus"/>). Never a verdict: a <see cref="RunStatus"/>
/// of <see cref="CoverageRunStatus.PartlyAutomated"/> with zero counts means checks ran and found nothing,
/// not that the criterion is met -- see <see cref="Label"/>.
/// </summary>
/// <param name="Number">The criterion number, e.g. "1.4.3".</param>
/// <param name="Name">The criterion name, e.g. "Contrast (Minimum)".</param>
/// <param name="Level">A or AA.</param>
/// <param name="RunStatus">What this run found for this criterion.</param>
/// <param name="WcagIssueCount">Findings on scanned screens citing this criterion with <see cref="FindingKind.WcagIssue"/>.</param>
/// <param name="NeedsReviewCount">Findings on scanned screens citing this criterion with <see cref="FindingKind.NeedsReview"/>.
/// <see cref="FindingKind.PlatformAdvisory"/> findings are never counted.</param>
/// <param name="ScreensRan">Number of scanned screens on which at least one rule mapped to this criterion ran.</param>
/// <param name="ScreensTotal">Total scanned screens this run.</param>
/// <param name="RulesThatDidNotRun">Rule ids mapped to this criterion that never ran on any screen this run.</param>
/// <param name="SkipReasons">Reasons collected for the rules in <paramref name="RulesThatDidNotRun"/>, when recorded.</param>
/// <param name="HowToCheck">How to check this criterion by hand (for <see cref="CoverageBaseStatus.Manual"/> or
/// <see cref="CoverageBaseStatus.PartlyAutomated"/>), or the WCAG2ICT "set of software programs" reasoning
/// (for <see cref="CoverageBaseStatus.UsuallyNotApplicable"/>). Never a compliance verdict.</param>
/// <param name="SourceUrls">Where the mapping comes from: the W3C "Understanding" page and/or a WCAG2ICT
/// "Applying SC ... to non-web software" page.</param>
/// <param name="Label">The fixed report wording for <see cref="RunStatus"/> (see <see cref="CoverageDisplay"/>),
/// combined with the counts above where relevant.</param>
public sealed record CoverageReportEntry(
    string Number,
    string Name,
    WcagLevel Level,
    CoverageRunStatus RunStatus,
    int WcagIssueCount,
    int NeedsReviewCount,
    int ScreensRan,
    int ScreensTotal,
    IReadOnlyList<string> RulesThatDidNotRun,
    IReadOnlyList<string> SkipReasons,
    string? HowToCheck,
    IReadOnlyList<string> SourceUrls,
    string Label);

/// <summary>Builds the run-level WCAG 2.2 A/AA coverage list and its one-line summary from a report's
/// scanned screens. Pure functions: no side effects, no I/O.</summary>
public static class CoverageReport
{
    /// <summary>The full 55-entry coverage list, in <see cref="CoverageCatalog"/> order, for the given
    /// scanned screens (record mode and multi-screen runs included: <see cref="ScreenActivityBuilder"/>
    /// builds one <see cref="ScreenActivity"/> per screen).</summary>
    public static IReadOnlyList<CoverageReportEntry> Build(IReadOnlyList<ScreenResult> screens)
    {
        var activity = ScreenActivityBuilder.For(screens);
        return [.. CoverageCatalog.All.Select(c =>
        {
            var result = c.ComputeRunStatus(activity);
            return new CoverageReportEntry(
                c.Criterion.Number, c.Criterion.Name, c.Criterion.Level, result.Status,
                result.WcagIssueCount, result.NeedsReviewCount, result.ScreensRan, result.ScreensTotal,
                result.RuleIdsThatDidNotRun, result.SkipReasons, c.Note, c.SourceUrls,
                CoverageDisplay.For(result));
        })];
    }

    /// <summary>
    /// One-line summary of <paramref name="entries"/>, shown near the top of the HTML report, e.g. "WCAG
    /// 2.2 A/AA: 13 criteria partly checked by automation, 38 need a manual check, 4 usually out of scope, 0
    /// not tested in this run." The four counts always add up to <paramref name="entries"/>.Count (55 for
    /// the full catalog): every criterion is in exactly one status.
    /// </summary>
    public static string Summary(IReadOnlyList<CoverageReportEntry> entries)
    {
        int Count(CoverageRunStatus status) => entries.Count(e => e.RunStatus == status);
        return $"WCAG 2.2 A/AA: {Count(CoverageRunStatus.PartlyAutomated)} criteria partly checked by automation, " +
               $"{Count(CoverageRunStatus.ManualCheckNeeded)} need a manual check, " +
               $"{Count(CoverageRunStatus.UsuallyNotApplicable)} usually out of scope, " +
               $"{Count(CoverageRunStatus.NotTestedInThisRun)} not tested in this run.";
    }
}
