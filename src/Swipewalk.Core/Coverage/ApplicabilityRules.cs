using Swipewalk.Core.Model;

namespace Swipewalk.Core.Coverage;

/// <summary>
/// A suggestion, not a verdict: computed once per screen at capture time (<see cref="Rules.RuleRunner.Run"/>),
/// because only the raw accessibility tree can answer this and the tree isn't kept past capture (results.json
/// stores <see cref="Model.ScreenResult"/>, the post-rule projection, not the tree). Shown to the tester in the
/// guided-checks UI as "This looks like it might not apply here: {Reason}. Confirm?" -- becomes a final
/// <see cref="ScreenCriterionStatus.NotApplicableHere"/> status only once a <see cref="GuidedAnswer"/> with
/// <see cref="GuidedAnswerResult.ConfirmedNotApplicable"/> exists for the same (screen, criterion).
///
/// Never generated for 4.1.2 (Name, Role, Value) or 2.1.1 (Keyboard): a screen with no interactive node right
/// now could still be missing something the tree can't see (a loading state, a custom view the tree failed to
/// expose, a future interactive element). Never generated at all for 1.1.1, 1.4.5, 2.5.1 or 2.5.7: no tree
/// signal is safe enough even to propose for those four (an icon-only button is a "button", not an "image"
/// role, so an absent image node proves nothing; gestures aren't exposed as tree facts at all).
/// </summary>
/// <param name="CriterionNumber">The WCAG criterion number this proposal is for -- always a real
/// <see cref="Wcag.WcagCriteria"/> entry.</param>
/// <param name="Reason">Why the tree suggests this criterion may not apply here, shown to the tester.</param>
public sealed record ProposedNotApplicable(string CriterionNumber, string Reason);

/// <summary>The tree-based applicability proposals for one screen. See <see cref="ProposedNotApplicable"/>.</summary>
public sealed record ScreenApplicability(IReadOnlyList<ProposedNotApplicable> Proposed)
{
    public static readonly ScreenApplicability Empty = new([]);
}

/// <summary>
/// Computes <see cref="ScreenApplicability"/> from a screen's accessibility tree. Pure function: no side
/// effects, no I/O. Starts narrow (the clear, tester-facing proposal cases below) and is expected to grow over
/// time, the same way <see cref="CoverageCatalog"/> itself grew rule-by-rule -- never automatically decides
/// N/A on its own; every proposal here still needs a tester's confirmation (a <see cref="GuidedAnswer"/> with
/// <see cref="GuidedAnswerResult.ConfirmedNotApplicable"/>) before it becomes a final status.
/// </summary>
public static class ApplicabilityRules
{
    /// <summary>Criteria this class must never propose N/A for, regardless of what the tree looks like: a
    /// screen with no interactive node today could still be missing keyboard support or a name/role/value for
    /// something the tree failed to expose.</summary>
    private static readonly string[] NeverPropose = ["4.1.2", "2.1.1"];

    /// <summary>Input-role nodes that establish a value a user provides or a state that changes (see
    /// <see cref="Wcag.WcagCriteria.OnInput"/>, <see cref="Wcag.WcagCriteria.ErrorIdentification"/>,
    /// <see cref="Wcag.WcagCriteria.LabelsOrInstructions"/>, <see cref="Wcag.WcagCriteria.ErrorSuggestion"/>).</summary>
    private static readonly string[] InputRoles = ["textfield", "checkbox", "switch", "radio", "slider"];

    /// <summary>No input-role node and no node with a <see cref="AccessibilityNode.Value"/>/
    /// <see cref="AccessibilityNode.StateDescription"/> (a custom control can expose a value/state without
    /// using one of the named roles -- the reviewer's required broadening) -- proposes N/A for these four.</summary>
    private static readonly string[] NoInputCriteria = ["3.2.2", "3.3.1", "3.3.2", "3.3.3"];

    /// <summary>No interactive/focusable node at all -- proposes N/A for these eight, including 3.3.8 (broadened
    /// from a textfield/password heuristic to "no interactive node at all": sign-in can be presented with
    /// custom, non-textfield controls a narrower heuristic would miss).</summary>
    private static readonly string[] NoInteractiveCriteria =
        ["1.4.13", "2.1.2", "2.4.7", "2.4.11", "2.5.2", "2.5.8", "3.2.1", "3.3.8"];

    /// <summary>
    /// Native class-name fragments for platform pickers/steppers that commonly map to a role outside
    /// <see cref="InputRoles"/> (Android Spinner/NumberPicker/DatePicker/TimePicker typically land on "button"/
    /// "group" with no <see cref="AccessibilityNode.Value"/> set; iOS UIPickerView/UIDatePicker/
    /// UISegmentedControl/UIStepper commonly land on "group"/"button" too) -- checked against
    /// <see cref="AccessibilityNode.NativeType"/> so these still count as "an input exists here" even though
    /// neither <see cref="InputRoles"/> nor <see cref="AccessibilityNode.Value"/> would otherwise catch them.
    /// This only ever WIDENS what counts as an input (fewer proposals, never more), so it can't turn a real
    /// applicable criterion into a wrongly suggested one -- it can only make the tool under-propose, which the
    /// tester's own confirmation step already exists to correct either way.
    /// </summary>
    private static readonly string[] PickerNativeTypeFragments =
        ["Spinner", "NumberPicker", "DatePicker", "TimePicker", "PickerView", "SegmentedControl", "Stepper"];

    private static bool LooksLikeAPicker(AccessibilityNode n) =>
        n.NativeType is { } nativeType && PickerNativeTypeFragments.Any(nativeType.Contains);

    public static ScreenApplicability Evaluate(AccessibilityNode root)
    {
        var nodes = root.DescendantsAndSelf().ToList();
        var proposed = new List<ProposedNotApplicable>();

        // Note: uiautomator (Android) leaves out content scrolled out of view, so "found" below always means
        // "found in the captured part of the screen" -- never a claim that nothing more exists off-screen.
        // Dropdowns/pickers/steppers are also not reliably recognized by role or Value across platforms (a
        // native picker/spinner commonly maps to "button"/"group" with no Value set), so these proposals can
        // under-detect a real input control; the reason text below says so, and the tester's confirmation --
        // never this signal alone -- is what actually decides it.
        var hasInputRole = nodes.Any(n => InputRoles.Contains(n.Role));
        var hasValueOrState = nodes.Any(n => n.Value is not null || n.StateDescription is not null);
        var hasPicker = nodes.Any(LooksLikeAPicker);
        if (!hasInputRole && !hasValueOrState && !hasPicker)
        {
            const string reason = "No text field, checkbox, switch, radio button, slider or picker was found " +
                "in the captured part of this screen. Custom controls may still not be recognized -- check for " +
                "them before confirming.";
            foreach (var number in NoInputCriteria)
                proposed.Add(new ProposedNotApplicable(number, reason));
        }

        var hasInteractive = nodes.Any(n => n.IsInteractive || n.IsFocusable);
        if (!hasInteractive)
        {
            const string reason = "No interactive or focusable element was found in the captured part of this screen.";
            foreach (var number in NoInteractiveCriteria)
                proposed.Add(new ProposedNotApplicable(number, reason));
        }

        var hasImage = nodes.Any(n => n.Role == "image");
        if (!hasInteractive && !hasImage)
        {
            proposed.Add(new ProposedNotApplicable("1.4.11",
                "No interactive control and no image element were found in the captured part of this screen. " +
                "Charts, graphs or icons drawn without an image element (common in Compose/SwiftUI) may not be " +
                "recognized -- check for them before confirming."));
        }

        var hasWebView = nodes.Any(n => n.Role == "webview");
        if (!hasWebView)
        {
            proposed.Add(new ProposedNotApplicable("1.4.12",
                "No web view was found in the captured part of this screen; 1.4.12 mainly applies to web " +
                "content and any in-app spacing option the app offers, which the tree can't detect -- likely " +
                "not applicable."));
        }

        return new ScreenApplicability([.. proposed.Where(p => !NeverPropose.Contains(p.CriterionNumber))]);
    }
}
