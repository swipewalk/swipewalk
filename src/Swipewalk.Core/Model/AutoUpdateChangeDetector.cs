namespace Swipewalk.Core.Model;

/// <summary>
/// Tells whether a screen kept changing on its own, without any input, across the captures in
/// <see cref="ScreenSnapshot.AutoUpdateCaptures"/> -- the evidence behind WCAG 2.2.2 Pause, Stop, Hide (see
/// <see cref="Rules.AutoUpdatingContentRule"/>). Compares the accessibility trees, not screenshot pixels: a
/// blinking text-input caret never appears in a node's <see cref="AccessibilityNode.Value"/>, so it can't
/// register as a change here, and the status bar (already masked before a capture is taken, and not part of
/// an app's own accessibility tree in the first place) is never compared either.
///
/// Requiring change across two CONSECUTIVE windows -- not just one -- is what tells sustained auto-updating
/// content (a carousel slide, a ticker, a countdown) from a one-off transition that happens to still be
/// mid-flight when a single follow-up capture lands: a loading spinner that appears and is gone a moment
/// later changes between the first two captures but not the next two, so it is never reported; genuinely
/// auto-updating content keeps changing, so it shows up in both windows. This is a heuristic, not a
/// guarantee: it does NOT prove the content kept changing for a full interval after it was first seen to
/// change -- two unrelated, near-instantaneous transitions that happen to land just before and just after the
/// middle capture (e.g. a screen that loads in two visible steps) would also satisfy "both windows changed",
/// and would be reported the same as genuine sustained content; documented as a limitation (see
/// KnownLimitations "auto-update-detection-heuristic"), not claimed as proof of duration. It also compares any
/// element that changed in each window, not necessarily the SAME element both times, so two different
/// unrelated one-off changes on the same screen can also trigger it. This can still miss real auto-updating
/// content: a carousel whose cycle length happens to match the interval almost exactly (it looks unchanged at
/// every capture), or content that changes only once, slowly, over more than one interval (a single lasting
/// change looks the same as a settled one here) -- also documented as a limitation, not silently claimed as
/// complete coverage.
/// </summary>
public static class AutoUpdateChangeDetector
{
    /// <summary>One element that differed between two captures of the same screen.</summary>
    /// <param name="Path">Child-index path from the root, matching <see cref="Finding.NodePath"/>'s shape.</param>
    /// <param name="What">A short, human-readable description, e.g. "text changed" or "appeared".</param>
    public sealed record ChangedElement(string Path, string Role, string? Label, string What);

    /// <summary>A bounds move smaller than this fraction of the screen's own width/height is treated as
    /// measurement jitter, not a real move (a scrolling ticker or carousel moves much further than this).</summary>
    private const double MinRelativeMove = 0.01;

    /// <summary>Never a real move below this many density-independent units either, however small the
    /// screen -- avoids treating a tiny screen's 1% as sub-pixel noise.</summary>
    private const double MinAbsoluteMove = 2.0;

    private const int MaxDescriptionLength = 40;

    /// <summary>
    /// True when the content kept changing across more than one interval between <paramref name="primary"/>
    /// and <paramref name="extraCaptures"/> (see the type's remarks) -- never true with fewer than two extra
    /// captures, since a single interval can't tell a settled one-off change from sustained auto-updating.
    /// </summary>
    public static bool IsSustained(
        ScreenSnapshot primary, IReadOnlyList<ScreenSnapshot> extraCaptures, out IReadOnlyList<ChangedElement> evidence)
    {
        evidence = [];
        if (extraCaptures.Count < 2)
            return false;

        var sequence = new List<ScreenSnapshot>(extraCaptures.Count + 1) { primary };
        sequence.AddRange(extraCaptures);

        var windows = new List<IReadOnlyList<ChangedElement>>(sequence.Count - 1);
        for (var i = 0; i < sequence.Count - 1; i++)
            windows.Add(Diff(sequence[i], sequence[i + 1]));

        for (var i = 0; i < windows.Count - 1; i++)
        {
            if (windows[i].Count == 0 || windows[i + 1].Count == 0)
                continue;
            evidence = windows[i]
                .Concat(windows[i + 1])
                .GroupBy(c => (c.Path, c.What))
                .Select(g => g.First())
                .ToList();
            return true;
        }

        return false;
    }

    /// <summary>Elements that changed between two captures of the same screen, matched by
    /// <see cref="AccessibilityNode.DescendantsAndSelfWithPath"/>'s child-index path. A node with no
    /// meaningful content (no visible text, value or label -- most layout containers) never triggers this on
    /// its own, so a screen re-laying out its existing content isn't reported just because a purely
    /// structural node's path shifted.</summary>
    public static IReadOnlyList<ChangedElement> Diff(ScreenSnapshot before, ScreenSnapshot after)
    {
        var beforeByPath = before.Root.DescendantsAndSelfWithPath().ToDictionary(t => t.Path, t => t.Node);
        var afterByPath = after.Root.DescendantsAndSelfWithPath().ToDictionary(t => t.Path, t => t.Node);
        var screenWidth = Math.Max(after.Root.Bounds.Width, 1);
        var screenHeight = Math.Max(after.Root.Bounds.Height, 1);

        var changed = new List<ChangedElement>();
        foreach (var (path, node) in afterByPath)
        {
            if (!beforeByPath.TryGetValue(path, out var previous))
            {
                if (HasContent(node))
                    changed.Add(new ChangedElement(path, node.Role, DisplayLabel(node), "appeared"));
                continue;
            }

            var beforeText = DisplayText(previous);
            var afterText = DisplayText(node);
            if (!string.Equals(beforeText, afterText, StringComparison.Ordinal))
                changed.Add(new ChangedElement(path, node.Role, DisplayLabel(node),
                    $"text changed from \"{Truncate(beforeText)}\" to \"{Truncate(afterText)}\""));
            else if (HasContent(node) && Moved(previous.Bounds, node.Bounds, screenWidth, screenHeight))
                changed.Add(new ChangedElement(path, node.Role, DisplayLabel(node), "moved"));
        }

        foreach (var path in beforeByPath.Keys.Except(afterByPath.Keys))
        {
            var node = beforeByPath[path];
            if (HasContent(node))
                changed.Add(new ChangedElement(path, node.Role, DisplayLabel(node), "disappeared"));
        }

        return changed;
    }

    private static bool HasContent(AccessibilityNode node) =>
        !string.IsNullOrWhiteSpace(node.VisibleText) || !string.IsNullOrWhiteSpace(node.Value) || !string.IsNullOrWhiteSpace(node.Label);

    private static string DisplayText(AccessibilityNode node) =>
        (node.VisibleText ?? node.Value ?? node.Label ?? "").Trim();

    private static string? DisplayLabel(AccessibilityNode node) =>
        node.Label ?? node.VisibleText ?? node.Value;

    private static string Truncate(string text) =>
        text.Length <= MaxDescriptionLength ? text : text[..MaxDescriptionLength] + "...";

    private static bool Moved(Bounds before, Bounds after, double screenWidth, double screenHeight)
    {
        var thresholdX = Math.Max(MinAbsoluteMove, screenWidth * MinRelativeMove);
        var thresholdY = Math.Max(MinAbsoluteMove, screenHeight * MinRelativeMove);
        return Math.Abs(after.X - before.X) > thresholdX || Math.Abs(after.Y - before.Y) > thresholdY;
    }
}
