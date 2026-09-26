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
        new EngineIssueRule(),
        new AtfIssueRule(),
        new PageTitledRule(),
        new InputPurposeRule(),
        new IconContrastRule(),
        // Registered now that a collector (AndroidCollector, via the Android TalkBack harness -- see
        // Swipewalk.Collectors.Android.AndroidHarness.RunScreenReaderCaptureAsync) can actually set
        // ScreenSnapshot.ScreenReaderCapture, opt-in via --screen-reader.
        new ScreenReaderCaptureRule(),
    ];

    public static IReadOnlyList<RuleCoverage> Coverage { get; } =
    [
        new("missing-name", "Interactive elements without an accessible name; images without a text alternative",
            [WcagCriteria.NonTextContent, WcagCriteria.NameRoleValue]),
        new("target-size", "Touch targets below 24×24 (with the spacing exception) and below platform guidelines",
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
        new("screen-reader-capture", "Differences between the predicted screen-reader transcript and what TalkBack actually said, captured by making Swipewalk's own text-to-speech engine TalkBack's default so it receives the exact spoken text (Android only for now, opt-in with --screen-reader; see KnownLimitations \"android-screen-reader-capture\")",
            // No Meaningful Sequence/Focus Order here: TalkBack's capture order is Swipewalk's own walk
            // order, not a real navigation order (see ScreenReaderCaptureRule's remarks), so an order
            // difference is never reported for it today.
            [WcagCriteria.NameRoleValue, WcagCriteria.NonTextContent]),
    ];

    /// <summary>The WCAG criteria at least one rule maps findings to, in catalog order.</summary>
    public static IReadOnlyList<WcagCriterion> MappedCriteria { get; } =
        WcagCriteria.All.Where(c => Coverage.Any(r => r.Criteria.Contains(c))).ToList();
}
