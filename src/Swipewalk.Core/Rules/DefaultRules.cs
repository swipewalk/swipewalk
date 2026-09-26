using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>What a rule checks, and which WCAG criteria it partly covers. Shown in reports.</summary>
public sealed record RuleCoverage(string RuleId, string Checks, IReadOnlyList<WcagCriterion> Criteria);

public static class DefaultRules
{
    public static IReadOnlyList<IRule> All { get; } =
    [
        new MissingNameRule(),
        new TargetSizeRule(),
        new IdentifierNameRule(),
        new LabelInNameRule(),
        new TextContrastRule(),
        new TextResizeRule(),
        new TextResizeLiveUpdateRule(),
        new TextResizeNavigationRule(),
        new LargeTextLostContentRule(),
        new OffscreenUnreachableRule(),
        new OrientationRestrictedRule(),
        new EngineIssueRule(),
        new AtfIssueRule(),
        new PageTitledRule(),
        new InputPurposeRule(),
        new IconContrastRule(),
        // Registered now that a collector (AndroidCollector, via the Android TalkBack harness -- see
        // Swipewalk.Collectors.Android.AndroidHarness.RunScreenReaderCaptureAsync) can actually set
        // ScreenSnapshot.ScreenReaderCapture, opt-in via --screen-reader.
        new ScreenReaderCaptureRule(),
        new ScreenReaderLabelInNameRule(),
    ];

    public static IReadOnlyList<RuleCoverage> Coverage { get; } =
    [
        new("missing-name", "Interactive elements without an accessible name; images without a text alternative",
            [WcagCriteria.NonTextContent, WcagCriteria.NameRoleValue]),
        new("target-size", "Touch targets below 24×24 (with the spacing exception, and a needs-review flag when it looks inline in text) and below platform guidelines",
            [WcagCriteria.TargetSizeMinimum]),
        new("identifier-name", "Accessible names that look like developer identifiers (for review)",
            [WcagCriteria.NonTextContent, WcagCriteria.HeadingsAndLabels]),
        new("label-in-name", "Visible text missing from the accessible name", [WcagCriteria.LabelInName]),
        new("text-contrast", "Text contrast measured from screenshot pixels", [WcagCriteria.ContrastMinimum]),
        new("text-resize", "Partial 1.4.4 check (record mode and scan --large-text): text that does not grow at the OS text-size setting, and text that newly overlaps (1.4.4 at Android 200%; Apple Dynamic Type advisory at iOS AX3)",
            [WcagCriteria.ResizeText]),
        new("text-resize-live", "Whether the OS text-size setting took effect while the app kept running, or only after a restart (platform advisory only, no WCAG criterion: 1.4.4 does not require live updates)",
            []),
        new("text-resize-navigation", "Whether a system text-size change made the app show a different screen (typically its first), so the person loses their place (Android only; platform advisory only, no WCAG criterion: 1.4.4 does not require an app to keep its navigation state across the change)",
            []),
        new("large-text-lost-content", "Controls or text present at normal text size that are gone from the accessibility tree at the larger size, with no scrollable container that could reveal them (1.4.4 at Android 200%; Apple Dynamic Type advisory at iOS AX3)",
            [WcagCriteria.ResizeText]),
        new("offscreen-unreachable", "Interactive controls or text present in the accessibility tree at normal text size but positioned wholly or mostly outside the visible screen, with no scrollable ancestor that could bring them into view (iOS only; see KnownLimitations \"offscreen-unreachable-android-gap\")",
            [WcagCriteria.Reflow]),
        new("orientation-restricted", "Whether the screen's content visibly follows the device being rotated between portrait and landscape (scan --orientation both; for review, since a single orientation can be essential to a screen)",
            [WcagCriteria.Orientation]),
        new("engine", "Issues reported by the platform's own accessibility engine (Apple audit on iOS)",
            [WcagCriteria.ContrastMinimum, WcagCriteria.ResizeText, WcagCriteria.NameRoleValue]),
        new("atf", "Issues reported by Google's Accessibility Test Framework (Android, via the harness in harness/android; see KnownLimitations \"android-atf-harness\")",
            [WcagCriteria.NonTextContent, WcagCriteria.NameRoleValue, WcagCriteria.ContrastMinimum, WcagCriteria.NonTextContrast,
                WcagCriteria.LinkPurposeInContext, WcagCriteria.FocusOrder, WcagCriteria.MeaningfulSequence, WcagCriteria.ResizeText]),
        new("page-titled", "Whether the screen exposes a pane title to screen readers (Android only, needs the instrumentation harness; see KnownLimitations \"android-atf-harness\" and \"android-page-title\")",
            [WcagCriteria.PageTitled]),
        new("input-purpose", "Text fields whose label, hint or identifier suggests they collect one of WCAG 1.3.5's input purposes (for review; see KnownLimitations \"input-purpose-heuristic\")",
            [WcagCriteria.IdentifyInputPurpose]),
        new("icon-contrast", "Icon-only interactive controls' contrast against their background, measured from screenshot pixels (iOS only; Android is covered by the atf rule's ImageContrastCheck)",
            [WcagCriteria.NonTextContrast]),
        new("screen-reader-capture", "Differences between the predicted screen-reader transcript and real evidence, opt-in with --screen-reader: on Android, what TalkBack actually said, captured by making Swipewalk's own text-to-speech engine TalkBack's default so it receives the exact spoken text (see KnownLimitations \"android-screen-reader-capture\"); on iOS (scan only for now), what Xcode's Accessibility Inspector reports while walking the screen over the macOS Accessibility API -- VoiceOver itself is never turned on (see KnownLimitations \"ios-inspector-walk-capture\")",
            // No Meaningful Sequence/Focus Order here for either source: TalkBack's capture order is
            // Swipewalk's own walk order, not a real navigation order (see ScreenReaderCaptureRule's
            // remarks); the Accessibility Inspector route's order differences are suppressed too -- confirmed
            // on a real device that its walk starts wherever the person clicked, not the top of the screen
            // (see KnownLimitations "ios-inspector-walk-capture").
            [WcagCriteria.NameRoleValue, WcagCriteria.NonTextContent]),
        new("screen-reader-label-in-name", "WCAG 2.5.3 Label in Name checked against what TalkBack actually said, for an interactive control with visible text (its own, or its only descendant's -- see KnownLimitations \"android-compose-merged-name\"): reported for review when the spoken name doesn't contain that text (Android only for now, opt-in with --screen-reader; see KnownLimitations \"android-screen-reader-capture\")",
            [WcagCriteria.LabelInName]),
    ];

    /// <summary>The WCAG criteria at least one rule maps findings to, in catalog order.</summary>
    public static IReadOnlyList<WcagCriterion> MappedCriteria { get; } =
        WcagCriteria.All.Where(c => Coverage.Any(r => r.Criteria.Contains(c))).ToList();
}
