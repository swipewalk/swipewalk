using System.Globalization;

namespace Swipewalk.Core.Standards;

/// <summary>A rule source Swipewalk checks against, with the version used and when the mapping was reviewed.</summary>
public sealed record RuleSource(string Name, string Version, string CheckedOn, string Source)
{
    /// <summary>Accepted <see cref="CheckedOn"/> formats: the original month-only "yyyy-MM", and the exact
    /// "yyyy-MM-dd" used for entries whose primary source was read on a specific day.</summary>
    private static readonly string[] CheckedOnFormats = ["yyyy-MM", "yyyy-MM-dd"];

    /// <summary>True when the review date is more than <paramref name="maxAge"/> before <paramref name="at"/>.</summary>
    public bool IsStale(DateTimeOffset at, TimeSpan maxAge) =>
        DateTime.TryParseExact(CheckedOn, CheckedOnFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var checkedOn)
        && at.UtcDateTime - checkedOn.ToUniversalTime() > maxAge;
}

/// <summary>
/// Everything findings are checked or mapped against: WCAG, platform guidelines and the standards in
/// <see cref="KnownStandards"/>. Reports list these so results record exactly which rule versions were used;
/// later changes are not reflected until Swipewalk is updated. Bump <see cref="RulesetVersion"/> whenever
/// rules, mappings or sources change.
/// </summary>
public static class RuleSources
{
    // Bumped from 2026.09.16 to 2026.09.21 for the offscreen-unreachable rule (interactive controls or
    // text positioned wholly or mostly outside the visible screen at normal text size, with no scrollable
    // ancestor -- mapped to WCAG 1.4.10 Reflow) and the accompanying change of 1.4.10's CoverageCatalog
    // status from Manual to PartlyAutomated (merged first), then to 2026.09.22 for this rule
    // (screen-reader-label-in-name: WCAG 2.5.3 checked against real screen-reader capture, opt-in with
    // --screen-reader), rebased on top of it, then to 2026.09.23 for the new orientation-restricted rule
    // (scan --orientation both; WCAG 1.3.4), then to 2026.09.24 for the beyond-WCAG clause catalog and
    // its per-run "Beyond WCAG" coverage section, rebased on top of both changes, then to 2026.09.25 for
    // the screen-reader-capture rule's iOS route (scan only for now): Xcode's Accessibility Inspector,
    // walked over the macOS Accessibility API, instead of Android's TalkBack -- see KnownLimitations
    // "ios-inspector-walk-capture", then to 2026.09.26 for the US states, countries and store-guidance
    // mapping (KnownJurisdictions, StoreGuidance): confirmed jurisdiction standards are opt-in via
    // --standard (never listed on every finding by default), unconfirmed/no-law/differs-from-WCAG
    // jurisdictions are recorded honestly rather than guessed, and CheckedOn now also accepts an exact
    // "yyyy-MM-dd" for entries checked on a specific day, then to 2026.09.27 for real screen-reader evidence
    // reflected in per-screen WCAG coverage (4.1.2, 1.1.1, 2.5.3 get a "captured evidence" sentence naming
    // the tool and real counts; 1.3.1 gets an informational Header-trait list from the Accessibility
    // Inspector route, never a finding or a status change) and the screen-reader-label-in-name coverage-gate
    // fix (it was wrongly reported as "ran" on an iOS Accessibility Inspector capture, which the underlying
    // rule never evaluates -- TalkBack only), then to 2026.09.28 for the real bug that made
    // screen-reader-label-in-name (WCAG 2.5.3) unable to ever fire on a genuine TalkBack capture:
    // TalkBackCollector.kt passed complete = false on every single return path, always, because its walk
    // only ever covers focusable/interactive elements -- a fixed SCOPE of that route, now its own field
    // (ScreenReaderCaptureScope), not something Complete should have kept meaning "the whole screen" for.
    // Complete now genuinely means "every focusable/interactive element this walk found was reached and
    // said something" for TalkBack, so screen-reader-label-in-name (and screen-reader-capture's per-screen
    // coverage) can finally run on real data; ScreenReaderCaptureComparer's Missing-diff check is never
    // reported for FocusableElementsOnly captures at all, complete or not, since the harness can't yet say
    // which exact elements it walked.
    // Then to 2026.09.29 for target-size's WCAG 2.5.8 inline-exception check: the tree cannot tell a target that
    // is genuinely "in a sentence" (the criterion's own wording,
    // w3.org/WAI/WCAG22/Understanding/target-size-minimum.html) from one that merely sits beside an
    // unrelated text label, so no exception is granted automatically -- a small target shaped like a
    // plain-text link next to other text is instead reported as needs-review (not a WCAG issue, not
    // silently exempted) when the spacing exception doesn't already explain it. Platform advisories
    // (Apple 44 pt, Android 48 dp) are unchanged.
    public const string RulesetVersion = "2026.09.29";

    /// <summary>Reports warn when a source was last reviewed longer ago than this.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(365);

    public static IReadOnlyList<RuleSource> All { get; } =
    [
        new("WCAG", "2.2 (W3C Recommendation)", "2026-09", "https://www.w3.org/TR/WCAG22/"),
        new("WCAG2ICT: Guidance on Applying WCAG 2 to Non-Web Information and Communications Technologies",
            "W3C Group Note, 11 December 2025 (WCAG 2.2)", "2026-09", "https://www.w3.org/TR/wcag2ict-22/"),
        new("Apple Human Interface Guidelines: accessibility (44×44 pt hit targets)", "current web edition", "2026-09",
            "https://developer.apple.com/design/human-interface-guidelines/accessibility"),
        new("Android accessibility guidance (48×48 dp touch targets)", "current web edition", "2026-09",
            "https://support.google.com/accessibility/android/answer/7101858"),
        new("Apple Human Interface Guidelines: typography (support Dynamic Type, including accessibility sizes)", "current web edition", "2026-09",
            "https://developer.apple.com/design/human-interface-guidelines/typography"),
        new("Android developer documentation: activity element configChanges (including fontScale)", "current web edition", "2026-09",
            "https://developer.android.com/guide/topics/manifest/activity-element#config"),
        new("Android developer documentation: handle configuration changes", "current web edition", "2026-09",
            "https://developer.android.com/guide/topics/resources/runtime-changes"),
        // Re-checked each ruleset update, and dropped once its fix (Microsoft.Maui.Controls 10.0.100) can be
        // assumed for every scanned app: fixes the iOS live text-resize issue the text-resize-live rule cites.
        new("dotnet/maui PR #34445: iOS fix for font autoscaling not updating in realtime",
            "merged 2026-06-23; released in Microsoft.Maui.Controls 10.0.100 (.NET 10 SR10)", "2026-09",
            "https://github.com/dotnet/maui/pull/34445"),
        .. KnownStandards.All.Select(s => new RuleSource(s.Name, s.Basis, s.CheckedOn, s.Source)),
        // US states, other countries and store guidance are opt-in (see KnownJurisdictions/StoreGuidance) and
        // deliberately NOT added here: every report always shows this list ("Checked against"), and this
        // project's own rule is that a report never lists every US state by default. Their own staleness is
        // still checked directly -- see KnownJurisdictionsTests.
    ];

    public static IReadOnlyList<RuleSource> Stale(DateTimeOffset at) => [.. All.Where(s => s.IsStale(at, MaxAge))];
}
