using Swipewalk.Core.Model;

namespace Swipewalk.Core.Reports;

/// <summary>The two orientation labels used throughout the orientation rescan, plus the combined label for a
/// finding seen in both.</summary>
public static class OrientationLabels
{
    public const string Portrait = "portrait";
    public const string Landscape = "landscape";

    /// <summary>The same rule reported the same element in both captures.</summary>
    public const string Both = "both";

    /// <summary>Reported by <c>Swipewalk.Engine.ScanService</c> when the orientation rescan was requested but
    /// the device is a physical iPhone: rotating it through Swipewalk isn't supported yet (see
    /// docs/limitations.md).</summary>
    public const string PhysicalIphoneNotSupportedReason = "rotating a physical iPhone isn't supported yet";
}

/// <summary>
/// Merges the full rule results from a screen captured in two orientations (<c>ScanOptions.OrientationBoth</c>)
/// into one <see cref="ScreenResult"/>, tagging every finding with which orientation(s) produced it. Only
/// used when the device actually rotated (see <see cref="Model.OrientationChangeDetector"/>); when it did
/// not, <c>Swipewalk.Engine.ScanService</c> keeps the primary screen's own findings (plus the
/// <c>orientation-restricted</c> finding from <see cref="Rules.OrientationRestrictedRule"/>) and never calls
/// this. Kept as a pure function of two already-run <see cref="ScreenResult"/>s (each produced the ordinary
/// way, by <see cref="Rules.RuleRunner.Run"/> on that orientation's own capture) so it can be unit tested
/// without a device, and so the WCAG mapping, fix guidance and standards relevance already computed per
/// finding are untouched -- orientation changes only which capture(s) a finding is attributed to, never what
/// it means. Modeled directly on <see cref="AppearanceMerge"/>; see its remarks for the matching rules this
/// mirrors exactly.
/// </summary>
public static class OrientationMerge
{
    /// <summary>
    /// Rule ids whose findings depend on input the orientation rescan's second capture never has -- a
    /// large-text capture (<c>ScreenSnapshot.LargeText</c>) or a screen-reader capture
    /// (<c>ScreenSnapshot.ScreenReaderCapture</c>), neither of which <c>Swipewalk.Engine.ScanService</c>'s
    /// orientation rescan collects for the second capture. Also excludes <c>orientation-restricted</c>
    /// itself: it only ever fires on the primary capture (via <see cref="ScreenSnapshot.Orientation"/>), and
    /// never fires at all once this merge runs (the device rotated). Same list (minus that last one)
    /// <see cref="AppearanceMerge.NotPartOfAppearanceComparison"/> and <c>ReportComparison.CheckDidNotRun</c> use
    /// for the same reason.
    /// </summary>
    private static readonly HashSet<string> NotPartOfOrientationComparison =
        ["text-resize", "text-resize-live", "text-resize-navigation", "large-text-lost-content", "screen-reader-capture", "orientation-restricted"];

    /// <summary>
    /// <paramref name="primary"/> keeps its own screenshot, transcript and every other per-screen field;
    /// <paramref name="other"/> contributes only its findings (merged into <paramref name="primary"/>'s,
    /// each tagged with the orientation that found it) and its screenshot (kept alongside, for the report to
    /// show both). Matching between the two captures follows <see cref="AppearanceMerge.Merge"/> exactly:
    /// first the same rule, kind and element (tree path and role), then, failing that, the same rule, kind,
    /// role and a non-null matching label -- a rotated layout can shuffle tree paths without being a new issue.
    /// </summary>
    public static ScreenResult Merge(ScreenResult primary, string primaryOrientation, ScreenResult other, string otherOrientation)
    {
        var unmatched = new List<Finding>(other.Findings);
        var merged = new List<Finding>(primary.Findings.Count);
        foreach (var f in primary.Findings)
        {
            if (NotPartOfOrientationComparison.Contains(f.RuleId))
            {
                merged.Add(f);
                continue;
            }
            var sameElement = Take(unmatched, o => o.RuleId == f.RuleId && o.Kind == f.Kind && o.NodePath == f.NodePath && o.Role == f.Role)
                ?? Take(unmatched, o => o.RuleId == f.RuleId && o.Kind == f.Kind && o.Role == f.Role && o.Label is not null && o.Label == f.Label);
            merged.Add(f with { Orientation = sameElement is not null ? OrientationLabels.Both : primaryOrientation });
        }
        merged.AddRange(unmatched.Where(f => !NotPartOfOrientationComparison.Contains(f.RuleId)).Select(f => f with { Orientation = otherOrientation }));

        return primary with
        {
            Findings = merged,
            Orientation = primaryOrientation,
            OtherOrientation = otherOrientation,
            OtherOrientationScreenshotPath = other.ScreenshotPath,
            OtherOrientationPixelScale = other.PixelScale,
            OrientationUnchanged = false,
        };
    }

    private static Finding? Take(List<Finding> pool, Func<Finding, bool> match)
    {
        var index = pool.FindIndex(f => match(f));
        if (index < 0)
            return null;
        var found = pool[index];
        pool.RemoveAt(index);
        return found;
    }
}
