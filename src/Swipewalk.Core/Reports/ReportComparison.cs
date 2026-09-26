using Swipewalk.Core.Model;

namespace Swipewalk.Core.Reports;

/// <summary>
/// A finding and the screen it was found on. <paramref name="Reason"/> is set for items in
/// <see cref="ReportComparison.NotCheckedAgain"/>, to say why the finding could not be compared: the screen wasn't
/// scanned again, or the check that found it didn't run this time.
/// </summary>
public sealed record ScreenFinding(string ScreenName, Finding Finding, string? Reason = null);

/// <summary>
/// What changed between two runs of the same app. A finding on a screen that wasn't scanned again is not "no
/// longer found" — it is listed under <see cref="NotCheckedAgain"/> instead, since we never looked again to know.
/// "No longer found" means the automated checks didn't report it this time, not that the issue was fixed.
/// </summary>
public sealed record ReportComparison
{
    public required IReadOnlyList<ScreenFinding> New { get; init; }
    public required IReadOnlyList<ScreenFinding> NoLongerFound { get; init; }
    public required IReadOnlyList<ScreenFinding> StillFound { get; init; }

    /// <summary>
    /// Earlier findings that can't be called "no longer found" because they were never looked at again this time:
    /// either the screen they were on wasn't scanned again, or (on a screen that was scanned again) the check that
    /// found them didn't run this time (e.g. the large-text check when no large-text capture was made). Each item's
    /// <see cref="ScreenFinding.Reason"/> says which.
    /// </summary>
    public IReadOnlyList<ScreenFinding> NotCheckedAgain { get; init; } = [];

    /// <summary>The reason text for a finding whose screen wasn't scanned again in the later run.</summary>
    public const string ScreenNotScannedAgainReason = "This screen was not scanned again, so this finding was not checked.";

    /// <summary>The reason text for a finding whose check didn't run again on a screen that was scanned again.</summary>
    public const string CheckDidNotRunReason = "This check did not run on this screen this time, so this finding was not checked.";

    /// <summary>Screens in the earlier run only; none of their findings could be compared (see <see cref="NotCheckedAgain"/>).</summary>
    public required IReadOnlyList<string> ScreensNotScannedAgain { get; init; }

    /// <summary>
    /// Screens in the later run only; their findings are all in <see cref="New"/>. This means the screen is new to
    /// this comparison (it wasn't reached in the earlier run, or wasn't reached before it was scanned), not
    /// necessarily that it is a new problem in the app.
    /// </summary>
    public required IReadOnlyList<string> NewScreens { get; init; }

    /// <summary>
    /// Compares two reports. Findings are matched per screen and rule, first on the same element (tree path and
    /// role), then on role and label, so small layout changes don't turn one issue into a "new" and a "no longer
    /// found" pair.
    /// </summary>
    public static ReportComparison Compare(ScanReport earlier, ScanReport later)
    {
        var withLargeText = later.Screens.Where(s => s.LargeTextSetting is not null).Select(s => s.ScreenName).ToHashSet();
        // Per-element contrast needs a usable screenshot; a blocked one gives a single screen-level finding instead.
        var withContrast = later.Screens
            .Where(s => s.ScreenshotPath is not null && !s.Findings.Any(f => f.RuleId == "text-contrast" && f.Role == "screen"))
            .Select(s => s.ScreenName).ToHashSet();
        // Whether the platform engine (Apple's audit) ran isn't recorded; a screen with no engine findings at all is
        // treated as not checked, so a failed audit can't make its earlier findings look "no longer found".
        var withEngine = later.Screens.Where(s => s.Findings.Any(f => f.RuleId.StartsWith("engine", StringComparison.Ordinal)))
            .Select(s => s.ScreenName).ToHashSet();
        // screen-reader-capture and screen-reader-label-in-name both need a real screen-reader capture
        // (--screen-reader, Android only); comparing a run that had one against a later run without it must
        // not read the earlier findings as "no longer found" -- the check simply didn't run this time.
        // screen-reader-label-in-name additionally needs the capture to be complete (see its own remarks).
        var withScreenReaderCapture = later.Screens.Where(s => s.ScreenReaderCapture is { Items.Count: > 0 })
            .Select(s => s.ScreenName).ToHashSet();
        var withCompleteScreenReaderCapture = later.Screens.Where(s => s.ScreenReaderCapture is { Items.Count: > 0, Complete: true })
            .Select(s => s.ScreenName).ToHashSet();
        var before = earlier.Screens.GroupBy(s => s.ScreenName).ToDictionary(g => g.Key, g => g.SelectMany(s => s.Findings).ToList());
        var after = later.Screens.GroupBy(s => s.ScreenName).ToDictionary(g => g.Key, g => g.SelectMany(s => s.Findings).ToList());

        List<ScreenFinding> added = [], gone = [], kept = [], notChecked = [];
        foreach (var (screen, findings) in after)
        {
            if (!before.TryGetValue(screen, out var old))
            {
                added.AddRange(findings.Select(f => new ScreenFinding(screen, f)));
                continue;
            }

            var unmatched = new List<Finding>(old);
            var remaining = new List<Finding>();
            foreach (var f in findings)
            {
                if (Take(unmatched, o => o.RuleId == f.RuleId && o.NodePath == f.NodePath && o.Role == f.Role))
                    kept.Add(new ScreenFinding(screen, f));
                else
                    remaining.Add(f);
            }
            foreach (var f in remaining)
            {
                if (Take(unmatched, o => o.RuleId == f.RuleId && o.Role == f.Role && o.Label == f.Label))
                    kept.Add(new ScreenFinding(screen, f));
                else
                    added.Add(new ScreenFinding(screen, f));
            }
            foreach (var f in unmatched)
            {
                if (CheckDidNotRun(f, screen))
                    notChecked.Add(new ScreenFinding(screen, f, CheckDidNotRunReason));
                else
                    gone.Add(new ScreenFinding(screen, f));
            }
        }

        // A screen scanned earlier but not this time was never looked at again, so none of its findings can be
        // called "no longer found" (that would claim the issue is gone) or matched against a check that did or
        // didn't run this time (there's nothing this time to check against).
        foreach (var screen in before.Keys.Where(k => !after.ContainsKey(k)))
            notChecked.AddRange(before[screen].Select(f => new ScreenFinding(screen, f, ScreenNotScannedAgainReason)));

        bool CheckDidNotRun(Finding f, string screen) => f.RuleId switch
        {
            "text-resize" => !withLargeText.Contains(screen),
            "text-contrast" => f.Role != "screen" && !withContrast.Contains(screen),
            _ when f.RuleId.StartsWith("engine", StringComparison.Ordinal) => !withEngine.Contains(screen),
            _ when f.RuleId.StartsWith("screen-reader-capture", StringComparison.Ordinal) => !withScreenReaderCapture.Contains(screen),
            "screen-reader-label-in-name" => !withCompleteScreenReaderCapture.Contains(screen),
            _ => false,
        };

        return new ReportComparison
        {
            New = added,
            NoLongerFound = gone,
            StillFound = kept,
            NotCheckedAgain = notChecked,
            ScreensNotScannedAgain = [.. before.Keys.Where(k => !after.ContainsKey(k))],
            NewScreens = [.. after.Keys.Where(k => !before.ContainsKey(k))],
        };
    }

    private static bool Take(List<Finding> pool, Func<Finding, bool> match)
    {
        var index = pool.FindIndex(f => match(f));
        if (index < 0)
            return false;
        pool.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// One line for a finding in lists, e.g. "Screen 1 · Button "Help": ... (WCAG 2.5.8)". Items in
    /// <see cref="NotCheckedAgain"/> carry a <see cref="ScreenFinding.Reason"/>, appended so the reader can tell a
    /// screen that wasn't scanned again from a check that didn't run on a screen that was.
    /// </summary>
    public static string Describe(ScreenFinding item)
    {
        var f = item.Finding;
        var element = string.IsNullOrEmpty(f.Label) ? f.Role : $"{f.Role} \"{f.Label}\"";
        var criteria = f.Criteria.Count > 0 ? $" (WCAG {string.Join(", ", f.Criteria.Select(c => c.Number))})"
            : f.Kind == FindingKind.PlatformAdvisory ? "" : " (No WCAG criterion mapped)";
        var reason = item.Reason is null ? "" : $" — {item.Reason}";
        return $"{item.ScreenName} · {element}: {f.Message}{criteria}{reason}";
    }
}
