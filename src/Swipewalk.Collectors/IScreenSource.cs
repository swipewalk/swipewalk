using Swipewalk.Core.Model;

namespace Swipewalk.Collectors;

/// <summary>
/// A live device that record mode watches. Each platform implements a cheap peek (to notice screen
/// changes) and full captures; the recording loop itself is platform-neutral.
/// </summary>
public interface IScreenSource : IAsyncDisposable
{
    Platform Platform { get; }

    /// <summary>Human-readable large-text setting, e.g. "Android font scale 2.0".</summary>
    string LargeTextSetting { get; }

    /// <summary>The text-size scale <see cref="LargeTextSetting"/> tests, as a multiple of normal size
    /// (Android font scale 2.0 = 200%; iOS accessibility size AX3 is about 2.35 = 235%).</summary>
    double LargeTextScale { get; }

    /// <summary>
    /// True when the large-text check changes the setting on a device its owner actually uses day to day
    /// (a physical phone), rather than a virtual emulator/simulator; callers show <see cref="PhysicalDeviceTextSizeNotice"/>
    /// once per session when this is true.
    /// </summary>
    bool ChangesPhysicalDeviceTextSize => false;

    /// <summary>The app being recorded (Android package or iOS bundle id); recording pauses while another app is in front.</summary>
    string TargetApp { get; }

    /// <summary>
    /// True when <see cref="PeekAsync"/> is expensive enough (e.g. it opens its own automation session, with
    /// a visible system banner) that <see cref="Swipewalk.Engine.Recorder"/>'s poll loop should not call it
    /// continuously just to notice the person is still navigating. Android's adb-based peek and an iOS source
    /// with a live session already running (auto-scan) are cheap and return false, the default; iOS's
    /// per-capture session default returns true. When true and auto-scan is off, the loop only calls
    /// <see cref="PeekAsync"/>/<see cref="CaptureAsync"/> when the person presses "Scan this screen now", and
    /// "the app isn't in front" is discovered at that point rather than while they navigate.
    /// </summary>
    bool PeekIsExpensive => false;

    /// <summary>
    /// Called once, before the recording loop starts, so a recording begins from a known point (see
    /// <see cref="BringToFront"/>): already in front, does nothing; running in the background, brings it
    /// forward (state normally preserved) and reports that through <paramref name="log"/>; not running, starts
    /// it and returns true so the caller can record that the recording began at the app's first screen. Only
    /// called at the start -- once recording is under way, the app is left alone if the person switches away
    /// on purpose (see <see cref="TargetNotInFrontException"/> from <see cref="PeekAsync"/>).
    /// </summary>
    /// <returns>True when the app had to be started from not running.</returns>
    Task<bool> EnsureInFrontAsync(IProgress<string>? log = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// A quick read of the current accessibility tree, without screenshot or audits. Throws
    /// <see cref="TargetNotInFrontException"/> when the target app is not in front.
    /// </summary>
    Task<AccessibilityNode> PeekAsync(CancellationToken cancellationToken = default);

    /// <summary>Captures the current screen into <paramref name="captureDir"/> and loads it.</summary>
    Task<ScreenSnapshot> CaptureAsync(string captureDir, string screenName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enlarges the system text size, captures the screen, and restores the setting. Waits for the app to
    /// return to the foreground first, since the change can restart it or briefly show another window.
    /// Returns a skipped result (with a reason) rather than throwing when the app never comes back to front,
    /// or when the screen it shows at the larger size differs from the one captured normally. In record mode,
    /// text that didn't visibly grow live is reported as skipped (<see cref="LargeTextCapture.DidNotGrowLive"/>
    /// / <see cref="LargeTextCapture.WentToAnotherScreenLive"/>) rather than escalated automatically -- see
    /// <see cref="BeginLargeTextAsync"/>.
    /// </summary>
    Task<LargeTextCapture> CaptureLargeTextAsync(string captureDir, string screenName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record mode's manual ask-flow only, for a screen already captured normally whose live large-text
    /// attempt (<see cref="CaptureLargeTextAsync"/>) came back <see cref="LargeTextCapture.DidNotGrowLive"/> or
    /// <see cref="LargeTextCapture.WentToAnotherScreenLive"/> and the person chose to check further anyway
    /// (see Swipewalk.Engine.Recorder and Swipewalk.Engine.LargeTextRestartPolicy): enlarges the system text
    /// size and, only if this platform needs an explicit restart before the size actually applies (iOS always;
    /// Android only for <see cref="LargeTextCapture.DidNotGrowLive"/> -- <see cref="LargeTextCapture.WentToAnotherScreenLive"/>
    /// already means the OS recreated the activity by itself), restarts the app now. Leaves the size enlarged
    /// and does not capture: the caller tells the person to navigate back to the screen being checked and
    /// calls <see cref="CompleteLargeTextAsync"/> once they press "Scan this screen now" again.
    /// </summary>
    Task BeginLargeTextAsync(string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures the screen at the size <see cref="BeginLargeTextAsync"/> set, compares it against
    /// <paramref name="before"/> (the normal-size capture of the screen being checked, from before the size
    /// was enlarged), and restores the normal size -- restarting the app first if <paramref name="reason"/>
    /// is <see cref="LargeTextCapture.DidNotGrowLive"/> (this platform doesn't pick up the setting without an
    /// explicit restart, so restoring it alone would leave the app showing large text until relaunched again,
    /// silently enlarging the next "normal" capture) or if this platform (iOS) always needs one. Returns
    /// <see cref="LargeTextCapture.DifferentScreen"/> if the person navigated to a different screen than the
    /// one being checked; the size is still restored either way. Never throws for an expected large-text
    /// reason.
    /// </summary>
    /// <param name="reason">The <see cref="LargeTextCapture.DidNotGrowLive"/> / <see cref="LargeTextCapture.WentToAnotherScreenLive"/>
    /// reason this check started from -- decides how <see cref="Swipewalk.Engine.Recorder"/>'s caller restores
    /// the app on this platform (see above).</param>
    /// <param name="liveAttemptFailedHere">True only when a live attempt was actually made on <em>this</em>
    /// screen and found <see cref="LargeTextCapture.DidNotGrowLive"/> (the screen that first taught the
    /// recording a restart is needed): only then does growing after this restart mean "applied after a
    /// restart" (<see cref="ScreenSnapshot.LargeTextAppliedLive"/> false rather than null) -- a later, learned
    /// screen never had its own live attempt, and <see cref="LargeTextCapture.WentToAnotherScreenLive"/> means
    /// the live attempt showed a different screen, so neither can honestly claim what would have happened
    /// live on this one.</param>
    Task<LargeTextCapture> CompleteLargeTextAsync(
        string captureDir, string screenName, ScreenSnapshot before, string reason, bool liveAttemptFailedHere,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the normal text size after <see cref="BeginLargeTextAsync"/> without capturing anything,
    /// because the recording ended (Finish, a cancellation, or an error) before the person navigated back to
    /// complete the check with <see cref="CompleteLargeTextAsync"/>. Deliberately never captures: whatever is
    /// in front at that point wasn't chosen by the person for this check -- it could be another app, a
    /// notification, or other content that may be personal and that the check was never asked to look at.
    /// Same restart mechanics as
    /// <see cref="CompleteLargeTextAsync"/>'s restore step for the given <paramref name="reason"/>.
    /// </summary>
    Task AbandonLargeTextAsync(string reason, CancellationToken cancellationToken = default);
}
