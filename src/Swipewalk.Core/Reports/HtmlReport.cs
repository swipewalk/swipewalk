using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Text;
using Swipewalk.Core.Coverage;
using Swipewalk.Core.Imaging;
using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;
using Swipewalk.Core.Standards;

namespace Swipewalk.Core.Reports;

/// <summary>
/// Self-contained HTML report: per screen, a screenshot with finding overlays and the predicted swipe
/// order, and per finding a close-up of the element, the WCAG mapping and a suggested fix.
/// </summary>
public static class HtmlReport
{
    public static string Render(ScanReport report)
    {
        var html = new StringBuilder();
        html.Append(Head);
        html.Append($"""
            <header class="top">
              <div>
                <p class="eyebrow">Swipewalk accessibility scan</p>
                <h1>Automated checks found {Plural(report.Count(FindingKind.WcagIssue), "WCAG issue")}{FocusSuffix(report)}</h1>
                <p class="meta">{report.Screens.Count} screen(s) · generated {report.GeneratedAt.ToLocalTime():yyyy-MM-dd HH:mm} · swipewalk {E(report.ToolVersion)}</p>
                <p class="meta wcag-summary">{E(report.CoverageSummary)} <a href="#wcag-coverage-title">See WCAG 2.2 coverage.</a></p>
                {SessionsNote(report)}
              </div>
              <div class="tiles">
                {Tile(report.Count(FindingKind.WcagIssue), "WCAG issues", "issue")}
                {Tile(report.Count(FindingKind.NeedsReview), "Needs review", "review")}
                {Tile(report.Count(FindingKind.PlatformAdvisory), "Platform advisories", "advisory")}
              </div>
            </header>
            <main>
            <p class="notice"><strong>Read this first.</strong> {E(report.Notice)}</p>
            """);
        if (report.EndedEarlyReason is { } endedEarly)
            html.Append($"""
                <p class="notice ended-early"><strong>This recording ended early.</strong> {E(endedEarly)}</p>
                """);
        if (report.StaleRuleSources.Count > 0)
            html.Append($"""
                <p class="notice stale"><strong>Rule mappings may be out of date.</strong> These were last reviewed more than a year before this report:
                {E(string.Join("; ", report.StaleRuleSources.Select(r => $"{r.Name} (reviewed {r.CheckedOn})")))}. Check the sources for changes, or update Swipewalk.</p>
                """);

        RenderWcagGaps(html, report);
        RenderStandards(html, report);
        RenderCoverage(html, report);

        for (var i = 0; i < report.Screens.Count; i++)
            RenderScreen(html, report, report.Screens[i], i);

        RenderLimitations(html, report);
        RenderWcagCoverage(html, report);

        html.Append("""
            </main>
            <footer>
              <h2>Automated checks, by rule</h2>
              <p class="hint">Technical detail behind the "WCAG 2.2 A/AA coverage" table above: each rule id used in this report's findings, what it looks for, and the criteria it partly covers. Each check tests only part of the listed criteria.</p>
              <table class="coverage"><thead><tr><th scope="col">Check</th><th scope="col">Looks for</th><th scope="col">WCAG criteria (partly)</th></tr></thead><tbody>
            """);
        foreach (var c in report.RuleCoverage)
        {
            var criteria = c.Criteria.Count > 0 ? string.Join("; ", c.Criteria) : "None (platform guideline only)";
            html.Append($"""<tr><td><code>{E(c.RuleId)}</code></td><td>{E(c.Checks)}</td><td>{E(criteria)}</td></tr>""");
        }
        html.Append("""
              </tbody></table>
              <h2>Still to test manually</h2>
              <ul>
                <li>Turn on TalkBack / VoiceOver / Narrator and swipe through each screen: is the order logical and is every name meaningful?</li>
                <li>Increase the system font size to the maximum: does any text get cut off or overlap?</li>
                <li>Check that information is not conveyed by color alone, and that errors are announced.</li>
                <li>Screens not listed in this report were not scanned.</li>
              </ul>
            </footer>
            """);
        html.Append(Script);
        html.Append("</body></html>\n");
        return html.ToString();
    }

    private static readonly IReadOnlyDictionary<CoverageRunStatus, int> WcagGroupOrder = new Dictionary<CoverageRunStatus, int>
    {
        [CoverageRunStatus.NotTestedInThisRun] = 0,
        [CoverageRunStatus.PartlyAutomated] = 1,
        [CoverageRunStatus.ManualCheckNeeded] = 2,
        [CoverageRunStatus.UsuallyNotApplicable] = 3,
    };

    private static readonly IReadOnlyDictionary<CoverageRunStatus, string> WcagGroupTitle = new Dictionary<CoverageRunStatus, string>
    {
        [CoverageRunStatus.NotTestedInThisRun] = "Not tested in this run",
        [CoverageRunStatus.PartlyAutomated] = "Partly checked by automation",
        [CoverageRunStatus.ManualCheckNeeded] = "Needs a manual check",
        [CoverageRunStatus.UsuallyNotApplicable] = "Usually out of scope for a single app",
    };

    /// <summary>
    /// A short notice near the top of the report listing only the criteria that are "Not tested in this
    /// run" (the full 55-criterion table is much further down, after the screens, so it doesn't push the
    /// findings out of view -- see <see cref="RenderWcagCoverage"/>). Renders nothing when there are none,
    /// so a run with no gaps doesn't get an empty notice.
    /// </summary>
    private static void RenderWcagGaps(StringBuilder html, ScanReport report)
    {
        var gaps = report.Coverage.Where(e => e.RunStatus == CoverageRunStatus.NotTestedInThisRun).ToList();
        if (gaps.Count == 0)
            return;

        html.Append($"""
            <section class="wcag-gaps" aria-labelledby="wcag-gaps-title">
              <h2 id="wcag-gaps-title">Not tested in this run <span class="count">{gaps.Count}</span></h2>
              <p class="hint">These WCAG 2.2 criteria have an automated check, but it didn't run on any scanned screen this time. See <a href="#wcag-coverage-title">WCAG 2.2 A/AA coverage</a> below for every criterion, including these, and how to check the rest by hand.</p>
              <ul>
            """);
        foreach (var e in gaps)
            html.Append($"""<li><strong>{E($"{e.Number} {e.Name} ({e.Level})")}</strong> — {E(e.Label)}</li>""");
        html.Append("""
              </ul>
            </section>
            """);
    }

    /// <summary>
    /// The full WCAG 2.2 A/AA coverage table (55 criteria): what this run's automated checks did for each
    /// one, grouped with "Not tested in this run" first so gaps are the most visible thing on the page, then
    /// "Partly checked by automation", "Needs a manual check", "Usually out of scope". No color or checkmark
    /// implies a pass for any status; see <see cref="ScanReport.Coverage"/> for how the data is computed.
    /// Placed after the screens (see <see cref="Render"/>): <see cref="RenderWcagGaps"/> keeps the gaps
    /// prominent near the top without pushing the findings themselves far down the page.
    /// </summary>
    private static void RenderWcagCoverage(StringBuilder html, ScanReport report)
    {
        html.Append($"""
            <section class="wcag-coverage" aria-labelledby="wcag-coverage-title">
              <h2 id="wcag-coverage-title">WCAG 2.2 A/AA coverage</h2>
              <p class="hint">Automated checks cover only part of WCAG. Every criterion below needs a person to check it; statuses describe what this scan did, not whether the app meets the criterion.</p>
              <table class="coverage wcag-coverage-table">
                <caption>{report.Coverage.Count} WCAG 2.2 Level A/AA success criteria</caption>
                <thead><tr><th scope="col">Criterion</th><th scope="col">Status</th><th scope="col">Counts</th><th scope="col">How to check the rest</th></tr></thead>
                <tbody>
            """);
        foreach (var group in report.Coverage.GroupBy(e => e.RunStatus).OrderBy(g => WcagGroupOrder[g.Key]))
        {
            html.Append($"""<tr class="wcag-group"><th colspan="4" scope="colgroup">{E(WcagGroupTitle[group.Key])} <span class="count">{group.Count()}</span></th></tr>""");
            foreach (var e in group)
            {
                var href = e.SourceUrls.Count > 0 ? e.SourceUrls[0] : null;
                var criterionLabel = $"{e.Number} {e.Name} ({e.Level})";
                var criterion = href is null ? E(criterionLabel) : $"""<a href="{E(href)}">{E(criterionLabel)}</a>""";
                var rowClass = e.RunStatus == CoverageRunStatus.NotTestedInThisRun ? " class=\"wcag-not-tested\"" : "";
                html.Append($"""
                    <tr{rowClass}>
                      <td>{criterion}</td>
                      <td>{E(e.Label)}</td>
                      <td>{E(WcagCounts(e))}</td>
                      <td>{E(e.HowToCheck ?? "")}</td>
                    </tr>
                    """);
            }
        }
        html.Append("""
              </tbody></table>
            </section>
            """);
    }

    /// <summary>The counts cell: findings and screens ran, for statuses that have them; "—" otherwise
    /// (Manual and Usually-out-of-scope criteria have no rule, so there is nothing to count).</summary>
    private static string WcagCounts(CoverageReportEntry e) => e.RunStatus switch
    {
        CoverageRunStatus.PartlyAutomated =>
            $"{Plural(e.WcagIssueCount, "issue")}, {e.NeedsReviewCount} to review · ran on {e.ScreensRan} of {e.ScreensTotal} screen(s)",
        CoverageRunStatus.NotTestedInThisRun => $"ran on 0 of {e.ScreensTotal} screen(s)",
        _ => "—",
    };

    private static string FocusSuffix(ScanReport report) =>
        report.FocusStandard is { } id && KnownStandards.Find(id) is { } s ? $" relevant to {E(s.ShortName)} ({E(s.Basis)})" : "";

    /// <summary>Empty for a scan, or a recording with a single, uninterrupted session (the normal case);
    /// otherwise says how many sessions this run was recorded across and when each one ran -- see
    /// <see cref="ScanReport.Sessions"/>.</summary>
    private static string SessionsNote(ScanReport report)
    {
        if (report.Sessions.Count < 2)
            return "";
        var sessions = string.Join("; ", report.Sessions.Select((s, i) =>
            $"session {i + 1}: {s.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm}–{s.EndedAt.ToLocalTime():HH:mm}"));
        return $"""<p class="meta">Recorded across {report.Sessions.Count} sessions ({E(sessions)}).</p>""";
    }

    /// <summary>Per standard: how many findings fall within its WCAG version and level.</summary>
    private static void RenderStandards(StringBuilder html, ScanReport report)
    {
        // Findings with no WCAG criterion mapped aren't judged against any standard's basis: exclude them
        // here entirely rather than counting them as "outside" every standard's basis.
        var findings = report.Screens.SelectMany(s => s.Findings)
            .Where(f => f.Kind != FindingKind.PlatformAdvisory && f.Criteria.Count > 0).ToList();
        html.Append("""
            <section class="standards" aria-labelledby="standards-title">
              <h2 id="standards-title">Relevance to laws and standards</h2>
              <table class="coverage"><thead><tr><th scope="col">Standard</th><th scope="col">Based on</th><th scope="col">WCAG issues</th><th scope="col">Needs review</th><th scope="col">Outside its WCAG basis</th></tr></thead><tbody>
            """);
        foreach (var s in report.Standards)
        {
            var relevant = findings.Where(f => f.RelevantStandards.Contains(s.Id)).ToList();
            var focus = s.Id == report.FocusStandard ? " class=\"focus\"" : "";
            html.Append($"""
                <tr{focus}><td><a href="{E(s.Source)}">{E(s.Name)}</a><br><span class="meta">{E(s.Jurisdiction)}</span></td><td>{E(s.Basis)}</td>
                <td>{relevant.Count(f => f.Kind == FindingKind.WcagIssue)}</td><td>{relevant.Count(f => f.Kind == FindingKind.NeedsReview)}</td>
                <td>{findings.Count - relevant.Count}</td></tr>
                """);
        }
        html.Append($"""
              </tbody></table>
              <details class="sources"><summary>Checked against (ruleset {E(report.RulesetVersion)})</summary>
                <p class="hint">Results reflect these versions. Later changes to WCAG, laws or platform guidelines are not reflected until Swipewalk is updated.</p>
                <ul>{string.Concat(report.RuleSources.Select(r => $"<li><a href=\"{E(r.Source)}\">{E(r.Name)}</a>: {E(r.Version)}, mapping reviewed {E(r.CheckedOn)}</li>"))}</ul>
              </details>
              <p class="hint">{E(report.StandardsNotice)} "Outside its WCAG basis" counts findings under WCAG criteria the standard does not reference or does not apply to apps (for example WCAG 2.2 criteria under a WCAG 2.1 standard). Requirements these standards add beyond WCAG are not checked.</p>
            </section>
            """);
    }

    /// <summary>Which screens were scanned (with links), and expected screens that were not.</summary>
    private static void RenderCoverage(StringBuilder html, ScanReport report)
    {
        if (report.Screens.Count < 2 && report.ExpectedScreens.Count == 0)
            return;

        html.Append($"""<nav class="coverage-nav" aria-labelledby="scanned-title"><h2 id="scanned-title">Screens scanned ({report.Screens.Count})</h2>""");
        if (report.Screens.FirstOrDefault()?.AppLaunchedNote is { } launchedNote)
            html.Append($"""<p class="hint">{E(launchedNote)}</p>""");
        html.Append("<ol>");
        for (var i = 0; i < report.Screens.Count; i++)
        {
            var s = report.Screens[i];
            var issues = s.Findings.Count(f => f.Kind == FindingKind.WcagIssue);
            var review = s.Findings.Count(f => f.Kind == FindingKind.NeedsReview);
            var largeTextNote = s.LargeTextScreenshotPath is not null
                ? ", large text checked"
                : s.LargeTextSkippedReason is { } reason ? $", large text not checked: {E(reason)}" : "";
            // Only Android has an ATF harness to skip; iOS/Windows screens show no note here (see
            // ScreenActivityBuilder, which records the "(Android only)" gap in the WCAG coverage section instead).
            var atfNote = s.Platform == Platform.Android && !s.AtfRan
                ? $", Google's accessibility checks did not run: {E(s.AtfSkippedReason ?? "not recorded")}"
                : "";
            html.Append($"""<li><a href="#s{i}">{E(s.ScreenName)}</a> <span class="meta">{issues} WCAG issue(s), {review} to review{largeTextNote}{atfNote}</span></li>""");
        }
        html.Append("</ol>");
        if (report.MissingScreens.Count > 0)
            html.Append($"""<p class="missing"><strong>Expected but not scanned:</strong> {E(string.Join(", ", report.MissingScreens))}. Test these manually or record them in another session.</p>""");
        html.Append("""<p class="hint">Screens not listed here were not scanned.</p></nav>""");
    }

    private static void RenderLimitations(StringBuilder html, ScanReport report)
    {
        var limitations = report.Limitations;
        if (limitations.Count == 0)
            return;

        html.Append($"""
            <section class="limits" aria-labelledby="limits-title">
              <h2 id="limits-title">Known limitations of this scan <span class="count">{limitations.Count}</span></h2>
              <details open>
                <summary>Show or hide the list</summary>
                <p class="hint">What these automated checks cannot see or may get wrong for the scanned platform and framework, and what to check by hand instead.</p>
            """);
        foreach (var group in limitations.GroupBy(l => l.ScopeLabel))
        {
            html.Append($"""<h3>{E(group.Key)}</h3><ul class="limit-list">""");
            foreach (var l in group)
            {
                var kind = l.Kind == Limitations.LimitationKind.FrameworkNote ? "Framework note" : "Limitation";
                var planned = l.Planned is null ? "" : $"""<p><strong>Planned:</strong> {E(l.Planned)}</p>""";
                html.Append($"""
                    <li id="limit-{E(l.Id)}">
                      <p class="limit-title">{E(l.Title)} <span class="chip">{kind}</span></p>
                      <p>{E(l.Description)}</p>
                      <p><strong>Impact:</strong> {E(l.Impact)}</p>
                      <p><strong>Check manually:</strong> {E(l.ManualCheck)}</p>
                      {planned}
                    </li>
                    """);
            }
            html.Append("</ul>");
        }
        html.Append("</details></section>");
    }

    private sealed record Shot(string Id, int Width, int Height, double Scale);

    /// <summary>The normal screenshot and, in record mode, the one at enlarged text size.</summary>
    private sealed record Shots(Shot? Normal, Shot? LargeText)
    {
        public Shot? For(Finding f) => IsLargeText(f) ? LargeText : Normal;
    }

    private static bool IsLargeText(Finding f) => f.Details.GetValueOrDefault("screenshot") == "largeText";

    private static string LargeTextFigure(ScreenResult screen, Shot large, List<(int Number, Finding Finding)> numbered)
    {
        var s = large.Scale;
        var boxes = string.Concat(numbered.Where(n => IsLargeText(n.Finding) && n.Finding.Bounds.Width > 0).Select(n =>
        {
            var b = n.Finding.Bounds;
            return $"""
                <g class="box review"><rect x="{N(b.X * s)}" y="{N(b.Y * s)}" width="{N(b.Width * s)}" height="{N(b.Height * s)}" rx="6"/>
                <rect class="tag" x="{N(b.X * s)}" y="{N(b.Y * s)}" width="{24 + 20 * n.Number.ToString().Length}" height="44" rx="6"/>
                <text x="{N(b.X * s)}" y="{N(b.Y * s)}" dx="12" dy="32">{n.Number}</text></g>
                """;
        }));
        return $"""
            <figcaption class="large-caption">With {E(screen.LargeTextSetting)}{E(MethodSuffix(screen))}</figcaption>
            <div class="stage large">
              <svg viewBox="0 0 {large.Width} {large.Height}" role="img" aria-label="Screenshot of {E(screen.ScreenName)} with {E(screen.LargeTextSetting)}"><use href="#{large.Id}"/>{boxes}</svg>
            </div>
            """;
    }

    /// <summary>How the large-text capture was produced, e.g. " (via system setting)" or, on a physical
    /// iPhone that applies Dynamic Type only at launch, " (via system setting, applied after a restart)".
    /// When a force-stop + relaunch comparison was captured but text still didn't grow (so
    /// <see cref="ScreenResult.LargeTextAppliedLive"/> is null, not confirmed live or restart-applied),
    /// " (via system setting, captured after restarting the app)" states that fact rather than staying
    /// silent about the restart. Empty when not recorded (older captures, or a platform with only one
    /// method).</summary>
    private static string MethodSuffix(ScreenResult screen) =>
        screen.LargeTextMethod is not { } method ? ""
        : screen.LargeTextAppliedLive == false ? $" (via {method}, applied after a restart)"
        : screen.LargeTextRestartCaptured == true ? $" (via {method}, captured after restarting the app)"
        : $" (via {method})";

    /// <summary>" · .NET MAUI 10.0.60" next to the platform/device when the framework was detected or set;
    /// " · .NET MAUI" when the framework is known but its version wasn't; "" for an unknown framework. Factual
    /// only -- states what was detected, not a verdict on the app.</summary>
    private static string FrameworkLabel(ScreenResult screen) => screen.Framework switch
    {
        AppFramework.Maui when screen.FrameworkVersion is { } version => $" · .NET MAUI {version}",
        AppFramework.Maui => " · .NET MAUI",
        _ => "",
    };

    private static void RenderScreen(StringBuilder html, ScanReport report, ScreenResult screen, int index)
    {
        var id = $"s{index}";
        var numbered = screen.Findings.Select((f, i) => (Number: i + 1, Finding: f)).ToList();
        var shot = EmbedScreenshot(html, screen.ScreenshotPath, screen.PixelScale, $"{id}-shot");
        var largeShot = EmbedScreenshot(html, screen.LargeTextScreenshotPath, screen.LargeTextPixelScale, $"{id}-large");

        html.Append($"""
            <section class="screen" id="{id}" aria-labelledby="{id}-title">
              <h2 id="{id}-title">{E(screen.ScreenName)} <span class="platform">{(screen.Device is { } device ? E(device.ToString()) : screen.Platform.ToString())}{E(FrameworkLabel(screen))}</span></h2>
              <div class="layout">
                <figure class="shot">
                  <div class="modes" role="group" aria-label="Overlay on screenshot">
                    <button type="button" class="mode" aria-pressed="true" data-mode="findings">Issues</button>
                    <button type="button" class="mode" aria-pressed="false" data-mode="order">Swipe order (predicted)</button>
                  </div>
                  <div class="stage" data-mode="findings">
            """);
        html.Append(shot is null ? "<p class=\"empty\">No screenshot captured.</p>" : Overlay(screen, shot, numbered));
        html.Append("</div>");
        if (screen.AppLaunchedNote is { } launchedNote)
            html.Append($"""<p class="hint">{E(launchedNote)}</p>""");
        if (screen.RescannedAt is { } rescannedAt)
            html.Append($"""<p class="hint">This capture was recognized as a screen already in this run (matched by title and most of its elements, not a guaranteed exact match) and was scanned again at {rescannedAt.ToLocalTime():yyyy-MM-dd HH:mm}; the earlier capture was replaced.</p>""");
        if (screen.BaselineTextSizeNote is { } baselineNote)
            html.Append($"""<p class="hint baseline-note">{E(baselineNote)}</p>""");
        if (largeShot is not null)
            html.Append(LargeTextFigure(screen, largeShot, numbered));
        else if (screen.LargeTextSkippedReason is { } skippedReason)
            html.Append($"""<p class="hint">Large-text check not done: {E(skippedReason)}. Check this screen at 200% text size by hand (iOS: Settings > Accessibility > Display &amp; Text Size > Larger Text; Android: Settings > Display > Font size).</p>""");
        html.Append($"""
                </figure>
                <div class="panel">
                  <div class="tabs" role="tablist" aria-label="{E(screen.ScreenName)} details">
                    <button type="button" role="tab" class="tab" id="{id}-tab-findings" aria-controls="{id}-panel-findings" aria-selected="true" data-tab="findings">Findings</button>
                    <button type="button" role="tab" class="tab" id="{id}-tab-transcript" aria-controls="{id}-panel-transcript" aria-selected="false" tabindex="-1" data-tab="transcript">Screen reader (predicted)</button>
            """);
        if (screen.ScreenReaderCapture is { Items.Count: > 0 })
            html.Append($"""<button type="button" role="tab" class="tab" id="{id}-tab-captured" aria-controls="{id}-panel-captured" aria-selected="false" tabindex="-1" data-tab="captured">Screen reader (captured)</button>""");
        html.Append($"""
                  </div>
                  <div class="tabpanel" role="tabpanel" id="{id}-panel-findings" aria-labelledby="{id}-tab-findings" data-tab="findings">
            """);

        if (numbered.Count == 0)
            html.Append("<p class=\"empty\">Automated checks found no issues on this screen. Manual testing is still required.</p>");

        var shots = new Shots(shot, largeShot);
        FindingGroup(html, screen, shots, id, "WCAG issues", "issue",
            numbered.Where(n => n.Finding.Kind == FindingKind.WcagIssue && report.InFocus(n.Finding)).ToList(), byCriterion: true);
        FindingGroup(html, screen, shots, id, "Needs review", "review",
            numbered.Where(n => n.Finding.Kind == FindingKind.NeedsReview && report.InFocus(n.Finding)).ToList(), byCriterion: false);
        if (report.FocusStandard is { } focusId && KnownStandards.Find(focusId) is { } focus)
            FindingGroup(html, screen, shots, id, $"Outside {focus.ShortName}'s WCAG basis ({focus.Basis})", "review",
                numbered.Where(n => !report.InFocus(n.Finding)).ToList(), byCriterion: true);
        FindingGroup(html, screen, shots, id, "Platform advisories (not WCAG)", "advisory",
            numbered.Where(n => n.Finding.Kind == FindingKind.PlatformAdvisory).ToList(), byCriterion: false);

        html.Append($"""
                  </div>
                  <div class="tabpanel" role="tabpanel" id="{id}-panel-transcript" aria-labelledby="{id}-tab-transcript" data-tab="transcript" hidden>
                    <p class="hint">What a screen reader is predicted to say as the user swipes forward, based on the accessibility tree. Not a recording.</p>
                    <ol class="transcript">
            """);
        foreach (var a in screen.PredictedTranscript)
            html.Append($"""<li class="{(a.HasName ? "" : "unnamed")}" data-order="{a.Order}"><span class="say">“{E(a.Text)}”</span></li>""");
        html.Append("""
                    </ol>
                  </div>
            """);
        if (screen.ScreenReaderCapture is { Items.Count: > 0 })
        {
            html.Append($"""
                      <div class="tabpanel" role="tabpanel" id="{id}-panel-captured" aria-labelledby="{id}-tab-captured" data-tab="captured" hidden>
                """);
            AppendCapturedTranscript(html, screen);
            html.Append("""
                      </div>
                """);
        }
        html.Append("""
                </div>
              </div>
            </section>
            """);
    }

    /// <summary>
    /// The "Screen reader (captured)" tab: real evidence (<see cref="ScreenResult.ScreenReaderCapture"/>),
    /// its differences from the predicted transcript (<see cref="ScreenReaderCaptureComparer"/>), and the
    /// captured items themselves. Only called (see <see cref="RenderScreen"/>) when the screen has a
    /// non-empty capture, so the tab simply does not appear for a screen with none, rather than showing a
    /// placeholder on every screen of every report. Reused item <c>data-order</c> values (the matched
    /// predicted stop's order, not the capture's own order) so hovering a captured item highlights the same
    /// swipe-order marker on the screenshot as the predicted transcript does; unmatched items carry no
    /// <c>data-order</c> and simply don't link to anything.
    /// </summary>
    private static void AppendCapturedTranscript(StringBuilder html, ScreenResult screen)
    {
        var capture = screen.ScreenReaderCapture!;

        html.Append($"""<p class="hint">{E(CaptureIntro(capture.Source))}</p>""");
        html.Append($"""<p class="meta">{E(ScreenReaderCaptureComparer.ToolLabel(capture.Source))} {E(capture.ToolVersion)}, captured {capture.CapturedAt.ToLocalTime():yyyy-MM-dd HH:mm}. """);
        html.Append(capture.Complete
            ? "Covered the whole screen."
            : $"""Did not cover the whole screen{(capture.NotCompleteReason is { } reason ? $": {E(reason)}" : "")}.""");
        html.Append("</p>");

        var predicted = screen.PredictedTranscript;
        var stopByPath = predicted.ToDictionary(a => a.NodePath, a => a.Order);
        var diffs = ScreenReaderCaptureComparer.Compare(predicted, capture);
        var diffsByItem = diffs.Where(d => d.Actual is not null).ToLookup(d => d.Actual!);

        if (diffs.Count == 0)
            html.Append("""<p class="hint">No differences from the predicted transcript.</p>""");
        else
        {
            html.Append("""<ol class="captured-diffs">""");
            foreach (var diff in diffs)
                html.Append($"""<li><span class="chip diff-{DiffKindClass(diff.Kind)}">{E(DiffLabel(diff.Kind))}</span> {E(diff.Description)}</li>""");
            html.Append("</ol>");
        }

        html.Append("""<ol class="transcript captured">""");
        foreach (var item in capture.Items)
        {
            var order = item.MatchedNodePath is { } path && stopByPath.TryGetValue(path, out var stopOrder) ? stopOrder : (int?)null;
            var hasDiff = diffsByItem[item].Any();
            var cls = string.Join(" ", new[] { hasDiff ? "diff" : "", item.MatchedNodePath is null ? "unmatched" : "" }.Where(c => c.Length > 0));
            var orderAttr = order is { } o ? $" data-order=\"{o}\"" : "";
            html.Append($"""<li class="{cls}"{orderAttr}><span class="say">{E(DescribeItem(item))}</span></li>""");
        }
        html.Append("</ol>");
    }

    private static string CaptureIntro(ScreenReaderSource source) => source switch
    {
        ScreenReaderSource.TalkBack =>
            "What TalkBack actually said as Swipewalk moved its focus to each focusable element in turn, captured by making Swipewalk's own text-to-speech engine TalkBack's default so it receives the exact spoken text (no audio is recorded or played). The order shown is Swipewalk's own walk order, not TalkBack's swipe order.",
        ScreenReaderSource.AccessibilityInspector =>
            "Captured from Xcode's Accessibility Inspector: the label, value and traits VoiceOver reads, in the Inspector's own navigation order. This is not recorded speech.",
        ScreenReaderSource.VoiceOverCaptions => "VoiceOver captions recorded while navigating this screen.",
        _ => "",
    };

    private static string DescribeItem(ScreenReaderCaptureItem item)
    {
        if (item.SpokenText is { Length: > 0 } spoken)
            return $"“{spoken}”";
        var name = item.Label ?? item.Value ?? "(no label)";
        var traits = item.Traits is { Count: > 0 } t ? $", {string.Join(", ", t)}" : "";
        return $"{name}{traits}";
    }

    private static string DiffLabel(ScreenReaderDifferenceKind kind) => kind switch
    {
        ScreenReaderDifferenceKind.Missing => "Missing",
        ScreenReaderDifferenceKind.Extra => "Extra",
        ScreenReaderDifferenceKind.TextMismatch => "Name",
        ScreenReaderDifferenceKind.OrderMismatch => "Order",
        ScreenReaderDifferenceKind.RoleMismatch => "Role",
        ScreenReaderDifferenceKind.Unmatched => "Unmatched",
        _ => kind.ToString(),
    };

    private static string DiffKindClass(ScreenReaderDifferenceKind kind) => kind.ToString().ToLowerInvariant();

    private static void FindingGroup(
        StringBuilder html, ScreenResult screen, Shots shots, string screenId, string title, string kind,
        List<(int Number, Finding Finding)> items, bool byCriterion)
    {
        if (items.Count == 0)
            return;

        html.Append($"""<div class="group {kind}"><h3>{E(title)} <span class="count">{items.Count}</span></h3>""");
        var groups = byCriterion
            ? items.GroupBy(i => i.Finding.Criteria.FirstOrDefault()?.ToString() ?? "")
                .OrderBy(g => g.Key, StringComparer.Ordinal)
            : items.GroupBy(_ => "");

        foreach (var group in groups)
        {
            if (group.Key.Length > 0)
                html.Append($"""<h4>{E(group.Key)}</h4>""");
            html.Append("<ol class=\"findings\">");
            foreach (var (number, f) in group)
                FindingCard(html, screen, shots.For(f), screenId, kind, number, f);
            html.Append("</ol>");
        }
        html.Append("</div>");
    }

    private static void FindingCard(StringBuilder html, ScreenResult screen, Shot? shot, string screenId, string kind, int number, Finding f)
    {
        var chips = f.Criteria.Count > 0
            ? string.Concat(f.Criteria.Select(c => $"""<span class="chip">{E(c.ToString())}</span>"""))
            : f.Kind == FindingKind.PlatformAdvisory
                ? ""
                : """<span class="chip none">No WCAG criterion mapped</span>""";
        if (f.PlatformGuideline is { } guideline)
            chips += $"""<span class="chip">{E(guideline)}</span>""";
        // A standards-relevance chip only makes sense once there's a WCAG criterion to check standards
        // against; a PlatformAdvisory or an unmapped NeedsReview (the "No WCAG criterion mapped" chip
        // above) has none, so "Not within the listed standards" would misleadingly read as a verdict
        // about something simply not mapped yet.
        if (f.Kind != FindingKind.PlatformAdvisory && f.Criteria.Count > 0)
        {
            var names = KnownStandards.All.Where(st => f.RelevantStandards.Contains(st.Id)).Select(st => st.ShortName).ToList();
            chips += names.Count == 0
                ? """<span class="chip law beyond">Not within the listed standards</span>"""
                : $"""<span class="chip law">Relevant to: {E(string.Join(" · ", names))}</span>""";
            var beyond = KnownStandards.All.Where(st => !f.RelevantStandards.Contains(st.Id)).Select(st => st.ShortName).ToList();
            if (names.Count > 0 && beyond.Count > 0)
                chips += $"""<span class="chip law beyond">Not in WCAG basis of: {E(string.Join(" · ", beyond))}</span>""";
        }
        foreach (var engine in f.Source == Finding.DefaultSource ? f.AlsoReportedBy : [f.Source, .. f.AlsoReportedBy])
            chips += $"""<span class="chip source">{(f.Source == engine ? "Reported by" : "Also reported by")} {E(engine)}</span>""";

        var element = f.Label is null ? E(f.Role) : $"{E(f.Role)} “{E(f.Label)}”";
        html.Append($"""
            <li class="finding" id="{screenId}-f{number}" data-node="{E(f.NodePath)}">
              <span class="num {kind}" aria-label="Finding {number}">{number}</span>
              <div class="body">
                <p class="element">{element}</p>
                <p>{E(f.Message)}</p>
                <p class="chips">{chips}</p>
                {Swatches(f)}
                {FixBlock(f, screen)}
              </div>
              {CloseUp(shot, f, kind)}
            </li>
            """);
    }

    private static string FixBlock(Finding f, ScreenResult screen)
    {
        if ((f.Fix ?? FixGuidance.For(f, screen.Platform, screen.Framework, screen.FrameworkVersion)) is not { } fix)
            return "";
        var code = fix.Code is null
            ? ""
            : $"""<p class="lang">{E(fix.CodeLanguage)}</p><pre><code>{E(fix.Code)}</code></pre>""";
        var causes = fix.LikelyCauses.Count == 0
            ? ""
            : $"""<p class="lang">Likely causes to check ({E(fix.CausesFor)})</p><ul class="causes">{string.Concat(fix.LikelyCauses.Select(c => $"<li>{E(c)}</li>"))}</ul>""";
        return $"""<details class="fix"><summary>How to fix</summary><p>{E(fix.Summary)}</p>{causes}{code}</details>""";
    }

    /// <summary>For contrast findings: the measured colors next to the suggested text color.</summary>
    private static string Swatches(Finding f)
    {
        if (!f.Details.TryGetValue("foreground", out var fg) || !f.Details.TryGetValue("background", out var bg)
            || !f.Details.TryGetValue("suggestedForeground", out var suggested))
            return "";
        var ratio = f.Details.GetValueOrDefault("ratio", "?");
        var suggestedRatio = TryParse(suggested) is { } s && TryParse(bg) is { } b
            ? Contrast.Ratio(s, b).ToString("0.00", CultureInfo.InvariantCulture)
            : "?";
        // The samples show the measured colors on purpose (including failing ones), so they are images of the
        // colors with a text alternative, not text that must itself meet contrast.
        static string Sample(string fg, string bg, string description) => $"""
            <svg class="sample" viewBox="0 0 44 28" width="44" height="28" role="img" aria-label="{E(description)}">
              <rect width="44" height="28" rx="6" fill="{E(bg)}" stroke="currentColor" stroke-opacity=".3"/>
              <text x="22" y="19" text-anchor="middle" font-size="16" font-weight="600" fill="{E(fg)}" aria-hidden="true">Aa</text>
            </svg>
            """;
        return $"""
            <div class="swatches">
              <div class="swatch">{Sample(fg, bg, $"Sample of the current colors, {fg} on {bg}")}<span>Now: {E(fg)} on {E(bg)} · {E(ratio)}:1</span></div>
              <div class="swatch">{Sample(suggested, bg, $"Sample of the suggested colors, {suggested} on {bg}")}<span>Suggested: {E(suggested)} · {suggestedRatio}:1</span></div>
            </div>
            """;
    }

    /// <summary>A cropped view of the screenshot around the element, reusing the screen's embedded image.</summary>
    private static string CloseUp(Shot? shot, Finding f, string kind)
    {
        if (shot is null || f.Bounds.Width <= 0 || f.Bounds.Height <= 0)
            return "";

        var (x, y, w, h) = (f.Bounds.X * shot.Scale, f.Bounds.Y * shot.Scale, f.Bounds.Width * shot.Scale, f.Bounds.Height * shot.Scale);
        var pad = 40 * shot.Scale / 2.5;
        var cw = Math.Min(shot.Width, Math.Max(w + 2 * pad, 360 * shot.Scale / 2.5));
        var ch = Math.Min(shot.Height, Math.Max(h + 2 * pad, w > shot.Width * 0.5 ? 0 : 180 * shot.Scale / 2.5));
        var cx = Math.Clamp(x + w / 2 - cw / 2, 0, shot.Width - cw);
        var cy = Math.Clamp(y + h / 2 - ch / 2, 0, shot.Height - ch);

        var wide = w > shot.Width * 0.5 ? " wide" : "";
        return $"""
            <svg class="closeup{wide}" viewBox="{N(cx)} {N(cy)} {N(cw)} {N(ch)}" role="img" aria-label="Close-up of the {E(f.Role)} on the screenshot">
              <use href="#{shot.Id}"/>
              <rect class="hl {kind}" x="{N(x)}" y="{N(y)}" width="{N(w)}" height="{N(h)}" rx="6"/>
            </svg>
            """;
    }

    /// <summary>Embeds a screenshot once as an SVG image definition that overlays and close-ups reuse.</summary>
    private static Shot? EmbedScreenshot(StringBuilder html, string? path, double scale, string id)
    {
        if (path is null || !File.Exists(path))
            return null;

        var png = File.ReadAllBytes(path);
        var shot = new Shot(id,
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)),
            scale);
        html.Append($"""
            <svg class="defs" aria-hidden="true"><defs><image id="{shot.Id}" width="{shot.Width}" height="{shot.Height}" href="data:image/png;base64,{Convert.ToBase64String(png)}"/></defs></svg>
            """);
        return shot;
    }

    private static string Overlay(ScreenResult screen, Shot shot, List<(int Number, Finding Finding)> numbered)
    {
        var s = shot.Scale;
        var svg = new StringBuilder();
        svg.Append($"""<svg viewBox="0 0 {shot.Width} {shot.Height}" role="img" aria-label="Screenshot of {E(screen.ScreenName)} with numbered findings"><use href="#{shot.Id}"/>""");

        svg.Append("<g class=\"layer findings\">");
        foreach (var node in numbered.Where(n => n.Finding.Bounds.Width > 0 && !IsLargeText(n.Finding)).GroupBy(n => n.Finding.NodePath))
        {
            var b = node.First().Finding.Bounds;
            var kind = node.Min(n => n.Finding.Kind) switch
            {
                FindingKind.WcagIssue => "issue",
                FindingKind.NeedsReview => "review",
                _ => "advisory",
            };
            var label = string.Join(",", node.Select(n => n.Number));
            svg.Append($"""
                <g class="box {kind}" data-node="{E(node.Key)}" data-first="{node.First().Number}">
                  <rect x="{N(b.X * s)}" y="{N(b.Y * s)}" width="{N(b.Width * s)}" height="{N(b.Height * s)}" rx="6"/>
                  <rect class="tag" x="{N(b.X * s)}" y="{N(b.Y * s)}" width="{24 + 20 * label.Length}" height="44" rx="6"/>
                  <text x="{N(b.X * s)}" y="{N(b.Y * s)}" dx="12" dy="32">{label}</text>
                </g>
                """);
        }
        svg.Append("</g><g class=\"layer order\">");
        foreach (var a in screen.PredictedTranscript)
        {
            var b = a.Bounds;
            svg.Append($"""
                <g class="stop{(a.HasName ? "" : " unnamed")}" data-order="{a.Order}">
                  <rect x="{N(b.X * s)}" y="{N(b.Y * s)}" width="{N(b.Width * s)}" height="{N(b.Height * s)}" rx="6"/>
                  <circle cx="{N(b.X * s)}" cy="{N(b.Y * s)}" r="26"/>
                  <text x="{N(b.X * s)}" y="{N(b.Y * s)}" dy="10">{a.Order}</text>
                </g>
                """);
        }
        svg.Append("</g></svg>");
        return svg.ToString();
    }

    private static Rgb? TryParse(string hex) =>
        hex.Length == 7 && hex[0] == '#'
        && int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)
            ? new Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v)
            : null;

    private static string N(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Tile(int value, string label, string kind) =>
        $"""<div class="tile {kind}"><span class="value">{value}</span><span class="label">{E(label)}</span></div>""";

    private static string Plural(int n, string noun) => $"{n} {noun}{(n == 1 ? "" : "s")}";

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");

    private const string Head = """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Swipewalk scan report</title>
        <style>
        :root {
          --bg: #f6f7f9; --surface: #ffffff; --text: #1b1f24; --muted: #57606a; --line: #d8dee4; --code: #f0f2f5;
          --issue: #b3261e; --issue-bg: #fdecea; --review: #7a4d00; --review-bg: #fff4d6;
          --advisory: #1f5fa8; --advisory-bg: #e7f0fb; --focus: #1f5fa8;
          --mark-issue: #c62828; --mark-review: #8a5700; --mark-advisory: #1f5fa8; --mark-order: #6b3fa0;
        }
        @media (prefers-color-scheme: dark) {
          :root:not([data-theme="light"]) {
            --bg: #0f1216; --surface: #171b21; --text: #e6e9ed; --muted: #a3adb8; --line: #2b323b; --code: #0f1216;
            --issue: #ff8a80; --issue-bg: #3a1a1a; --review: #f2c14e; --review-bg: #33290f;
            --advisory: #8ab4f8; --advisory-bg: #14243a; --focus: #8ab4f8;
          }
        }
        :root[data-theme="dark"] {
          --bg: #0f1216; --surface: #171b21; --text: #e6e9ed; --muted: #a3adb8; --line: #2b323b; --code: #0f1216;
          --issue: #ff8a80; --issue-bg: #3a1a1a; --review: #f2c14e; --review-bg: #33290f;
          --advisory: #8ab4f8; --advisory-bg: #14243a; --focus: #8ab4f8;
        }
        * { box-sizing: border-box; }
        body { margin: 0; background: var(--bg); color: var(--text); font: 15px/1.5 system-ui, -apple-system, "Segoe UI", sans-serif; padding: 24px 16px 48px; }
        body > * { max-width: 1240px; margin-left: auto; margin-right: auto; }
        h1 { font-size: 26px; margin: 4px 0; } h2 { font-size: 20px; } h3 { font-size: 16px; margin: 20px 0 8px; } h4 { font-size: 14px; margin: 12px 0 6px; color: var(--muted); }
        p { margin: 0 0 6px; }
        :focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; }
        a { color: var(--advisory); } a:visited { color: var(--advisory); }
        .defs { position: absolute; width: 0; height: 0; overflow: hidden; }
        .top { display: flex; flex-wrap: wrap; gap: 16px; justify-content: space-between; align-items: end; }
        .eyebrow { text-transform: uppercase; letter-spacing: .06em; font-size: 12px; color: var(--muted); }
        .meta, .hint { color: var(--muted); font-size: 13px; }
        .tiles { display: flex; gap: 8px; flex-wrap: wrap; }
        .tile { background: var(--surface); border: 1px solid var(--line); border-left: 4px solid; border-radius: 8px; padding: 8px 14px; min-width: 120px; }
        .tile .value { display: block; font-size: 24px; font-weight: 700; } .tile .label { font-size: 13px; color: var(--muted); }
        .tile.issue { border-left-color: var(--mark-issue); } .tile.review { border-left-color: var(--mark-review); } .tile.advisory { border-left-color: var(--mark-advisory); }
        .notice { background: var(--surface); border: 1px solid var(--line); border-radius: 8px; padding: 12px 14px; margin-top: 16px; font-size: 14px; }
        .screen { background: var(--surface); border: 1px solid var(--line); border-radius: 12px; padding: 16px; margin-top: 24px; }
        .screen h2 { margin: 0 0 12px; } .platform { font-size: 13px; font-weight: 500; color: var(--muted); border: 1px solid var(--line); border-radius: 999px; padding: 2px 8px; vertical-align: middle; }
        .layout { display: grid; grid-template-columns: minmax(0, 340px) minmax(0, 1fr); gap: 24px; align-items: start; }
        @media (max-width: 800px) { .layout { grid-template-columns: minmax(0, 1fr); } .shot { position: static; } }
        .shot { margin: 0; position: sticky; top: 12px; }
        .stage { border: 1px solid var(--line); border-radius: 12px; overflow: hidden; line-height: 0; }
        .stage svg { width: 100%; height: auto; display: block; }
        .large-caption { font-size: 13px; color: var(--muted); margin: 12px 0 6px; }
        .stage.large { width: 60%; }
        .stage[data-mode="findings"] .order, .stage[data-mode="order"] .findings { display: none; }
        .box rect { fill: none; stroke-width: 6; } .box .tag { stroke: none; } .box text { fill: #fff; font: 700 30px system-ui, sans-serif; }
        .box.issue rect { stroke: var(--mark-issue); } .box.issue .tag { fill: var(--mark-issue); }
        .box.review rect { stroke: var(--mark-review); } .box.review .tag { fill: var(--mark-review); }
        .box.advisory rect { stroke: var(--mark-advisory); } .box.advisory .tag { fill: var(--mark-advisory); }
        .box { cursor: pointer; } .box.active rect:first-child { stroke-width: 12; fill: rgba(255, 214, 0, .2); }
        .stop rect { fill: none; stroke: var(--mark-order); stroke-width: 4; stroke-dasharray: 10 6; } .stop circle { fill: var(--mark-order); } .stop text { fill: #fff; font: 700 28px system-ui, sans-serif; text-anchor: middle; }
        .stop.unnamed rect { stroke: var(--mark-issue); } .stop.unnamed circle { fill: var(--mark-issue); }
        .stop.active rect { stroke-width: 10; fill: rgba(107, 63, 160, .2); }
        .modes, .tabs { display: flex; gap: 6px; margin-bottom: 10px; flex-wrap: wrap; }
        .mode, .tab { font: inherit; font-size: 13px; color: var(--text); background: transparent; border: 1px solid var(--line); border-radius: 999px; padding: 4px 12px; cursor: pointer; }
        .mode[aria-pressed="true"], .tab[aria-selected="true"] { background: var(--text); color: var(--surface); border-color: var(--text); }
        .group h3 .count { font-size: 12px; font-weight: 600; border-radius: 999px; padding: 1px 8px; margin-left: 4px; }
        .group.issue h3 .count { background: var(--issue-bg); color: var(--issue); } .group.review h3 .count { background: var(--review-bg); color: var(--review); } .group.advisory h3 .count { background: var(--advisory-bg); color: var(--advisory); }
        .findings { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
        .finding { display: grid; grid-template-columns: 28px minmax(0, 1fr) minmax(0, 240px); gap: 12px; border: 1px solid var(--line); border-radius: 10px; padding: 12px; }
        @media (max-width: 1000px) { .finding { grid-template-columns: 28px minmax(0, 1fr); } .closeup { grid-column: 2; } }
        .finding.active { border-color: var(--focus); box-shadow: 0 0 0 1px var(--focus); }
        .num { width: 28px; height: 28px; border-radius: 6px; display: grid; place-items: center; font-weight: 700; font-size: 13px; color: #fff; }
        .num.issue { background: var(--mark-issue); } .num.review { background: var(--mark-review); } .num.advisory { background: var(--mark-advisory); }
        .element { font-weight: 600; } .chips { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 6px; }
        .chip { font-size: 12px; border-radius: 999px; padding: 2px 8px; background: var(--bg); border: 1px solid var(--line); }
        .chip.source { font-style: italic; }
        .closeup.wide { grid-column: 2 / -1; max-width: 640px; }
        .closeup { width: 100%; height: auto; border: 1px solid var(--line); border-radius: 8px; background: var(--bg); }
        .closeup .hl { fill: none; stroke-width: 6; } .closeup .hl.issue { stroke: var(--mark-issue); } .closeup .hl.review { stroke: var(--mark-review); } .closeup .hl.advisory { stroke: var(--mark-advisory); }
        .swatches { display: flex; flex-wrap: wrap; gap: 12px; margin-top: 8px; font-size: 13px; }
        .swatch { display: flex; align-items: center; gap: 8px; }
        .sample { flex: none; }
        .fix { margin-top: 8px; } .fix summary { cursor: pointer; font-weight: 600; font-size: 14px; }
        .fix p { margin-top: 6px; } .lang { font-size: 12px; color: var(--muted); margin: 8px 0 2px; }
        .standards { background: var(--surface); border: 1px solid var(--line); border-radius: 12px; padding: 12px 16px; margin-top: 16px; }
        .standards h2 { font-size: 18px; margin: 4px 0 8px; } .standards .coverage { margin-bottom: 8px; }
        .standards tr.focus td { background: var(--advisory-bg); font-weight: 600; }
        .notice.stale { border-color: var(--mark-review); background: var(--review-bg); }
        .sources { font-size: 14px; margin: 8px 0; } .sources summary { cursor: pointer; font-weight: 600; } .sources ul { margin: 4px 0; padding-left: 20px; }
        .chip.law { border-style: dashed; } .chip.beyond { color: var(--muted); }
        .chip.none { font-style: italic; color: var(--muted); }
        .causes { margin: 2px 0 8px; padding-left: 20px; font-size: 14px; }
        pre { background: var(--code); border: 1px solid var(--line); border-radius: 6px; padding: 8px 10px; overflow-x: auto; font-size: 13px; margin: 0; }
        .transcript { margin: 0; padding-left: 28px; display: grid; gap: 4px; }
        .transcript li { padding: 4px 8px; border-radius: 6px; } .transcript li.active { background: var(--bg); }
        .transcript li.unnamed .say { color: var(--issue); font-weight: 600; }
        .transcript.captured li.diff .say { color: var(--review); font-weight: 600; }
        .transcript.captured li.unmatched .say { color: var(--muted); font-style: italic; }
        .captured-diffs { list-style: none; margin: 8px 0; padding: 0; display: grid; gap: 6px; font-size: 14px; }
        .captured-diffs li { border: 1px solid var(--line); border-radius: 8px; padding: 6px 10px; }
        .empty { color: var(--muted); }
        .coverage-nav { background: var(--surface); border: 1px solid var(--line); border-radius: 12px; padding: 12px 16px; margin-top: 24px; }
        .coverage-nav h2 { font-size: 18px; margin: 4px 0 8px; } .coverage-nav ol { margin: 0 0 8px; padding-left: 24px; }
        .coverage-nav a { color: var(--advisory); } .missing { color: var(--issue); }
        .limits { background: var(--surface); border: 1px solid var(--line); border-radius: 12px; padding: 12px 16px; margin-top: 24px; }
        .limits h2 { font-size: 18px; margin: 4px 0 8px; } .limits summary { cursor: pointer; font-size: 14px; margin-bottom: 8px; }
        .limits .count { font-size: 12px; font-weight: 600; border-radius: 999px; padding: 1px 8px; background: var(--bg); border: 1px solid var(--line); vertical-align: middle; }
        .limit-list { list-style: none; padding: 0; margin: 0; display: grid; gap: 10px; }
        .limit-list li { border: 1px solid var(--line); border-radius: 8px; padding: 10px 12px; font-size: 14px; }
        .limit-title { font-weight: 600; }
        .coverage { border-collapse: collapse; width: 100%; font-size: 13px; margin-bottom: 24px; }
        .coverage th, .coverage td { text-align: left; border-bottom: 1px solid var(--line); padding: 6px 8px; vertical-align: top; }
        .coverage caption { text-align: left; font-weight: 600; margin-bottom: 6px; color: var(--muted); font-size: 13px; }
        footer { margin-top: 32px; } footer h2 { font-size: 18px; } footer li { margin-bottom: 4px; }
        .wcag-summary { margin-top: 2px; }
        .wcag-gaps { background: var(--surface); border: 1px solid var(--line); border-left: 4px solid var(--review); border-radius: 12px; padding: 12px 16px; margin-top: 16px; }
        .wcag-gaps h2 { font-size: 18px; margin: 4px 0 8px; }
        .wcag-gaps h2 .count { font-size: 13px; font-weight: 600; border-radius: 999px; padding: 1px 8px; margin-left: 4px; background: var(--review-bg); color: var(--review); }
        .wcag-gaps ul { margin: 8px 0 0; padding-left: 20px; font-size: 14px; }
        .wcag-gaps li { margin-bottom: 4px; }
        .wcag-coverage { background: var(--surface); border: 1px solid var(--line); border-radius: 12px; padding: 12px 16px; margin-top: 16px; }
        .wcag-coverage h2 { font-size: 18px; margin: 4px 0 8px; }
        .wcag-coverage-table tr.wcag-group th { background: var(--bg); font-size: 12px; text-transform: uppercase; letter-spacing: .04em; color: var(--muted); }
        .wcag-coverage-table tr.wcag-group th .count { font-weight: 700; text-transform: none; letter-spacing: normal; color: var(--text); margin-left: 4px; }
        .wcag-coverage-table tr.wcag-not-tested td:first-child { border-left: 4px solid var(--review); padding-left: 6px; }
        </style>
        </head>
        <body>
        """;

    private const string Script = """
        <script>
        document.querySelectorAll('.screen').forEach(screen => {
          const stage = screen.querySelector('.stage');
          const modes = [...screen.querySelectorAll('.mode')];
          const setMode = mode => {
            modes.forEach(b => b.setAttribute('aria-pressed', String(b.dataset.mode === mode)));
            if (stage) stage.dataset.mode = mode;
          };
          modes.forEach(btn => btn.addEventListener('click', () => setMode(btn.dataset.mode)));

          const tabs = [...screen.querySelectorAll('[role="tab"]')];
          const select = tab => {
            tabs.forEach(t => {
              const on = t === tab;
              t.setAttribute('aria-selected', String(on));
              t.tabIndex = on ? 0 : -1;
              document.getElementById(t.getAttribute('aria-controls')).hidden = !on;
            });
            setMode(tab.dataset.tab === 'transcript' || tab.dataset.tab === 'captured' ? 'order' : 'findings');
          };
          tabs.forEach((tab, i) => {
            tab.addEventListener('click', () => select(tab));
            tab.addEventListener('keydown', e => {
              const next = e.key === 'ArrowRight' ? 1 : e.key === 'ArrowLeft' ? -1 : 0;
              if (!next) return;
              const target = tabs[(i + next + tabs.length) % tabs.length];
              select(target);
              target.focus();
            });
          });

          const link = (items, selector, key) => items.forEach(item => {
            const target = () => screen.querySelector(`${selector}[data-${key}="${item.dataset[key]}"]`);
            item.addEventListener('mouseenter', () => { item.classList.add('active'); target()?.classList.add('active'); });
            item.addEventListener('mouseleave', () => { item.classList.remove('active'); target()?.classList.remove('active'); });
          });
          link(screen.querySelectorAll('.finding'), '.box', 'node');
          link(screen.querySelectorAll('.transcript li'), '.stop', 'order');
          screen.querySelectorAll('.box').forEach(box => box.addEventListener('click', () => {
            document.getElementById(`${screen.id}-f${box.dataset.first}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
          }));
        });
        </script>
        """;
}
