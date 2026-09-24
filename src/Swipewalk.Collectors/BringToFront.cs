namespace Swipewalk.Collectors;

/// <summary>
/// Where the app under test is, relative to the screen, before a scan or the start of a recording. Decided
/// from cheap platform reads already used elsewhere (the focused window, whether the app is installed,
/// whether its process is running) -- see <see cref="Android.AndroidCollector"/> and the iOS harness's
/// <c>app(_:)</c>/<c>ensure-front</c>.
/// </summary>
public enum AppForegroundState
{
    /// <summary>Already the frontmost app.</summary>
    InFront,

    /// <summary>Running, but another app (or the home screen) is in front. Bringing it back forward doesn't
    /// restart it, so its state is usually preserved.</summary>
    Background,

    /// <summary>Installed but not currently running. Starting it shows its own first screen (possibly
    /// onboarding or a terms screen), not wherever it was last left.</summary>
    NotRunning,

    /// <summary>Not installed at all.</summary>
    NotInstalled,
}

/// <summary>
/// The one rule above all: never silently change what's on screen. So the decision behind bringing the app
/// under test to the front before a capture, from its <see cref="AppForegroundState"/>: already in front
/// means doing nothing at all -- "scan the screen shown now" must mean exactly that, or a person who
/// navigated somewhere on purpose would lose their place. A backgrounded app is brought forward without being
/// restarted, so its state is usually preserved. A not-running app has to be started, which always shows its
/// first screen rather than wherever it was left -- callers must report that (see <see cref="LaunchedNote"/>),
/// never treat it the same as a background app that was merely brought forward. An app that isn't installed
/// can't be helped automatically at all.
/// </summary>
public static class BringToFront
{
    public enum Action
    {
        /// <summary>Already in front: do nothing.</summary>
        None,

        /// <summary>Running in the background: bring it forward.</summary>
        Activate,

        /// <summary>Not running: start it. The caller must record that this happened (see <see cref="LaunchedNote"/>).</summary>
        Launch,

        /// <summary>Not installed: nothing can be done automatically; fail with the existing "install a build
        /// first" guidance.</summary>
        Fail,
    }

    public static Action Decide(AppForegroundState state) => state switch
    {
        AppForegroundState.InFront => Action.None,
        AppForegroundState.Background => Action.Activate,
        AppForegroundState.NotRunning => Action.Launch,
        AppForegroundState.NotInstalled => Action.Fail,
        _ => Action.Fail,
    };

    /// <summary>Progress line logged when a backgrounded app is brought forward (not restarted, so its state
    /// is usually preserved).</summary>
    public static string BroughtToFrontLog(string app) => $"Brought {app} to the front.";

    /// <summary>Progress line logged when the app wasn't running and had to be started.</summary>
    public static string StartedLog(string app) => $"{app} wasn't running; Swipewalk started it.";

    /// <summary>
    /// Screen/report note for a scan: recorded when the app had to be started from not running, so the report
    /// is honest that this is the app's first screen (possibly onboarding or a terms screen), not wherever the
    /// person had it open before Swipewalk ran.
    /// </summary>
    public const string LaunchedNote =
        "Swipewalk started the app for this scan, so this is the app's first screen (possibly onboarding), not wherever it was open before.";

    /// <summary>Same fact, worded for the start of a recording (Swipewalk.Engine.Recorder reports this on the
    /// first screen and in the coverage note).</summary>
    public const string LaunchedNoteRecording =
        "Swipewalk started the app for this recording, so it began at the app's first screen (possibly onboarding), not wherever it was open before.";
}
