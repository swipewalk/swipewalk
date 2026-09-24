using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

internal static class RuleFinding
{
    public static Finding Create(
        string ruleId, FindingKind kind, string message, AccessibilityNode node, string path,
        IReadOnlyList<WcagCriterion>? criteria = null, string? guideline = null) => new()
    {
        RuleId = ruleId,
        Kind = kind,
        Message = message,
        Criteria = criteria ?? [],
        PlatformGuideline = guideline,
        NodePath = path,
        Role = node.Role,
        // When the element carries no label or visible text of its own (e.g. a plain container built
        // from a labeled icon + text child), fall back to the name a screen reader would announce for
        // it, so reports don't show findings with no identifying label.
        Label = node.Label ?? node.VisibleText ?? ScreenReaderPredictor.AccessibleName(node),
        Bounds = node.Bounds,
    };

    public static bool HasArea(AccessibilityNode node) => node.Bounds.Width > 0 && node.Bounds.Height > 0;

    /// <summary>Roles for editable text-entry controls (Android EditText/AutoCompleteTextView/MultiAutoCompleteTextView;
    /// iOS textField/secureTextField/searchField/textView all map to "textfield"). Their content is user-entered
    /// text, not an image, so an empty one is not non-text content under WCAG 1.1.1.</summary>
    private static readonly HashSet<string> EditableRoles = ["textfield"];

    /// <summary>
    /// True for controls whose content is not text (icon buttons, custom-drawn controls): no visible text
    /// on the element or its children, and not an editable text-entry control. Such controls are non-text
    /// content under WCAG 1.1.1.
    /// </summary>
    public static bool IsImageBased(AccessibilityNode node) =>
        !EditableRoles.Contains(node.Role)
        && string.IsNullOrWhiteSpace(node.VisibleText)
        && !node.Children.SelectMany(c => c.DescendantsAndSelf()).Any(d => !string.IsNullOrWhiteSpace(d.VisibleText));
}
