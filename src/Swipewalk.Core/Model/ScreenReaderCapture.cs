namespace Swipewalk.Core.Model;

/// <summary>
/// Where a <see cref="ScreenReaderCapture"/> came from. Each source fills a different subset of
/// <see cref="ScreenReaderCaptureItem"/>'s fields -- see that type's remarks.
/// </summary>
public enum ScreenReaderSource
{
    /// <summary>
    /// A screen reader's own spoken output, read automatically without a person driving it (for example
    /// Android's TalkBack): fills <see cref="ScreenReaderCaptureItem.SpokenText"/>, matched to tree nodes by
    /// bounds or id.
    /// </summary>
    TalkBack,

    /// <summary>
    /// Xcode's Accessibility Inspector, walked without VoiceOver itself turned on: fills the structured
    /// fields (label, value, traits, identifier, hint, class -- see <see cref="ScreenReaderCaptureItem"/>)
    /// instead of spoken text, matched to tree nodes by order, label and class (the Inspector exposes no
    /// frame/geometry).
    /// </summary>
    AccessibilityInspector,

    /// <summary>
    /// A screen reader's spoken output captured while a person drives it by hand (for example VoiceOver on
    /// a connected phone): fills <see cref="ScreenReaderCaptureItem.SpokenText"/> with timestamps. Items may
    /// be unmatched to any tree node (<see cref="ScreenReaderCaptureItem.MatchedNodePath"/> null): a person
    /// can navigate, scroll or trigger hints in ways a single tree snapshot doesn't represent.
    /// </summary>
    VoiceOverCaptions,
}

/// <summary>How confidently a <see cref="ScreenReaderCaptureItem"/> was matched to a node in the scanned
/// accessibility tree (<see cref="ScreenReaderCaptureItem.MatchedNodePath"/>). A low-confidence match is a
/// weaker basis for treating a name/role difference as evidence of anything -- see
/// <see cref="ScreenReader.ScreenReaderCaptureComparer"/>.</summary>
public enum MatchConfidence
{
    /// <summary><see cref="ScreenReaderCaptureItem.MatchedNodePath"/> is null: not matched to any node.</summary>
    None,

    /// <summary>Matched, but on a weak signal alone (for example order only, on a busy screen where
    /// several elements share a label and class).</summary>
    Weak,

    /// <summary>Matched on more than one signal (for example order plus label, or order plus label and
    /// class) that agreed, but not on an identifier or exact bounds.</summary>
    Likely,

    /// <summary>Matched on a strong, close-to-unique signal: bounds/id, or an accessibility identifier.</summary>
    Exact,
}

/// <summary>
/// One stop in a real screen reader's own order, as actually reported by the tool named in
/// <see cref="ScreenReaderCapture.Source"/> -- distinct from <see cref="Announcement"/>, which is Swipewalk's
/// own *prediction* of what a screen reader would say (see <see cref="ScreenReader.ScreenReaderPredictor"/>).
/// Different sources fill different fields:
/// <list type="bullet">
/// <item><see cref="ScreenReaderSource.TalkBack"/> and <see cref="ScreenReaderSource.VoiceOverCaptions"/>
/// fill <see cref="SpokenText"/> (the verbatim utterance or caption) and leave the structured fields null.</item>
/// <item><see cref="ScreenReaderSource.AccessibilityInspector"/> fills <see cref="Label"/>,
/// <see cref="Value"/>, <see cref="Traits"/>, <see cref="Hint"/>, <see cref="Identifier"/> and
/// <see cref="ClassName"/> instead (what the Inspector's panel shows), and leaves <see cref="SpokenText"/>
/// null -- VoiceOver is never turned on for this route, so there is no speech to record.</item>
/// </list>
/// </summary>
/// <param name="Order">1-based position in this capture's own order. Compare against
/// <see cref="Announcement.Order"/> for the same tree node to detect an order difference.</param>
/// <param name="SpokenText">The verbatim utterance or caption-panel text, including any role word the
/// reader itself appended. Null for <see cref="ScreenReaderSource.AccessibilityInspector"/>, which has no
/// speech to record.</param>
/// <param name="Label">The Inspector's "Label" field (its closest analogue of an accessible name). Null when
/// not applicable to this source or not set.</param>
/// <param name="Value">The Inspector's "Value" field (for example a slider's current value, or a switch's
/// state). Null when not applicable to this source or not set.</param>
/// <param name="Traits">The Inspector's "Traits" list (for example "Button", "Link", "Adjustable") -- the
/// closest analogue of an exposed role/behavior. Null when not applicable to this source or not captured;
/// an empty (non-null) list means the Inspector was read and reported no traits, which is itself evidence
/// (see <see cref="ScreenReader.ScreenReaderCaptureComparer"/>'s role-mismatch check).</param>
/// <param name="Hint">The Inspector's "Hint" field. Null when not applicable to this source or not set.</param>
/// <param name="Identifier">The Inspector's "Identifier" field (accessibilityIdentifier). Null when not
/// applicable to this source or not set.</param>
/// <param name="ClassName">The Inspector's "Class" field (the element's native class, for example
/// "UIButton" or "Static Text"). Null when not applicable to this source or not set.</param>
/// <param name="Timestamp">When this item was captured/heard, for sources that can measure it (in
/// particular <see cref="ScreenReaderSource.VoiceOverCaptions"/>, one item at a time from a recorded
/// session). Null when not measured.</param>
/// <param name="MatchedNodePath">The scanned tree node this item corresponds to, in the same child-index
/// path scheme as <see cref="Finding.NodePath"/> and <see cref="Announcement.NodePath"/> (for example
/// "0/2/1"; the root is ""), decided by whichever collector built this capture. Null when the item could not
/// be matched to any node -- always the case when <see cref="MatchConfidence"/> is
/// <see cref="Model.MatchConfidence.None"/>, and possible for <see cref="ScreenReaderSource.VoiceOverCaptions"/>
/// even otherwise (a person can navigate in ways a single tree snapshot doesn't represent).</param>
/// <param name="MatchConfidence">How confident that match is; see <see cref="Model.MatchConfidence"/>.</param>
public sealed record ScreenReaderCaptureItem(
    int Order,
    string? SpokenText,
    string? Label,
    string? Value,
    IReadOnlyList<string>? Traits,
    string? Hint,
    string? Identifier,
    string? ClassName,
    DateTimeOffset? Timestamp,
    string? MatchedNodePath,
    MatchConfidence MatchConfidence);

/// <summary>
/// What a <see cref="ScreenReaderCapture"/> ever ATTEMPTS to cover, independent of whether this particular
/// run succeeded (<see cref="ScreenReaderCapture.Complete"/>). A fixed property of the route/source, not of
/// one capture: every TalkBack capture is <see cref="FocusableElementsOnly"/>, every Accessibility Inspector
/// capture is <see cref="AllElements"/> -- see <see cref="ScreenReaderCapture.Complete"/>'s remarks for why
/// this had to be split out as its own field (0.3.0: <see cref="Rules.ScreenReaderLabelInNameRule"/> could never
/// fire on a real TalkBack capture until <see cref="ScreenReaderCapture.Complete"/>'s meaning was fixed to
/// depend on this).
/// </summary>
public enum ScreenReaderCaptureScope
{
    /// <summary>The capture covers every element the tool could reach on the screen, informational text
    /// included -- matches the historical, single-flag meaning of <see cref="ScreenReaderCapture.Complete"/>
    /// alone. Used by the Accessibility Inspector route. Default value, for backward compatibility with
    /// every existing caller/fixture that doesn't set this field.</summary>
    AllElements,

    /// <summary>The capture only ever attempts focusable/interactive elements (approximately the same
    /// accessibility flags <see cref="Model.AccessibilityNode.IsInteractive"/>/<see cref="Model.AccessibilityNode.IsFocusable"/>
    /// read from -- see <c>TalkBackCollector.kt</c>'s <c>collectStops</c>): plain informational text was
    /// never a candidate the walk was trying to reach at all, by design, to keep the per-element cost down.
    /// Used by TalkBack's harness. For this scope, <see cref="ScreenReaderCapture.Complete"/> means "every
    /// focusable/interactive element was walked", never "every element on the screen" -- and
    /// <see cref="ScreenReader.ScreenReaderCaptureComparer"/>'s Missing-diff check is never reported for this
    /// scope at all (not even when <see cref="ScreenReaderCapture.Complete"/> is true): the harness has no
    /// way today to say which exact elements it walked, only how many items it captured, so treating a
    /// predicted-but-uncaptured focusable stop as a real absence would risk a false 4.1.2 citation from a
    /// node-identity mismatch or a single slow element, not a genuine gap.</summary>
    FocusableElementsOnly,
}

/// <summary>
/// Real screen-reader evidence for one <see cref="ScreenSnapshot"/> -- what a real tool actually reported,
/// as opposed to <see cref="ScreenReader.ScreenReaderPredictor"/>'s prediction from the tree. Attached to
/// <see cref="ScreenSnapshot.ScreenReaderCapture"/>; null there (the common case today, since no collector
/// currently produces this data) means no such capture was made for this screen, not that it found nothing.
/// </summary>
/// <param name="Source">Which tool produced this evidence; see <see cref="ScreenReaderSource"/>.</param>
/// <param name="ToolVersion">The tool's own version, for example "17.0.1" -- recorded because behavior can
/// change between versions.</param>
/// <param name="CapturedAt">When this capture was taken.</param>
/// <param name="Items">The captured stops, in this capture's own order (<see cref="ScreenReaderCaptureItem.Order"/>).</param>
/// <param name="Complete">True when the capture covered everything within its own <see cref="Scope"/> (every
/// element within that scope the tool would reach was walked/heard) -- NEVER "the whole screen" on its own
/// for a <see cref="ScreenReaderCaptureScope.FocusableElementsOnly"/> capture; see <see cref="Scope"/>. False
/// for a capture that hit a real problem: for the Accessibility Inspector, a device disconnected, the walk
/// timed out, or an order wrapped back to an already-seen element; for TalkBack, the app never came back to
/// the front, TalkBack refused to speak through Swipewalk's engine, the per-capture element cap was reached,
/// or it said nothing for one or more elements it focused. So readers and
/// <see cref="ScreenReader.ScreenReaderCaptureComparer"/> know an item predicted but not seen may simply be
/// past where the capture stopped, not a real difference. See <see cref="NotCompleteReason"/>.</param>
/// <param name="NotCompleteReason">Why the capture is not <see cref="Complete"/>, for example "stopped after
/// the walk order repeated an already-seen element" or "device disconnected mid-capture". Null when
/// <see cref="Complete"/> is true.</param>
/// <param name="Language">The device's system language when this capture ran (BCP-47, e.g. "es-ES");
/// Android/TalkBack only for now, null for other sources or when not read. TalkBack speaks its own role and
/// hint words (for example "Button", "Edit box") in this language, but never translates the app's own
/// accessible names -- see <see cref="ScreenReader.ScreenReaderCaptureComparer"/>'s remarks for how it uses
/// this: its own role/hint word vocabulary is English only, so on any other language a role or hint word
/// TalkBack says can go unrecognized and must not be mistaken for a wrong or missing accessible name.</param>
/// <param name="Scope">What this capture ever attempts to cover; see <see cref="ScreenReaderCaptureScope"/>.
/// Defaults to <see cref="ScreenReaderCaptureScope.AllElements"/> so every pre-existing caller/fixture that
/// doesn't set this keeps behaving exactly as before -- a collector for a scoped source (currently only
/// Android's <c>AndroidCollector</c>, for TalkBack) sets this explicitly rather than relying on the default.
/// <see cref="Reports.JsonReport.Deserialize"/> also forces this to
/// <see cref="ScreenReaderCaptureScope.FocusableElementsOnly"/> for any <see cref="ScreenReaderSource.TalkBack"/>
/// capture, so a results.json saved before this field existed (which has no "scope" property at all, and
/// would otherwise silently default to <see cref="ScreenReaderCaptureScope.AllElements"/>) still gets the
/// right scope back.</param>
public sealed record ScreenReaderCapture(
    ScreenReaderSource Source,
    string ToolVersion,
    DateTimeOffset CapturedAt,
    IReadOnlyList<ScreenReaderCaptureItem> Items,
    bool Complete,
    string? NotCompleteReason,
    string? Language = null,
    ScreenReaderCaptureScope Scope = ScreenReaderCaptureScope.AllElements);
