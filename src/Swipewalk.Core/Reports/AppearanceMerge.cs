using Swipewalk.Core.Model;

namespace Swipewalk.Core.Reports;

/// <summary>The two appearance labels used throughout the dark/light rescan, plus the combined label for a
/// finding seen in both.</summary>
public static class AppearanceLabels
{
    public const string Dark = "dark";
    public const string Light = "light";

    /// <summary>The same rule reported the same element in both captures.</summary>
    public const string Both = "both";

    /// <summary>Reported by <c>Swipewalk.Engine.ScanService</c> when the appearance rescan was requested but
    /// the device is a physical iPhone: switching appearance there isn't supported yet (see
    /// docs/limitations.md).</summary>
    public const string PhysicalIphoneNotSupportedReason = "changing appearance on a physical iPhone isn't supported yet";
}

/// <summary>
/// Merges the full rule results from a screen captured in two appearances (<c>ScanOptions.AppearanceBoth</c>)
/// into one <see cref="ScreenResult"/>, tagging every finding with which appearance(s) produced it. Kept as a
/// pure function of two already-run <see cref="ScreenResult"/>s (each produced the ordinary way, by
/// <see cref="Rules.RuleRunner.Run"/> on that appearance's own capture) so it can be unit tested without a
/// device, and so the WCAG mapping, fix guidance and standards relevance already computed per finding are
/// untouched -- appearance changes only which capture(s) a finding is attributed to, never what it means.
/// </summary>
public static class AppearanceMerge
{
    /// <summary>
    /// Rule ids whose findings depend on input the appearance rescan's second capture never has -- a
    /// large-text capture (<c>ScreenSnapshot.LargeText</c>) or a screen-reader capture
    /// (<c>ScreenSnapshot.ScreenReaderCapture</c>), neither of which <c>Swipewalk.Engine.ScanService</c>'s
    /// appearance rescan collects for the second capture. A finding from one of these says nothing about
    /// whether the OTHER appearance has the same issue -- it was simply never tested there -- so tagging it
    /// "only in dark/light appearance" would be a claim the scan never checked. Left with
    /// <see cref="Finding.Appearance"/> null instead -- the same reason ReportComparison.CheckDidNotRun
    /// avoids treating its own large-text/contrast/engine/screen-reader-capture cases as "no longer found"
    /// when the check behind them didn't run (not the identical set of rule ids: that list also covers
    /// text-contrast and engine, which don't need excluding here since the appearance rescan's second
    /// capture does take a screenshot and can run the platform engine).
    /// </summary>
    private static readonly HashSet<string> NotPartOfAppearanceComparison =
        ["text-resize", "text-resize-live", "text-resize-navigation", "large-text-lost-content",
            "screen-reader-capture", "screen-reader-label-in-name"];

    /// <summary>
    /// <paramref name="primary"/> keeps its own screenshot, transcript and every other per-screen field;
    /// <paramref name="other"/> contributes only its findings (merged into <paramref name="primary"/>'s,
    /// each tagged with the appearance that found it) and its screenshot (kept alongside, for the report to
    /// show both). A finding is matched between the two captures the same way
    /// <see cref="ReportComparison.Compare"/> matches findings across two runs: first the same rule, kind and
    /// element (tree path and role), then, failing that, the same rule, kind, role and a non-null matching
    /// label -- so small layout differences between the two captures don't turn one real issue into two, but
    /// two different unlabeled elements (both with a null label) are never matched by label alone. Kind is
    /// part of both matches so a contrast finding that measured as a WCAG issue in one appearance and only
    /// "needs review" in the other is kept as two separate, correctly-labeled findings rather than one that
    /// silently drops the worse of the two.
    /// </summary>
    public static ScreenResult Merge(ScreenResult primary, string primaryAppearance, ScreenResult other, string otherAppearance)
    {
        var unmatched = new List<Finding>(other.Findings);
        var merged = new List<Finding>(primary.Findings.Count);
        foreach (var f in primary.Findings)
        {
            if (NotPartOfAppearanceComparison.Contains(f.RuleId))
            {
                merged.Add(f);
                continue;
            }
            var sameElement = Take(unmatched, o => o.RuleId == f.RuleId && o.Kind == f.Kind && o.NodePath == f.NodePath && o.Role == f.Role)
                ?? Take(unmatched, o => o.RuleId == f.RuleId && o.Kind == f.Kind && o.Role == f.Role && o.Label is not null && o.Label == f.Label);
            merged.Add(f with { Appearance = sameElement is not null ? AppearanceLabels.Both : primaryAppearance });
        }
        merged.AddRange(unmatched.Where(f => !NotPartOfAppearanceComparison.Contains(f.RuleId)).Select(f => f with { Appearance = otherAppearance }));

        return primary with
        {
            Findings = merged,
            Appearance = primaryAppearance,
            OtherAppearance = otherAppearance,
            OtherAppearanceScreenshotPath = other.ScreenshotPath,
            OtherAppearancePixelScale = other.PixelScale,
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
