using Swipewalk.Core.Model;
using Swipewalk.Core.Standards;

namespace Swipewalk.Core.Coverage;

/// <summary>
/// The status Swipewalk reports for one <see cref="BeyondWcagClause"/> on one run. Uses the same vocabulary as
/// <see cref="CoverageRunStatus"/>: never a compliance verdict, and never "automated" alone -- see
/// <see cref="BeyondWcagDisplay"/>.
/// </summary>
public enum BeyondWcagRunStatus
{
    /// <summary>An existing Swipewalk signal (see <see cref="BeyondWcagEvidenceSignal"/>) ran on at least one
    /// scanned screen this run. Carries no finding count of its own: these clauses are not first-class WCAG
    /// criteria, so nothing counts findings against them directly.</summary>
    PartlyAutomated,

    /// <summary>Needs a person to follow <see cref="BeyondWcagClause.GuidedSteps"/>.</summary>
    Guided,

    /// <summary>Out of scope for a running-app scanner: documentation, support services, or platform/OS behaviour.</summary>
    NotTestableBySwipewalk,

    /// <summary>The clause's applicability condition is confirmed absent from the captured screens (e.g. no
    /// video controls were found), so it does not apply to what was scanned. Never inferred without a
    /// specific per-clause signal -- see <see cref="BeyondWcagCoverageExtensions.ComputeRunStatus"/>.</summary>
    NotApplicableHere,

    /// <summary>Default for a <see cref="BeyondWcagApplicability.Conditional"/> clause whose condition was not
    /// confirmed either way this run (no feature detector exists yet for it), and for an
    /// <see cref="BeyondWcagApplicability.Always"/>, <see cref="BeyondWcagCheckMethod.PartlyAutomated"/> clause
    /// whose evidence signal did not run on any scanned screen.</summary>
    NotTested,
}

/// <summary>One row of the run-level "Beyond WCAG" list for a standard: a clause's static facts (see
/// <see cref="KnownBeyondWcagClauses"/>) combined with what this run's automated checks did. Never a verdict.</summary>
public sealed record BeyondWcagCoverageEntry(
    string StandardId,
    string ClauseNumber,
    string Title,
    string Summary,
    BeyondWcagRunStatus RunStatus,
    string Label,
    IReadOnlyList<string> GuidedSteps,
    string Source,
    string CheckedOn);

/// <summary>All beyond-WCAG clauses for one standard, for the report's "Beyond WCAG" section.</summary>
public sealed record BeyondWcagStandardCoverage(string StandardId, string StandardName, IReadOnlyList<BeyondWcagCoverageEntry> Clauses);

public static class BeyondWcagCoverageExtensions
{
    /// <summary>
    /// Combines a clause's static facts with what this run's screens show. Pure function: no side effects, no I/O.
    /// </summary>
    public static BeyondWcagRunStatus ComputeRunStatus(this BeyondWcagClause clause, IReadOnlyList<ScreenResult> screens)
    {
        if (clause.Applicability == BeyondWcagApplicability.PlatformOrOrganizational)
            return BeyondWcagRunStatus.NotTestableBySwipewalk;

        if (clause.Applicability == BeyondWcagApplicability.Conditional)
        {
            // No per-clause feature detector (e.g. "does any screen have video/voice-call controls?") exists
            // yet, so a conditional clause is never reported as "not applicable here" today -- only "not
            // tested". When a detector is added for a specific clause, it decides NotApplicableHere here,
            // with the reason drawn from the captured screens; see BeyondWcagCoverageTests for how that path
            // is exercised on a synthetic clause.
            return BeyondWcagRunStatus.NotTested;
        }

        // Always from here.
        return clause.CheckMethod switch
        {
            BeyondWcagCheckMethod.Guided => BeyondWcagRunStatus.Guided,
            BeyondWcagCheckMethod.NotTestable => BeyondWcagRunStatus.NotTestableBySwipewalk,
            BeyondWcagCheckMethod.PartlyAutomated => HasEvidence(clause.Evidence, screens)
                ? BeyondWcagRunStatus.PartlyAutomated
                : BeyondWcagRunStatus.NotTested,
            _ => throw new ArgumentOutOfRangeException(nameof(clause), clause.CheckMethod, "Unhandled BeyondWcagCheckMethod."),
        };
    }

    private static bool HasEvidence(BeyondWcagEvidenceSignal evidence, IReadOnlyList<ScreenResult> screens) => evidence switch
    {
        BeyondWcagEvidenceSignal.TreeRolesAndNames => screens.Count > 0,
        BeyondWcagEvidenceSignal.UserPreferenceRescan => screens.Any(s => s.LargeTextSetting is not null || s.OtherAppearance is not null),
        BeyondWcagEvidenceSignal.None => false,
        _ => throw new ArgumentOutOfRangeException(nameof(evidence), evidence, "Unhandled BeyondWcagEvidenceSignal."),
    };
}

/// <summary>Fixed report wording for each <see cref="BeyondWcagRunStatus"/>, matching <see cref="CoverageDisplay"/>'s
/// vocabulary. Never a compliance verdict.</summary>
public static class BeyondWcagDisplay
{
    /// <param name="screens">This run's scanned screens, so <see cref="BeyondWcagRunStatus.PartlyAutomated"/>
    /// for a <see cref="BeyondWcagEvidenceSignal.UserPreferenceRescan"/> clause can name which rescan actually
    /// ran this run, rather than always listing both possible ones.</param>
    public static string For(BeyondWcagClause clause, BeyondWcagRunStatus status, IReadOnlyList<ScreenResult> screens) => status switch
    {
        // Mirrors CoverageDisplay.PartlyAutomated's "Manual check needed." suffix. Unlike that one, there is no
        // finding count to report here (see BeyondWcagRunStatus.PartlyAutomated's remarks: findings, if any,
        // are counted under the WCAG criterion they were found against, not under this clause).
        BeyondWcagRunStatus.PartlyAutomated => $"Partly checked by automation: {PartlyAutomatedExplanation(clause, screens)} Manual check needed.",
        // Differs from CoverageDisplay.ManualCheckNeeded (a fixed "Not checked automatically: manual check
        // needed.") by including the clause's own 1-3 concrete steps, since every beyond-WCAG Guided clause
        // has them, unlike WCAG's generic Manual status.
        BeyondWcagRunStatus.Guided => WithSteps("Needs a guided check", clause),
        BeyondWcagRunStatus.NotTestableBySwipewalk => $"Not testable by Swipewalk: {clause.Explanation}",
        BeyondWcagRunStatus.NotApplicableHere => $"Not applicable here: {clause.Explanation}",
        BeyondWcagRunStatus.NotTested => NotTested(clause),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unhandled BeyondWcagRunStatus."),
    };

    /// <summary>Names which rescan(s) actually ran this run, so the text never claims a rescan that didn't run.</summary>
    private static string PartlyAutomatedExplanation(BeyondWcagClause clause, IReadOnlyList<ScreenResult> screens)
    {
        if (clause.Evidence != BeyondWcagEvidenceSignal.UserPreferenceRescan)
            return clause.Explanation;

        var largeTextRan = screens.Any(s => s.LargeTextSetting is not null);
        var appearanceRan = screens.Any(s => s.OtherAppearance is not null);
        var ran = (largeTextRan, appearanceRan) switch
        {
            (true, true) => "the large-text rescan and the dark/light appearance rescan both ran",
            (true, false) => "the large-text rescan ran",
            (false, true) => "the dark/light appearance rescan ran",
            _ => "a user-preference rescan ran", // HasEvidence already guarantees one of the above; this is unreachable in practice.
        };
        return $"{ran} on at least one scanned screen this run. {clause.Explanation}";
    }

    private static string WithSteps(string prefix, BeyondWcagClause clause)
    {
        if (clause.GuidedSteps.Count == 0)
            return $"{prefix}: {clause.Explanation}";
        var steps = string.Join(" ", clause.GuidedSteps.Select((s, i) => $"{i + 1}. {s}"));
        return $"{prefix}: {clause.Explanation} {steps}";
    }

    /// <summary>Two different reasons share this status, worded differently so a reader isn't left guessing
    /// which one applies: an <see cref="BeyondWcagApplicability.Always"/> clause whose evidence rescan simply
    /// didn't run this time (fixable by rerunning with the named option), vs. a <see
    /// cref="BeyondWcagApplicability.Conditional"/> clause whose triggering feature was never confirmed either
    /// way (fixable only by a person deciding first whether it applies at all -- for which the clause's own
    /// guided steps are included, since deciding often needs the same look the guided check would take anyway).</summary>
    private static string NotTested(BeyondWcagClause clause) =>
        clause.Applicability == BeyondWcagApplicability.Always
            ? $"Not tested in this run: {EvidenceName(clause.Evidence)} did not run on any scanned screen this time."
            : WithSteps($"Not tested: may not apply -- decide first whether {clause.ConditionDescription}, since Swipewalk doesn't yet detect that automatically", clause);

    /// <summary>Names the evidence signal and how to run it, for <see cref="NotTested"/>'s "always applicable
    /// but didn't run" case -- mirrors <see cref="CoverageDisplay.NotTestedInThisRun(string,string)"/>'s "name
    /// the check, name why" shape.</summary>
    private static string EvidenceName(BeyondWcagEvidenceSignal evidence) => evidence switch
    {
        BeyondWcagEvidenceSignal.UserPreferenceRescan => "the large-text rescan (--large-text) or the dark/light appearance rescan (--appearance both)",
        BeyondWcagEvidenceSignal.TreeRolesAndNames => "a scan with a readable accessibility tree",
        BeyondWcagEvidenceSignal.None => "the underlying check",
        _ => throw new ArgumentOutOfRangeException(nameof(evidence), evidence, "Unhandled BeyondWcagEvidenceSignal."),
    };
}

/// <summary>Builds the run-level "Beyond WCAG" list for a standard from a report's scanned screens.
/// Pure function: no side effects, no I/O.</summary>
public static class BeyondWcagCoverageReport
{
    /// <summary>Every <see cref="KnownBeyondWcagClauses"/> entry for <paramref name="standardId"/>, in catalog
    /// order, combined with what this run's screens show.</summary>
    public static IReadOnlyList<BeyondWcagCoverageEntry> Build(string standardId, IReadOnlyList<ScreenResult> screens) =>
        [.. KnownBeyondWcagClauses.All.Where(c => c.StandardId == standardId).Select(c =>
        {
            var status = c.ComputeRunStatus(screens);
            return new BeyondWcagCoverageEntry(
                c.StandardId, c.ClauseNumber, c.Title, c.Summary, status,
                BeyondWcagDisplay.For(c, status, screens), c.GuidedSteps, c.Source, c.CheckedOn);
        })];

    /// <summary>One <see cref="BeyondWcagStandardCoverage"/> per standard that has at least one beyond-WCAG
    /// clause defined (some standards, e.g. ADA Title II and the UK regulations, have none in the catalog and
    /// are omitted rather than shown with an empty list).</summary>
    public static IReadOnlyList<BeyondWcagStandardCoverage> BuildAll(IReadOnlyList<Standard> standards, IReadOnlyList<ScreenResult> screens) =>
        [.. standards
            .Select(s => new BeyondWcagStandardCoverage(s.Id, s.ShortName, Build(s.Id, screens)))
            .Where(sc => sc.Clauses.Count > 0)];
}
