namespace Swipewalk.Core.Model;

/// <summary>
/// Tells whether a screen captured again after switching the device's dark/light appearance (see
/// <c>Swipewalk.Engine.ScanOptions.AppearanceBoth</c>) actually looks any different, or whether the app did
/// not visibly respond to the change -- some frameworks (e.g. .NET MAUI apps that only read the theme at
/// launch) need a restart to pick up a new appearance, the same way some apps only pick up a larger system
/// text size after a restart. This is a pure, testable decision: same accessibility tree (so it's genuinely
/// the same screen, not a relaunch that landed somewhere else) and near-identical average screenshot
/// brightness. A different screen, or a real color change, is never called "unchanged".
/// </summary>
public static class AppearanceChangeDetector
{
    /// <summary>Average-luminance difference (0-255 scale) below which two screenshots are treated as
    /// visually the same. A real light/dark theme swap moves this by tens of points; a few points of noise
    /// can come from anti-aliasing, a status bar clock, or JPEG-like screenshot compression artifacts.</summary>
    private const double LuminanceDifferenceThreshold = 10.0;

    /// <summary>
    /// True when <paramref name="after"/> (captured once the device's appearance was switched) looks like
    /// nothing happened compared with <paramref name="before"/> (the primary capture). False -- not "unknown"
    /// -- when either screenshot is missing (blocked, or not captured): there is nothing to compare, so this
    /// never claims "unchanged" without evidence.
    /// </summary>
    public static bool LooksUnchanged(ScreenSnapshot before, ScreenSnapshot after)
    {
        if (before.Screenshot is null || after.Screenshot is null)
            return false;
        if (!SameVisibleText(before.Root, after.Root))
            return false;
        return Math.Abs(before.Screenshot.AverageLuminance() - after.Screenshot.AverageLuminance()) < LuminanceDifferenceThreshold;
    }

    /// <summary>Same screen, loosely: the visible text of every node, in tree order, is identical. Colors
    /// aren't part of this comparison on purpose -- that's what the luminance check above is for.</summary>
    private static bool SameVisibleText(AccessibilityNode a, AccessibilityNode b) =>
        string.Join('\n', a.DescendantsAndSelf().Select(n => n.VisibleText))
            == string.Join('\n', b.DescendantsAndSelf().Select(n => n.VisibleText));
}
