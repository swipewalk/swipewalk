using Swipewalk.Core.Model;

namespace Swipewalk.Core.Rules;

/// <summary>
/// Reports when the enlarged system text size only took effect after the app under test was terminated and
/// relaunched, not while it kept running (<see cref="ScreenSnapshot.LargeTextAppliedLive"/> is false). Seen
/// on iOS, where the collector escalates to a restart and reports what it measured; on Android, only when
/// the collector observed that the text grew after a relaunch, having not grown while the app kept running
/// (Android still reports null, not false, when its own default activity-recreation behavior on a
/// font-scale change makes "live" ambiguous -- see the "android-font-scale-restart" limitation).
///
/// WCAG 1.4.4 Resize Text asks for text to reach 200% by some mechanism; it does not require that mechanism
/// to apply without restarting the app. So this is always a platform advisory, never a WCAG finding: against
/// Apple's Human Interface Guidelines for Dynamic Type on iOS, or Android's developer documentation for
/// handling configuration changes on Android. One finding per screen.
///
/// Only fires when the after-restart capture (<see cref="ScreenSnapshot.LargeText"/>) actually shows the
/// text grew (see <see cref="AfterRestartCaptureGrew"/>): otherwise the app never applied the setting at
/// all, and <see cref="TextResizeRule"/> already reports that as a 1.4.4 finding ("did not get taller").
/// Claiming here that a restart fixed it, while that rule reports the text still isn't taller, would
/// contradict it. Both rules use the same <see cref="TextResizeRule.MinimumGrowth"/> threshold and the same
/// median-of-matched-texts measure (mirroring <c>Swipewalk.Collectors.TextGrowth</c>, which Core cannot
/// reference) so they can never disagree about whether a capture shows growth.
/// </summary>
public sealed class TextResizeLiveUpdateRule : IRule
{
    /// <summary>
    /// Cited on Android findings: the "React to configuration changes" section of
    /// developer.android.com/guide/topics/resources/runtime-changes, which lists changing the font size as a
    /// configuration change and explains that an activity declaring it handles a change itself must update
    /// its UI (here: re-apply text sizes). Distinct from
    /// <see cref="TextResizeNavigationRule.AndroidPreserveStateGuideline"/> (preserving UI state across
    /// recreation); there is no Android equivalent of Apple's Dynamic Type guideline to cite instead.
    /// </summary>
    public const string AndroidConfigChangesGuideline =
        "Android developer documentation: Handle configuration changes -- react to configuration changes the activity handles itself (font size is one)";

    public string Id => "text-resize-live";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        if (snapshot.LargeText is null || snapshot.LargeTextAppliedLive != false || !AfterRestartCaptureGrew(snapshot))
            yield break;

        var message = "The text didn't change size while the app was running; it did after the app was restarted. " +
                      "People who change the text size while using the app see the old size until they restart it.";

        // Only .NET MAUI has a confirmed, sourced cause for this behavior, and it differs by platform. Other
        // frameworks get the same measurement without a claimed cause; on Android they still get a neutral,
        // non-causal sentence about what was observed, since the finding itself is Android-specific evidence
        // (the screen didn't pick up the new font scale) even when the cause in this app's code is unknown.
        message += (snapshot.Platform, snapshot.Framework) switch
        {
            (Platform.iOS, AppFramework.Maui) => MauiIosLiveUpdateCause(snapshot.FrameworkVersion),
            (Platform.Android, AppFramework.Maui) =>
                " In .NET MAUI on Android, a likely cause is MainActivity handling font-scale changes itself " +
                "(ConfigChanges.FontScale in ConfigurationChanges) without updating its text.",
            (Platform.Android, _) =>
                " The screen did not refresh when the system font scale changed.",
            _ => "",
        };

        var guideline = snapshot.Platform == Platform.Android
            ? AndroidConfigChangesGuideline
            : TextResizeRule.DynamicTypeGuideline;

        yield return new Finding
        {
            RuleId = Id,
            Kind = FindingKind.PlatformAdvisory,
            Message = message,
            PlatformGuideline = guideline,
            NodePath = "",
            Role = "screen",
            Details = new Dictionary<string, string> { ["screenshot"] = "largeText" },
        };
    }

    /// <summary>
    /// The iOS + MAUI cause sentence, selected by whether the app's own MAUI version is known
    /// (<see cref="ScreenSnapshot.FrameworkVersion"/>) and whether it is before or at/after the version that
    /// fixed dotnet/maui#34445 (see <see cref="FrameworkVersions"/>). Unknown keeps the original wording,
    /// which cannot rule out either cause; a known version states which one applies.
    /// </summary>
    private static string MauiIosLiveUpdateCause(string? frameworkVersion)
    {
        var version = FrameworkVersions.Parse(frameworkVersion);
        if (version is null)
            return " In .NET MAUI before 10.0.100 this is a known issue, fixed in 10.0.100 (dotnet/maui#34445). Swipewalk " +
                   "can't read the app's MAUI version; check the Microsoft.Maui.Controls version in the project. If the " +
                   "app already uses 10.0.100 or later, custom handlers or code that sets FontSize may be the cause.";
        return version < FrameworkVersions.MauiLiveTextSizeFix
            ? $" This app uses .NET MAUI {frameworkVersion}. In .NET MAUI before 10.0.100 this is a known issue, fixed in " +
              "10.0.100 (dotnet/maui#34445); updating Microsoft.Maui.Controls to 10.0.100 or later should make standard " +
              "controls apply the new size while the app runs; rescan to confirm."
            : $" This app uses .NET MAUI {frameworkVersion}, which includes the fix for live text-size changes " +
              "(dotnet/maui#34445), so custom handlers or code that sets FontSize are the likely cause.";
    }

    /// <summary>
    /// True when the median height ratio across texts paired between <paramref name="snapshot"/>'s normal
    /// capture and its after-restart large-text capture meets <see cref="TextResizeRule.MinimumGrowth"/>.
    /// False (no evidence of growth) when there is nothing to compare. Mirrors
    /// <c>Swipewalk.Collectors.TextGrowth.LooksGrown</c>'s measure, which Core cannot reference because it
    /// lives in Swipewalk.Collectors; using the same threshold and the same median-based measure means this
    /// can never say a screen grew when <see cref="TextResizeRule"/>'s "did not get taller" finding covers
    /// most/all of the same texts (that finding requires at least 80% of at least 3 paired texts to be
    /// under the threshold, so their median -- and this check -- also falls under it).
    /// </summary>
    private static bool AfterRestartCaptureGrew(ScreenSnapshot snapshot)
    {
        if (snapshot.LargeText is not { } large)
            return false;

        var ratios = Pair(TextNodes(snapshot.Root), TextNodes(large.Root))
            .Select(p => p.Large.Bounds.Height / p.Normal.Bounds.Height)
            .OrderBy(r => r)
            .ToList();
        if (ratios.Count == 0)
            return false;

        var median = ratios[ratios.Count / 2];
        return median >= TextResizeRule.MinimumGrowth;
    }

    /// <summary>Text elements with visible text and non-zero bounds. Mirrors <see cref="TextResizeRule"/>'s node filter.</summary>
    private static List<AccessibilityNode> TextNodes(AccessibilityNode root) =>
        root.DescendantsAndSelf()
            .Where(n => n.Role == "text" && !string.IsNullOrWhiteSpace(n.VisibleText) && RuleFinding.HasArea(n))
            .ToList();

    /// <summary>Pairs text elements by their text, in order; unmatched ones (e.g. scrolled away) are ignored.
    /// Mirrors <see cref="TextResizeRule"/>'s own pairing.</summary>
    private static List<(AccessibilityNode Normal, AccessibilityNode Large)> Pair(
        List<AccessibilityNode> normal, List<AccessibilityNode> large)
    {
        var remaining = large.ToList();
        var pairs = new List<(AccessibilityNode, AccessibilityNode)>();
        foreach (var n in normal)
        {
            var index = remaining.FindIndex(l => l.VisibleText == n.VisibleText);
            if (index < 0)
                continue;
            pairs.Add((n, remaining[index]));
            remaining.RemoveAt(index);
        }
        return pairs;
    }
}
