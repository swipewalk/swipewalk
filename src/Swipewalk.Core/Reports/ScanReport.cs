using Swipewalk.Core.Coverage;
using Swipewalk.Core.Limitations;
using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Standards;
using RuleSourceCatalog = Swipewalk.Core.Standards.RuleSources;

namespace Swipewalk.Core.Reports;

/// <summary>Top-level JSON document. Bump <see cref="CurrentSchemaVersion"/> on breaking changes.</summary>
public sealed record ScanReport
{
    /// <summary>0.2: "coverage" now holds the full 55-criterion WCAG 2.2 A/AA run coverage (see
    /// <see cref="Coverage"/>) instead of the per-rule list, which moved to <see cref="RuleCoverage"/>.
    /// 0.3: adds <see cref="GuidedAnswers"/> (loaded from guided-answers.json, the only new field actually
    /// stored on this record -- everything else guided checks add is computed, like <see cref="Coverage"/>
    /// already was) and <see cref="ScreenResult.ScreenId"/>/<see cref="ScreenResult.ProposedNotApplicable"/> on
    /// each screen.</summary>
    public const string CurrentSchemaVersion = "0.3";

    public const string Disclaimer =
        "Automated checks find only some accessibility issues. Results are not a statement of conformance " +
        "with WCAG or any law; manual testing with assistive technology is still required. Screen-reader " +
        "output in this report is predicted from the accessibility tree, not recorded. A screen with no findings " +
        "has not been shown to conform: these checks test only parts of the WCAG criteria listed under coverage, " +
        "and all other WCAG 2.2 success criteria were not tested. See the known limitations for this scan.";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string ToolVersion { get; init; }
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Notice { get; init; } = Disclaimer;
    public required IReadOnlyList<ScreenResult> Screens { get; init; }

    /// <summary>Screens the user expected to cover (record mode --expect), for the coverage section.</summary>
    public IReadOnlyList<string> ExpectedScreens { get; init; } = [];

    /// <summary>Expected screens with no scanned screen whose name contains them.</summary>
    public IReadOnlyList<string> MissingScreens =>
        [.. ExpectedScreens.Where(e => !Screens.Any(s => s.ScreenName.Contains(e, StringComparison.OrdinalIgnoreCase)))];

    /// <summary>The checks that ran and the WCAG criteria they partly cover, by rule id. See
    /// <see cref="Coverage"/> for the criterion-first view every WCAG 2.2 A/AA success criterion appears in.</summary>
    public IReadOnlyList<RuleCoverage> RuleCoverage { get; init; } = DefaultRules.Coverage;

    /// <summary>
    /// Every WCAG 2.2 A/AA success criterion (55), with what this run's automated checks did about it (see
    /// Swipewalk.Core.Coverage.CoverageCatalog and CoverageReport). Computed from <see cref="Screens"/>
    /// rather than stored, so it always reflects the screens actually in this report -- including a report
    /// loaded from an older results.json that predates this field, which has no "coverage" property to
    /// conflict with it.
    /// </summary>
    public IReadOnlyList<CoverageReportEntry> Coverage => CoverageReport.Build(Screens);

    /// <summary>One-line summary of <see cref="Coverage"/>; see <see cref="CoverageReport.Summary"/>.</summary>
    public string CoverageSummary => CoverageReport.Summary(Coverage);

    /// <summary>
    /// Requirements Section 508 and EN 301 549 add beyond what they reference from WCAG (see
    /// Swipewalk.Core.Standards.KnownBeyondWcagClauses), with an honest per-run status for each (see
    /// Swipewalk.Core.Coverage.BeyondWcagCoverageReport). One entry per standard that has a WCAG basis in
    /// scope for this run (<see cref="FocusStandard"/> narrows it to just that standard); standards with no
    /// beyond-WCAG clauses in the catalog (ADA Title II, the UK regulations) are omitted. Computed from
    /// <see cref="Screens"/> rather than stored, like <see cref="Coverage"/>.
    /// </summary>
    public IReadOnlyList<BeyondWcagStandardCoverage> BeyondWcag =>
        BeyondWcagCoverageReport.BuildAll(
            FocusStandard is { } id ? [.. KnownStandards.All.Where(s => s.Id == id)] : KnownStandards.All,
            Screens);

    /// <summary>Known limitations and framework notes that apply to the scanned platforms and frameworks.</summary>
    public IReadOnlyList<Limitation> Limitations =>
        [.. KnownLimitations.All.Where(l => Screens.Any(s => l.AppliesTo(s.Platform, s.Framework)))];

    /// <summary>Standard the report focuses on (--standard); headline counts include only findings relevant to it.</summary>
    public string? FocusStandard { get; init; }

    /// <summary>
    /// App identifier for this run (Android package, iOS bundle id): the id passed in when the scan started, or
    /// else detected from the capture itself (see <see cref="ScreenResult.AppId"/>). Null when neither is known,
    /// including every results.json written before this field existed. Swipewalk.Engine.RunHistory uses this
    /// (as <c>RunRecord.AppKey</c>) to group and compare runs of the same app without a typed
    /// <c>--package</c>/<c>--bundle-id</c>; a null value here must never be treated as matching another run's
    /// null value, since two unidentified runs could be different apps. Omitted from results.json when null.
    /// </summary>
    public string? AppId { get; init; }

    /// <summary>
    /// Set (record mode only) when the recording ended before "Finish" was chosen -- a cancellation or an
    /// error while capturing a screen, including the very last screen attempted (see Swipewalk.Engine.Recorder /
    /// Swipewalk.Engine.EndedEarlyReasons). Null for a recording that ran to "Finish", and always null for a
    /// single scan. The report and run history state this factually; screens after the interruption are
    /// simply not in <see cref="Screens"/>, and the coverage section must not be read as saying they were
    /// checked and found fine.
    /// </summary>
    public string? EndedEarlyReason { get; init; }

    /// <summary>
    /// Record mode only: one entry per period of recording activity in this run -- from the recording starting
    /// (fresh, or via <c>record --continue</c>/the desktop app's Continue) to it stopping (Finish, a
    /// cancellation, an error, or the process ending). A single, uninterrupted recording has exactly one entry.
    /// More than one means the report says so ("recorded across N sessions"). Empty
    /// for a scan, and for a record run saved before this field existed.
    /// </summary>
    public IReadOnlyList<RecordingSession> Sessions { get; init; } = [];

    /// <summary>The standards findings are mapped to, with the disclaimer on what "relevant" means.</summary>
    public IReadOnlyList<Standard> Standards => KnownStandards.All;

    public string StandardsNotice => KnownStandards.Disclaimer;

    /// <summary>Version of Swipewalk's rules and mappings, and the sources they were checked against.</summary>
    public string RulesetVersion { get; init; } = RuleSourceCatalog.RulesetVersion;

    public IReadOnlyList<RuleSource> RuleSources { get; init; } = RuleSourceCatalog.All;

    /// <summary>Sources whose mapping was last reviewed more than a year before this report.</summary>
    public IReadOnlyList<RuleSource> StaleRuleSources => [.. RuleSources.Where(r => r.IsStale(GeneratedAt, RuleSourceCatalog.MaxAge))];

    /// <summary>Findings of a kind; with <see cref="FocusStandard"/>, WCAG findings count only if relevant to it.</summary>
    public int Count(FindingKind kind) => Screens.Sum(s => s.Findings.Count(f => f.Kind == kind && InFocus(f)));

    public bool InFocus(Finding f) =>
        FocusStandard is null || f.Kind == FindingKind.PlatformAdvisory || f.RelevantStandards.Contains(FocusStandard);

    /// <summary>
    /// Guided-check answers a tester recorded for this run (loaded from guided-answers.json -- see
    /// <c>Engine.GuidedAnswerStore</c>). Unlike everything else on this record, these can't be recomputed from
    /// the screens, so they're the one guided-checks field actually stored here rather than computed.
    /// </summary>
    public IReadOnlyList<GuidedAnswer> GuidedAnswers { get; init; } = [];

    /// <summary>Per screen, per WCAG 2.2 A/AA criterion, what Swipewalk automated and what a tester recorded --
    /// see <see cref="Coverage.ScreenCoverageBuilder"/>. Computed, not stored, exactly like <see cref="Coverage"/>.</summary>
    public IReadOnlyList<ScreenCriterionReport> ScreenCoverage => ScreenCoverageBuilder.Build(Screens, GuidedAnswers);

    /// <summary>One status per criterion across the whole run, worst case wins -- see <see cref="GuidedRollupBuilder"/>.</summary>
    public IReadOnlyList<GuidedCriterionRollup> GuidedRollup => GuidedRollupBuilder.Build(ScreenCoverage);

    /// <summary>
    /// Guided answers that disagree with an automated finding, or with an earlier answer for the same (screen,
    /// criterion) -- see <see cref="Coverage.ContradictionChecker"/>. The CLI (<c>swipewalk guide</c>) and the
    /// desktop Guided checks page both show a contradiction immediately when it happens; the HTML report
    /// (<see cref="HtmlReport"/>) renders this same list in a "Flagged" section near the top, never buried
    /// inside one screen's row only.
    /// </summary>
    public IReadOnlyList<CoverageContradiction> Contradictions => ContradictionChecker.Find(Screens, GuidedAnswers);

    /// <summary>
    /// Gap check: recorded screens with no guided answer at all yet, listed by name (not just a count) so a
    /// tester can jump straight to them.
    /// </summary>
    public IReadOnlyList<ScreenResult> ScreensWithNoGuidedAnswers =>
        [.. Screens.Where(s => !GuidedAnswers.Any(a => a.ScreenId == s.ScreenId))];
}
