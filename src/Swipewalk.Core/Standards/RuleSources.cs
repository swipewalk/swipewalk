using System.Globalization;

namespace Swipewalk.Core.Standards;

/// <summary>A rule source Swipewalk checks against, with the version used and when the mapping was reviewed.</summary>
public sealed record RuleSource(string Name, string Version, string CheckedOn, string Source)
{
    /// <summary>True when the review date is more than <paramref name="maxAge"/> before <paramref name="at"/>.</summary>
    public bool IsStale(DateTimeOffset at, TimeSpan maxAge) =>
        DateTime.TryParseExact(CheckedOn, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var checkedOn)
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
    // Bumped from 2026.09.15 (already used by another PR merged first, for a Compose clickable-node
    // name-merging change) for the screen-reader-capture rule going live (Android TalkBack capture,
    // opt-in with --screen-reader).
    public const string RulesetVersion = "2026.09.16";

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
    ];

    public static IReadOnlyList<RuleSource> Stale(DateTimeOffset at) => [.. All.Where(s => s.IsStale(at, MaxAge))];
}
