using Swipewalk.Collectors;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Rules;
using Swipewalk.Core.ScreenReader;

namespace Swipewalk.Engine;

/// <summary>Lets the caller (keyboard in the CLI, buttons in the desktop app) steer a recording. Thread-safe.</summary>
public sealed class RecorderControl
{
    private volatile bool _scanNow;
    private volatile bool _stop;

    /// <summary>-1: no live override yet (Recorder uses the policy it started with); otherwise a
    /// <see cref="LargeTextRestartPolicy"/> value. See <see cref="SetLargeTextRestartPolicy"/>.</summary>
    private volatile int _largeTextRestartPolicyOverride = -1;

    /// <summary>Scan the current screen now, even if it was scanned before (e.g. after opening a menu). Also
    /// the trigger that completes a pending large-text check once the person has navigated back -- see
    /// <see cref="Recorder"/>.</summary>
    public void RequestScanNow() => _scanNow = true;

    public void Stop() => _stop = true;

    /// <summary>Clears a pending Stop so the recording loop can keep going: used only by Recorder itself,
    /// after the person accepts the offer to revisit screens they chose not to check at the larger size (see
    /// RevisitSkippedScreensAsker) -- never a public "un-stop", since Stop always means "finish" everywhere
    /// else (the CLI's q, the desktop app's Finish button).</summary>
    internal void ResumeAfterReviewOffer() => _stop = false;

    /// <summary>
    /// Changes what happens when a large-text check finds text that needs a restart to check further (see
    /// <see cref="LargeTextRestartPolicy"/>) for the rest of the recording, from right now -- e.g. a live
    /// picker in the desktop app, or a CLI keyboard shortcut. Choosing "always check"/"never check" from the
    /// per-screen prompt does the same thing; either way, it's a setting the person can change again at any
    /// time during the recording, never a one-way choice.
    /// </summary>
    public void SetLargeTextRestartPolicy(LargeTextRestartPolicy policy) => _largeTextRestartPolicyOverride = (int)policy;

    internal bool TakeScanNow()
    {
        var value = _scanNow;
        _scanNow = false;
        return value;
    }

    /// <summary>Non-consuming peek at <see cref="RequestScanNow"/>, used only to wake <see cref="Recorder"/>'s
    /// poll wait early so "Scan this screen now" feels immediate rather than waiting out the rest of the poll
    /// interval.</summary>
    internal bool ScanNowPending => _scanNow;

    internal LargeTextRestartPolicy? LargeTextRestartPolicyOverride =>
        _largeTextRestartPolicyOverride < 0 ? null : (LargeTextRestartPolicy)_largeTextRestartPolicyOverride;

    internal bool StopRequested => _stop;
}

/// <summary>
/// Record mode: the user navigates the app; by default a screen is only scanned on request ("Scan this
/// screen now" / Enter), so a recording never captures a screen the person is still navigating through --
/// <paramref name="autoScanOnScreenChange"/> restores the old "scan every new screen automatically" behaviour
/// for anyone who wants it. Each scanned screen is also captured at large text size (when requested): the
/// first time this recording finds that the larger size needs a restart to show (see
/// <see cref="LargeTextCapture.DidNotGrowLive"/> / <see cref="LargeTextCapture.WentToAnotherScreenLive"/>),
/// the person is asked whether to check anyway; every later screen asks BEFORE touching the size at all,
/// since the cost is already known. The report is rewritten
/// after every screen.
/// </summary>
public sealed class Recorder(
    IScreenSource source, string outDir, string toolVersion, AppFramework? framework, string? frameworkVersion,
    IReadOnlyList<string> expectedScreens, bool largeText, string? focusStandard, bool autoScanOnScreenChange,
    RecorderControl control, IProgress<string> log,
    LargeTextRestartPolicy largeTextRestartPolicy = LargeTextRestartPolicy.Never, LargeTextRestartAsker? largeTextRestartAsk = null,
    RevisitSkippedScreensAsker? revisitSkippedScreensAsk = null, RecordContinuation? continuation = null)
{
    /// <summary>Raised after each screen is scanned, for live views.</summary>
    public event Action<ScreenResult>? ScreenScanned;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1.5);

    private static readonly System.Text.RegularExpressions.Regex TitleSuffixPattern =
        new(@"^(?<base>.*) \((?<n>\d+)\)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    // continuation is null for a fresh recording, in which case every field below starts empty/at its normal
    // default -- see RecordContinuation's own remarks for what record --continue (or the desktop app's
    // Continue action) carries over from an earlier session of the same run.
    private readonly List<ScreenResult> _results = [.. continuation?.PriorResults ?? []];

    /// <summary>Identity fingerprint per entry in <see cref="_results"/> (same order, same count) -- used by
    /// <see cref="FindSameScreenIndex"/> to recognize a rescan of a screen already kept in this run, including
    /// one kept from an earlier session -- these only ever hold this run's own screens; separate runs are
    /// never merged.</summary>
    private readonly List<ScreenFingerprint> _identities = [.. continuation?.PriorIdentities ?? []];

    /// <summary>Folder number per entry in <see cref="_results"/> (same order, same count), so a same-screen
    /// replacement (<see cref="ReplaceScreenAsync"/>... see ScanCurrentScreenAsync) can find and delete the
    /// superseded screen's <c>screens/{n:00}</c> folder.</summary>
    private readonly List<int> _screenNumbers = [.. continuation?.PriorScreenNumbers ?? []];
    private readonly HashSet<string> _seen = [];
    private readonly Dictionary<string, int> _titles = SeedTitles(continuation?.PriorResults);
    private readonly RuleRunner _runner = new(DefaultRules.All);
    private bool _away;

    /// <summary>Next number for a screen's <c>screens/{n:00}</c> folder and its "Screen n:" log lines --
    /// continues from where an earlier session left off rather than restarting at 1, so a continued run's
    /// folders never collide with ones already on disk (see RecordState.NextScreenNumber).</summary>
    private int _nextScreenNumber = continuation?.NextScreenNumber ?? 1;

    /// <summary>The screen number this session's first capture will get (set at the top of <see cref="RunAsync"/>):
    /// <see cref="BuildResult"/> uses this, not literally "number == 1", to decide whether a screen is this
    /// session's own first one for <see cref="_appLaunchedNote"/> -- a continued run's first new capture isn't
    /// numbered 1, but it's still the first screen of ITS session if the app had to be launched again.</summary>
    private int _firstScreenNumberThisSession;

    private readonly DateTimeOffset _sessionStartedAt = DateTimeOffset.Now;

    /// <summary>The policy's own starting value; <see cref="RecorderControl.LargeTextRestartPolicyOverride"/>
    /// (set by an "always check"/"never check" answer, or directly by the desktop app's picker / a CLI
    /// shortcut) takes over the moment it's set, and can be changed again at any time -- see
    /// <see cref="ResolveChoiceAsync"/>.</summary>
    private LargeTextRestartPolicy _restartPolicy = largeTextRestartPolicy;

    /// <summary>
    /// Once a large-text check finds that the larger size needs a restart to show (see
    /// <see cref="LargeTextCapture.DidNotGrowLive"/> / <see cref="LargeTextCapture.WentToAnotherScreenLive"/>),
    /// this remembers which -- so every later screen asks (or applies the standing policy) BEFORE touching
    /// the text size at all, instead of repeating the disruptive live attempt that taught us this in the
    /// first place. Null means either nothing has needed a restart yet, or every screen so far grew text
    /// live: this is decided per screen from what was observed, never from platform or framework version.
    /// </summary>
    private string? _learnedRestartReason = continuation?.LearnedRestartReason;

    /// <summary>
    /// Set once the person has agreed to check a screen at the larger size and the size has been enlarged
    /// (<see cref="IScreenSource.BeginLargeTextAsync"/>): the NEXT "Scan this screen now" completes it
    /// (<see cref="CompletePendingLargeTextAsync"/>) instead of starting a new screen, since nothing is
    /// captured until the person has navigated back and pressed it again.
    /// </summary>
    private PendingLargeText? _pendingLargeText;

    /// <summary>_results indexes already included in a Finish-time revisit offer (see RunAsync): offered at
    /// most once each, so accepting once and rescanning doesn't get the same stale entries offered again on
    /// every later Finish.</summary>
    private readonly HashSet<int> _offeredForRevisit = [];

    /// <param name="Reason">DidNotGrowLive or WentToAnotherScreenLive: which restart mechanics
    /// <see cref="IScreenSource.CompleteLargeTextAsync"/> needs to use for this platform when restoring.</param>
    /// <param name="ConfirmedLiveFailure">True only when a live attempt was actually made on this screen and
    /// found <see cref="LargeTextCapture.DidNotGrowLive"/> -- see
    /// <see cref="IScreenSource.CompleteLargeTextAsync"/>'s <c>liveAttemptFailedHere</c> parameter.</param>
    /// <param name="RescannedAt">Carried through to the completed <see cref="ScreenResult.RescannedAt"/> --
    /// set when the normal-size capture that started this pending check itself replaced an earlier capture of
    /// the same screen (see ScanCurrentScreenAsync's FindSameScreenIndex); null otherwise.</param>
    private sealed record PendingLargeText(
        int Index, int Number, string ScreenName, string Dir, ScreenSnapshot NormalSnapshot, string Reason, bool ConfirmedLiveFailure,
        DateTimeOffset? RescannedAt);

    /// <summary>Set when the app had to be started for this recording (see <see cref="BringToFront"/>);
    /// attached to the first scanned screen and to the coverage note, so the report is honest that the
    /// recording began at the app's own first screen, not wherever it was last left.</summary>
    private string? _appLaunchedNote;

    /// <summary>
    /// Set when the recording ended for a reason other than pressing "Finish" (RecorderControl.Stop): a
    /// cancellation (Ctrl+C, or the caller's own token) or an error while capturing a screen -- including one
    /// on the very last screen attempted. Null means the recording ended normally: either the person chose to
    /// stop, or (outside an interactive session) the loop was told to stop.
    /// </summary>
    private string? _endedEarlyReason;

    public async Task<ScanReport> RunAsync(CancellationToken cancellationToken)
    {
        _firstScreenNumberThisSession = _nextScreenNumber;

        // Only at the start: once the recording is under way, the app is left alone if the person switches
        // away on purpose (see the TargetNotInFrontException handling in the loop below).
        if (await source.EnsureInFrontAsync(log, cancellationToken))
            _appLaunchedNote = BringToFront.LaunchedNoteRecording;

        // iOS's per-capture session (the default -- see IosScreenSource.PeekIsExpensive) makes PeekAsync open
        // its own automation session, with iOS's own "Automation Running" banner, each time it's called; auto-
        // scan already needs continuous polling (and keeps one session open for the whole recording, so
        // polling it costs nothing extra), but manual capture must not poll at all, or the banner would flash
        // on and off every ~1.5s while the person is just navigating -- the opposite of what a per-capture
        // session is for. When this is false, the loop below only calls PeekAsync/CaptureAsync in reaction to
        // "Scan this screen now", and "the app isn't in front" is discovered at that point (see
        // ScanCurrentScreenAsync/CompletePendingLargeTextAsync's own TargetNotInFrontException handling)
        // instead of while the person navigates.
        var pollForChanges = !source.PeekIsExpensive || autoScanOnScreenChange;

        log.Report($"Recording {source.TargetApp} on {source.Platform}. Use the app normally. " +
                   (autoScanOnScreenChange
                       ? "Each new screen is scanned automatically once it settles; press Scan this screen now (Enter) to also capture one on request."
                       : "Nothing is captured until you press Scan this screen now (Enter); choose Finish (q) when you're done.") +
                   (pollForChanges
                       ? " Recording pauses while another app is in front."
                       : $" Only \"Scan this screen now\" checks whether {source.TargetApp} is in front; nothing else is checked while you navigate.") +
                   " Overlays such as Notification Center may still be captured on iOS; review screenshots before sharing.");
        if (!pollForChanges)
            log.Report("  On a physical iPhone, iOS's \"Automation Running\" banner appears only while Swipewalk is " +
                       "working on the app (checking it's in front, capturing a screen, changing the text size), not while you navigate.");
        if (largeText && source.ChangesPhysicalDeviceTextSize)
            log.Report($"  {PhysicalDeviceTextSizeNotice.Text}");

        string? last = null;
        var stableFor = 0;
        // Runs the poll loop; on a clean Stop (not a cancellation or error), if any screen was left unchecked
        // at the larger size (the person chose not to, or this run doesn't ask), offers to keep recording so
        // the person can navigate back and check them -- never automatic, and declinable, so this loop only
        // repeats if they say yes (see RevisitSkippedScreensAsker). A cancellation or an error always
        // finishes outright.
        while (true)
        {
            try
            {
                while (!control.StopRequested && !cancellationToken.IsCancellationRequested)
                {
                    if (!pollForChanges)
                    {
                        if (control.TakeScanNow())
                        {
                            try
                            {
                                if (_pendingLargeText is not null)
                                    await CompletePendingLargeTextAsync(cancellationToken);
                                else
                                    await ScanCurrentScreenAsync(cancellationToken);
                            }
                            catch (TargetNotInFrontException)
                            {
                                // Unlike the polling path below, nothing here confirmed the app was in front a
                                // moment ago -- it may never have come back after the person switched away, so
                                // "left the screen during capture" (which implies it was there and then wasn't)
                                // would describe something that didn't happen. Say what's known and what to do.
                                log.Report($"  {source.TargetApp} isn't in front, so nothing was captured. Switch back to it and press Scan this screen now again.");
                            }
                        }
                        await WaitAsync(cancellationToken);
                        continue;
                    }

                    AccessibilityNode root;
                    try
                    {
                        root = await source.PeekAsync(cancellationToken);
                    }
                    catch (TargetNotInFrontException)
                    {
                        if (!_away)
                            log.Report($"  {source.TargetApp} is not in front; not scanning until you return to it.");
                        _away = true;
                        last = null;
                        await WaitAsync(cancellationToken);
                        continue;
                    }
                    catch (InvalidOperationException ex)
                    {
                        log.Report($"  (could not read the screen: {ex.Message.Split('\n')[0]}; retrying)");
                        await WaitAsync(cancellationToken);
                        continue;
                    }
                    _away = false;

                    var signature = ScreenIdentity.Signature(root);
                    stableFor = signature == last ? stableFor + 1 : 0;
                    last = signature;

                    if ((autoScanOnScreenChange && stableFor >= 1 && !_seen.Contains(signature)) || control.TakeScanNow())
                    {
                        try
                        {
                            if (_pendingLargeText is not null)
                                await CompletePendingLargeTextAsync(cancellationToken);
                            else
                                await ScanCurrentScreenAsync(cancellationToken);
                            _seen.Add(signature);
                        }
                        catch (TargetNotInFrontException)
                        {
                            log.Report($"  {source.TargetApp} left the screen during capture; nothing was kept.");
                        }
                    }
                    await WaitAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _endedEarlyReason = EndedEarlyReasons.Cancelled;
                break;
            }
            catch (Exception ex)
            {
                // A device disconnecting, the harness dying, an unexpected bug elsewhere in Swipewalk, or any
                // other failure mid-capture -- including on the very last screen attempted -- must not lose the
                // screens already captured (see Runner.RunAsync/RunHistory, which save this partial report
                // rather than nothing at all) and must leave an honest ended-early note so `record --continue`
                // (or the desktop app's Continue) can resume it: a crash of Swipewalk itself must still appear
                // in History with what was captured and an ended-early note. Caught broadly (not a narrow allowlist of exception types)
                // so a bug that throws something unanticipated still ends the recording honestly instead of
                // crashing the process with no note at all -- the one case this still can't cover is the
                // process being killed outright (SIGKILL) or a fatal runtime crash (StackOverflowException and
                // similar can't be caught by any managed try/catch); OperationCanceledException is handled by
                // the catch above this one, so it never reaches here. Only the first line goes into the
                // report/results.json: the rest of a .NET exception message can run to many lines and mention
                // local file paths or a device serial, which never belong in the report -- the full
                // message is still visible on the console, same as the retry message above.
                var firstLine = ex.Message.Split('\n')[0];
                _endedEarlyReason = EndedEarlyReasons.Error(firstLine);
                log.Report($"Recording interrupted: {ex.Message}");
                break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _endedEarlyReason = EndedEarlyReasons.Cancelled;
                break;
            }

            try
            {
                if (_pendingLargeText is { } pending)
                {
                    await AbandonPendingLargeTextAsync(pending, LargeTextCapture.NotCompletedBeforeFinish);
                    _pendingLargeText = null;
                }

                // Offered once per screen entry (_offeredForRevisit), not every Finish: otherwise a person who
                // accepts, rescans everything, and finishes would be asked again about the same, now-stale
                // entries forever. Navigating back and rescanning replaces the earlier entry for that screen
                // (see ScanCurrentScreenAsync's FindSameScreenIndex), so it isn't counted
                // twice here or in coverage -- and RemoveResultAt keeps this set's indices valid across that.
                var notChecked = _results
                    .Select((r, i) => (Result: r, Index: i))
                    .Where(x => x.Result.LargeTextSkippedReason is LargeTextCapture.DeclinedByPerson or LargeTextCapture.NotCheckedThisRun
                        or LargeTextCapture.NotCompletedBeforeFinish && !_offeredForRevisit.Contains(x.Index))
                    .ToList();
                var notCheckedNames = notChecked.Select(x => x.Result.ScreenName).ToList();
                if (notCheckedNames.Count == 0 || revisitSkippedScreensAsk is null)
                    break;
                if ((control.LargeTextRestartPolicyOverride ?? _restartPolicy) == LargeTextRestartPolicy.Never)
                    log.Report("  (This recording is set to \"never check\": revisiting won't check these screens unless that's changed first.)");
                if (!await revisitSkippedScreensAsk(notCheckedNames, cancellationToken))
                    break;
                foreach (var (_, index) in notChecked)
                    _offeredForRevisit.Add(index);
                log.Report("  Continuing to record: navigate back to the listed screen(s) and press Scan this screen now when ready; choose Finish again when done.");
                control.ResumeAfterReviewOffer();
            }
            catch (OperationCanceledException)
            {
                // Cancelled while the Finish-time revisit offer itself was pending (e.g. Ctrl+C at that
                // prompt): a cancellation, not a normal "no, finish now" answer.
                _endedEarlyReason = EndedEarlyReasons.Cancelled;
                break;
            }
        }

        // Every exit path above (Finish, cancel, error) must not leave a screen saying its larger-size check
        // is still "in progress" -- only the clean-Finish path above resolves it before this point.
        if (_pendingLargeText is { } stillPending)
        {
            await AbandonPendingLargeTextAsync(stillPending, LargeTextCapture.NotCompletedBeforeFinish);
            _pendingLargeText = null;
        }

        if (_endedEarlyReason is not null)
            log.Report($"  Recording ended early: {_endedEarlyReason}. Screens after that point were not scanned.");
        return Report();
    }

    private async Task ScanCurrentScreenAsync(CancellationToken cancellationToken)
    {
        var number = _nextScreenNumber++;
        var dir = Path.Combine(outDir, "screens", number.ToString("00"));
        log.Report($"Screen {number}: capturing...");

        var captured = await source.CaptureAsync(dir, "", cancellationToken);
        var fingerprint = ScreenIdentity.Fingerprint(captured);

        // Same screen already kept in this run (this session, or an earlier one continued into it -- see
        // FindSameScreenIndex): keep its display name, delete its earlier capture, and note when the rescan
        // happened, instead of adding a second entry for the same screen -- a newer capture of the same
        // screen replaces the older one, but only within the same run.
        string name;
        DateTimeOffset? rescannedAt = null;
        if (FindSameScreenIndex(captured) is { } matchIndex)
        {
            name = _results[matchIndex].ScreenName;
            rescannedAt = DateTimeOffset.Now;
            // Named here, not just in the report, so a wrong match (two different screens that happen to share
            // a title and most labels -- see ScreenIdentity.IsSameScreen) is visible while recording, not only
            // discovered later when the earlier capture is already gone.
            log.Report($"Screen {number}: matches the earlier capture of \"{name}\" (same title and most elements); replacing it.");
            DeleteScreenFolder(_screenNumbers[matchIndex]);
            RemoveResultAt(matchIndex);
        }
        else
        {
            name = UniqueTitle(ScreenIdentity.GuessTitle(captured));
        }
        var snapshot = captured with
        {
            ScreenName = name,
            Framework = framework ?? captured.Framework,
            FrameworkVersion = frameworkVersion ?? captured.FrameworkVersion,
        };

        if (!largeText)
        {
            await StoreAsync(number, name, BuildResult(number, snapshot, null, null, rescannedAt), fingerprint);
            return;
        }

        var outcome = await StartLargeTextAsync(number, dir, name, snapshot, cancellationToken);
        // Set from this screen's own live attempt (see LargeTextOutcome.WentToAnotherScreen), regardless of
        // what the person goes on to decide about checking further -- carried into every branch below (even
        // Captured can't happen together with this, since a same-screen live attempt is what "Captured" is)
        // so TextResizeNavigationRule can report it.
        if (outcome.WentToAnotherScreen)
            snapshot = snapshot with { LargeTextWentToAnotherScreen = true };
        switch (outcome.Kind)
        {
            case LargeTextOutcomeKind.Captured:
                var merged = snapshot with
                {
                    LargeText = outcome.Snapshot,
                    LargeTextSetting = source.LargeTextSetting,
                    LargeTextScale = source.LargeTextScale,
                    LargeTextMethod = outcome.Snapshot!.LargeTextMethod,
                    LargeTextAppliedLive = outcome.Snapshot.LargeTextAppliedLive,
                    LargeTextRestartCaptured = outcome.Snapshot.LargeTextRestartCaptured,
                };
                await StoreAsync(number, name, BuildResult(number, merged, null, null, rescannedAt), fingerprint);
                break;
            case LargeTextOutcomeKind.NotChecked:
                await StoreAsync(number, name, BuildResult(number, snapshot, outcome.Reason, outcome.BaselineNote, rescannedAt), fingerprint);
                break;
            case LargeTextOutcomeKind.AwaitingNavigateBack:
                // Recorded now with an in-progress placeholder; CompletePendingLargeTextAsync replaces this
                // same entry once the person navigates back and presses "Scan this screen now" again.
                var result = BuildResult(number, snapshot, LargeTextCapture.CheckInProgress, null, rescannedAt);
                AddResult(result, fingerprint, number);
                _pendingLargeText = new PendingLargeText(
                    _results.Count - 1, number, name, dir, snapshot, outcome.PendingReason!, outcome.PendingConfirmedLiveFailure, rescannedAt);
                await SaveAsync(number, name, result);
                break;
        }
    }

    /// <summary>
    /// Decides what happens to a screen's large-text check and, when the person is checking anyway, begins
    /// it: unlearned (<see cref="_learnedRestartReason"/> null), attempts the cheap live capture first (normal
    /// capture, set large text, capture again to see whether the text grew live) and, only if that finds the
    /// size needs a restart to show, asks the person -- learning the reason for every later screen either way.
    /// Once learned, asks (or applies the standing policy) BEFORE touching the text size at all, since the cost
    /// is already known.
    /// </summary>
    private async Task<LargeTextOutcome> StartLargeTextAsync(int number, string dir, string name, ScreenSnapshot normalSnapshot, CancellationToken cancellationToken)
    {
        if (_learnedRestartReason is { } learned)
            return await AskAndMaybeBeginAsync(number, name, learned, learnedFromEarlierScreen: true, cancellationToken);

        log.Report($"Screen {number}: capturing with {source.LargeTextSetting}...");
        var largeDir = Path.Combine(dir, "large");
        var large = await source.CaptureLargeTextAsync(largeDir, name, cancellationToken);
        if (large.Snapshot is not null)
        {
            if (large.ResetNotice is { } resetNotice)
                log.Report($"Screen {number}: {resetNotice}");
            return LargeTextOutcome.Captured(large.Snapshot);
        }
        if (large.BaselineTextSizeNote is { } note)
        {
            log.Report($"Screen {number}: {note}");
            return LargeTextOutcome.NotChecked(large.SkippedReason ?? LargeTextCapture.DifferentScreen, note);
        }

        var reason = large.SkippedReason ?? LargeTextCapture.DifferentScreen;
        if (reason is not (LargeTextCapture.DidNotGrowLive or LargeTextCapture.WentToAnotherScreenLive))
        {
            log.Report($"Screen {number}: large-text check not done: {reason}. Check large text by hand.");
            return LargeTextOutcome.NotChecked(reason, null);
        }

        _learnedRestartReason = reason;
        return await AskAndMaybeBeginAsync(number, name, reason, learnedFromEarlierScreen: false, cancellationToken);
    }

    /// <summary>Asks (or applies the standing policy) whether to check a screen at the larger size, given
    /// <paramref name="reason"/> the recording already knows about; on "yes" (or "always"), enlarges the
    /// system text size (<see cref="IScreenSource.BeginLargeTextAsync"/>) and tells the person to navigate
    /// back, leaving the actual capture to <see cref="CompletePendingLargeTextAsync"/>.
    /// <paramref name="learnedFromEarlierScreen"/> is false only for the screen whose own live attempt just
    /// found <paramref name="reason"/> -- see <see cref="LargeTextRestartAsker"/> and
    /// <see cref="Swipewalk.Collectors.IScreenSource.CompleteLargeTextAsync"/>'s <c>liveAttemptFailedHere</c>.
    /// </summary>
    private async Task<LargeTextOutcome> AskAndMaybeBeginAsync(int number, string name, string reason, bool learnedFromEarlierScreen, CancellationToken cancellationToken)
    {
        // True only for the one screen whose own live attempt just found this (never for a later screen
        // asked from what an earlier one taught the recording -- see TextResizeNavigationRule's remarks):
        // that screen's own capture really did show a different screen when the text size changed, whatever
        // the person goes on to decide about checking further, so ScanCurrentScreenAsync sets
        // ScreenSnapshot.LargeTextWentToAnotherScreen on it regardless of the outcome below.
        var wentToAnotherScreen = !learnedFromEarlierScreen && reason == LargeTextCapture.WentToAnotherScreenLive;
        var (choice, asked) = await ResolveChoiceAsync(name, reason, learnedFromEarlierScreen, cancellationToken);
        if (choice is LargeTextRestartChoice.RestartAndCheck or LargeTextRestartChoice.AlwaysRestart)
        {
            try
            {
                await source.BeginLargeTextAsync(reason, cancellationToken);
            }
            catch (TextSizeReadFailedException ex)
            {
                // The very first step -- reading the device's current text size -- failed, before anything
                // was changed (see TextSizeReadFailedException): skip this screen's check with a clear,
                // accurate reason instead of losing the whole recording to an unhandled exception (the outer
                // poll loop in RunAsync would otherwise treat it as a fatal error).
                log.Report($"Screen {number}: could not check \"{name}\" at the larger size: {ex.Message.Split('\n')[0]}");
                return LargeTextOutcome.NotChecked(LargeTextCapture.CouldNotReadTextSize, null, wentToAnotherScreen);
            }
            catch (InvalidOperationException ex)
            {
                // Anything else here happens after the device's original text size was already read and
                // remembered (see TextSizeReadFailedException's remarks), so the device may already be
                // partway changed: best-effort put it back now, rather than silently leaving the rest of the
                // recording at the enlarged size until the next preflight/doctor check (which may not run
                // until a whole separate session later). The wording deliberately doesn't claim "nothing was
                // changed" here, unlike the read-failure case above.
                log.Report($"Screen {number}: could not check \"{name}\" at the larger size: {ex.Message.Split('\n')[0]}");
                try
                {
                    await source.AbandonLargeTextAsync(reason, CancellationToken.None);
                }
                catch (Exception abandonEx) when (abandonEx is InvalidOperationException or TargetNotInFrontException)
                {
                    log.Report($"Screen {number}: the text size may still be enlarged; the next `swipewalk doctor` or scan will put it back.");
                }
                return LargeTextOutcome.NotChecked(LargeTextCapture.CouldNotStartCheck, null, wentToAnotherScreen);
            }
            log.Report($"Screen {number}: navigate back to \"{name}\", then press Scan this screen now; nothing is captured until you do.");
            var confirmedLiveFailure = !learnedFromEarlierScreen && reason == LargeTextCapture.DidNotGrowLive;
            return LargeTextOutcome.AwaitingNavigateBack(reason, confirmedLiveFailure, wentToAnotherScreen);
        }
        log.Report(asked
            ? $"Screen {number}: not checking \"{name}\" at the larger size (your choice)."
            : $"Screen {number}: not checking \"{name}\" at the larger size (this recording is set to \"never check\").");
        return LargeTextOutcome.NotChecked(asked ? LargeTextCapture.DeclinedByPerson : LargeTextCapture.NotCheckedThisRun, null, wentToAnotherScreen);
    }

    /// <summary>
    /// Resolves what to do (see <see cref="LargeTextRestartChoice"/>): <see cref="LargeTextRestartPolicy.Always"/>/
    /// <see cref="LargeTextRestartPolicy.Never"/> decide on their own without asking (<paramref name="Asked"/>
    /// false); <see cref="LargeTextRestartPolicy.Ask"/> calls <paramref name="largeTextRestartAsk"/> for this
    /// screen (falling back to "don't check" if none was wired up, e.g. a non-interactive run) and, if the
    /// answer is "always check"/"never check", updates the policy so later screens aren't asked again. A live
    /// override (the desktop app's picker, a CLI shortcut, or an earlier "always"/"never" answer) always wins
    /// over the policy the recording started with, and can change again at any time. A cancellation while a
    /// question is pending propagates as <see cref="OperationCanceledException"/> (askers set that instead of
    /// resolving "don't check" on their own -- see the CLI's ConsoleRecordingInput), so it reaches
    /// <see cref="RunAsync"/>'s own cancellation handling instead of being recorded as the person's choice.
    /// </summary>
    private async Task<(LargeTextRestartChoice Choice, bool Asked)> ResolveChoiceAsync(
        string screenName, string reason, bool learnedFromEarlierScreen, CancellationToken cancellationToken)
    {
        switch (control.LargeTextRestartPolicyOverride ?? _restartPolicy)
        {
            case LargeTextRestartPolicy.Always:
                return (LargeTextRestartChoice.RestartAndCheck, false);
            case LargeTextRestartPolicy.Never:
                return (LargeTextRestartChoice.SkipForThisScreen, false);
            default:
                if (largeTextRestartAsk is null)
                    return (LargeTextRestartChoice.SkipForThisScreen, false);
                var choice = await largeTextRestartAsk(screenName, reason, learnedFromEarlierScreen, source.Platform, cancellationToken);
                if (choice == LargeTextRestartChoice.AlwaysRestart)
                    control.SetLargeTextRestartPolicy(LargeTextRestartPolicy.Always);
                else if (choice == LargeTextRestartChoice.AlwaysSkip)
                    control.SetLargeTextRestartPolicy(LargeTextRestartPolicy.Never);
                return (choice, true);
        }
    }

    /// <summary>Completes a large-text check the person agreed to: captures the screen they've navigated
    /// back to, compares it against the normal-size capture taken before the size was enlarged, and restores
    /// the normal size (see <see cref="IScreenSource.CompleteLargeTextAsync"/>) -- which disrupts the app a
    /// second time, so the person is told to navigate to whatever they scan next.</summary>
    private async Task CompletePendingLargeTextAsync(CancellationToken cancellationToken)
    {
        var pending = _pendingLargeText!;
        _pendingLargeText = null;
        log.Report($"Screen {pending.Number}: capturing with {source.LargeTextSetting}...");
        var large = await source.CompleteLargeTextAsync(
            Path.Combine(pending.Dir, "large"), pending.ScreenName, pending.NormalSnapshot, pending.Reason, pending.ConfirmedLiveFailure, cancellationToken);

        ScreenResult updated;
        if (large.Snapshot is not null)
        {
            var merged = pending.NormalSnapshot with
            {
                LargeText = large.Snapshot,
                LargeTextSetting = source.LargeTextSetting,
                LargeTextScale = source.LargeTextScale,
                LargeTextMethod = large.Snapshot.LargeTextMethod,
                LargeTextAppliedLive = large.Snapshot.LargeTextAppliedLive,
                LargeTextRestartCaptured = large.Snapshot.LargeTextRestartCaptured,
            };
            updated = BuildResult(pending.Number, merged, null, null, pending.RescannedAt);
        }
        else
        {
            var reason = large.SkippedReason ?? LargeTextCapture.DifferentScreen;
            log.Report($"Screen {pending.Number}: large-text check not done: {reason}. Check large text by hand.");
            updated = BuildResult(pending.Number, pending.NormalSnapshot, reason, null, pending.RescannedAt);
        }
        _results[pending.Index] = updated;
        log.Report("  Text size restored. Navigate to the next screen you want to scan, then press Scan this screen now.");
        await SaveAsync(pending.Number, updated.ScreenName, updated);
    }

    /// <summary>
    /// A large-text check the person agreed to, but the recording ended (Finish, a cancellation, or an error)
    /// before they navigated back and pressed "Scan this screen now" to complete it: best-effort restores the
    /// app's text size anyway (<see cref="IScreenSource.AbandonLargeTextAsync"/> -- never captures, since
    /// whatever is in front at this point wasn't chosen by the person for this check), so the device isn't
    /// left enlarged and the report never claims the check is still in progress. Always uses
    /// <see cref="CancellationToken.None"/> for the restore: the caller's own token may already be the reason
    /// this is running. Catches broadly (not just the "expected" large-text exceptions): this runs on the
    /// error/cancel exit paths too, where the device may already be disconnected or the token already
    /// cancelled, and losing the ended-early report here would be worse than a failed best-effort restore.
    /// </summary>
    private async Task AbandonPendingLargeTextAsync(PendingLargeText pending, string reason)
    {
        try
        {
            await source.AbandonLargeTextAsync(pending.Reason, CancellationToken.None);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TargetNotInFrontException or IOException
            or System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            log.Report($"  (could not confirm the text size was restored: {ex.Message.Split('\n')[0]}; the next scan or doctor run will check it)");
        }
        var updated = _results[pending.Index] with { LargeTextSkippedReason = reason };
        _results[pending.Index] = updated;
        await ReportWriter.WriteAsync(Report(), outDir);
        await SaveRecordStateAsync();
    }

    private ScreenResult BuildResult(
        int number, ScreenSnapshot snapshot, string? largeTextSkippedReason, string? baselineTextSizeNote, DateTimeOffset? rescannedAt = null)
    {
        var result = _runner.Run(snapshot);
        if (largeTextSkippedReason is not null)
            result = result with { LargeTextSkippedReason = largeTextSkippedReason };
        if (baselineTextSizeNote is not null)
            result = result with { BaselineTextSizeNote = baselineTextSizeNote };
        if (rescannedAt is not null)
            result = result with { RescannedAt = rescannedAt };
        if (number == _firstScreenNumberThisSession && _appLaunchedNote is not null)
            result = result with { AppLaunchedNote = _appLaunchedNote };
        return result;
    }

    private async Task StoreAsync(int number, string name, ScreenResult result, ScreenFingerprint fingerprint)
    {
        AddResult(result, fingerprint, number);
        await SaveAsync(number, name, result);
    }

    private void AddResult(ScreenResult result, ScreenFingerprint fingerprint, int number)
    {
        _results.Add(result);
        _identities.Add(fingerprint);
        _screenNumbers.Add(number);
    }

    /// <summary>Removes a kept screen (a same-screen replacement -- see ScanCurrentScreenAsync's
    /// FindSameScreenIndex): drops it from <see cref="_results"/>/<see cref="_identities"/>/<see cref="_screenNumbers"/>
    /// together, and keeps <see cref="_offeredForRevisit"/>'s indices valid (they refer into
    /// <see cref="_results"/>, so removing an earlier entry shifts every later one down by one).</summary>
    private void RemoveResultAt(int index)
    {
        _results.RemoveAt(index);
        _identities.RemoveAt(index);
        _screenNumbers.RemoveAt(index);
        if (_offeredForRevisit.Count == 0)
            return;
        var updated = new HashSet<int>();
        foreach (var i in _offeredForRevisit)
            if (i != index)
                updated.Add(i > index ? i - 1 : i);
        _offeredForRevisit.Clear();
        foreach (var i in updated)
            _offeredForRevisit.Add(i);
    }

    /// <summary>The index into <see cref="_results"/>/<see cref="_identities"/> of a screen already kept in
    /// this run that <paramref name="snapshot"/> is a rescan of (see
    /// <see cref="ScreenIdentity.IsSameScreen(ScreenFingerprint,ScreenSnapshot)"/>), or null if this is a
    /// screen not seen before in this run.</summary>
    private int? FindSameScreenIndex(ScreenSnapshot snapshot)
    {
        for (var i = 0; i < _identities.Count; i++)
            if (ScreenIdentity.IsSameScreen(_identities[i], snapshot))
                return i;
        return null;
    }

    /// <summary>Deletes a superseded screen's <c>screens/{number:00}</c> folder (its screenshot, raw capture
    /// and any large-text subfolder) after a same-screen replacement. Best-effort: a failure here still keeps
    /// the newer capture as what's reported, it just leaves the old files on disk instead of losing the run.</summary>
    private void DeleteScreenFolder(int number)
    {
        var dir = Path.Combine(outDir, "screens", number.ToString("00"));
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch (IOException ex)
        {
            log.Report($"  (could not delete the earlier capture of this screen: {ex.Message})");
        }
    }

    private async Task SaveAsync(int number, string name, ScreenResult result)
    {
        log.Report($"Screen {number}: \"{name}\": {result.Findings.Count(f => f.Kind == FindingKind.WcagIssue)} WCAG issue(s), " +
                          $"{result.Findings.Count(f => f.Kind == FindingKind.NeedsReview)} to review.");
        // Write the report to disk, then tell listeners: RunHistory.OnScreenSaved (wired to this event by
        // Runner/the CLI/the desktop app) reads results.json back from outDir to save incrementally, so it
        // must never fire before the file exists (found on the Android emulator: the very first screen's
        // incremental save always failed with "file not found" when this was ordered the other way round).
        await ReportWriter.WriteAsync(Report(), outDir);
        await SaveRecordStateAsync();
        ScreenScanned?.Invoke(result);
    }

    /// <summary>Saves record-state.json (see <see cref="RecordState"/>) alongside report.html/results.json,
    /// after every screen -- the same "save as it's captured" discipline as the report itself, so
    /// <c>record --continue</c> can resume from wherever this recording last got to, not just from a clean
    /// Finish. Best-effort: a failure here doesn't stop the recording, it just means a later
    /// <c>record --continue</c> may not fully resume this run and starts fresh instead -- the report itself
    /// (saved just above) is unaffected either way.</summary>
    private async Task SaveRecordStateAsync()
    {
        try
        {
            await new RecordState
            {
                NextScreenNumber = _nextScreenNumber,
                LearnedRestartReason = _learnedRestartReason,
                Screens = [.. _screenNumbers.Zip(_identities, (n, f) => new RecordedScreenState(n, f))],
            }.SaveAsync(outDir);
        }
        catch (IOException ex)
        {
            log.Report($"  (could not save the recording's resume state: {ex.Message}; \"record --continue\" may not fully resume this run)");
        }
    }

    private ScanReport Report() => new()
    {
        ToolVersion = toolVersion,
        Screens = [.. _results],
        ExpectedScreens = expectedScreens,
        FocusStandard = focusStandard,
        // source.TargetApp is always known in record mode: it's either the --package/--bundle-id given, or
        // (Android only) the foreground app's package resolved once when recording started (see
        // AndroidScreenSource.ConnectAsync). Run history uses this to group and compare runs of the same app.
        AppId = source.TargetApp,
        // One entry per session; continuation's earlier sessions (if any) first, then this one, updated live
        // (EndedAt is "now" every time this is computed, i.e. after every screen -- see SaveAsync) so an
        // uncleanly-ended session still shows an honest, if slightly early, end time.
        Sessions = [.. continuation?.PriorSessions ?? [], new RecordingSession(_sessionStartedAt, DateTimeOffset.Now)],
        EndedEarlyReason = _endedEarlyReason is null ? null
            : $"{char.ToUpperInvariant(_endedEarlyReason[0])}{_endedEarlyReason[1..]}. " +
              "Screens after that point were not scanned; scan or test them manually.",
    };

    private string UniqueTitle(string title)
    {
        var count = _titles[title] = _titles.GetValueOrDefault(title) + 1;
        return count == 1 ? title : $"{title} ({count})";
    }

    /// <summary>Seeds <see cref="_titles"/> from a continued run's earlier screen names, so a title that
    /// already appeared (plainly, or already suffixed "(2)", "(3)", ...) keeps being uniquified correctly
    /// instead of restarting the count and colliding with an existing display name.</summary>
    private static Dictionary<string, int> SeedTitles(IReadOnlyList<ScreenResult>? priorResults)
    {
        var titles = new Dictionary<string, int>();
        if (priorResults is null)
            return titles;
        foreach (var name in priorResults.Select(r => r.ScreenName))
        {
            var match = TitleSuffixPattern.Match(name);
            var (baseName, count) = match.Success ? (match.Groups["base"].Value, int.Parse(match.Groups["n"].Value)) : (name, 1);
            titles[baseName] = Math.Max(titles.GetValueOrDefault(baseName), count);
        }
        return titles;
    }

    /// <summary>Waits one poll interval, returning early when a scan or stop is requested.</summary>
    private async Task WaitAsync(CancellationToken cancellationToken)
    {
        var until = DateTime.UtcNow + PollInterval;
        while (DateTime.UtcNow < until && !cancellationToken.IsCancellationRequested && !control.StopRequested && !control.ScanNowPending)
            await Task.Delay(100, CancellationToken.None);
    }
}

/// <summary>What <see cref="Recorder.StartLargeTextAsync"/> decided for one screen's large-text check.</summary>
internal enum LargeTextOutcomeKind { Captured, NotChecked, AwaitingNavigateBack }

/// <summary>See <see cref="LargeTextOutcomeKind"/>. <paramref name="PendingReason"/>/<paramref name="PendingConfirmedLiveFailure"/>
/// are only set for <see cref="LargeTextOutcomeKind.AwaitingNavigateBack"/> -- see
/// <see cref="Swipewalk.Collectors.IScreenSource.CompleteLargeTextAsync"/>. <paramref name="WentToAnotherScreen"/>
/// is true only when this screen's own live attempt just found <c>LargeTextCapture.WentToAnotherScreenLive</c>
/// (see <see cref="Recorder.AskAndMaybeBeginAsync"/>): <see cref="Recorder.ScanCurrentScreenAsync"/> sets
/// <see cref="ScreenSnapshot.LargeTextWentToAnotherScreen"/> from it, for <see cref="TextResizeNavigationRule"/>.</summary>
internal sealed record LargeTextOutcome(
    LargeTextOutcomeKind Kind, ScreenSnapshot? Snapshot, string? Reason, string? BaselineNote,
    string? PendingReason = null, bool PendingConfirmedLiveFailure = false, bool WentToAnotherScreen = false)
{
    public static LargeTextOutcome Captured(ScreenSnapshot snapshot) => new(LargeTextOutcomeKind.Captured, snapshot, null, null);

    public static LargeTextOutcome NotChecked(string reason, string? baselineNote, bool wentToAnotherScreen = false) =>
        new(LargeTextOutcomeKind.NotChecked, null, reason, baselineNote, WentToAnotherScreen: wentToAnotherScreen);

    public static LargeTextOutcome AwaitingNavigateBack(string reason, bool confirmedLiveFailure, bool wentToAnotherScreen = false) =>
        new(LargeTextOutcomeKind.AwaitingNavigateBack, null, null, null, reason, confirmedLiveFailure, wentToAnotherScreen);
}

/// <summary>Wording for <see cref="Recorder"/>'s <c>EndedEarlyReason</c>: factual, not alarming -- the report
/// and run history say plainly that the recording stopped before the person chose "Finish", and that screens
/// after that point were not scanned, without implying anything about them either way.</summary>
public static class EndedEarlyReasons
{
    public const string Cancelled = "the recording was cancelled";

    public static string Error(string message) => $"an error interrupted the recording: {message}";
}
