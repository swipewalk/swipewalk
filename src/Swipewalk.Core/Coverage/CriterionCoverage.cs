using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Coverage;

/// <summary>
/// What Swipewalk can say about a WCAG 2.2 success criterion before any scan runs, independent of a
/// particular app or report. See <see cref="CoverageCatalog"/> for the full A/AA catalog and
/// <see cref="CriterionCoverageExtensions.ComputeRunStatus"/> for the per-run status.
/// </summary>
public enum CoverageBaseStatus
{
    /// <summary>A rule flags some possible failures automatically, but a manual check is still needed (e.g.
    /// to confirm meaning, or to check states or content automation can't reach). This is the status for
    /// every criterion at least one rule currently maps to: none of Swipewalk's rules fully confirm a
    /// criterion without a manual check, so there is currently no "fully automated" status.</summary>
    PartlyAutomated,

    /// <summary>No Swipewalk rule checks this criterion; it needs a manual check.</summary>
    Manual,

    /// <summary>
    /// WCAG2ICT says this criterion, written for "a set of Web pages", applies to non-web software only
    /// across "a set of software programs" (separate programs from the same author, distributed together
    /// and interlinked so users can move between them) and notes such sets "appear to be extremely rare" —
    /// so a single self-contained app is usually outside its scope. "Usually": say so, don't claim it never
    /// applies.
    /// </summary>
    UsuallyNotApplicable,
}

/// <summary>The status Swipewalk reports for a criterion on one run, after combining <see cref="CoverageBaseStatus"/>
/// with which checks actually ran on the scanned screens. Never "passed": <see cref="PartlyAutomated"/> carries
/// finding counts instead of a verdict, and a 0 count is never displayed as "no issues" (see
/// <see cref="CoverageDisplay"/>).</summary>
public enum CoverageRunStatus
{
    /// <summary>At least one rule mapped to this criterion ran on at least one scanned screen. Carries the
    /// number of WcagIssue and NeedsReview findings that cite this criterion (PlatformAdvisory findings are
    /// never counted here: see <see cref="CriterionCoverageExtensions.ComputeRunStatus"/>).</summary>
    PartlyAutomated,

    /// <summary>Base status is <see cref="CoverageBaseStatus.Manual"/>: no rule runs for this criterion.</summary>
    ManualCheckNeeded,

    /// <summary>Base status is <see cref="CoverageBaseStatus.UsuallyNotApplicable"/>.</summary>
    UsuallyNotApplicable,

    /// <summary>Base status is <see cref="CoverageBaseStatus.PartlyAutomated"/>, but none of the rules mapped
    /// to this criterion ran on any scanned screen this run (e.g. the large-text capture was skipped on
    /// every screen, or the app is Android and no Apple audit engine ran). Distinct from
    /// <see cref="ManualCheckNeeded"/>: a check exists, it just didn't run this time.</summary>
    NotTestedInThisRun,
}

/// <summary>
/// Static, per-criterion facts shown in every report: what Swipewalk can automate today, which rule ids
/// contribute, how to check the rest by hand, and where the mapping comes from. Independent of any one scan;
/// see <see cref="RunActivity"/> and <see cref="CriterionCoverageExtensions.ComputeRunStatus"/> for what a
/// particular run found.
/// </summary>
/// <param name="Criterion">The WCAG 2.2 success criterion.</param>
/// <param name="BaseStatus">What Swipewalk can automate for this criterion in general.</param>
/// <param name="RuleIds">Rule ids (see <c>DefaultRules.Coverage</c>) that contribute findings for this
/// criterion. These decide which checks "ran"; they are not used to count findings (see
/// <see cref="CriterionCoverageExtensions.ComputeRunStatus"/>, which counts by
/// <c>Finding.Criteria.Contains(Criterion)</c> instead, since a rule can cite different criteria per
/// finding). Empty for <see cref="CoverageBaseStatus.Manual"/> and <see cref="CoverageBaseStatus.UsuallyNotApplicable"/>.</param>
/// <param name="Note">For <see cref="CoverageBaseStatus.Manual"/> or <see cref="CoverageBaseStatus.PartlyAutomated"/>:
/// a short, concrete "how to check by hand" instruction (native-app oriented, naming TalkBack/VoiceOver where
/// relevant). For <see cref="CoverageBaseStatus.UsuallyNotApplicable"/>: the one-sentence WCAG2ICT reasoning.
/// Never a compliance verdict.</param>
/// <param name="SourceUrls">The W3C "Understanding" page(s) for the criterion, and/or the WCAG2ICT "Applying
/// SC ... to non-web software" page for <see cref="CoverageBaseStatus.UsuallyNotApplicable"/> entries and a
/// few others WCAG2ICT specifically reinterprets for software.</param>
public sealed record CriterionCoverage(
    WcagCriterion Criterion,
    CoverageBaseStatus BaseStatus,
    IReadOnlyList<string> RuleIds,
    string? Note,
    IReadOnlyList<string> SourceUrls);

/// <summary>
/// What happened on one scanned screen, as far as rule coverage is concerned: which rule ids ran (even if
/// they produced no findings), which were attempted but skipped on this screen (with why -- e.g. "no usable
/// screenshot" for text-contrast, "no LargeText capture" for text-resize), and the findings this screen
/// produced. Engine findings use rule ids like "engine:hitRegion", "engine:contrast" (one per Apple audit
/// issue type; see <c>EngineIssueRule</c>): <see cref="CriterionCoverageExtensions.ComputeRunStatus"/>
/// matches these by prefix against the coverage rule id "engine", so "engine" only counts as having run on
/// a screen where an engine audit (currently Apple's, iOS only) actually ran there.
/// </summary>
public sealed record ScreenActivity(
    IReadOnlySet<string> RanRuleIds,
    IReadOnlyDictionary<string, string> SkippedRuleIds,
    IReadOnlyList<Finding> Findings);

/// <summary>
/// What happened on a run: the activity of every screen scanned. Kept intentionally simple (a list of
/// per-screen activity) so the later wiring can build it directly from each screen's <c>ScreenSnapshot</c>
/// (which rules ran and were skipped) and <c>ScreenResult</c> (the findings).
/// </summary>
public sealed record RunActivity(IReadOnlyList<ScreenActivity> Screens)
{
    public static readonly RunActivity Empty = new([]);
}

/// <summary>How many of the scanned screens one rule mapped to a criterion ran on, and why it was skipped on
/// the rest. Reported per rule (not just per criterion) so a rule that ran on some but not all screens --
/// e.g. the large-text rescan on a screen the app never returned to at the larger size -- isn't hidden by a
/// criterion-level count that only asks "did anything run".</summary>
/// <param name="RuleId">The rule id, e.g. "text-resize". Never shown to readers directly -- see
/// <see cref="CoverageDisplay.RuleName"/> for the display name.</param>
/// <param name="ScreensRan">Number of scanned screens this rule ran on.</param>
/// <param name="ScreensTotal">Total scanned screens this run.</param>
/// <param name="SkipReasons">Distinct reasons recorded on the screens where this rule was skipped.</param>
public sealed record RuleRunSummary(string RuleId, int ScreensRan, int ScreensTotal, IReadOnlyList<string> SkipReasons);

/// <summary>The status computed for one criterion on one run, with the evidence behind it. Never "passed": a
/// 0/0 finding count under <see cref="CoverageRunStatus.PartlyAutomated"/> means automated checks found
/// nothing on the screens where they ran, not that the criterion is met -- see <see cref="CoverageDisplay"/>
/// for how this is worded.</summary>
/// <param name="Status">The computed run status.</param>
/// <param name="WcagIssueCount">Findings citing this criterion with <see cref="FindingKind.WcagIssue"/>.</param>
/// <param name="NeedsReviewCount">Findings citing this criterion with <see cref="FindingKind.NeedsReview"/>.
/// <see cref="FindingKind.PlatformAdvisory"/> findings are never counted here.</param>
/// <param name="ScreensRan">Number of scanned screens on which at least one rule mapped to this criterion ran.</param>
/// <param name="ScreensTotal">Total scanned screens this run.</param>
/// <param name="RuleIdsThatDidNotRun">Rule ids mapped to this criterion that never ran on any screen this run
/// (a subset of <see cref="RuleActivity"/>: those with <see cref="RuleRunSummary.ScreensRan"/> zero).</param>
/// <param name="SkipReasons">Distinct reasons collected from <see cref="ScreenActivity.SkippedRuleIds"/> for
/// the rule ids in <paramref name="RuleIdsThatDidNotRun"/>, when any screen recorded one.</param>
/// <param name="RuleActivity">Per-rule breakdown for every rule id mapped to this criterion, including ones
/// that ran everywhere (so callers can tell "fully covered" apart from "ran on some screens"). See
/// <see cref="CoverageDisplay.PartlyAutomated(int,int,IReadOnlyList{RuleRunSummary})"/> for how a gap here is
/// surfaced in the displayed label -- never silently, and never by rule id.</param>
public sealed record CriterionRunResult(
    CoverageRunStatus Status,
    int WcagIssueCount,
    int NeedsReviewCount,
    int ScreensRan,
    int ScreensTotal,
    IReadOnlyList<string> RuleIdsThatDidNotRun,
    IReadOnlyList<string> SkipReasons,
    IReadOnlyList<RuleRunSummary> RuleActivity);

public static class CriterionCoverageExtensions
{
    /// <summary>
    /// Combines a criterion's static <see cref="CriterionCoverage.BaseStatus"/> with what actually ran this
    /// run. Pure function: no side effects, no I/O. Counts findings by
    /// <c>finding.Criteria.Contains(coverage.Criterion)</c>, split by <see cref="FindingKind.WcagIssue"/> vs
    /// <see cref="FindingKind.NeedsReview"/>; <see cref="FindingKind.PlatformAdvisory"/> findings are never
    /// counted, and <see cref="CriterionCoverage.RuleIds"/> only decides which checks "ran", not what's counted.
    /// </summary>
    public static CriterionRunResult ComputeRunStatus(this CriterionCoverage coverage, RunActivity activity)
    {
        var screensTotal = activity.Screens.Count;

        switch (coverage.BaseStatus)
        {
            case CoverageBaseStatus.UsuallyNotApplicable:
                return new CriterionRunResult(CoverageRunStatus.UsuallyNotApplicable, 0, 0, 0, screensTotal, [], [], []);
            case CoverageBaseStatus.Manual:
                return new CriterionRunResult(CoverageRunStatus.ManualCheckNeeded, 0, 0, 0, screensTotal, [], [], []);
        }

        // PartlyAutomated base status from here. A criterion in that state should always list at least one
        // rule id (see CoverageCatalogTests); treat the data error defensively rather than throw.
        if (coverage.RuleIds.Count == 0)
            return new CriterionRunResult(CoverageRunStatus.ManualCheckNeeded, 0, 0, 0, screensTotal, [], [], []);

        var ranCountByRuleId = coverage.RuleIds.ToDictionary(id => id, _ => 0);
        var skipReasonsByRuleId = new Dictionary<string, HashSet<string>>();
        var screensRan = 0;

        foreach (var screen in activity.Screens)
        {
            var ranHere = false;
            foreach (var ruleId in coverage.RuleIds)
            {
                var skippedHere = screen.SkippedRuleIds.Where(kv => Matches(ruleId, kv.Key)).ToList();
                if (skippedHere.Count > 0)
                {
                    var reasons = skipReasonsByRuleId.TryGetValue(ruleId, out var set) ? set : skipReasonsByRuleId[ruleId] = [];
                    foreach (var (_, reason) in skippedHere)
                        reasons.Add(reason);
                    continue; // explicitly skipped on this screen takes precedence over also appearing to have run
                }
                if (screen.RanRuleIds.Any(id => Matches(ruleId, id)))
                {
                    ranCountByRuleId[ruleId]++;
                    ranHere = true;
                }
            }
            if (ranHere)
                screensRan++;
        }

        var ruleActivity = coverage.RuleIds
            .Select(id => new RuleRunSummary(id, ranCountByRuleId[id], screensTotal,
                [.. skipReasonsByRuleId.TryGetValue(id, out var set) ? set : []]))
            .ToList();
        var didNotRun = ruleActivity.Where(r => r.ScreensRan == 0).Select(r => r.RuleId).ToList();

        if (screensRan == 0)
        {
            var reasons = ruleActivity.SelectMany(r => r.SkipReasons).Distinct().ToList();
            return new CriterionRunResult(CoverageRunStatus.NotTestedInThisRun, 0, 0, 0, screensTotal, didNotRun, reasons, ruleActivity);
        }

        var findings = activity.Screens.SelectMany(s => s.Findings).Where(f => f.Criteria.Contains(coverage.Criterion)).ToList();
        var wcagIssues = findings.Count(f => f.Kind == FindingKind.WcagIssue);
        var needsReview = findings.Count(f => f.Kind == FindingKind.NeedsReview);
        // Reasons for rules that didn't cover every screen (never ran, or ran on only some) -- fixes an
        // earlier bug where this branch always reported an empty SkipReasons even when RuleIdsThatDidNotRun
        // was non-empty, hiding the gap from anyone reading only the flat fields.
        var partialReasons = ruleActivity.Where(r => r.ScreensRan < r.ScreensTotal).SelectMany(r => r.SkipReasons).Distinct().ToList();

        return new CriterionRunResult(CoverageRunStatus.PartlyAutomated, wcagIssues, needsReview, screensRan, screensTotal, didNotRun, partialReasons, ruleActivity);
    }

    /// <summary>True when <paramref name="coverageRuleId"/> (e.g. "engine") equals, or is the colon-prefix
    /// of, <paramref name="actualId"/> (e.g. "engine:hitRegion" from an Apple audit finding).</summary>
    private static bool Matches(string coverageRuleId, string actualId) =>
        actualId == coverageRuleId || actualId.StartsWith(coverageRuleId + ":", StringComparison.Ordinal);
}

/// <summary>
/// Fixed report wording for each <see cref="CoverageRunStatus"/>. Never a compliance verdict: a
/// <see cref="CoverageRunStatus.PartlyAutomated"/> result with 0 findings reads as "0 automated findings",
/// never "no issues" or "passed". Never shows a rule id to a reader: see <see cref="RuleName"/>.
/// </summary>
public static class CoverageDisplay
{
    public const string ManualCheckNeeded = "Not checked automatically: manual check needed.";

    public const string UsuallyNotApplicableText =
        "Usually out of scope for a single app (WCAG2ICT: applies to sets of software programs). Confirm.";

    /// <summary>The exact reason <c>ScreenActivityBuilder</c> records for "engine" on a non-iOS screen. Used
    /// so <see cref="RuleGap"/> can phrase that specific, expected-by-design gap calmly ("(iOS only)")
    /// instead of the generic, more alarming "Not run: ... (reason)." wording.</summary>
    public const string EngineIosOnlyReason = "Apple accessibility audit runs on iOS only";

    /// <summary>The exact reason <c>ScreenActivityBuilder</c> records for "atf" on a non-Android screen; see
    /// <see cref="EngineIosOnlyReason"/> for the iOS equivalent.</summary>
    public const string AtfAndroidOnlyReason = "Google Accessibility Test Framework runs on Android only";

    /// <summary>The exact reason <c>ScreenActivityBuilder</c> records for "page-titled" on a non-Android
    /// screen: it shares <see cref="AtfAndroidOnlyReason"/>'s platform gate (same harness pass) but is a
    /// different rule id, so it needs its own exact-match constant for <see cref="RuleGap"/>.</summary>
    public const string PageTitledAndroidOnlyReason = "The page-titled check needs the Android instrumentation harness, so it runs on Android only";

    /// <summary>The exact reason <c>ScreenActivityBuilder</c> records for "icon-contrast" on a non-iOS
    /// screen: Android icon contrast is covered instead by atf's ImageContrastCheck.</summary>
    public const string IconContrastIosOnlyReason = "The icon-contrast check runs on iOS only; Android icon contrast is covered by the Google Accessibility Test Framework's ImageContrastCheck instead";

    /// <summary>The exact reason <c>ScreenActivityBuilder</c> records for "text-resize-navigation" on a
    /// non-Android screen: the underlying signal (an activity recreated on a font-scale change, losing the
    /// person's navigation place) is Android-specific -- see <c>TextResizeNavigationRule</c>'s remarks.</summary>
    public const string TextResizeNavigationAndroidOnlyReason = "The text-resize-navigation check runs on Android only";

    /// <summary>The exact reason <c>ScreenActivityBuilder</c> records for "offscreen-unreachable" on a
    /// non-iOS screen: Android's uiautomator dump clips every node's reported bounds to the visible screen,
    /// so this rule has nothing to measure there -- see <c>OffscreenUnreachableRule</c>'s own remarks.</summary>
    public const string OffscreenUnreachableIosOnlyReason = "The offscreen-unreachable check runs on iOS only; Android's uiautomator dump clips element bounds to the visible screen, so it has nothing to measure there";

    /// <summary>Short, reader-facing names for rule ids. Reports never show a raw rule id in a label or the
    /// WCAG coverage table; results.json keeps the ids (e.g. <see cref="CriterionRunResult.RuleIdsThatDidNotRun"/>)
    /// for tooling, and this is the one place that maps them to text a person reads.</summary>
    private static readonly IReadOnlyDictionary<string, string> RuleNames = new Dictionary<string, string>
    {
        ["text-resize"] = "large-text rescan",
        ["text-contrast"] = "contrast measurement",
        ["engine"] = "Apple accessibility audit",
        ["atf"] = "Google Accessibility Test Framework",
        ["missing-name"] = "missing-name check",
        ["target-size"] = "target-size check",
        ["identifier-name"] = "identifier-as-label check",
        ["label-in-name"] = "label-in-name check",
        ["page-titled"] = "page-titled check",
        ["input-purpose"] = "input-purpose check",
        ["icon-contrast"] = "icon-contrast check",
        ["offscreen-unreachable"] = "off-screen-content check",
    };

    /// <summary>The reader-facing name for a rule id (falls back to the id itself for any rule not in the
    /// table above, so a future rule doesn't silently disappear from a label).</summary>
    public static string RuleName(string ruleId) => RuleNames.GetValueOrDefault(ruleId, ruleId);

    public static string PartlyAutomated(int wcagIssueCount, int needsReviewCount, IReadOnlyList<RuleRunSummary>? ruleActivity = null)
    {
        var counts = wcagIssueCount == 0 && needsReviewCount == 0
            ? "0 automated findings"
            : $"{wcagIssueCount} issue{(wcagIssueCount == 1 ? "" : "s")}, {needsReviewCount} to review";
        var text = $"Partly checked by automation: {counts}. Manual check needed.";

        // Rules mapped to this criterion that didn't cover every scanned screen: never hide this behind a
        // criterion-level "something ran" status. Rules that ran on every screen need no mention.
        var gaps = (ruleActivity ?? []).Where(r => r.ScreensRan < r.ScreensTotal).Select(RuleGap).ToList();
        return gaps.Count == 0 ? text : $"{text} {string.Join(" ", gaps)}";
    }

    /// <summary>One sentence describing a single rule's gap, using its display name, never its id: "Not run:
    /// <c>name</c> (<c>reason</c>)." when it never ran on any screen; "<c>name</c> ran on N of M screen(s)."
    /// when it ran on some but not all; the Apple accessibility audit's iOS-only reason is phrased as a fact
    /// about the platform ("(iOS only)"), not as an alarming failure.</summary>
    private static string RuleGap(RuleRunSummary rule)
    {
        var name = RuleName(rule.RuleId);
        if (rule.ScreensRan > 0)
            return $"{name} ran on {rule.ScreensRan} of {rule.ScreensTotal} screen(s).";
        if (rule.RuleId == "engine" && rule.SkipReasons.Contains(EngineIosOnlyReason))
            return $"{name} (iOS only).";
        if (rule.RuleId == "atf" && rule.SkipReasons.Contains(AtfAndroidOnlyReason))
            return $"{name} (Android only).";
        if (rule.RuleId == "page-titled" && rule.SkipReasons.Contains(PageTitledAndroidOnlyReason))
            return $"{name} (Android only).";
        if (rule.RuleId == "icon-contrast" && rule.SkipReasons.Contains(IconContrastIosOnlyReason))
            return $"{name} (iOS only).";
        if (rule.RuleId == "offscreen-unreachable" && rule.SkipReasons.Contains(OffscreenUnreachableIosOnlyReason))
            return $"{name} (iOS only).";
        var reason = rule.SkipReasons.Count > 0 ? string.Join("; ", rule.SkipReasons) : "did not run on any scanned screen";
        return $"Not run: {name} ({reason}).";
    }

    public static string NotTestedInThisRun(string check, string reason) =>
        $"Not tested in this run: {check} did not run ({reason}).";

    /// <summary>Builds the <see cref="NotTestedInThisRun(string,string)"/> text from a
    /// <see cref="CriterionRunResult"/> with that status.</summary>
    public static string NotTestedInThisRun(CriterionRunResult result)
    {
        var check = result.RuleIdsThatDidNotRun.Count > 0
            ? string.Join(", ", result.RuleIdsThatDidNotRun.Select(RuleName))
            : "the check";
        var reason = result.SkipReasons.Count > 0
            ? string.Join("; ", result.SkipReasons)
            : "it did not run on any scanned screen";
        return NotTestedInThisRun(check, reason);
    }

    /// <summary>The display text for a run result, combining the fixed wording above.</summary>
    public static string For(CriterionRunResult result) => result.Status switch
    {
        CoverageRunStatus.PartlyAutomated => PartlyAutomated(result.WcagIssueCount, result.NeedsReviewCount, result.RuleActivity),
        CoverageRunStatus.ManualCheckNeeded => ManualCheckNeeded,
        CoverageRunStatus.UsuallyNotApplicable => UsuallyNotApplicableText,
        CoverageRunStatus.NotTestedInThisRun => NotTestedInThisRun(result),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unhandled CoverageRunStatus."),
    };
}
