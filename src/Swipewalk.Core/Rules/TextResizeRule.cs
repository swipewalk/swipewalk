using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// WCAG 1.4.4 Resize Text, partial automated check. Uses the platform's own text-size setting as the
/// resize mechanism (Android font scale 2.0 = 200%; iOS accessibility size AX3, about 235%). Flags text
/// whose element does not get taller and text that newly overlaps other text.
///
/// Text that does not get taller (at all, or nearly not) implies it cannot reach 200% by this mechanism
/// regardless of how far past 200% the tested scale goes, so it is always reported against 1.4.4, as
/// NeedsReview: wrapping, scrolling, Android 14+ nonlinear scaling of large text, or another resize
/// mechanism (e.g. an in-app text size control) can explain or satisfy it.
///
/// Text that newly overlaps at the tested scale is reported against 1.4.4 (NeedsReview) only when the
/// tested scale is at or below 200% (Android font scale 2.0): 1.4.4 asks for 200%, so overlap seen at or
/// below that level plausibly implies overlap at 200% too. Above 200% (iOS AX3, about 235%) it is a
/// PlatformAdvisory citing Apple's Human Interface Guidelines instead: WCAG 1.4.4 does not require text to
/// keep growing without clipping past 200%, so an overlap seen only there is not itself a WCAG failure.
///
/// Does not test 1.4.10 Reflow or 1.4.12 Text Spacing.
/// </summary>
public sealed class TextResizeRule : IRule
{
    /// <summary>Below this height ratio the text is considered not to have grown.</summary>
    public const double MinimumGrowth = 1.15;

    /// <summary>WCAG 1.4.4 Resize Text asks for 200%; above this, growth/clipping findings are platform advisories.</summary>
    public const double WcagScale = 2.0;

    /// <summary>Cited on overlap findings seen only beyond the WCAG 1.4.4 scale (200%).</summary>
    public const string DynamicTypeGuideline =
        "Apple Human Interface Guidelines: support Dynamic Type, including accessibility sizes";

    /// <summary>
    /// When at least this many texts were compared and at least <see cref="ScreenLevelShare"/> of them did not
    /// grow, the app did not apply the setting at all (system-drawn titles may still grow): one finding.
    /// </summary>
    private const int ScreenLevelMinimum = 3;
    private const double ScreenLevelShare = 0.8;

    public string Id => "text-resize";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        if (snapshot.LargeText is not { } large)
            yield break;

        var normalTexts = TextNodes(snapshot.Root);
        var largeTexts = TextNodes(large.Root);
        var pairs = Pair(normalTexts, largeTexts);
        var setting = snapshot.LargeTextSetting ?? "enlarged system text";
        var unit = snapshot.Platform switch { Platform.Android => "dp", Platform.iOS => "pt", _ => "DIP" };
        var evidence = new Dictionary<string, string> { ["screenshot"] = "largeText" };
        // Unknown scale (older captures without it) is treated as at-or-below 200%, the same as today: there's
        // no evidence it went beyond what 1.4.4 asks for.
        var overlapIsWcagIssue = snapshot.LargeTextScale is not (> WcagScale);

        var notGrown = pairs.Where(p => p.Large.Node.Bounds.Height / p.Normal.Node.Bounds.Height < MinimumGrowth).ToList();
        var screenLevel = pairs.Count >= ScreenLevelMinimum && notGrown.Count >= pairs.Count * ScreenLevelShare;
        if (screenLevel)
        {
            // Almost nothing grew: the app did not apply the setting while running (seen with .NET MAUI on Android
            // and iOS, which apply it after a restart). One finding instead of one per text.
            //
            // The base sentence differs by whether a restart was actually captured and still didn't help
            // (snapshot.LargeTextRestartCaptured == true, set once a collector's force-stop + relaunch comparison
            // completed): saying "may apply only after it restarts" and "relaunch and check" would contradict what
            // was observed if the app already was relaunched and the text still didn't grow. This applies to any
            // platform/framework, since it states what was observed, not a cause. Text that doesn't grow at the
            // tested scale doesn't grow at 200% either (unlike overlap/clipping, which can depend on how far
            // past 200% the tested scale goes -- see overlapIsWcagIssue below): the manual check is always
            // against 200%, the level WCAG 1.4.4 asks for, regardless of the tested scale ({setting}).
            var restartConfirmedNoGrowth = snapshot.LargeTextRestartCaptured == true;
            var message = restartConfirmedNoGrowth
                ? $"{notGrown.Count} of the {pairs.Count} texts on this screen did not get taller when the system text size was changed to {setting}, " +
                  "even after the app was restarted. Check this screen at 200% text size by hand."
                : $"{notGrown.Count} of the {pairs.Count} texts on this screen did not get taller when the system text size was changed to {setting} " +
                  "while the app was running. The app may apply the new size only after it restarts, or may not support it. " +
                  "Relaunch the app at 200% text size and check that text grows and is not clipped.";
            var screenEvidence = evidence;

            // Only .NET MAUI on iOS has a confirmed, sourced cause list for this (see TextResizeLiveUpdateRule).
            // Whether restarting the app was actually confirmed not to help changes which causes apply: confirmed
            // rules out the pre-10.0.100 known issue (that grows after a restart), unconfirmed cannot rule it out,
            // so both are listed with the uncertainty stated.
            if (snapshot.Platform == Platform.iOS && snapshot.Framework == AppFramework.Maui)
            {
                message += restartConfirmedNoGrowth
                    ? " On iOS with .NET MAUI, likely causes are FontAutoScalingEnabled=\"False\" set widely (for example " +
                      "in a global Style) or on this text; Shell tab bar titles also don't grow in tested versions."
                    : MauiIosCouldNotConfirmCause(snapshot.FrameworkVersion);
                screenEvidence = new Dictionary<string, string>(evidence)
                {
                    ["largeTextRestartCaptured"] = restartConfirmedNoGrowth ? "true" : "false",
                };
            }

            yield return new Finding
            {
                RuleId = Id,
                Kind = FindingKind.NeedsReview,
                Message = message,
                Criteria = [WcagCriteria.ResizeText],
                NodePath = "",
                Role = "screen",
                Details = screenEvidence,
            };
        }

        // Per-text "did not grow" findings only when the app did apply the setting; overlaps always.
        foreach (var (normal, enlarged) in screenLevel ? [] : notGrown)
        {
            yield return RuleFinding.Create(Id, FindingKind.NeedsReview,
                $"Text \"{normal.Node.VisibleText}\" did not get noticeably taller with {setting} (height {normal.Node.Bounds.Height:0} → " +
                $"{enlarged.Node.Bounds.Height:0} {unit}). It may not scale with the system text size, or may be clipped. " +
                "Check it at 200% text size; another resize mechanism in the app can also satisfy 1.4.4.",
                enlarged.Node, enlarged.Path, [WcagCriteria.ResizeText]) with { Details = evidence };
        }

        var reported = new HashSet<string>();
        foreach (var (a, largeA) in pairs)
            foreach (var (b, largeB) in pairs)
            {
                if (string.CompareOrdinal(a.Path, b.Path) >= 0 || !Overlaps(largeA.Node.Bounds, largeB.Node.Bounds)
                    || Overlaps(a.Node.Bounds, b.Node.Bounds) || !reported.Add(largeA.Path))
                    continue;
                yield return overlapIsWcagIssue
                    ? RuleFinding.Create(Id, FindingKind.NeedsReview,
                        $"Text \"{a.Node.VisibleText}\" overlaps \"{b.Node.VisibleText}\" with {setting} but not at normal size; check whether " +
                        "content is hidden (this can also be a sticky header or toolbar over scrolled content).",
                        largeA.Node, largeA.Path, [WcagCriteria.ResizeText]) with { Details = evidence }
                    // Overlap seen only beyond the 200% WCAG 1.4.4 asks for: an advisory against the platform's
                    // own larger accessibility sizes, not itself a WCAG failure.
                    : RuleFinding.Create(Id, FindingKind.PlatformAdvisory,
                        $"At {setting} (beyond the 200% WCAG 1.4.4 Resize Text asks for), text \"{a.Node.VisibleText}\" overlaps " +
                        $"\"{b.Node.VisibleText}\" but not at normal size; check whether content is hidden (this can also be a sticky " +
                        "header or toolbar over scrolled content).",
                        largeA.Node, largeA.Path, guideline: DynamicTypeGuideline) with { Details = evidence };
            }
    }

    /// <summary>
    /// The iOS + MAUI cause sentence used when a restart wasn't confirmed to help or not
    /// (<c>restartConfirmedNoGrowth</c> false), selected by whether the app's own MAUI version is known and
    /// whether it is before or at/after the version that fixed dotnet/maui#34445 (see
    /// <see cref="FrameworkVersions"/>). Unknown keeps the original wording, which can't rule out the
    /// pre-10.0.100 restart issue; a known version at or after the fix rules it out, and a known version before
    /// it keeps both possible causes, since the fix and the "still doesn't grow" causes are independent here.
    /// </summary>
    private static string MauiIosCouldNotConfirmCause(string? frameworkVersion)
    {
        var version = FrameworkVersions.Parse(frameworkVersion);
        if (version is null)
            return " On iOS with .NET MAUI, the scan could not confirm whether restarting the app would make this text " +
                   "grow, so this could be an older .NET MAUI version (before 10.0.100; fixed in 10.0.100, dotnet/maui#34445) " +
                   "that only applies the larger size after a restart, or FontAutoScalingEnabled=\"False\" or an unscaled " +
                   "control (for example a Shell tab bar title) that would not grow even after a restart.";
        return version >= FrameworkVersions.MauiLiveTextSizeFix
            ? $" This app uses .NET MAUI {frameworkVersion}, which includes the fix for live text-size changes " +
              "(dotnet/maui#34445). The scan could not confirm whether restarting the app would make this text grow, " +
              "so FontAutoScalingEnabled=\"False\" or an unscaled control (for example a Shell tab bar title) is the likely cause."
            : $" This app uses .NET MAUI {frameworkVersion}, an older version affected by a known issue (before 10.0.100; " +
              "fixed in 10.0.100, dotnet/maui#34445) that normally only applies the larger text size after a restart. The " +
              "scan could not confirm whether restarting the app would make this text grow, so that known issue, or " +
              "FontAutoScalingEnabled=\"False\" or an unscaled control (for example a Shell tab bar title), are both " +
              "possible causes.";
    }

    private static List<(AccessibilityNode Node, string Path)> TextNodes(AccessibilityNode root) =>
        root.DescendantsAndSelfWithPath()
            .Where(e => e.Node.Role == "text" && !string.IsNullOrWhiteSpace(e.Node.VisibleText) && RuleFinding.HasArea(e.Node))
            .ToList();

    /// <summary>Pairs text elements by their text, in order; unmatched ones (e.g. scrolled away) are ignored.</summary>
    private static List<((AccessibilityNode Node, string Path) Normal, (AccessibilityNode Node, string Path) Large)> Pair(
        List<(AccessibilityNode Node, string Path)> normal, List<(AccessibilityNode Node, string Path)> large)
    {
        var remaining = large.ToList();
        var pairs = new List<((AccessibilityNode, string), (AccessibilityNode, string))>();
        foreach (var n in normal)
        {
            var index = remaining.FindIndex(l => l.Node.VisibleText == n.Node.VisibleText);
            if (index < 0)
                continue;
            pairs.Add((n, remaining[index]));
            remaining.RemoveAt(index);
        }
        return pairs;
    }

    /// <summary>True when the rectangles share more than a sliver (2 units) in both directions.</summary>
    private static bool Overlaps(Bounds a, Bounds b) =>
        Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X) > 2
        && Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y) > 2;
}
