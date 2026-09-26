using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Coverage;

/// <summary>
/// Every WCAG 2.2 Level A and AA success criterion (55 of them: 31 A + 24 AA), drawn from the single
/// catalog in <see cref="WcagCriteria"/> (<c>src/Swipewalk.Core/Wcag/WcagCriteria.cs</c>). Verified against
/// the WCAG 2.2 Recommendation (2024-12-12) and the WCAG2ICT Group Note (2025-12-11) on 2026-09-22.
///
/// 4.1.1 Parsing is not listed: it was removed in WCAG 2.2 (obsolete). See <see cref="ObsoleteNote"/>.
///
/// No entry is fully "Automated": see <see cref="CoverageBaseStatus.PartlyAutomated"/>'s doc comment.
/// </summary>
public static class CoverageCatalog
{
    public const string ObsoleteNote =
        "4.1.1 Parsing was removed in WCAG 2.2 (obsolete): it is not one of the 55 Level A/AA criteria and is not listed here.";

    // The WCAG2ICT Group Note itself is in RuleSources.All; per-criterion entries below cite a specific
    // "Applying SC ... to non-web software" anchor instead (passed in directly at each call site).

    private const string SetOfSoftwareProgramsReasoning =
        "WCAG2ICT applies this criterion to non-web software only across a \"set of software programs\": " +
        "separate programs from the same author, distributed together and interlinked so users can move " +
        "between them. It notes such sets \"appear to be extremely rare\", so a single app is usually " +
        "outside its scope. Confirm your app is not part of such a set; consistency within the app is still " +
        "good practice.";

    private const string KeyboardSetup =
        "Connect a hardware keyboard. On iOS, also turn on Settings > Accessibility > Keyboards > Full Keyboard Access. ";

    private static string Understanding(string slug) => $"https://www.w3.org/WAI/WCAG22/Understanding/{slug}.html";

    /// <summary>Rule ids from <see cref="DefaultRules.Coverage"/> already mapped to <paramref name="criterion"/>.
    /// Decides which checks "ran" for this criterion; not used to count findings.</summary>
    private static IReadOnlyList<string> RulesFor(WcagCriterion criterion) =>
        [.. DefaultRules.Coverage.Where(r => r.Criteria.Contains(criterion)).Select(r => r.RuleId)];

    private static CriterionCoverage PartlyAutomated(
        WcagCriterion criterion, string howToCheck, string slug, params string[] extraSources) =>
        new(criterion, CoverageBaseStatus.PartlyAutomated, RulesFor(criterion), howToCheck, [Understanding(slug), .. extraSources]);

    private static CriterionCoverage Manual(
        WcagCriterion criterion, string howToCheck, string slug, params string[] extraSources) =>
        new(criterion, CoverageBaseStatus.Manual, [], howToCheck, [Understanding(slug), .. extraSources]);

    /// <summary>For Manual entries WCAG2ICT reinterprets with its own page instead of (or in addition to)
    /// the standard Understanding page.</summary>
    private static CriterionCoverage ManualWithSource(WcagCriterion criterion, string howToCheck, string sourceUrl) =>
        new(criterion, CoverageBaseStatus.Manual, [], howToCheck, [sourceUrl]);

    /// <summary>Like <see cref="PartlyAutomated"/>, for a criterion WCAG2ICT reinterprets with its own page
    /// instead of (or in addition to) the standard Understanding page.</summary>
    private static CriterionCoverage PartlyAutomatedWithSource(WcagCriterion criterion, string howToCheck, string sourceUrl) =>
        new(criterion, CoverageBaseStatus.PartlyAutomated, RulesFor(criterion), howToCheck, [sourceUrl]);

    private static CriterionCoverage NotApplicable(WcagCriterion criterion, string wcag2IctAnchorUrl) =>
        new(criterion, CoverageBaseStatus.UsuallyNotApplicable, [], SetOfSoftwareProgramsReasoning, [wcag2IctAnchorUrl]);

    public static IReadOnlyList<CriterionCoverage> All { get; } =
    [
        // 1 Perceivable
        PartlyAutomated(WcagCriteria.NonTextContent,
            "With TalkBack/VoiceOver, swipe to every image and icon, not only flagged ones. Check each " +
            "informative one has a text alternative that conveys its purpose (the scan only detects missing " +
            "names, not poor ones), and decorative ones are hidden from the screen reader.",
            "non-text-content"),
        Manual(WcagCriteria.AudioOnlyAndVideoOnlyPrerecorded,
            "Play any audio-only or video-only content and check a text transcript (audio) or an audio " +
            "description/text alternative (video) is provided nearby.",
            "audio-only-and-video-only-prerecorded"),
        Manual(WcagCriteria.CaptionsPrerecorded,
            "Play prerecorded video with sound and check synchronized captions are available and can be turned on.",
            "captions-prerecorded"),
        Manual(WcagCriteria.AudioDescriptionOrMediaAlternativePrerecorded,
            "Play prerecorded video and check an audio description track, or a full text alternative " +
            "describing the visuals, is available.",
            "audio-description-or-media-alternative-prerecorded"),
        Manual(WcagCriteria.CaptionsLive,
            "During any live audio or video stream in the app, check live captions are available.",
            "captions-live"),
        Manual(WcagCriteria.AudioDescriptionPrerecorded,
            "Play prerecorded video and check an audio description track is available, not only a text alternative.",
            "audio-description-prerecorded"),
        Manual(WcagCriteria.InfoAndRelationships,
            "With TalkBack/VoiceOver, swipe through the screen and check headings, groups, lists and form " +
            "fields are announced with their structure, not just as plain text.",
            "info-and-relationships"),
        PartlyAutomated(WcagCriteria.MeaningfulSequence,
            "Swipe through the screen with TalkBack/VoiceOver and check the reading order keeps the meaning " +
            "(labels before their values, steps and sentences in order). On Android, when the instrumentation " +
            "harness ran (see docs/limitations.md \"android-atf-harness\"), Google's ATF flags elements whose " +
            "screen-reader traversal settings (accessibilityTraversalBefore/After) form a loop or conflict " +
            "(TraversalOrderCheck); it does not compare the reading order with the visual layout.",
            "meaningful-sequence"),
        Manual(WcagCriteria.SensoryCharacteristics,
            "Check instructions never rely only on shape, color, size or position (\"tap the round green " +
            "button\") without also naming the control.",
            "sensory-characteristics"),
        PartlyAutomated(WcagCriteria.Orientation,
            "Rotate the device between portrait and landscape with rotation lock off and check the app " +
            "supports both, unless a specific orientation is essential. `scan --orientation both` checks part " +
            "of this automatically: it rotates the device itself and flags a screen for review when its " +
            "content did not visibly follow (see docs/limitations.md) -- it can tell whether the screen's " +
            "shape changed, not whether an essential orientation applies or whether the screen still works " +
            "correctly in the other one, so still check every flagged screen, and any screen the automated " +
            "rescan did not cover (record mode), by hand -- and on a physical iPhone, check any flagged " +
            "screen with rotation lock off too, since Swipewalk can't tell rotation lock apart from a real " +
            "restriction (see docs/limitations.md).",
            "orientation"),
        PartlyAutomated(WcagCriteria.IdentifyInputPurpose,
            "Check common input fields (name, email, phone, address) use the platform's autofill/input-type " +
            "hints (e.g. Android autofillHints, iOS textContentType) so autofill and personalization tools " +
            "can identify them. The scan only flags candidate fields by their visible label or hint text -- " +
            "neither platform's accessibility tree exposes whether the app actually declared the hint (see " +
            "docs/limitations.md \"input-purpose-heuristic\") -- so every field must still be checked by hand.",
            "identify-input-purpose"),
        Manual(WcagCriteria.UseOfColor,
            "Check color is never the only way information, a state or an action is conveyed (e.g. an error " +
            "shown only by a red border).",
            "use-of-color"),
        Manual(WcagCriteria.AudioControl,
            "If any screen plays audio automatically for more than 3 seconds, check there is a visible " +
            "control to pause, stop or mute it, or a control to set its volume separately from the system volume.",
            "audio-control"),
        PartlyAutomated(WcagCriteria.ContrastMinimum,
            "Check text and labels flagged as needing review (3:1-4.5:1 measured contrast) against their " +
            "true rendered text size and weight: the automated check measures screenshot pixels, not font " +
            "size from the tree. Also check text the scan could not measure: text over images or gradients, " +
            "other states (pressed, selected, error), dark mode, and screens that were not scanned.",
            "contrast-minimum"),
        PartlyAutomated(WcagCriteria.ResizeText,
            "Set the system text size to about 200% (Android font size at 200%; on iOS, Larger Text at AX3, " +
            "about 235%; AX2 is about 194% for body text), relaunching the app if needed. Check that text " +
            "flagged as not growing, and all other text, grows without losing content or functionality. Text " +
            "that is already large (headings) need not reach 200% (WCAG2ICT). Clipping seen only above 200% " +
            "is a platform advisory, not a 1.4.4 issue. On Android, when the instrumentation harness ran " +
            "(see docs/limitations.md \"android-atf-harness\"), Google's ATF also flags text sized in dp/px " +
            "(rather than sp) or in a fixed-size container, which may not grow with the system text size " +
            "(TextSizeCheck) -- unverified whether this check can obtain the text-size unit through " +
            "UiAutomation on every device; it may simply not run rather than find nothing.",
            "resize-text",
            "https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-4-resize-text-to-non-web-software"),
        Manual(WcagCriteria.ImagesOfText,
            "Check text is rendered as real text rather than as an image of text, except logos or where the " +
            "exact presentation is essential.",
            "images-of-text"),
        PartlyAutomated(WcagCriteria.Reflow,
            "Phone screens in portrait are about 320-430 dp/pt wide, close to the 320 CSS px 1.4.10 uses " +
            "(WCAG2ICT treats dp/pt as CSS px). In portrait, with the largest Android Display size or iOS " +
            "Display Zoom, and on tablets in the narrowest split-screen/Slide Over width, check no content " +
            "needs scrolling in two directions. In landscape, check horizontally scrolling content at a " +
            "height near 256 dp/pt. Maps, data tables, video, games and toolbars that must stay visible may " +
            "use 2D layout. The scan flags controls or text positioned wholly or mostly outside the visible " +
            "screen at normal text size with no scrollable ancestor for review (offscreen-unreachable, iOS " +
            "only for now) -- check whether they can be reached another way.",
            "reflow"),
        PartlyAutomated(WcagCriteria.NonTextContrast,
            "Measure the contrast of UI component boundaries/icons and focus indicators against their " +
            "background and check it reaches at least 3:1. On Android, when the instrumentation harness ran " +
            "(see docs/limitations.md \"android-atf-harness\"), Google's ATF flags some low-contrast images " +
            "and icons (ImageContrastCheck, its own pixel-based method). On iOS, Swipewalk's own " +
            "icon-contrast rule separately measures icon-only interactive controls from screenshot pixels. " +
            "Focus indicators and non-icon component borders are still not checked on either platform.",
            "non-text-contrast"),
        Manual(WcagCriteria.TextSpacing,
            "Android and iOS don't offer a system-wide line, paragraph, letter or word spacing setting, and " +
            "WCAG2ICT does not require apps to add one. This applies to native screens only if the app " +
            "offers a spacing option. Check that option, and any in-app web view content with the 1.4.12 " +
            "values applied (line height 1.5, paragraph spacing 2, letter spacing 0.12, word spacing 0.16 x " +
            "font size), for clipped or overlapping text.",
            "text-spacing"),
        Manual(WcagCriteria.ContentOnHoverOrFocus,
            "Trigger any tooltip or popover shown on hover, keyboard focus, or touch focus (e.g. with a " +
            "stylus or attached pointer) and check it can be dismissed without moving focus (e.g. with Esc), " +
            "stays visible while pointed at or focused, and its content is itself hoverable. Platform-drawn " +
            "tooltips the app hasn't customized (e.g. the OS's own long-press tooltip) are exempt.",
            "content-on-hover-or-focus"),

        // 2 Operable
        Manual(WcagCriteria.Keyboard,
            KeyboardSetup + "Check every function available by touch can be reached and operated with Tab, " +
            "arrow keys, Space/Enter, except functions that depend on the path of movement (e.g. freehand drawing).",
            "keyboard"),
        Manual(WcagCriteria.NoKeyboardTrap,
            KeyboardSetup + "Tab through the screen and check focus can always move away from every control " +
            "using only the keyboard.",
            "no-keyboard-trap"),
        Manual(WcagCriteria.CharacterKeyShortcuts,
            "If the app defines single-character keyboard shortcuts, check they can be turned off, " +
            "remapped, or only apply while the relevant control has focus.",
            "character-key-shortcuts"),
        Manual(WcagCriteria.TimingAdjustable,
            "For any time limit (session timeout, auto-advancing content), check the user can turn it off, " +
            "adjust it, or extend it.",
            "timing-adjustable"),
        PartlyAutomated(WcagCriteria.PauseStopHide,
            "Swipewalk can optionally take a few captures of a screen a few seconds apart with no input " +
            "(scan --auto-update-content) and flag content that keeps changing on its own across more than " +
            "one interval, for review -- it can tell content changed, not whether anything else is shown " +
            "alongside it, whether a pause/stop/hide control exists somewhere on the screen, or whether the " +
            "update is essential to an activity (the W3C Understanding document's own examples of essential " +
            "content, such as an explanatory animation or a stock ticker, still ship with their own " +
            "pause/restart buttons), so check all of that by hand. It can also miss a cycle whose length " +
            "happens to match the interval, or a screen not checked this way. For anything it doesn't catch: " +
            "check every moving, blinking or scrolling element that starts automatically, lasts more than 5 " +
            "seconds and is shown alongside other content, and every auto-updating element that starts " +
            "automatically and is shown alongside other content, for a way to pause, stop or hide it (or, for " +
            "auto-updating content, control its frequency) -- content that is the only thing on the screen, " +
            "such as a preloader with nothing else on the page, is exempt either way.",
            "pause-stop-hide"),
        Manual(WcagCriteria.ThreeFlashesOrBelowThreshold,
            "Check nothing flashes more than three times in any one-second period, unless the flashes are " +
            "below the general flash and red flash thresholds.",
            "three-flashes-or-below-threshold"),
        NotApplicable(WcagCriteria.BypassBlocks,
            "https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-1-bypass-blocks-to-non-web-documents-and-software"),
        PartlyAutomatedWithSource(WcagCriteria.PageTitled,
            "Check each screen has a title describing its topic or purpose (navigation bar or toolbar " +
            "title). With TalkBack, check it is announced on screen change (Android window/pane title). " +
            "With VoiceOver, check the title is reachable as a header near the top. WCAG2ICT applies this as " +
            "\"Non-web Software Titled\". On Android, when the instrumentation harness ran (see " +
            "docs/limitations.md \"android-atf-harness\"), Swipewalk flags a screen with no pane title " +
            "anywhere in its tree for review -- a missing pane title isn't by itself a failure, since the " +
            "activity/window title, which this check doesn't read, can still convey the screen's purpose " +
            "(see \"android-page-title\"). A visible toolbar title or heading doesn't clear this finding: " +
            "MAUI's Page.Title does not set an Android pane title. Not automated on iOS.",
            "https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-2-page-titled-to-non-web-software"),
        PartlyAutomated(WcagCriteria.FocusOrder,
            "Swipe through the screen with TalkBack/VoiceOver and check the reading/focus order follows a " +
            "logical sequence that preserves meaning. On Android, when the instrumentation harness ran (see " +
            "docs/limitations.md \"android-atf-harness\"), Google's ATF flags elements whose screen-reader " +
            "traversal settings (accessibilityTraversalBefore/After) form a loop or conflict " +
            "(TraversalOrderCheck); it does not compare the reading order with the visual layout.",
            "focus-order"),
        PartlyAutomated(WcagCriteria.LinkPurposeInContext,
            "With TalkBack/VoiceOver, check each link's (or button's, when it navigates) accessible name " +
            "makes its destination or action clear from the name and its immediate context. On Android, when " +
            "the instrumentation harness ran (see docs/limitations.md \"android-atf-harness\"), Google's ATF " +
            "flags some unclear link text (LinkPurposeUnclearCheck).",
            "link-purpose-in-context"),
        NotApplicable(WcagCriteria.MultipleWays,
            "https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-5-multiple-ways-to-non-web-documents-and-software"),
        PartlyAutomated(WcagCriteria.HeadingsAndLabels,
            "Check that every visible heading and label describes its topic or purpose. The scan only flags " +
            "labels that look like developer identifiers.",
            "headings-and-labels"),
        Manual(WcagCriteria.FocusVisible,
            KeyboardSetup + "Tab through controls and check a visible focus indicator appears on the " +
            "focused control.",
            "focus-visible"),
        Manual(WcagCriteria.FocusNotObscuredMinimum,
            KeyboardSetup + "Move keyboard focus (Tab) to each control and check it is not " +
            "entirely hidden behind a sticky header, footer, on-screen keyboard or banner.",
            "focus-not-obscured-minimum"),
        Manual(WcagCriteria.PointerGestures,
            "Check every multipoint or path-based gesture (pinch, swipe path) has a single-tap or " +
            "single-point alternative, unless the gesture is essential.",
            "pointer-gestures"),
        Manual(WcagCriteria.PointerCancellation,
            "Check that pressing and holding a control, then dragging off it before release, cancels the " +
            "action instead of triggering it on press-down.",
            "pointer-cancellation"),
        PartlyAutomated(WcagCriteria.LabelInName,
            "For each control flagged, check the spoken accessible name actually contains the visible text, " +
            "in the order TalkBack/VoiceOver reads it. Check other controls with visible text too. With " +
            "Voice Access (Android) or Voice Control (iOS), say the visible label and check the control " +
            "activates. Best practice is for the name to start with the visible text. On Android, when a " +
            "real, complete screen-reader capture ran (--screen-reader; see docs/limitations.md " +
            "\"android-screen-reader-capture\") and matched a control with more than weak confidence, " +
            "Swipewalk also checks this against what TalkBack actually said -- the TalkBack comparison " +
            "runs only when the capture reached every focusable or interactive element it found and " +
            "TalkBack said something for each. It could report a control whose visible text is only on a " +
            "child node the tree-only check still can't merge onto the control itself (for example more " +
            "than one content-description child alongside a single visible-text child -- see " +
            "\"android-compose-merged-name\"), when TalkBack's announcement leaves that text out. " +
            "samples/NativeAndroid's Compose bug N5 used to be exactly this kind of control; the " +
            "tree-only check now evaluates it directly (it merges the one shape confirmed against real " +
            "TalkBack evidence) and, consistent with that capture, reports nothing there -- either way, " +
            "none of this is proof of what speech-input software matches, so check by hand regardless.",
            "label-in-name"),
        Manual(WcagCriteria.MotionActuation,
            "If a feature is triggered by shaking or tilting the device, check there is also a standard " +
            "on-screen control, and that motion input can be turned off.",
            "motion-actuation"),
        Manual(WcagCriteria.DraggingMovements,
            "For any drag interaction (reorder, slider, swipe-to-delete), check there is also a single-tap " +
            "alternative (e.g. a menu action) that achieves the same result.",
            "dragging-movements"),
        PartlyAutomated(WcagCriteria.TargetSizeMinimum,
            "With the screen reader off, tap near the edges of targets flagged for review (small targets " +
            "inside or next to other targets) and check no unintended control activates. For issues the scan " +
            "reports, check whether an exception the scan does not test applies: an equivalent larger " +
            "control on the same screen, an inline link in text, an unmodified platform control, or an " +
            "essential presentation. Custom-drawn targets not in the accessibility tree are not measured. " +
            "Targets at least 24x24 dp/pt but under 44 pt (Apple) or 48 dp (Android) are platform " +
            "advisories, not WCAG issues.",
            "target-size-minimum"),

        // 3 Understandable
        Manual(WcagCriteria.LanguageOfPage,
            "Set the device language to one the app supports and check TalkBack/VoiceOver reads the " +
            "interface in that language's voice. If the app has its own language setting that differs from " +
            "the device, check the screen reader still uses the right language.",
            "language-of-page"),
        Manual(WcagCriteria.LanguageOfParts,
            "If a screen shows text in a different language than the rest of the app, check that passage's " +
            "language is marked (Android LocaleSpan / iOS accessibilityLanguage or attributed-string " +
            "language) so the screen reader switches its pronunciation for it. EN 301 549 voids this " +
            "requirement for software (it applies to web content only); WCAG2ICT applies it directly to " +
            "non-web software, so it's listed here as WCAG-relevant.",
            "language-of-parts"),
        Manual(WcagCriteria.OnFocus,
            KeyboardSetup + "Move keyboard focus onto each control (Tab) and check it does not trigger a " +
            "screen change or other unexpected action just from receiving focus; also check the same when " +
            "moving TalkBack/VoiceOver focus.",
            "on-focus"),
        Manual(WcagCriteria.OnInput,
            "Change the value of each field or toggle and check it does not automatically navigate away or " +
            "submit the screen without the user activating a control, unless the user was told beforehand " +
            "that changing it would do so.",
            "on-input"),
        NotApplicable(WcagCriteria.ConsistentNavigation,
            "https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-2-3-consistent-navigation-to-non-web-documents-and-software"),
        Manual(WcagCriteria.ConsistentIdentification,
            "Check that controls with the same function (e.g. Back, Search, Save, Help) have the same " +
            "label, icon and accessible name on every screen where they appear. WCAG2ICT applies 3.2.4 only " +
            "across a set of software programs, but EN 301 549 v4.1.1 (11.3.2.4) applies it within a single app.",
            "consistent-identification"),
        NotApplicable(WcagCriteria.ConsistentHelp,
            "https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-2-6-consistent-help-to-non-web-documents-and-software"),
        Manual(WcagCriteria.ErrorIdentification,
            "Submit a form with an invalid or missing field and check the error is described in text (not " +
            "only color) and announced by TalkBack/VoiceOver.",
            "error-identification"),
        Manual(WcagCriteria.LabelsOrInstructions,
            "Check every input field has a visible label or instructions describing what to enter, before " +
            "the user submits.",
            "labels-or-instructions"),
        Manual(WcagCriteria.ErrorSuggestion,
            "Trigger a validation error and check the message suggests how to fix it, when a suggestion is " +
            "known and doesn't compromise security.",
            "error-suggestion"),
        Manual(WcagCriteria.ErrorPreventionLegalFinancialData,
            "For screens that make a legal commitment, a financial transaction, or delete user data, check " +
            "the submission can be reversed, checked, or confirmed before it takes effect.",
            "error-prevention-legal-financial-data"),
        Manual(WcagCriteria.RedundantEntry,
            "In a multi-step flow, check information the user already entered is auto-populated or " +
            "selectable again, rather than requiring retyping, unless re-entry is essential or required for " +
            "security (e.g. re-entering a password to confirm a change).",
            "redundant-entry"),
        Manual(WcagCriteria.AccessibleAuthenticationMinimum,
            "Check sign-in does not rely only on a cognitive function test (e.g. transcribing a remembered " +
            "password) without an alternative such as a passkey, biometrics, a password manager/autofill " +
            "(iOS textContentType password, Android autofill), or paste support.",
            "accessible-authentication-minimum"),

        // 4 Robust
        PartlyAutomated(WcagCriteria.NameRoleValue,
            "With TalkBack/VoiceOver, focus each custom control, flagged or not, and check its role, name, " +
            "value and state (e.g. checked, expanded) are announced correctly, including after it changes. " +
            "The scan checks accessible names; it only partly checks roles (iOS traits); it does not check " +
            "values or states automatically.",
            "name-role-value"),
        Manual(WcagCriteria.StatusMessages,
            "Trigger a status change that doesn't move focus (e.g. \"saved\", a validation summary, a " +
            "loading spinner finishing) and check TalkBack/VoiceOver announces it without the user needing " +
            "to navigate to it.",
            "status-messages"),
    ];
}
