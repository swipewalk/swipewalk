using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// Interactive elements with no accessible name are announced only by role ("Unlabeled, Button").
/// Images with no text alternative are flagged for review: automated checks can't tell whether they are decorative.
/// </summary>
public sealed class MissingNameRule : IRule
{
    public string Id => "missing-name";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        foreach (var (node, path) in snapshot.Root.DescendantsAndSelfWithPath())
        {
            if (!RuleFinding.HasArea(node) || ScreenReaderPredictor.AccessibleName(node) is not null)
                continue;

            if (node.IsInteractive && node.IsAccessible && node.Role == "textfield" && !string.IsNullOrWhiteSpace(node.VisibleText))
            {
                // Reached only when the field has no label AND no hint (node.Hint) -- and node.Hint
                // already picks up AccessibilityNodeInfo#getHintText() from the Android instrumentation
                // harness when uiautomator's own "hint" attribute is empty (see UiAutomatorParser.Convert
                // and KnownLimitations "android-edittext-text": on some devices/versions the dump omits
                // "hint" even though the field has one). So by the time execution reaches here with the
                // harness having run, there really is no hint anywhere, and node.VisibleText is the field's
                // rendered text with nothing else to explain it.
                //
                // node.IsShowingHintText (AccessibilityNodeInfo#isShowingHintText(), API 26) still tells
                // apart the two ways that can happen, but neither is a certain WcagIssue on its own:
                //
                // True means VisibleText is itself the placeholder, but getHintText() came back empty --
                // an unusual node (a standard TextView sets both together; this is typically a custom
                // accessibility delegate), not the normal case. In the state actually captured, TalkBack
                // does speak the placeholder (e.g. "Email, Edit box"), so "no accessible name" isn't true
                // right now -- only that the name would disappear once something is typed, which Swipewalk
                // can't observe. NeedsReview, not a certain finding.
                //
                // False only proves the visible text is a typed value; it does NOT prove the field has no
                // name at all -- Android can also give a field a name through android:labelFor/getLabeledBy(),
                // which uiautomator dump never exposes and the harness does not read yet (see
                // KnownLimitations "android-edittext-text"), so this also stays NeedsReview.
                //
                // Null means the harness didn't run (older Android, harness unavailable, or any
                // non-Android platform): the same ambiguity, so also NeedsReview.
                var announced = ScreenReaderPredictor.Announce(node, snapshot.Platform);
                if (node.IsShowingHintText == true)
                {
                    yield return RuleFinding.Create(Id, FindingKind.NeedsReview,
                        "Android reports this field's visible text as a placeholder but exposes no hint text. " +
                        "TalkBack may announce the placeholder while the field is empty, but once something is " +
                        $"typed the field may be announced with no name (predicted now: \"{announced}\"). " +
                        "Type into the field and check with TalkBack.",
                        node, path, [WcagCriteria.NameRoleValue]);
                }
                else if (node.IsShowingHintText == false)
                {
                    yield return RuleFinding.Create(Id, FindingKind.NeedsReview,
                        "This text field has no content description or hint; its visible text is the entered " +
                        "value. Unless it is labelled by another element (android:labelFor), screen readers " +
                        $"announce the value with no indication of what the field is for (predicted: \"{announced}\"). " +
                        "Check with TalkBack.",
                        node, path, [WcagCriteria.NameRoleValue]);
                }
                else
                {
                    var checkHint = snapshot.Platform == Platform.Android
                        ? "Check with TalkBack."
                        : "Check with a screen reader (TalkBack, VoiceOver or Narrator).";
                    yield return RuleFinding.Create(Id, FindingKind.NeedsReview,
                        "This text field has no accessible name apart from its visible text. If that text is an " +
                        "entered value rather than a placeholder or label, screen readers announce the field " +
                        $"without a name (predicted: \"{announced}\"). " +
                        checkHint,
                        node, path, [WcagCriteria.NameRoleValue]);
                }
            }
            else if (node.IsInteractive && node.IsAccessible)
            {
                IReadOnlyList<WcagCriterion> criteria = RuleFinding.IsImageBased(node)
                    ? [WcagCriteria.NonTextContent, WcagCriteria.NameRoleValue]
                    : [WcagCriteria.NameRoleValue];
                yield return RuleFinding.Create(Id, FindingKind.WcagIssue,
                    $"Interactive {node.Role} has no accessible name. Predicted screen-reader output: " +
                    $"\"{ScreenReaderPredictor.Announce(node, snapshot.Platform)}\".",
                    node, path, criteria);
            }
            else if (node.Role == "image" && !node.IsInteractive
                     && (node.IsFocusable || !HasNamedFocusableAncestor(snapshot.Root, path)))
            {
                // Hidden-on-purpose images are included: uiautomator and XCUITest don't expose the
                // decorative flag. Skip !IsAccessible here once a collector can see it.
                //
                // An image that is itself a screen-reader stop (focusable) is always flagged. One that
                // is not a separate stop and sits under an ancestor that is one stop and already has an
                // accessible name is read as part of that ancestor, so it needs no name of its own
                // (e.g. a menu icon inside a row whose content-desc/label comes from a sibling).
                yield return RuleFinding.Create(Id, FindingKind.NeedsReview,
                    "Image has no text alternative, so screen readers skip it. If it conveys information, " +
                    "add a description; if it is decorative, no change is needed.",
                    node, path, [WcagCriteria.NonTextContent]);
            }
        }
    }

    /// <summary>
    /// True when an ancestor on the path from the root is itself an accessibility-focusable element
    /// (interactive or focusable, and not hidden from assistive technology) with an accessible name:
    /// screen readers announce that ancestor as one stop and read its non-focusable descendants,
    /// including <paramref name="path"/>'s node, as part of it.
    /// </summary>
    private static bool HasNamedFocusableAncestor(AccessibilityNode root, string path)
    {
        if (path.Length == 0)
            return false;

        var indices = path.Split('/');
        var ancestor = root;
        if (IsNamedFocusStop(ancestor))
            return true;
        for (var i = 0; i < indices.Length - 1; i++)
        {
            ancestor = ancestor.Children[int.Parse(indices[i])];
            if (IsNamedFocusStop(ancestor))
                return true;
        }
        return false;

        static bool IsNamedFocusStop(AccessibilityNode node) =>
            node.IsAccessible && (node.IsInteractive || node.IsFocusable)
            && ScreenReaderPredictor.AccessibleName(node) is not null;
    }
}
