using Swipewalk.Core.Model;

namespace Swipewalk.Core.Rules;

/// <summary>
/// Reports when a system text-size change made the app return to a different screen than the one being
/// captured -- typically the app's own first/launch screen (<see cref="ScreenSnapshot.LargeTextWentToAnotherScreen"/>
/// is true). Seen on Android with the default .NET MAUI template: MainActivity's ConfigurationChanges list
/// omits FontScale, so a font-scale change recreates the activity and the app rebuilds its first page
/// (App.CreateWindow). Verified 2026-09-23 on the emulator with BuggyApp (.NET MAUI 10.0.110, a
/// NavigationPage app).
///
/// This finding is about the lost navigation place only. Whether text on any screen reaches the larger size
/// is a separate question, reported by <see cref="TextResizeRule"/> and <see cref="TextResizeLiveUpdateRule"/>
/// -- this rule does not claim an answer either way. WCAG 1.4.4 Resize Text was considered for this finding
/// and not mapped: this finding does not show lost content or lost input, only a navigation reset, and 1.4.4
/// does not ask an app to keep its navigation state across a system text-size change. Reported as a platform
/// advisory against Android's own guidance for preserving UI state across activity recreation.
///
/// Restricted to Android: the evidence above is Android-specific (the default .NET MAUI Android template),
/// and Swipewalk has not verified an equivalent scenario, or a fitting platform-guideline citation, on iOS --
/// so this rule does not fire there, rather than guess a cause or a citation. When a collector sets
/// <see cref="ScreenSnapshot.LargeTextWentToAnotherScreen"/>, it is expected to be for Android captures only;
/// the platform check here is a safeguard in case a future snapshot sets it on another platform.
///
/// Wired up in <c>Swipewalk.Engine.Recorder</c> (record mode, from
/// <c>Swipewalk.Collectors.LargeTextCapture.WentToAnotherScreenLive</c>) and <c>Swipewalk.Engine.ScanService</c>
/// (scan mode's equivalent: the same activity-recreation-on-first-attempt signal, reported there as
/// <c>Swipewalk.Collectors.LargeTextCapture.DifferentScreen</c> before any deliberate restart -- see each for
/// exactly where). Set on whichever single screen actually exhibited it, never on a
/// later screen just because an earlier one did (see each wiring site's own remarks), so a report/run can
/// only ever have this finding on the one screen where it was observed -- there is no separate once-per-run
/// de-duplication to do here.
/// </summary>
public sealed class TextResizeNavigationRule : IRule
{
    /// <summary>
    /// Cited on this rule's findings: the same Android developer-documentation page as
    /// <see cref="TextResizeLiveUpdateRule.AndroidConfigChangesGuideline"/>, but the section on preserving UI
    /// state across a configuration-triggered activity recreation, which is what this rule is about (not
    /// re-applying resources, which is <see cref="TextResizeLiveUpdateRule"/>'s concern).
    /// </summary>
    public const string AndroidPreserveStateGuideline =
        "Android developer documentation: Handle configuration changes -- preserve UI state across activity recreation";

    public string Id => "text-resize-navigation";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        if (snapshot.Platform != Platform.Android || snapshot.LargeTextWentToAnotherScreen != true)
            yield break;

        yield return new Finding
        {
            RuleId = Id,
            Kind = FindingKind.PlatformAdvisory,
            // "Captured a different screen from this one", not "the app showed a different screen": the
            // signal behind LargeTextWentToAnotherScreen (ScreenIdentity.IsSameScreen, matching on the
            // screen's title/element names) is a heuristic, and at a larger text size truncated text or
            // elements pushed off screen can look like a different screen even when it isn't. "Probably lost
            // their place", not "lost their place", for the same reason (wcag-reviewer, 2026-09-23).
            Message = "After the system text size changed, Swipewalk captured a different screen from this " +
                      "one (typically the app's first screen), so the person probably lost their place and " +
                      "has to navigate back. This finding is about the lost place only; whether this screen's " +
                      "text grows at the larger size is reported separately.",
            PlatformGuideline = AndroidPreserveStateGuideline,
            NodePath = "",
            Role = "screen",
        };
    }
}
