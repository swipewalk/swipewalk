using Swipewalk.Core.Model;

namespace Swipewalk.Collectors;

/// <summary>
/// Result of an attempt to capture the screen at a larger system text size: the snapshot, or why it
/// wasn't taken. Scan and record report a skipped attempt with the same wording.
/// </summary>
/// <param name="ResetNotice">Set (record mode only) when the app had to be relaunched once more, after the
/// system text size was restored, to clear a stale enlarged process left behind by the terminate + relaunch
/// escalation (see <see cref="LargeTextReset"/>). Recorder surfaces this through the same progress log as
/// everything else, since the relaunch interrupts whatever screen the user was on.</param>
public sealed record LargeTextCapture(ScreenSnapshot? Snapshot, string? SkippedReason, string? BaselineTextSizeNote = null, string? ResetNotice = null)
{
    /// <summary>The larger size showed a different screen (the app likely restarted).</summary>
    public const string DifferentScreen = "a different screen was showing at the larger text size";

    /// <summary>The app didn't return to the foreground after the text size changed.</summary>
    public const string NotInFront = "the app wasn't in front after the text size changed";

    /// <summary>
    /// Record mode's "check anyway" restart flow (<see cref="Swipewalk.Collectors.IScreenSource.BeginLargeTextAsync"/>)
    /// could not even read the device's current text size, so nothing was changed: the marker
    /// (<see cref="TextSizeRestore"/>) is only ever written once the original size is known, so a read
    /// failure here means the device was never touched and there is nothing to restore. The Engine's
    /// Recorder distinguishes this specific failure (a
    /// <see cref="Swipewalk.Collectors.TextSizeReadFailedException"/>) from any later one -- see
    /// <see cref="CouldNotStartCheck"/> -- so the report never claims "nothing was changed" when something
    /// might have been. Scan mode never shows this text: its own physical-iPhone flow falls back to a
    /// per-app launch argument instead of reporting a read failure.
    /// </summary>
    public const string CouldNotReadTextSize = "could not read this device's current text size, so nothing was changed";

    /// <summary>
    /// Record mode's "check anyway" restart flow failed at some point after the device's original text size
    /// was already read and remembered (see <see cref="CouldNotReadTextSize"/> for the one failure that
    /// happens before that): the device may already be partway changed, so
    /// the Engine's Recorder also asks <see cref="Swipewalk.Collectors.IScreenSource.AbandonLargeTextAsync"/>
    /// to put it back as a best effort before reporting this. Says the *check* could not be set up, not
    /// that the text size specifically failed to change, since this also covers a failure in the relaunch
    /// that follows applying the larger size -- by then the size itself may already be correct.
    /// </summary>
    public const string CouldNotStartCheck = "the check at the larger text size could not be set up, so this screen wasn't checked at the larger size";

    /// <summary>
    /// Record mode only: the text size changed but the same screen stayed up with no visible growth. Record
    /// mode never force-stops + relaunches the app on its own here -- that would interrupt the person's
    /// navigation and lose their place -- so Swipewalk.Engine.Recorder asks whether to check further (see
    /// Swipewalk.Engine.LargeTextRestartPolicy) instead of escalating by itself; once it does,
    /// <see cref="Swipewalk.Collectors.IScreenSource.BeginLargeTextAsync"/> /
    /// <see cref="Swipewalk.Collectors.IScreenSource.CompleteLargeTextAsync"/> perform the restart.
    /// </summary>
    public const string DidNotGrowLive = "the text didn't change size while the app kept running";

    /// <summary>
    /// Record mode only, Android: the activity restarted when the text size changed (no
    /// android:configChanges="fontScale") and came back on a different screen -- typically the app's first
    /// screen -- before any deliberate restart was attempted. Like <see cref="DidNotGrowLive"/>, record mode
    /// asks rather than deciding on its own: the person is told which screen to navigate back to.
    /// </summary>
    public const string WentToAnotherScreenLive = "the app went back to its first screen when the text size changed";

    /// <summary>
    /// The person answered "don't check this screen at the larger size" (or the standing "always" choice
    /// resolved to that) for <see cref="DidNotGrowLive"/> or <see cref="WentToAnotherScreenLive"/>. Never
    /// "skipped": the person decided, and the wording says so.
    /// </summary>
    public const string DeclinedByPerson = "you chose not to check this screen at the larger size";

    /// <summary>
    /// This recording is set not to check screens that need the app restarted to check larger text further (
    /// <c>--large-text-restart never</c>, or the default for a non-interactive run): unlike
    /// <see cref="DeclinedByPerson"/>, nobody was asked about this particular screen.
    /// </summary>
    public const string NotCheckedThisRun = "this recording was set to \"never check\" screens that need the app restarted to check larger text";

    /// <summary>Scan's equivalent of <see cref="NotCheckedThisRun"/>: this one-screen scan is set (
    /// <c>--large-text-restart never</c>) not to check a screen that needs the app restarted -- unlike
    /// <see cref="DeclinedByPerson"/>, nobody was asked.</summary>
    public const string NotCheckedThisScan = "this scan was set to \"never check\" a screen that needs the app restarted to check larger text";

    /// <summary>
    /// The reason reported when a terminate + relaunch escalation (scan mode; iOS Simulator, physical iPhone
    /// and Android all reach this) shows a different screen than the one scanned normally: the app lost its
    /// place after restarting, so there's nothing to report safely for this screen. Shared by both platforms
    /// so <c>Swipewalk.Engine.ScanService</c> can recognize it (across the Swipewalk.Collectors assembly
    /// boundary) and point to record mode instead of "check by hand", since record mode -- unlike scan -- lets
    /// the person navigate back after the restart.
    /// </summary>
    public const string DifferentScreenAfterRestart =
        "the app applies the text size only after a restart, and after restarting it showed a different screen";

    /// <summary>
    /// The person asked to check this screen at the larger size, navigated away to do it, and the recording
    /// ended (Finish, a cancellation, or an error) before they navigated back and pressed "Scan this screen
    /// now" to complete it.
    /// </summary>
    public const string NotCompletedBeforeFinish = "the recording ended before this screen's larger-size check was completed";

    /// <summary>
    /// Shown for a screen while its larger-size check is in progress: the system text size has been enlarged
    /// and Swipewalk.Engine.Recorder is waiting for the person to navigate back and press "Scan this screen
    /// now" again (see Swipewalk.Collectors.IScreenSource.CompleteLargeTextAsync). Replaced with the actual
    /// result once that capture completes, or with <see cref="NotCompletedBeforeFinish"/> if the recording
    /// ends first; only visible if the report is read mid-recording.
    /// </summary>
    public const string CheckInProgress = "the larger-size check was still in progress (waiting for this screen to be scanned again at the larger size)";

    /// <summary>Shown in record mode after the post-restore reset relaunch (see <see cref="LargeTextReset"/>).</summary>
    public const string RestartedToRestoreNotice =
        "The app was restarted to put the text size back; go back to the screen you were on.";

    public static LargeTextCapture Captured(ScreenSnapshot snapshot) => new(snapshot, null);

    public static LargeTextCapture Skipped(string reason) => new(null, reason);

    /// <summary>
    /// Skipped because the device's text size was already enlarged before the scan started (see
    /// <see cref="BaselineTextSize"/>): the normal-size capture isn't at default size, so a large-vs-large
    /// comparison would be meaningless. <paramref name="value"/> is the original reading (font_scale,
    /// content_size, or the physical iPhone's TextSizeState), quoted in <see cref="BaselineTextSize.Warning"/>.
    /// </summary>
    public static LargeTextCapture SkippedBaselineEnlarged(string value) =>
        new(null, BaselineTextSize.AlreadyEnlargedReason, BaselineTextSize.Warning(value));
}

/// <summary>
/// Scan mode's ask-before-restart hook: called by the iOS and Android large-text capture methods just before
/// the escalation that terminates and relaunches the app under test, once a live attempt found the text didn't
/// visibly grow (<paramref name="reason"/> is <see cref="LargeTextCapture.DidNotGrowLive"/>). Returns null to
/// proceed with the restart (today's automatic behaviour, unchanged); otherwise the skip reason to use instead
/// -- <see cref="LargeTextCapture.DeclinedByPerson"/> or <see cref="LargeTextCapture.NotCheckedThisScan"/>.
/// Swipewalk.Engine.ScanService builds this from the resolved <c>LargeTextRestartPolicy</c> and the CLI/desktop
/// asker, keeping that ask/policy plumbing in Swipewalk.Engine (this project doesn't reference it) while the
/// restart mechanics stay here. Record mode has its own separate ask flow (Swipewalk.Engine.Recorder, via
/// <see cref="IScreenSource.BeginLargeTextAsync"/>/<see cref="IScreenSource.CompleteLargeTextAsync"/>) and
/// never passes this.
/// </summary>
public delegate Task<string?> ScanLargeTextRestartAsk(string reason, CancellationToken cancellationToken);
