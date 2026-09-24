using Swipewalk.Core.Model;

namespace Swipewalk.Core.ScreenReader;

/// <summary>
/// Predicts screen-reader output (TalkBack, VoiceOver, Narrator) from the accessibility tree. This is
/// an approximation of the reader's rules, not recorded speech; reports must label it as predicted.
/// </summary>
public static class ScreenReaderPredictor
{
    /// <summary>
    /// The name a screen reader uses: the explicit label, else visible text, else (for interactive
    /// elements) the text of their children, else the hint. For an editable text field, "visible text"
    /// is excluded: it is the field's entered value, not a name (the WAI-ARIA accessible-name
    /// computation excludes a control's value the same way), so an unlabeled, unhinted field is
    /// nameless even though it shows text.
    /// </summary>
    public static string? AccessibleName(AccessibilityNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Label))
            return node.Label.Trim();
        if (node.Role != "textfield" && !string.IsNullOrWhiteSpace(node.VisibleText))
            return node.VisibleText.Trim();
        if (node.IsInteractive || node.IsFocusable)
        {
            var childText = string.Join(", ", node.Children
                .SelectMany(c => c.DescendantsAndSelf())
                .Where(d => d.IsAccessible && !d.IsInteractive)
                .Select(d => d.Label ?? d.VisibleText)
                .Where(t => !string.IsNullOrWhiteSpace(t)));
            if (childText.Length > 0)
                return childText;
        }
        return string.IsNullOrWhiteSpace(node.Hint) ? null : node.Hint.Trim();
    }

    /// <summary>
    /// Swipe order: a depth-first walk where each accessible focusable or interactive element is one
    /// stop (its children are read as part of it), and other accessible elements with a name are stops.
    /// </summary>
    public static IReadOnlyList<Announcement> Predict(ScreenSnapshot snapshot)
    {
        var stops = new List<Announcement>();
        Walk(snapshot.Root, "", snapshot.Platform, stops);
        return snapshot.Platform == Platform.iOS ? SortGeometrically(stops) : stops;
    }

    /// <summary>Predicted announcement, using the role words of the platform's screen reader.</summary>
    public static string Announce(AccessibilityNode node, Platform platform = Platform.Android)
    {
        var name = AccessibleName(node);
        // A text field's typed value is not part of its accessible name (see AccessibleName), but
        // TalkBack/VoiceOver still speak it when the field has no name: an unlabeled, unhinted field
        // showing "15" is announced "15, Edit box", not "Unlabeled, Edit box".
        if (name is null && node.Role == "textfield" && !string.IsNullOrWhiteSpace(node.Value))
            name = node.Value.Trim();
        var parts = new List<string> { name ?? "Unlabeled" };
        if (RoleWord(node.Role, platform) is { } role)
            parts.Add(role);
        if (!node.IsEnabled)
            parts.Add("disabled");
        return string.Join(", ", parts);
    }

    private static void Walk(AccessibilityNode node, string path, Platform platform, List<Announcement> stops)
    {
        // A focusable element is one stop with its children read as part of it, except containers that screen
        // readers move through instead: scrolling lists, and focusable wrappers with no text or action of their
        // own around focusable children.
        if (node.IsAccessible && (node.IsFocusable || node.IsInteractive) && !IsPassThroughContainer(node))
        {
            stops.Add(new Announcement(stops.Count + 1, path, Announce(node, platform), node.Bounds, AccessibleName(node) is not null));
            return;
        }

        if (node.IsAccessible && !IsPassThroughContainer(node) && AccessibleName(node) is not null)
            stops.Add(new Announcement(stops.Count + 1, path, Announce(node, platform), node.Bounds, HasName: true));

        for (var i = 0; i < node.Children.Count; i++)
            Walk(node.Children[i], path.Length == 0 ? $"{i}" : $"{path}/{i}", platform, stops);
    }

    /// <summary>
    /// VoiceOver reads views without an explicit element order by position: top to bottom, and left to
    /// right within a row. Stops whose vertical extents overlap the row's first stop form a row.
    /// </summary>
    private static IReadOnlyList<Announcement> SortGeometrically(List<Announcement> stops)
    {
        var byTop = stops.OrderBy(a => a.Bounds.Y).ToList();
        var ordered = new List<Announcement>();
        while (byTop.Count > 0)
        {
            var first = byTop[0];
            var center = first.Bounds.Y + first.Bounds.Height / 2;
            var row = byTop.Where(a => a.Bounds.Y <= center && a.Bounds.Y + a.Bounds.Height >= center).ToList();
            if (!row.Contains(first))
                row.Add(first);
            ordered.AddRange(row.OrderBy(a => a.Bounds.X));
            byTop.RemoveAll(row.Contains);
        }
        return ordered.Select((a, i) => a with { Order = i + 1 }).ToList();
    }

    private static bool IsPassThroughContainer(AccessibilityNode node)
    {
        if (node.Children.Count == 0)
            return false;
        if (node.IsScrollable)
            return true;
        var ownText = !string.IsNullOrWhiteSpace(node.Label) || !string.IsNullOrWhiteSpace(node.VisibleText);
        return !node.IsInteractive && !ownText
               && node.Children.SelectMany(c => c.DescendantsAndSelf()).Any(d => d.IsAccessible && (d.IsFocusable || d.IsInteractive));
    }

    private static string? RoleWord(string role, Platform platform) => role switch
    {
        "button" => "Button",
        "link" => "Link",
        "textfield" => platform == Platform.Android ? "Edit box" : "Text field",
        "image" => "Image",
        "checkbox" => "Checkbox",
        "switch" => "Switch",
        "radio" => "Radio button",
        "slider" => "Slider",
        "heading" => "Heading",
        "webview" => "Web view",
        _ => null,
    };
}
