namespace Swipewalk.Core.Model;

/// <summary>
/// Tells whether a screen captured again after rotating the device (see
/// <c>Swipewalk.Engine.ScanOptions.OrientationBoth</c>) actually changed shape on screen, by comparing the
/// screenshot's aspect ratio (falling back to the root node's bounds when no screenshot was captured) between
/// the primary capture and the one taken after rotating. This is the evidence behind WCAG 1.3.4 Orientation:
/// an app that keeps showing the same portrait- (or landscape-) shaped content after the device itself was
/// physically rotated is restricted to one orientation -- unless that orientation is essential to this screen
/// (a piano keyboard, a bank cheque deposit, slides meant for a projector or television, or virtual reality
/// content), which a scan can't tell from the outside, so
/// <see cref="Rules.OrientationRestrictedRule"/> reports it for review, never as a confirmed failure.
/// </summary>
public static class OrientationChangeDetector
{
    /// <summary>
    /// True when the two captures are in different aspect categories (one wider than tall, the other not) --
    /// the device rotated and the app's content followed. False when both captures are in the SAME aspect
    /// category despite the device having been rotated -- the content did not visibly rotate. Null when
    /// either capture has no usable evidence (no screenshot and no usable root bounds) to decide either way --
    /// never guessed.
    /// </summary>
    public static bool? Rotated(ScreenSnapshot before, ScreenSnapshot after)
    {
        var b = IsLandscape(before);
        var a = IsLandscape(after);
        return b is null || a is null ? null : b != a;
    }

    /// <summary>True = wider than tall (landscape-shaped); false = portrait-shaped; null = neither the
    /// screenshot nor the root bounds give a usable (non-zero) size.</summary>
    private static bool? IsLandscape(ScreenSnapshot snapshot)
    {
        if (snapshot.Screenshot is { } shot && shot.Width > 0 && shot.Height > 0)
            return shot.Width > shot.Height;
        var bounds = snapshot.Root.Bounds;
        return bounds.Width > 0 && bounds.Height > 0 ? bounds.Width > bounds.Height : null;
    }
}
