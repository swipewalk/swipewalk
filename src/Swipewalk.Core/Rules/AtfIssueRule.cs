using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// Maps issues from Google's Accessibility Test Framework (ATF), run by the Android instrumentation
/// harness (harness/android), to findings -- the Android equivalent of <see cref="EngineIssueRule"/> for
/// Apple's audit on iOS. Most ATF issues are reported as needing review, the same reasoning as
/// <see cref="EngineIssueRule"/>: ATF's own thresholds and heuristics are not documented against WCAG.
/// One check (<see cref="Map"/>'s <c>TouchTargetSizeCheck</c> case) is a platform advisory instead,
/// because it fails below the WCAG threshold at a stricter Android-only one.
/// Duplicates of Swipewalk's own findings are merged by <see cref="RuleRunner"/>.
/// </summary>
public sealed class AtfIssueRule : IRule
{
    public string Id => "atf";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        foreach (var issue in snapshot.AtfIssues)
        {
            var (kind, criteria, guideline, explanation) = Map(issue.CheckName);
            yield return new Finding
            {
                RuleId = $"atf:{issue.CheckName}",
                Source = EngineName,
                Kind = kind,
                // ATF's own messages already end with a period, unlike Apple's audit descriptions that
                // EngineIssueRule builds the same "{desc}. {explanation}" join for -- avoid a doubled "..".
                Message = $"{EngineName}: {issue.Description.TrimEnd('.')}. {explanation}".Trim(),
                Criteria = criteria,
                PlatformGuideline = guideline,
                NodePath = issue.NodePath ?? "",
                Role = "element",
                Label = issue.Label,
                Bounds = issue.Bounds,
            };
        }
    }

    public const string EngineName = "Google Accessibility Test Framework";

    /// <summary>One ATF check this rule knows about, and what docs/checks.md shows for it (see
    /// <see cref="Catalog"/>). <see cref="What"/> is a plain-words description of what the check looks
    /// for, kept separate from <see cref="Map"/>'s per-finding "check this by hand" explanation text.</summary>
    public sealed record IssueMapping(string CheckName, string What, FindingKind Kind, IReadOnlyList<WcagCriterion> Criteria, string? PlatformGuideline);

    /// <summary>All 14 of ATF 4.1.1's checks (see harness/android/README.md and KnownLimitations
    /// "android-atf-harness"), in the order docs/checks.md lists them. A check name not in this list
    /// (a future ATF version) still gets a finding -- see <see cref="Map"/>'s default case -- just not
    /// its own row on that page.</summary>
    public static IReadOnlyList<IssueMapping> Catalog { get; } =
    [
        Describe("SpeakableTextPresentCheck", "Whether an element a screen reader would focus on (for example a button, image or icon) has text for the screen reader to speak."),
        Describe("EditableContentDescCheck", "Whether an editable field has a fixed content description that could hide what was typed from a screen reader."),
        Describe("ClassNameCheck", "Whether an element's exposed accessibility class matches a role a screen reader would recognize."),
        Describe("TextContrastCheck", "Text color contrast against its background, estimated from the screenshot."),
        Describe("ImageContrastCheck", "Icon or image contrast against its background, estimated from the screenshot."),
        Describe("LinkPurposeUnclearCheck", "Whether a link's text is a generic phrase (for example \"click here\" or \"learn more\") that doesn't say where it leads on its own."),
        Describe("TraversalOrderCheck", "Whether custom traversal-order settings (accessibilityTraversalBefore/After) form a loop or conflict."),
        Describe("TextSizeCheck", "Whether text sized in dp/px, or placed in a fixed-size container, may not grow with the system font size. Reported a finding on a physical Pixel 4a (Android 13) against a px-sized TextView (samples/NativeAndroid), but not on an Android 16 emulator scanning the exact same screen -- see docs/limitations.md."),
        Describe("UnexposedTextCheck", "Whether text drawn on screen is exposed to the accessibility API at all. Currently never reports anything: it needs text recognition the harness doesn't supply yet (see docs/limitations.md)."),
        Describe("TouchTargetSizeCheck", "Touch targets below Android's own 48×48 dp guideline (stricter than WCAG's 24×24)."),
        Describe("ClickableSpanCheck", "Whether a clickable span inside text is reachable by a screen reader; depends on the API level."),
        Describe("DuplicateClickableBoundsCheck", "Whether two or more clickable elements share the same screen bounds."),
        Describe("DuplicateSpeakableTextCheck", "Whether two or more elements have identical accessible names."),
        Describe("RedundantDescriptionCheck", "Whether an accessible name redundantly states the element's role (for example the word \"Button\" inside a button's name)."),
    ];

    private static IssueMapping Describe(string checkName, string what)
    {
        var (kind, criteria, guideline, _) = Map(checkName);
        return new IssueMapping(checkName, what, kind, criteria, guideline);
    }

    /// <summary>
    /// Maps each of ATF 4.1.1's 14 checks (see harness/android/README.md) to WCAG 2.2 criteria, only where
    /// the mapping is solid; checks whose purpose doesn't correspond to one specific criterion are left
    /// unmapped ("No WCAG criterion mapped" -- <see cref="FindingKind.NeedsReview"/> with no criteria still
    /// tells the reader to look, without inventing a citation). Reviewed against ATF's own check
    /// descriptions and result-id text before every commit that changes this table.
    /// </summary>
    internal static (FindingKind Kind, IReadOnlyList<WcagCriterion> Criteria, string? Guideline, string Explanation) Map(string checkName) => checkName switch
    {
        // Also flags an ImageView important for accessibility with no description, including a
        // decorative one -- same as Swipewalk's own missing-name rule, which is why this usually merges
        // with it (see RuleRunner.MergeEngineDuplicates) rather than showing as a separate finding.
        "SpeakableTextPresentCheck" => (FindingKind.NeedsReview, [WcagCriteria.NonTextContent, WcagCriteria.NameRoleValue],
            null, "Check that the element has an accessible name that describes its purpose, or, if it is purely decorative, is hidden from screen readers."),
        "EditableContentDescCheck" => (FindingKind.NeedsReview, [WcagCriteria.NameRoleValue],
            null, "A fixed description on an editable field can stop a screen reader from announcing what was typed; check with TalkBack."),
        "ClassNameCheck" => (FindingKind.NeedsReview, [WcagCriteria.NameRoleValue],
            null, "Check that the element's exposed role matches what it does."),
        "TextContrastCheck" => (FindingKind.NeedsReview, [WcagCriteria.ContrastMinimum],
            null, "Check the text against the 4.5:1 (normal) or 3:1 (large) minimum."),
        // ATF estimates these colors from the screenshot, the same approximation as Swipewalk's own
        // text-contrast rule. 1.4.11 only covers graphics needed to understand the content or operate a
        // control; decorative images and photos are exempt.
        "ImageContrastCheck" => (FindingKind.NeedsReview, [WcagCriteria.NonTextContrast],
            null, "Colors estimated from the screenshot. If the icon or image is needed to understand the content or operate a control, check it reaches 3:1 against its background; photos and decorative images are exempt."),
        "LinkPurposeUnclearCheck" => (FindingKind.NeedsReview, [WcagCriteria.LinkPurposeInContext],
            null, "Check that the link's text (or the surrounding text) describes where it leads."),
        // ATF only flags accessibilityTraversalBefore/After settings that form a loop or conflict; it
        // does not compare the reading order against the visual layout. Those settings drive the
        // screen-reader (TalkBack) traversal sequence specifically, which is why 1.3.2 fits; 2.4.3 is
        // included too since TalkBack swiping is a form of sequential navigation.
        "TraversalOrderCheck" => (FindingKind.NeedsReview, [WcagCriteria.MeaningfulSequence, WcagCriteria.FocusOrder],
            null, "Its accessibilityTraversalBefore/After settings form a loop or conflict, which can make screen-reader order unpredictable; check with TalkBack that swiping reaches every element in a logical order."),
        // Text sized in dp/px (not sp), or in a container with a fixed size, may not grow -- or may be
        // clipped -- when the system font size increases. WCAG sets no minimum text size, but this is
        // squarely part of 1.4.4 (whether text can reach 200%); NeedsReview because the app's own
        // text-size control (if any) can still satisfy 1.4.4 independently of this check.
        "TextSizeCheck" => (FindingKind.NeedsReview, [WcagCriteria.ResizeText],
            null, "Text sized in dp or px, or placed in a fixed-size container, may not grow or may be cut off when the system font size is increased; confirm with the large-text rescan (1.4.4 at 200%), unless the app offers its own text-size control."),
        // Text drawn on screen (e.g. a custom Canvas) but not exposed to the accessibility API at all is
        // non-text content with no accessible alternative -- 1.1.1, not 1.3.1 (which is about structure
        // and relationships, not whether content is exposed at all). Needs the harness to supply text
        // recognition (OCR) ATF can use, which it does not yet -- see KnownLimitations "android-atf-harness".
        "UnexposedTextCheck" => (FindingKind.NeedsReview, [WcagCriteria.NonTextContent],
            null, "Text appears on screen but isn't exposed to accessibility services; check with TalkBack whether it is announced."),
        // Android's own guidance (Material Design), not a WCAG minimum: WCAG 2.5.8 asks for 24x24 CSS
        // px, below ATF's 48dp default threshold here. Swipewalk's own target-size rule already reports
        // a WCAG issue for anything under 24x24 (see TargetSizeRule), so this only adds the platform's
        // stricter guidance for a target between the two thresholds. Guideline text matches
        // TargetSizeRule's, so a merged or adjacent finding reads the same.
        "TouchTargetSizeCheck" => (FindingKind.PlatformAdvisory, [],
            "Android accessibility guidelines: touch targets at least 48×48 dp", ""),
        // No specific WCAG 2.2 success criterion covers these; left unmapped rather than guessed.
        // ClickableSpanCheck: whether it's a problem depends on the API level; DuplicateClickableBoundsCheck:
        // no specific criterion; DuplicateSpeakableTextCheck: identical labels aren't a failure by
        // themselves; RedundantDescriptionCheck: a role word in the name is a usability issue, not a
        // WCAG failure.
        "ClickableSpanCheck" or "DuplicateClickableBoundsCheck" or "DuplicateSpeakableTextCheck" or "RedundantDescriptionCheck"
            => (FindingKind.NeedsReview, [], null, "No WCAG criterion is mapped for this check; review it by hand."),
        _ => (FindingKind.NeedsReview, [],
            null, "Swipewalk has not mapped this ATF check to a WCAG criterion; review it by hand."),
    };
}
