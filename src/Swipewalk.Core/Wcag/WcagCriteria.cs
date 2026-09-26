namespace Swipewalk.Core.Wcag;

/// <summary>
/// Every WCAG 2.2 Level A and AA success criterion (55 of them: 31 A + 24 AA). Rules reference these
/// fields instead of constructing criteria, so a finding can't cite an invented one. Numbers, names and
/// levels verified against the WCAG 2.2 W3C Recommendation (2024-12-12), https://www.w3.org/TR/WCAG22/, on
/// 2026-09-22. Add entries only after checking the name, level and introducing version against the spec.
///
/// A rule currently cites only 15 of these (see <see cref="Rules.DefaultRules.MappedCriteria"/>); the other
/// 40 are here so <c>Coverage.CoverageCatalog</c> (every A/AA criterion, with what Swipewalk automates for
/// it) has one catalog to draw from instead of two. 4.1.1 Parsing is not listed: it was removed in WCAG 2.2
/// (obsolete).
/// </summary>
public static class WcagCriteria
{
    // --- Criteria at least one rule currently cites ---
    public static readonly WcagCriterion NonTextContent = new("1.1.1", "Non-text Content", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion Orientation = new("1.3.4", "Orientation", WcagLevel.AA, WcagVersion.V2_1);
    public static readonly WcagCriterion ContrastMinimum = new("1.4.3", "Contrast (Minimum)", WcagLevel.AA, WcagVersion.V2_0);
    public static readonly WcagCriterion ResizeText = new("1.4.4", "Resize Text", WcagLevel.AA, WcagVersion.V2_0);
    public static readonly WcagCriterion NonTextContrast = new("1.4.11", "Non-text Contrast", WcagLevel.AA, WcagVersion.V2_1);
    public static readonly WcagCriterion Keyboard = new("2.1.1", "Keyboard", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion NoKeyboardTrap = new("2.1.2", "No Keyboard Trap", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion FocusOrder = new("2.4.3", "Focus Order", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion HeadingsAndLabels = new("2.4.6", "Headings and Labels", WcagLevel.AA, WcagVersion.V2_0);
    public static readonly WcagCriterion FocusVisible = new("2.4.7", "Focus Visible", WcagLevel.AA, WcagVersion.V2_0);
    public static readonly WcagCriterion LabelInName = new("2.5.3", "Label in Name", WcagLevel.A, WcagVersion.V2_1);
    public static readonly WcagCriterion TargetSizeMinimum = new("2.5.8", "Target Size (Minimum)", WcagLevel.AA, WcagVersion.V2_2);
    public static readonly WcagCriterion NameRoleValue = new("4.1.2", "Name, Role, Value", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion StatusMessages = new("4.1.3", "Status Messages", WcagLevel.AA, WcagVersion.V2_1);
    public static readonly WcagCriterion PauseStopHide =
        new("2.2.2", "Pause, Stop, Hide", WcagLevel.A, WcagVersion.V2_0);

    // --- The other 40, level A (24) ---
    public static readonly WcagCriterion AudioOnlyAndVideoOnlyPrerecorded =
        new("1.2.1", "Audio-only and Video-only (Prerecorded)", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion CaptionsPrerecorded =
        new("1.2.2", "Captions (Prerecorded)", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion AudioDescriptionOrMediaAlternativePrerecorded =
        new("1.2.3", "Audio Description or Media Alternative (Prerecorded)", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion InfoAndRelationships =
        new("1.3.1", "Info and Relationships", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion MeaningfulSequence =
        new("1.3.2", "Meaningful Sequence", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion SensoryCharacteristics =
        new("1.3.3", "Sensory Characteristics", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion UseOfColor =
        new("1.4.1", "Use of Color", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion AudioControl =
        new("1.4.2", "Audio Control", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion CharacterKeyShortcuts =
        new("2.1.4", "Character Key Shortcuts", WcagLevel.A, WcagVersion.V2_1);
    public static readonly WcagCriterion TimingAdjustable =
        new("2.2.1", "Timing Adjustable", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion ThreeFlashesOrBelowThreshold =
        new("2.3.1", "Three Flashes or Below Threshold", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion BypassBlocks =
        new("2.4.1", "Bypass Blocks", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion PageTitled =
        new("2.4.2", "Page Titled", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion LinkPurposeInContext =
        new("2.4.4", "Link Purpose (In Context)", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion PointerGestures =
        new("2.5.1", "Pointer Gestures", WcagLevel.A, WcagVersion.V2_1);
    public static readonly WcagCriterion PointerCancellation =
        new("2.5.2", "Pointer Cancellation", WcagLevel.A, WcagVersion.V2_1);
    public static readonly WcagCriterion MotionActuation =
        new("2.5.4", "Motion Actuation", WcagLevel.A, WcagVersion.V2_1);
    public static readonly WcagCriterion LanguageOfPage =
        new("3.1.1", "Language of Page", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion OnFocus =
        new("3.2.1", "On Focus", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion OnInput =
        new("3.2.2", "On Input", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion ConsistentHelp =
        new("3.2.6", "Consistent Help", WcagLevel.A, WcagVersion.V2_2);
    public static readonly WcagCriterion ErrorIdentification =
        new("3.3.1", "Error Identification", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion LabelsOrInstructions =
        new("3.3.2", "Labels or Instructions", WcagLevel.A, WcagVersion.V2_0);
    public static readonly WcagCriterion RedundantEntry =
        new("3.3.7", "Redundant Entry", WcagLevel.A, WcagVersion.V2_2);

    // --- The other 40, level AA (16) ---
    public static readonly WcagCriterion CaptionsLive =
        new("1.2.4", "Captions (Live)", WcagLevel.AA, WcagVersion.V2_0);
    public static readonly WcagCriterion AudioDescriptionPrerecorded =
        new("1.2.5", "Audio Description (Prerecorded)", WcagLevel.AA, WcagVersion.V2_0);
    public static readonly WcagCriterion IdentifyInputPurpose =
        new("1.3.5", "Identify Input Purpose", WcagLevel.AA, WcagVersion.V2_1);
    public static readonly WcagCriterion ImagesOfText =
        new("1.4.5", "Images of Text", WcagLevel.AA, WcagVersion.V2_0);
    public static readonly WcagCriterion Reflow =
        new("1.4.10", "Reflow", WcagLevel.AA, WcagVersion.V2_1);
    public static readonly WcagCriterion TextSpacing =
        new("1.4.12", "Text Spacing", WcagLevel.AA, WcagVersion.V2_1);
    public static readonly WcagCriterion ContentOnHoverOrFocus =
        new("1.4.13", "Content on Hover or Focus", WcagLevel.AA, WcagVersion.V2_1);
    public static readonly WcagCriterion MultipleWays =
        new("2.4.5", "Multiple Ways", WcagLevel.AA, WcagVersion.V2_0);
    public static readonly WcagCriterion FocusNotObscuredMinimum =
        new("2.4.11", "Focus Not Obscured (Minimum)", WcagLevel.AA, WcagVersion.V2_2);
    public static readonly WcagCriterion DraggingMovements =
        new("2.5.7", "Dragging Movements", WcagLevel.AA, WcagVersion.V2_2);
    public static readonly WcagCriterion LanguageOfParts =
        new("3.1.2", "Language of Parts", WcagLevel.AA, WcagVersion.V2_0);
    public static readonly WcagCriterion ConsistentNavigation =
        new("3.2.3", "Consistent Navigation", WcagLevel.AA, WcagVersion.V2_0);
    public static readonly WcagCriterion ConsistentIdentification =
        new("3.2.4", "Consistent Identification", WcagLevel.AA, WcagVersion.V2_0);
    public static readonly WcagCriterion ErrorSuggestion =
        new("3.3.3", "Error Suggestion", WcagLevel.AA, WcagVersion.V2_0);
    public static readonly WcagCriterion ErrorPreventionLegalFinancialData =
        new("3.3.4", "Error Prevention (Legal, Financial, Data)", WcagLevel.AA, WcagVersion.V2_0);
    public static readonly WcagCriterion AccessibleAuthenticationMinimum =
        new("3.3.8", "Accessible Authentication (Minimum)", WcagLevel.AA, WcagVersion.V2_2);

    /// <summary>All 55 Level A/AA criteria. <see cref="Rules.DefaultRules.MappedCriteria"/> filters this to
    /// the ones a rule currently cites, for reports that only list what findings can reference.</summary>
    public static IReadOnlyList<WcagCriterion> All { get; } =
    [
        NonTextContent, Orientation, ContrastMinimum, ResizeText, NonTextContrast,
        Keyboard, NoKeyboardTrap, FocusOrder, HeadingsAndLabels, FocusVisible,
        LabelInName, TargetSizeMinimum, NameRoleValue, StatusMessages,

        AudioOnlyAndVideoOnlyPrerecorded, CaptionsPrerecorded, AudioDescriptionOrMediaAlternativePrerecorded,
        InfoAndRelationships, MeaningfulSequence, SensoryCharacteristics, UseOfColor, AudioControl,
        CharacterKeyShortcuts, TimingAdjustable, PauseStopHide, ThreeFlashesOrBelowThreshold, BypassBlocks,
        PageTitled, LinkPurposeInContext, PointerGestures, PointerCancellation, MotionActuation, LanguageOfPage,
        OnFocus, OnInput, ConsistentHelp, ErrorIdentification, LabelsOrInstructions, RedundantEntry,

        CaptionsLive, AudioDescriptionPrerecorded, IdentifyInputPurpose, ImagesOfText, Reflow, TextSpacing,
        ContentOnHoverOrFocus, MultipleWays, FocusNotObscuredMinimum, DraggingMovements, LanguageOfParts,
        ConsistentNavigation, ConsistentIdentification, ErrorSuggestion, ErrorPreventionLegalFinancialData,
        AccessibleAuthenticationMinimum,
    ];
}
