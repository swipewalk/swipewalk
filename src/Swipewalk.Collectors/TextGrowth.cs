using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;

namespace Swipewalk.Collectors;

/// <summary>
/// Whether text visibly grew between two captures of (nominally) the same screen, at a lower cost than a
/// full per-element analysis: used by Android and iOS to decide whether a large-text capture reflects the
/// system setting live, or whether the app needs a terminate + relaunch to pick it up (some frameworks,
/// e.g. .NET MAUI, apply Dynamic Type / font scale only at launch, confirmed on iOS 27.0).
/// The actual per-element WCAG 1.4.4 reporting is <see cref="TextResizeRule"/>'s job; this only answers the
/// narrower "did the setting take effect live" question that decides whether to escalate to a restart.
/// </summary>
internal static class TextGrowth
{
    /// <summary>
    /// Pairs text elements between <paramref name="before"/> and <paramref name="after"/> by their visible
    /// text and checks the median height ratio against <see cref="TextResizeRule.MinimumGrowth"/> (the same
    /// threshold the resize rule itself uses). False when there is nothing to compare.
    /// </summary>
    internal static bool LooksGrown(ScreenSnapshot before, ScreenSnapshot after)
    {
        var afterByText = TextHeights(after.Root).GroupBy(t => t.Text).ToDictionary(g => g.Key, g => g.First().Height);
        var ratios = new List<double>();
        foreach (var (text, height) in TextHeights(before.Root))
        {
            if (height > 0 && afterByText.TryGetValue(text, out var afterHeight))
                ratios.Add(afterHeight / height);
        }
        if (ratios.Count == 0)
            return false;
        ratios.Sort();
        var median = ratios[ratios.Count / 2];
        return median >= TextResizeRule.MinimumGrowth;
    }

    private static IEnumerable<(string Text, double Height)> TextHeights(AccessibilityNode root) =>
        root.DescendantsAndSelf()
            .Where(n => n.Role == "text" && !string.IsNullOrWhiteSpace(n.VisibleText) && n.Bounds.Height > 0)
            .Select(n => (n.VisibleText!, n.Bounds.Height));
}
