using Swipewalk.Core.Model;

namespace Swipewalk.Engine;

/// <summary>
/// What Recorder (and ScanService) does when a large-text
/// check finds that the larger size needs a restart to show (see
/// <see cref="Swipewalk.Collectors.LargeTextCapture.DidNotGrowLive"/> and
/// <see cref="Swipewalk.Collectors.LargeTextCapture.WentToAnotherScreenLive"/>): <see cref="Ask"/> the person
/// each time (the default when a console or the desktop app can ask), <see cref="Always"/> check without
/// asking, or <see cref="Never"/> don't check (an honest reason, without touching the app). Resolved once per
/// run from <c>--large-text-restart</c> / swipewalk.json's <c>largeTextRestart</c>, or the desktop app's own
/// default (see <see cref="LargeTextRestartPolicies.Default"/> for what "default" means for each of scan and
/// record); choosing "always check"/"never check" mid-recording (see <see cref="LargeTextRestartChoice"/>)
/// updates it for the rest of the session, so the person is asked at most once unless they want to keep
/// deciding screen by screen -- a scan is one screen, so that distinction doesn't apply there.
/// </summary>
public enum LargeTextRestartPolicy { Ask, Always, Never }

/// <summary>
/// The person's answer to whether to check a screen at the larger size, once the recording knows doing so
/// needs a restart (see Recorder). <see cref="AlwaysRestart"/>/<see cref="AlwaysSkip"/> also update <see cref="LargeTextRestartPolicy"/>
/// for the rest of the recording. Nothing here is a one-way choice: it can be changed again at any time.
/// </summary>
public enum LargeTextRestartChoice { RestartAndCheck, SkipForThisScreen, AlwaysRestart, AlwaysSkip }

/// <summary>
/// Asks the person what to do (see <see cref="LargeTextRestartChoice"/>) for one screen; called only while the
/// policy is <see cref="LargeTextRestartPolicy.Ask"/>. The CLI prompts on the console; the desktop app shows a
/// dialog on the New scan page. For the screen that first taught this recording that a restart is needed,
/// this asks right after that live attempt (<paramref name="learnedFromEarlierScreen"/> false); for every
/// later screen it asks before the text size is touched at all, since the cost is already known
/// (<paramref name="learnedFromEarlierScreen"/> true -- see <see cref="LargeTextAskWording.WhatHappened"/> for
/// how the wording differs). <paramref name="screenName"/> is shown so the person knows what they would
/// navigate back to; <paramref name="reason"/> is <see cref="Swipewalk.Collectors.LargeTextCapture.DidNotGrowLive"/>
/// or <see cref="Swipewalk.Collectors.LargeTextCapture.WentToAnotherScreenLive"/>, so the prompt's wording is
/// driven by what actually happened (the text stayed the same size; or the screen changed on its own) rather
/// than guessed from the framework version. <paramref name="platform"/> only picks between the specific
/// Android wording ("goes back to its first screen", verified on real devices) and a platform-neutral one for
/// iOS, where the same observation is rarer and unverified to specifically mean "the first screen" -- see
/// <see cref="LargeTextAskWording.WhatHappened"/>. The wording never says "skip": the option reads like "don't
/// check this screen at the larger size" -- the person decides, so nothing is ever silently "skipped".
/// </summary>
public delegate Task<LargeTextRestartChoice> LargeTextRestartAsker(
    string screenName, string reason, bool learnedFromEarlierScreen, Platform platform, CancellationToken cancellationToken);

/// <summary>
/// Asked once, when the person presses "Finish"/Stop, if any screen wasn't checked at the larger size (the
/// person chose not to, or this run doesn't ask -- see
/// <see cref="Swipewalk.Collectors.LargeTextCapture.DeclinedByPerson"/> /
/// <see cref="Swipewalk.Collectors.LargeTextCapture.NotCheckedThisRun"/>): offers to keep recording so the
/// person can navigate back to <paramref name="screenNames"/> and check them (never automatic -- the person
/// still does the navigating and pressing themselves). True continues the recording; false (or no asker wired
/// up, e.g. a non-interactive run) finishes as normal.
/// </summary>
public delegate Task<bool> RevisitSkippedScreensAsker(IReadOnlyList<string> screenNames, CancellationToken cancellationToken);

/// <summary>Parses <c>--large-text-restart</c> / swipewalk.json's <c>largeTextRestart</c>.</summary>
public static class LargeTextRestartPolicies
{
    public static LargeTextRestartPolicy Parse(string value) => value.ToLowerInvariant() switch
    {
        "ask" => LargeTextRestartPolicy.Ask,
        "always" => LargeTextRestartPolicy.Always,
        "never" => LargeTextRestartPolicy.Never,
        _ => throw new InvalidOperationException($"--large-text-restart must be \"ask\", \"always\" or \"never\", not \"{value}\"."),
    };

    /// <summary>
    /// The default policy when nothing set one explicitly (no <c>--large-text-restart</c> /
    /// <c>largeTextRestart</c> in swipewalk.json): <paramref name="interactive"/> (a console that isn't
    /// redirected, or the desktop app, which can always ask) means <see cref="LargeTextRestartPolicy.Ask"/> for
    /// both scan and record. Not interactive (CI, piped input, or an unattended <c>swipewalk run</c>) means
    /// <see cref="LargeTextRestartPolicy.Never"/> for record -- so an unattended recording never blocks
    /// waiting for an answer nobody will give -- but <see cref="LargeTextRestartPolicy.Always"/> for
    /// <paramref name="recordMode"/> false (scan): unlike record, a one-shot scan has no later screens to keep
    /// blocking on, so it keeps the automatic restart CI already relied on before this policy applied to scan
    /// at all.
    /// </summary>
    public static LargeTextRestartPolicy Default(bool interactive, bool recordMode) =>
        interactive ? LargeTextRestartPolicy.Ask : recordMode ? LargeTextRestartPolicy.Never : LargeTextRestartPolicy.Always;
}

/// <summary>Shared wording for the large-text ask prompt (CLI console prompt, desktop dialog): what actually
/// happened, and the cost of checking anyway. Driven by <paramref name="reason"/> (what was observed), never
/// guessed from the platform or framework version.</summary>
public static class LargeTextAskWording
{
    /// <summary>
    /// States what was actually observed, not a diagnosis: text that "didn't change size" might still never
    /// grow even after a restart (e.g. a genuinely unscaled control) -- only <c>TextResizeRule</c>'s own
    /// pixel comparison decides that, never this prompt. <paramref name="learnedFromEarlierScreen"/> makes
    /// clear the observation is from an earlier screen in this recording, not this one, when asking before
    /// touching the size (see <see cref="LargeTextRestartAsker"/>). <see cref="LargeTextCapture.WentToAnotherScreenLive"/>
    /// on Android specifically means "back to its first screen" (verified on real
    /// devices); the same reason on iOS only means a different screen came up -- rarer, and not confirmed to
    /// always be the first one -- so it gets a platform-neutral phrasing instead.
    /// </summary>
    public static string WhatHappened(string reason, bool learnedFromEarlierScreen, Platform platform) =>
        (reason == Swipewalk.Collectors.LargeTextCapture.WentToAnotherScreenLive, learnedFromEarlierScreen, platform == Platform.Android) switch
        {
            (true, false, true) => "This app goes back to its first screen when the text size changes.",
            (true, true, true) => "On an earlier screen, this app went back to its first screen when the text size changed.",
            (true, false, false) => "This app showed a different screen when the text size changed; checking it at the larger size means restarting the app.",
            (true, true, false) => "On an earlier screen, this app showed a different screen when the text size changed; checking this one at the larger size means restarting the app.",
            (false, false, _) => "The text didn't change size while the app kept running; checking it at the larger size means restarting the app.",
            (false, true, _) => "On an earlier screen, the text didn't change size while the app kept running; checking this one at the larger size means restarting the app.",
        };

    /// <param name="recordMode">True (record) asks about navigating back, since the person does that
    /// themselves once the size is enlarged; false (scan) doesn't -- a scan restarts the app and re-captures
    /// the same screen itself, with nobody to navigate anywhere.</param>
    public static string Question(string screenName, bool recordMode) =>
        recordMode
            ? $"Check \"{screenName}\" at the larger size? You'll need to navigate back twice."
            : $"Check \"{screenName}\" at the larger size? Swipewalk will restart the app and try again.";

    /// <summary>
    /// Shared by the record and scan prompts (CLI console, desktop dialog). Conditional on purpose (wcag-
    /// reviewer, 2026-09-23): "avoid" instead of "remove" doesn't by itself hedge anything -- both claim the
    /// same ability -- so the sentence is true only when the text does grow after the restart, which is what
    /// the "if" states; only then does the report's fix guidance for that screen say anything about avoiding
    /// it (an app's own fix is confirmed for .NET MAUI on iOS so far, not Android -- see docs/user-guide.md).
    /// When the person declines, or the restart shows a different screen, no such fix guidance exists, so the
    /// sentence correctly promises nothing there.
    /// </summary>
    public const string DeveloperNote = "If the text grows after the restart, the report explains how the app's developers can avoid this step.";

    public static string Message(string screenName, string reason, bool learnedFromEarlierScreen, Platform platform, bool recordMode) =>
        $"{WhatHappened(reason, learnedFromEarlierScreen, platform)} {Question(screenName, recordMode)} {DeveloperNote}";
}
