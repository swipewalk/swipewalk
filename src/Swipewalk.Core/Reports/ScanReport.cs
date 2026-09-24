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
    /// <see cref="Coverage"/>) instead of the per-rule list, which moved to <see cref="RuleCoverage"/>.</summary>
    public const string CurrentSchemaVersion = "0.2";

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
}
