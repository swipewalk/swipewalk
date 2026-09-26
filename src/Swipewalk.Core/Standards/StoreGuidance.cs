using Swipewalk.Core.Model;

namespace Swipewalk.Core.Standards;

/// <summary>
/// App store accessibility guidance a finding can be "also relevant to" -- never a store's own review
/// mandate: neither Apple's App Store Review Guidelines nor Google Play's Developer Program Policy were found
/// to have a general accessibility-quality clause (both push accessibility through softer mechanisms:
/// Apple's Human Interface Guidelines and self-declared Accessibility Nutrition Labels, Google's pre-launch
/// report). Worded "also relevant to" / "related to", never "will pass review" or a claim that Swipewalk
/// observed the store itself flag anything -- see <see cref="KnownStandards.Disclaimer"/> for the same rule
/// applied to legal standards.
/// </summary>
/// <param name="Store">"Apple" or "Google Play".</param>
/// <param name="Platforms">Which platform(s) this note applies to -- Apple's guidance is iOS-only, Google
/// Play's is Android-only, so a finding on the other platform never gets it.</param>
/// <param name="Name">The guidance document's name, e.g. "Human Interface Guidelines: Accessibility".</param>
/// <param name="Wording">The exact sentence to show next to a matching finding. Carries its own caveat in
/// visible text (never relies only on <see cref="Detail"/>, which some renderers may not show).</param>
/// <param name="Detail">Further plain-language context: what the guidance says, and how it's declared or
/// checked. Rendered as visible text alongside <see cref="Wording"/>, not only in a hover-only attribute.</param>
/// <param name="RelatedCriteria">WCAG criterion numbers this note relates to (Swipewalk's own mapping to them,
/// not a claim the source itself names these numbers).</param>
/// <param name="Source">Primary source URL.</param>
/// <param name="CheckedOn">When this was last checked ("yyyy-MM-dd").</param>
public sealed record StoreGuidanceNote(
    string Store,
    IReadOnlyList<Platform> Platforms,
    string Name,
    string Wording,
    string Detail,
    IReadOnlyList<string> RelatedCriteria,
    string Source,
    string CheckedOn);

public static class StoreGuidance
{
    private const string CheckedOn = "2026-09-25";

    /// <summary>Source name for a finding Google's Accessibility Test Framework produced -- see
    /// <c>Rules.AtfIssueRule.EngineName</c>, duplicated here (as elsewhere in this codebase, e.g.
    /// <c>Coverage.CriterionCoverage</c>) rather than adding a Reports/Standards-&gt;Rules reference.</summary>
    public const string AtfEngineName = "Google Accessibility Test Framework";

    public static IReadOnlyList<StoreGuidanceNote> All { get; } =
    [
        new(
            "Apple",
            [Platform.iOS],
            "Human Interface Guidelines: Accessibility",
            "Also relevant to Apple's Human Interface Guidelines (Accessibility) -- design guidance, not an App Store Review requirement.",
            "Apple's own HIG names the WCAG AA contrast thresholds directly (4.5:1 up to 17pt; 3:1 at 18pt or " +
            "for bold text of any size -- WCAG's own large-text 3:1 threshold is 18pt, or 14pt bold, so HIG's " +
            "\"bold at any size\" allowance is slightly looser) and recommends letting people enlarge text by at " +
            "least 200 percent (matching WCAG 1.4.4). Apple's own default iOS/iPadOS touch target is 44x44pt, " +
            "with a stated minimum of 28x28pt -- both numerically larger than WCAG 2.5.8's 24x24 CSS px " +
            "minimum, though the units differ, so don't treat this as a strict comparison.",
            ["1.4.3", "1.4.4", "1.4.1", "2.5.8", "4.1.2"],
            "https://developer.apple.com/design/human-interface-guidelines/accessibility",
            CheckedOn),
        new(
            "Apple",
            [Platform.iOS],
            "Accessibility Nutrition Labels",
            "Also relevant to an Accessibility Nutrition Label an app's App Store page might claim -- self-declared by the developer, not verified by Apple.",
            "Nine labelable features (VoiceOver, Voice Control, Larger Text, Dark Interface, Differentiate " +
            "Without Color Alone, Sufficient Contrast, Reduced Motion, Captions, Audio Descriptions) are, in " +
            "Swipewalk's own mapping, related to these WCAG criteria -- Apple's page does not itself cite WCAG. " +
            "Currently voluntary; Apple has stated intent to make it mandatory later, with no date confirmed, " +
            "and says it may contact a developer if a label looks intentionally misleading or harmful.",
            ["1.4.3", "1.4.4", "1.4.1", "2.3.3", "1.2.2", "1.2.4", "1.2.1", "1.2.3", "1.2.5"],
            "https://developer.apple.com/help/app-store-connect/manage-app-accessibility/overview-of-accessibility-nutrition-labels",
            CheckedOn),
        new(
            "Google Play",
            [Platform.Android],
            "Pre-launch report (Accessibility Test Framework)",
            "Related to checks in Google Play's pre-launch report, which uses Google's Accessibility Test Framework (the check set may differ).",
            "Google's own developer documentation states directly: \"Google Play runs accessibility tests using " +
            "the Accessibility Test Framework, with results appearing in a table on the Accessibility tab of " +
            "your app's pre-launch report.\" The report's own categories (content labeling, touch target size, " +
            "implementation, low contrast) were confirmed from Play Console's own help page. This is background " +
            "on what the pre-launch report checks, not a claim that Google Play actually flagged this specific " +
            "finding. Even when the finding did come from Swipewalk's own Accessibility Test Framework harness " +
            "(shown separately as \"Reported by\"/\"Also reported by\"), Google Play's own ATF version, check " +
            "set and thresholds are unconfirmed, so that still doesn't predict Google Play will flag it.",
            ["4.1.2", "2.5.8", "1.4.3", "1.1.1"],
            "https://developer.android.com/guide/topics/ui/accessibility/testing",
            CheckedOn),
    ];

    /// <summary>Notes relevant to a finding's WCAG criteria and platform (empty for a platform advisory, an
    /// unmapped finding, or a platform the note doesn't apply to -- Apple's guidance only ever appears for iOS,
    /// Google Play's only for Android).</summary>
    public static IReadOnlyList<StoreGuidanceNote> For(Finding finding, Platform platform) =>
        finding.Kind == FindingKind.PlatformAdvisory || finding.Criteria.Count == 0
            ? []
            : [.. All.Where(n => n.Platforms.Contains(platform) && finding.Criteria.Any(c => n.RelatedCriteria.Contains(c.Number)))];

    /// <summary>True when this finding actually came from Google's ATF harness (as its own source, or merged
    /// with a Swipewalk rule that reported the same issue) -- the only time it's fair to say a finding was
    /// "also reported by" the engine behind Google Play's pre-launch report.</summary>
    public static bool CameFromAtf(Finding finding) =>
        finding.Source == AtfEngineName || finding.AlsoReportedBy.Contains(AtfEngineName);
}
