using Swipewalk.Collectors;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.ScreenReader;
using Swipewalk.Engine;

namespace Swipewalk.Core.Tests;

/// <summary>
/// A scriptable <see cref="IScreenSource"/> for Recorder tests, standing in for a real device: PeekAsync
/// returns the current entry of <see cref="Screens"/> (advance with <see cref="Advance"/>), CaptureAsync can
/// be told to throw, and the large-text methods report whatever a test configures. Everything unused by a
/// given test keeps a harmless default.
/// </summary>
internal sealed class FakeScreenSource : IScreenSource
{
    public Platform Platform => Platform.Android;
    public string LargeTextSetting => "fake 200%";
    public double LargeTextScale => 2.0;
    public string TargetApp => "org.example.app";

    /// <summary>Defaults to false (Android's real cheap peek); a test sets this true to stand in for iOS's
    /// per-capture session (see IScreenSource.PeekIsExpensive), where Recorder must not poll continuously.</summary>
    public bool PeekIsExpensive { get; set; }

    public int PeekCount { get; private set; }

    /// <summary>Screens PeekAsync/CaptureAsync serve, in order; <see cref="Advance"/> moves to the next one.
    /// A screen is a (title, extra distinguishing text) pair so ScreenIdentity sees a real change.</summary>
    public List<(string Title, string Marker)> Screens { get; } = [("Screen A", "a")];
    private int _index;

    public int CaptureCount { get; private set; }
    private int _throwOnCapture = -1;

    /// <summary>1-based: CaptureAsync throws <see cref="CaptureException"/> the Nth time it's called.</summary>
    public void ThrowOnNthCapture(int n, Exception ex)
    {
        _throwOnCapture = n;
        CaptureException = ex;
    }
    public Exception? CaptureException { get; private set; }

    /// <summary>What CaptureLargeTextAsync (the live-observe attempt) returns; defaults to "not in front" so
    /// tests that don't care about large text keep a harmless default.</summary>
    public Func<string, string, CancellationToken, Task<LargeTextCapture>>? LargeTextCaptureBehavior { get; set; }
    public int LargeTextCaptureCalls { get; private set; }
    public int BeginLargeTextCalls { get; private set; }
    public int CompleteLargeTextCalls { get; private set; }
    public int AbandonLargeTextCalls { get; private set; }
    public List<string> BeginReasons { get; } = [];

    /// <summary>Set by a test to make <see cref="BeginLargeTextAsync"/> throw -- standing in for a physical
    /// iPhone's read step failing before anything is changed (see IosScreenSource.BeginLargeTextCoreAsync).
    /// Thrown only once, on the next call, then cleared, so a test can assert a later screen still checks
    /// normally.</summary>
    public Exception? BeginLargeTextException { get; set; }

    public void Advance() => _index = Math.Min(_index + 1, Screens.Count - 1);

    public Task<bool> EnsureInFrontAsync(IProgress<string>? log = null, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<AccessibilityNode> PeekAsync(CancellationToken cancellationToken = default)
    {
        PeekCount++;
        return Task.FromResult(Tree(Screens[_index]));
    }

    public Task<ScreenSnapshot> CaptureAsync(string captureDir, string screenName, CancellationToken cancellationToken = default)
    {
        // Snapshot the current screen before incrementing CaptureCount, which a test waits on (via TestWait) to
        // know a capture has started and it's now safe to call Advance() for the next one. Reading Screens[_index]
        // lazily at the end of this method (in the returned ScreenSnapshot's initializer) raced a test's Advance()
        // call on another thread once CaptureCount was already visible as incremented but before this method's
        // return value was built: Advance() could land in between, so this call's own capture picked up the
        // already-advanced screen instead of the one it was asked to capture. That made two different screens
        // capture identical content, which Recorder's same-screen matching (correctly) then treated as a rescan --
        // an artifact of this fake's lazy field read, not something a real collector can do (it captures actual
        // on-device state atomically when called). Capturing the screen into a local first, before the counter
        // moves, makes CaptureCount>=1 a reliable "this call's content is already fixed" signal.
        var screen = Screens[_index];
        CaptureCount++;
        if (CaptureCount == _throwOnCapture)
            throw CaptureException!;
        // A real collector writes the capture (screenshot, raw tree) into captureDir; stand in for that with a
        // small marker file so tests can check a same-screen replacement actually deletes the superseded
        // screen's folder (see Recorder.DeleteScreenFolder / ScanCurrentScreenAsync).
        Directory.CreateDirectory(captureDir);
        File.WriteAllText(Path.Combine(captureDir, "capture.marker"), CaptureCount.ToString());
        return Task.FromResult(new ScreenSnapshot { Platform = Platform.Android, ScreenName = screenName, Root = Tree(screen) });
    }

    public async Task<LargeTextCapture> CaptureLargeTextAsync(string captureDir, string screenName, CancellationToken cancellationToken = default)
    {
        LargeTextCaptureCalls++;
        return LargeTextCaptureBehavior is not null
            ? await LargeTextCaptureBehavior(captureDir, screenName, cancellationToken)
            : LargeTextCapture.Skipped(LargeTextCapture.NotInFront);
    }

    public Task BeginLargeTextAsync(string reason, CancellationToken cancellationToken = default)
    {
        BeginLargeTextCalls++;
        BeginReasons.Add(reason);
        if (BeginLargeTextException is { } ex)
        {
            BeginLargeTextException = null;
            throw ex;
        }
        return Task.CompletedTask;
    }

    public Task<LargeTextCapture> CompleteLargeTextAsync(
        string captureDir, string screenName, ScreenSnapshot before, string reason, bool liveAttemptFailedHere, CancellationToken cancellationToken = default)
    {
        CompleteLargeTextCalls++;
        return Task.FromResult(LargeTextCapture.Captured(new ScreenSnapshot
        {
            Platform = Platform.Android, ScreenName = screenName, Root = Tree(Screens[_index]),
            LargeTextMethod = "system setting", LargeTextAppliedLive = liveAttemptFailedHere ? false : null, LargeTextRestartCaptured = true,
        }));
    }

    public Task AbandonLargeTextAsync(string reason, CancellationToken cancellationToken = default)
    {
        AbandonLargeTextCalls++;
        return Task.CompletedTask;
    }

    /// <summary>What CaptureScreenReaderAsync returns; defaults to null (nothing to attach), the harmless
    /// default for tests that don't care about it -- see IScreenSource.CaptureScreenReaderAsync's own default.</summary>
    public Func<ScreenSnapshot, ScreenReaderCapture?>? ScreenReaderCaptureBehavior { get; set; }
    public int ScreenReaderCaptureCalls { get; private set; }
    public List<IProgress<string>?> ScreenReaderCaptureLogs { get; } = [];

    public Task<ScreenReaderCapture?> CaptureScreenReaderAsync(ScreenSnapshot snapshot, IProgress<string>? log = null, CancellationToken cancellationToken = default)
    {
        ScreenReaderCaptureCalls++;
        ScreenReaderCaptureLogs.Add(log);
        return Task.FromResult(ScreenReaderCaptureBehavior?.Invoke(snapshot));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static AccessibilityNode Tree((string Title, string Marker) screen) => new()
    {
        Role = "window",
        Children =
        [
            new AccessibilityNode { Role = "text", VisibleText = screen.Title },
            new AccessibilityNode { Role = "text", VisibleText = screen.Marker },
        ],
    };
}

internal sealed class NoopLog : IProgress<string>
{
    public List<string> Lines { get; } = [];
    public void Report(string value) => Lines.Add(value);
}

/// <summary>
/// Polls a condition built from a fake's own counters (or a captured local, e.g. an ask-callback's call count)
/// until it's true, instead of a fixed <c>Task.Delay</c> guess. <see cref="RecorderControl.RequestScanNow"/> is
/// only noticed on <see cref="Recorder"/>'s next poll-loop wake-up (up to ~100ms, longer if the machine is
/// under load -- e.g. a shared CI runner), so a fixed short delay before <see cref="RecorderControl.Stop"/> can
/// be too short: if Stop() lands before Recorder has actually picked up the pending scan, the loop exits
/// without ever running it, silently dropping it (seen once in CI:
/// PeekExpensive_AutoOff_ScanNowReportsNotInFrontFromCapture -- the report was empty as expected, since nothing
/// was captured, but the "isn't in front" log line the test also expects was never written, because the scan
/// was never attempted at all). Waiting for a counter that only changes once the scan has actually started
/// (e.g. <see cref="FakeScreenSource.CaptureCount"/>, which increments before a scripted throw, not just on
/// success) guarantees Stop() is only called once Recorder is already committed to finishing that scan -- from
/// there, <c>await run</c> guarantees any log line or report update the scan produces (including on an
/// exception path) is visible, since RunAsync's task can't complete until that loop iteration does. Returns as
/// soon as the condition holds; only throws -- a real failure, not a timing fluke -- if it's still false after
/// <paramref name="timeout"/> (default generous enough for a loaded CI machine, well beyond any plausible delay).
/// </summary>
internal static class TestWait
{
    public static async Task UntilAsync(Func<bool> condition, string because, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException($"Timed out waiting for: {because}");
            await Task.Delay(10);
        }
    }
}

public class RecorderTests
{
    private static Recorder MakeRecorder(
        FakeScreenSource source, string outDir, bool autoScan, RecorderControl? control = null, NoopLog? log = null,
        LargeTextRestartPolicy policy = LargeTextRestartPolicy.Never, LargeTextRestartAsker? ask = null, bool largeText = false,
        RevisitSkippedScreensAsker? revisitAsk = null, RecordContinuation? continuation = null) =>
        new(source, outDir, "1.0.0", null, null, [], largeText, null, autoScan, control ?? new RecorderControl(), log ?? new NoopLog(), policy, ask,
            revisitAsk, continuation);

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cf-recorder-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task AutoOff_NoCaptureWithoutScanNow()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource();
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control);
            using var cts = new CancellationTokenSource();
            var run = recorder.RunAsync(cts.Token);
            await Task.Delay(400); // several poll ticks would have happened by now if auto-scan fired
            control.Stop();
            var report = await run;

            Assert.Empty(report.Screens);
            Assert.Equal(0, source.CaptureCount);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task AutoOff_ScanNowCapturesTheCurrentScreen()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource();
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "the screen to be captured");
            control.Stop();
            var report = await run;

            Assert.Single(report.Screens);
            Assert.Null(report.EndedEarlyReason);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task AutoOn_CapturesOnScreenChangeWithoutScanNow()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource();
            source.Screens.Add(("Screen B", "b"));
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: true, control: control);
            var run = recorder.RunAsync(CancellationToken.None);
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "the first screen to settle and be captured automatically");
            source.Advance();
            await TestWait.UntilAsync(() => source.CaptureCount >= 2, "the second screen to settle and be captured automatically");
            control.Stop();
            var report = await run;

            Assert.Equal(2, report.Screens.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Stands in for iOS's per-capture session (PeekIsExpensive true): with auto-scan off, Recorder
    /// must not call PeekAsync while the person is just navigating -- each call would open its own automation
    /// session and show iOS's banner -- but "Scan this screen now" must still work, calling PeekAsync at most
    /// the once a capture needs.</summary>
    [Fact]
    public async Task PeekExpensive_AutoOff_DoesNotPollButScanNowStillCaptures()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource { PeekIsExpensive = true };
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(400); // several poll ticks would have called PeekAsync by now if it were polling
            Assert.Equal(0, source.PeekCount);

            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "the screen to be captured");
            control.Stop();
            var report = await run;

            Assert.Single(report.Screens);
            Assert.Equal(1, source.CaptureCount);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Auto-scan needs continuous polling regardless of how expensive a single peek is (see
    /// PeekExpensive_AutoOff_DoesNotPollButScanNowStillCaptures) -- an iOS source built for auto-scan already
    /// keeps one session open for the whole recording, so PeekIsExpensive is false there in practice, but
    /// Recorder itself must still poll whenever autoScanOnScreenChange is on.</summary>
    [Fact]
    public async Task PeekExpensive_AutoOn_StillPollsAndCapturesOnScreenChange()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource { PeekIsExpensive = true };
            source.Screens.Add(("Screen B", "b"));
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: true, control: control);
            var run = recorder.RunAsync(CancellationToken.None);
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "the first screen to settle and be captured automatically");
            source.Advance();
            await TestWait.UntilAsync(() => source.CaptureCount >= 2, "the second screen to settle and be captured automatically");
            control.Stop();
            var report = await run;

            Assert.Equal(2, report.Screens.Count);
            Assert.True(source.PeekCount > 0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>With PeekIsExpensive and auto-scan off, "the app isn't in front" is only discovered when the
    /// person presses Scan (see the Recorder loop change above) -- the capture itself throws
    /// TargetNotInFrontException, and Recorder reports its own wording for that case (distinct from the
    /// polling path's "left the screen during capture", since nothing here confirmed the app was in front a
    /// moment ago).</summary>
    [Fact]
    public async Task PeekExpensive_AutoOff_ScanNowReportsNotInFrontFromCapture()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource { PeekIsExpensive = true };
            source.ThrowOnNthCapture(1, new TargetNotInFrontException("org.example.app is not in front; nothing was captured."));
            var control = new RecorderControl();
            var log = new NoopLog();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, log: log);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            // CaptureAsync's counter (see FakeScreenSource) increments before it throws, so this proves the
            // scan was actually attempted -- not just that RequestScanNow() was called -- before Stop() runs:
            // see TestWait's remarks for why that ordering is what the flake needed.
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "the scan attempt that throws TargetNotInFrontException");
            control.Stop();
            var report = await run;

            Assert.Empty(report.Screens);
            Assert.Contains(log.Lines, l => l.Contains("isn't in front, so nothing was captured", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Cancellation_EndsEarly_WithScreensCapturedSoFarKept()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource();
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control);
            using var cts = new CancellationTokenSource();
            var run = recorder.RunAsync(cts.Token);
            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "the screen to be captured before cancelling");
            cts.Cancel();
            var report = await run;

            Assert.Single(report.Screens);
            Assert.NotNull(report.EndedEarlyReason);
            Assert.Contains("cancelled", report.EndedEarlyReason);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ErrorOnAScreen_EndsEarly_WithEarlierScreensKept()
    {
        // Screen 1 and 2 are two distinct screens (same-screen replacement -- see the SameScreenRescan tests
        // below -- would otherwise fold two scans of the identical screen into one entry) and capture fine; the
        // 3rd throws (a device disconnecting, the harness dying, etc.). Screens 1-2 must still be in the
        // report, with an honest reason for why it stopped there.
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource();
            source.Screens.Add(("Screen B", "b"));
            source.ThrowOnNthCapture(3, new InvalidOperationException("device disconnected"));
            var control = new RecorderControl();
            var log = new NoopLog();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, log: log);
            var run = recorder.RunAsync(CancellationToken.None);
            for (var i = 0; i < 3; i++)
            {
                if (i == 1)
                    source.Advance();
                control.RequestScanNow();
                // Waiting for this iteration's own capture attempt (CaptureCount increments even on the 3rd,
                // which throws) before advancing/re-requesting -- otherwise a not-yet-picked-up RequestScanNow
                // from this iteration could be silently coalesced with the next one, and Advance() could land
                // before screen 1 was actually captured, collapsing two distinct screens into one.
                await TestWait.UntilAsync(() => source.CaptureCount >= i + 1, $"capture attempt {i + 1}");
            }
            var report = await run;

            Assert.Equal(2, report.Screens.Count);
            Assert.NotNull(report.EndedEarlyReason);
            Assert.Contains("device disconnected", report.EndedEarlyReason);
            Assert.Contains(log.Lines, l => l.Contains("ended early", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task UnexpectedExceptionType_StillEndsEarly_NotAnUnhandledCrash()
    {
        // A bug elsewhere in Swipewalk (or anything else unanticipated) must still leave an honest ended-early
        // note -- not just the specific "operational failure" types (IOException etc.) -- so the run still
        // shows up in History and `record --continue` can resume it: a crash of Swipewalk itself must still
        // appear in History with what was captured and an ended-early note. ArgumentException stands in for
        // "some exception type nobody specifically anticipated"; OperationCanceledException is deliberately
        // excluded (own catch, own meaning) and TargetNotInFrontException (also deliberately excluded -- means
        // "switched away", not "ended early") is covered by other tests.
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource();
            source.ThrowOnNthCapture(1, new ArgumentException("something nobody anticipated"));
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            // No Stop()/Cancel() follows -- ArgumentException propagates out of the loop on its own and ends
            // the recording, so awaiting `run` directly (no intervening delay to get wrong) is deterministic.
            var report = await run;

            Assert.NotNull(report.EndedEarlyReason);
            Assert.Contains("something nobody anticipated", report.EndedEarlyReason);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ErrorOnTheVeryLastScreenRequested_StillKeepsEarlierScreens()
    {
        // Only one screen is ever requested, and it fails: zero screens captured, but the report still comes
        // back (not an unhandled exception) with the reason, so a caller can still save it to history.
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource();
            source.ThrowOnNthCapture(1, new IOException("disk full"));
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            // Same reasoning as UnexpectedExceptionType_StillEndsEarly_NotAnUnhandledCrash above.
            var report = await run;

            Assert.Empty(report.Screens);
            Assert.NotNull(report.EndedEarlyReason);
            Assert.Contains("disk full", report.EndedEarlyReason);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LargeText_GrowsLive_NeverAsksOrTouchesTheSize()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource
            {
                LargeTextCaptureBehavior = (_, name, _) => Task.FromResult(LargeTextCapture.Captured(
                    new ScreenSnapshot { Platform = Platform.Android, ScreenName = name, Root = new AccessibilityNode { Role = "window" }, LargeTextAppliedLive = true })),
            };
            var control = new RecorderControl();
            var asked = false;
            Task<LargeTextRestartChoice> Ask(string _, string __, bool ____, Platform _____, CancellationToken ___) { asked = true; return Task.FromResult(LargeTextRestartChoice.RestartAndCheck); }
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, policy: LargeTextRestartPolicy.Ask, ask: Ask, largeText: true);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "the screen to be captured");
            control.Stop();
            var report = await run;

            Assert.Single(report.Screens);
            Assert.False(asked);
            Assert.Equal(0, source.BeginLargeTextCalls);
            Assert.True(report.Screens[0].LargeTextAppliedLive);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LargeText_NeedsRestart_PolicyNever_NotCheckedThisRun_AppNeverTouched()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource
            {
                LargeTextCaptureBehavior = (_, _, _) => Task.FromResult(LargeTextCapture.Skipped(LargeTextCapture.DidNotGrowLive)),
            };
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, policy: LargeTextRestartPolicy.Never, largeText: true);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "the screen to be captured");
            control.Stop();
            var report = await run;

            Assert.Single(report.Screens);
            Assert.Equal(LargeTextCapture.NotCheckedThisRun, report.Screens[0].LargeTextSkippedReason);
            Assert.Equal(0, source.BeginLargeTextCalls);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LargeText_NeedsRestart_PolicyAlways_ChecksWithoutAsking_SecondScanNowCompletesIt()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource
            {
                LargeTextCaptureBehavior = (_, _, _) => Task.FromResult(LargeTextCapture.Skipped(LargeTextCapture.DidNotGrowLive)),
            };
            var control = new RecorderControl();
            var asked = false;
            Task<LargeTextRestartChoice> Ask(string _, string __, bool ____, Platform _____, CancellationToken ___) { asked = true; return Task.FromResult(LargeTextRestartChoice.SkipForThisScreen); }
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, policy: LargeTextRestartPolicy.Always, ask: Ask, largeText: true);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow(); // normal capture + live observe (fails) + Begin, since policy is Always
            await TestWait.UntilAsync(() => source.BeginLargeTextCalls >= 1, "the large-text check to begin");
            Assert.Equal(1, source.BeginLargeTextCalls);
            Assert.Equal(0, source.CompleteLargeTextCalls); // still waiting for the person to navigate back
            control.RequestScanNow(); // completes the pending large-text check
            await TestWait.UntilAsync(() => source.CompleteLargeTextCalls >= 1, "the pending large-text check to complete");
            control.Stop();
            var report = await run;

            Assert.False(asked); // "always" decides on its own
            Assert.Single(report.Screens);
            Assert.Equal(1, source.CompleteLargeTextCalls);
            Assert.True(report.Screens[0].LargeTextRestartCaptured);
            // This screen's own live attempt confirmed DidNotGrowLive, so growing after the restart
            // legitimately means "applied after a restart" (false), not an unconfirmed guess (null).
            Assert.False(report.Screens[0].LargeTextAppliedLive);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LargeText_ReadFails_NotChecked_RecordingContinues_LaterScreenStillChecks()
    {
        // Standing in for BeginLargeTextAsync's very first step (reading the device's current text size,
        // shared by the physical-iPhone, Simulator and Android sources) failing before anything is changed:
        // this must be reported as a clean "not checked" reason for that one screen, not lose the whole
        // recording to an unhandled exception -- a later screen still gets a normal chance to check.
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource
            {
                LargeTextCaptureBehavior = (_, _, _) => Task.FromResult(LargeTextCapture.Skipped(LargeTextCapture.DidNotGrowLive)),
                BeginLargeTextException = new TextSizeReadFailedException("could not read this iPhone's current text size (iOS harness textsize-read failed: settings-text-size: element not found: Larger Text slider)"),
            };
            source.Screens.Add(("Screen B", "b"));
            var control = new RecorderControl();
            var log = new NoopLog();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, log: log, policy: LargeTextRestartPolicy.Always, largeText: true);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow(); // screen 1: live observe fails -> policy Always -> BeginLargeTextAsync throws
            await TestWait.UntilAsync(() => source.BeginLargeTextCalls >= 1, "the large-text check to be attempted");
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "screen 1 to be captured despite the failed begin");
            source.Advance();
            control.RequestScanNow(); // screen 2: begin succeeds this time (the exception was one-shot)
            await TestWait.UntilAsync(() => source.BeginLargeTextCalls >= 2, "screen 2's large-text check to begin");
            control.RequestScanNow(); // completes screen 2's pending check
            await TestWait.UntilAsync(() => source.CompleteLargeTextCalls >= 1, "screen 2's pending check to complete");
            control.Stop();
            var report = await run;

            Assert.Null(report.EndedEarlyReason); // the failure did not end the recording
            Assert.Equal(2, report.Screens.Count);
            Assert.Equal(LargeTextCapture.CouldNotReadTextSize, report.Screens[0].LargeTextSkippedReason);
            Assert.Contains(log.Lines, l => l.Contains("could not check", StringComparison.Ordinal) && l.Contains("Screen A", StringComparison.Ordinal));
            Assert.Equal(0, source.AbandonLargeTextCalls); // nothing was changed, so there's nothing to put back
            // Screen 2's own check went through normally: proves one screen's failed read doesn't poison later ones.
            Assert.Equal(1, source.CompleteLargeTextCalls);
            Assert.True(report.Screens[1].LargeTextRestartCaptured);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LargeText_BeginFailsAfterChange_NeutralReason_AbandonCalled()
    {
        // Unlike a read failure, a failure applying the larger size (or the relaunch after it) happens once
        // the device's original size was already read and remembered: the wording must not claim "nothing
        // was changed" (it might have been), and Recorder must ask AbandonLargeTextAsync to put it back as a
        // best effort rather than leaving the device enlarged until a separate preflight/doctor run.
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource
            {
                LargeTextCaptureBehavior = (_, _, _) => Task.FromResult(LargeTextCapture.Skipped(LargeTextCapture.DidNotGrowLive)),
                BeginLargeTextException = new InvalidOperationException("iOS harness textsize-restore failed: settings-text-size: verification failed"),
            };
            source.Screens.Add(("Screen B", "b"));
            var control = new RecorderControl();
            var log = new NoopLog();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, log: log, policy: LargeTextRestartPolicy.Always, largeText: true);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.AbandonLargeTextCalls >= 1, "the best-effort restore to run");
            control.Stop();
            var report = await run;

            Assert.Null(report.EndedEarlyReason); // the failure did not end the recording
            Assert.Single(report.Screens);
            Assert.Equal(LargeTextCapture.CouldNotStartCheck, report.Screens[0].LargeTextSkippedReason);
            Assert.DoesNotContain("nothing was changed", report.Screens[0].LargeTextSkippedReason);
            Assert.Equal(1, source.AbandonLargeTextCalls);
            Assert.Equal(0, source.CompleteLargeTextCalls); // never reached "navigate back and complete"
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LargeText_AskPolicy_DontCheck_ReasonIsDeclinedByPerson_AppNeverTouched()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource
            {
                LargeTextCaptureBehavior = (_, _, _) => Task.FromResult(LargeTextCapture.Skipped(LargeTextCapture.DidNotGrowLive)),
            };
            var control = new RecorderControl();
            Task<LargeTextRestartChoice> Ask(string _, string __, bool ____, Platform _____, CancellationToken ___) => Task.FromResult(LargeTextRestartChoice.SkipForThisScreen);
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, policy: LargeTextRestartPolicy.Ask, ask: Ask, largeText: true);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "the screen to be captured");
            control.Stop();
            var report = await run;

            Assert.Single(report.Screens);
            Assert.Equal(LargeTextCapture.DeclinedByPerson, report.Screens[0].LargeTextSkippedReason);
            Assert.Equal(0, source.BeginLargeTextCalls);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LargeText_LearnsFromFirstScreen_LaterScreenAsksBeforeTouchingTheSize()
    {
        // Screen 1 teaches the recording that this app needs a restart (one live-observe attempt, then
        // asked). Screen 2 must be asked BEFORE the size is touched at all -- no second live-observe attempt
        // (CaptureLargeTextAsync stays at 1 call).
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource
            {
                LargeTextCaptureBehavior = (_, _, _) => Task.FromResult(LargeTextCapture.Skipped(LargeTextCapture.WentToAnotherScreenLive)),
            };
            source.Screens.Add(("Screen B", "b"));
            var control = new RecorderControl();
            var askCount = 0;
            var reasonsAsked = new List<string>();
            var learnedFlags = new List<bool>();
            Task<LargeTextRestartChoice> Ask(string _, string reason, bool learnedFromEarlierScreen, Platform ____, CancellationToken ___)
            {
                askCount++;
                reasonsAsked.Add(reason);
                learnedFlags.Add(learnedFromEarlierScreen);
                return Task.FromResult(LargeTextRestartChoice.SkipForThisScreen);
            }
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, policy: LargeTextRestartPolicy.Ask, ask: Ask, largeText: true);
            var run = recorder.RunAsync(CancellationToken.None);

            await Task.Delay(200);
            control.RequestScanNow(); // screen 1: live observe -> WentToAnotherScreenLive -> asked
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "screen 1 to be captured");
            source.Advance();
            control.RequestScanNow(); // screen 2: already learned -> asked before touching the size
            await TestWait.UntilAsync(() => source.CaptureCount >= 2, "screen 2 to be captured");
            control.Stop();
            var report = await run;

            Assert.Equal(2, report.Screens.Count);
            Assert.Equal(2, askCount);
            Assert.All(reasonsAsked, r => Assert.Equal(LargeTextCapture.WentToAnotherScreenLive, r));
            Assert.Equal([false, true], learnedFlags); // screen 1 just learned it; screen 2 was told up front
            Assert.Equal(1, source.LargeTextCaptureCalls); // only screen 1 ever attempted the live capture
            Assert.Equal(0, source.BeginLargeTextCalls); // both screens declined
            Assert.All(report.Screens, s => Assert.Equal(LargeTextCapture.DeclinedByPerson, s.LargeTextSkippedReason));

            // ScreenSnapshot.LargeTextWentToAnotherScreen (set from screen 1's own live attempt) makes
            // TextResizeNavigationRule report a platform advisory -- but only for screen 1, the one that
            // actually exhibited it: screen 2 was only told the app behaves this way, its own capture never
            // showed it, so it must not get the same finding (see TextResizeNavigationRule's own remarks).
            Assert.Contains(report.Screens[0].Findings, f => f.RuleId == "text-resize-navigation" && f.Kind == FindingKind.PlatformAdvisory);
            Assert.DoesNotContain(report.Screens[1].Findings, f => f.RuleId == "text-resize-navigation");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LargeText_AlwaysSkip_StopsAskingButLaterScreensAreNotCheckedThisRun_NotDeclined()
    {
        // Answering "always skip" once is still the person deciding for that screen (DeclinedByPerson); every
        // later screen is skipped by the resulting policy without asking (NotCheckedThisRun), a real
        // distinction the report wording depends on.
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource
            {
                LargeTextCaptureBehavior = (_, _, _) => Task.FromResult(LargeTextCapture.Skipped(LargeTextCapture.DidNotGrowLive)),
            };
            source.Screens.Add(("Screen B", "b"));
            var control = new RecorderControl();
            var askCount = 0;
            Task<LargeTextRestartChoice> Ask(string _, string __, bool ____, Platform _____, CancellationToken ___) { askCount++; return Task.FromResult(LargeTextRestartChoice.AlwaysSkip); }
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, policy: LargeTextRestartPolicy.Ask, ask: Ask, largeText: true);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "screen 1 to be captured");
            source.Advance();
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 2, "screen 2 to be captured");
            control.Stop();
            var report = await run;

            Assert.Equal(2, report.Screens.Count);
            Assert.Equal(1, askCount); // asked once; "always skip" answered the second screen on its own
            Assert.Equal(LargeTextCapture.DeclinedByPerson, report.Screens[0].LargeTextSkippedReason);
            Assert.Equal(LargeTextCapture.NotCheckedThisRun, report.Screens[1].LargeTextSkippedReason);
            Assert.Equal(0, source.BeginLargeTextCalls);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Finish_WithScreensNotCheckedAtTheLargerSize_OffersToRevisit_AcceptingKeepsRecording()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource
            {
                LargeTextCaptureBehavior = (_, _, _) => Task.FromResult(LargeTextCapture.Skipped(LargeTextCapture.DidNotGrowLive)),
            };
            source.Screens.Add(("Screen B", "b"));
            var control = new RecorderControl();
            var offers = 0;
            Task<bool> Revisit(IReadOnlyList<string> names, CancellationToken _)
            {
                offers++;
                // Each screen is offered once (see Recorder's _offeredForRevisit): screen 1 at the first
                // offer, only the newly-scanned screen 2 at the second -- not screen 1 again.
                Assert.Single(names);
                return Task.FromResult(offers == 1); // accept the first offer, decline the second
            }
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, policy: LargeTextRestartPolicy.Never, largeText: true, revisitAsk: Revisit);
            var run = recorder.RunAsync(CancellationToken.None);

            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "screen 1 to be captured");
            control.Stop(); // first Finish: one screen not checked -> offer accepted -> recording continues

            await TestWait.UntilAsync(() => offers >= 1, "the revisit offer to be asked");
            Assert.Equal(1, offers);

            source.Advance();
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 2, "screen 2 to be captured");
            control.Stop(); // second Finish: still unchecked -> offer declined -> recording actually ends

            var report = await run;

            Assert.Equal(2, offers);
            Assert.Equal(2, report.Screens.Count);
            Assert.Null(report.EndedEarlyReason); // declining the offer is a normal finish, not "ended early"
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Finish_WhileALargeTextCheckIsPending_MarksItIncompleteInsteadOfLeavingItInProgress()
    {
        // The person agreed to check anyway, was told to navigate back, but pressed Finish instead of
        // completing it: the screen must not be left saying "in progress" forever.
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource
            {
                LargeTextCaptureBehavior = (_, _, _) => Task.FromResult(LargeTextCapture.Skipped(LargeTextCapture.DidNotGrowLive)),
            };
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, policy: LargeTextRestartPolicy.Always, largeText: true);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow(); // begins the check and asks to navigate back
            await TestWait.UntilAsync(() => source.BeginLargeTextCalls >= 1, "the large-text check to begin");
            Assert.Equal(1, source.BeginLargeTextCalls);
            control.Stop(); // Finish pressed before navigating back and pressing Scan again
            var report = await run;

            Assert.Single(report.Screens);
            Assert.Equal(LargeTextCapture.NotCompletedBeforeFinish, report.Screens[0].LargeTextSkippedReason);
            // The abandoned check still gets a best-effort restore attempt (AbandonLargeTextAsync -- never
            // CompleteLargeTextAsync, which would capture whatever happens to be in front) so the app/device
            // isn't left showing enlarged text -- see Recorder.AbandonPendingLargeTextAsync.
            Assert.Equal(1, source.AbandonLargeTextCalls);
            Assert.Equal(0, source.CompleteLargeTextCalls);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LargeText_LearnedScreen_CheckedAnyway_AppliedLiveIsNullNotFalse()
    {
        // Screen 2 never had its own live attempt (the recording already knew a restart was needed and asked
        // before touching the size) -- growing after the restart there must not be reported as "confirmed
        // applied only after a restart" (false), since that would assert an observation nobody made on this
        // screen. See IScreenSource.CompleteLargeTextAsync's liveAttemptFailedHere.
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource
            {
                LargeTextCaptureBehavior = (_, _, _) => Task.FromResult(LargeTextCapture.Skipped(LargeTextCapture.DidNotGrowLive)),
            };
            source.Screens.Add(("Screen B", "b"));
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, policy: LargeTextRestartPolicy.Always, largeText: true);
            var run = recorder.RunAsync(CancellationToken.None);

            await Task.Delay(200);
            control.RequestScanNow(); // screen 1: learns DidNotGrowLive, begins
            await TestWait.UntilAsync(() => source.BeginLargeTextCalls >= 1, "screen 1's large-text check to begin");
            control.RequestScanNow(); // screen 1: completes
            await TestWait.UntilAsync(() => source.CompleteLargeTextCalls >= 1, "screen 1's large-text check to complete");

            source.Advance();
            control.RequestScanNow(); // screen 2: already learned, begins without its own live attempt
            await TestWait.UntilAsync(() => source.BeginLargeTextCalls >= 2, "screen 2's large-text check to begin");
            control.RequestScanNow(); // screen 2: completes
            await TestWait.UntilAsync(() => source.CompleteLargeTextCalls >= 2, "screen 2's large-text check to complete");
            control.Stop();
            var report = await run;

            Assert.Equal(2, report.Screens.Count);
            Assert.False(report.Screens[0].LargeTextAppliedLive); // screen 1: confirmed live failure -> restart
            Assert.Null(report.Screens[1].LargeTextAppliedLive); // screen 2: never tested live on this screen
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LargeText_WentToAnotherScreenLive_CheckedAnyway_StillReportsTheNavigationAdvisory()
    {
        // The person checking anyway (and the later restart succeeding, via CompleteLargeTextAsync) must not
        // lose ScreenSnapshot.LargeTextWentToAnotherScreen: it describes what THIS screen's own live attempt
        // showed, before the restart, not the outcome of the restart itself -- see
        // LargeTextOutcome.WentToAnotherScreen's remarks.
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource
            {
                LargeTextCaptureBehavior = (_, _, _) => Task.FromResult(LargeTextCapture.Skipped(LargeTextCapture.WentToAnotherScreenLive)),
            };
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, policy: LargeTextRestartPolicy.Always, largeText: true);
            var run = recorder.RunAsync(CancellationToken.None);

            await Task.Delay(200);
            control.RequestScanNow(); // screen 1: live observe -> WentToAnotherScreenLive -> begins (Always)
            await TestWait.UntilAsync(() => source.BeginLargeTextCalls >= 1, "the large-text check to begin");
            control.RequestScanNow(); // screen 1: completes (navigated back, captured)
            await TestWait.UntilAsync(() => source.CompleteLargeTextCalls >= 1, "the large-text check to complete");
            control.Stop();
            var report = await run;

            Assert.Single(report.Screens);
            Assert.Equal(1, source.CompleteLargeTextCalls);
            Assert.Null(report.Screens[0].LargeTextSkippedReason); // the check itself succeeded
            Assert.Contains(report.Screens[0].Findings, f => f.RuleId == "text-resize-navigation" && f.Kind == FindingKind.PlatformAdvisory);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Cancellation_WhileLargeTextAskIsPending_EndsEarly_NotRecordedAsADecline()
    {
        // A cancellation (e.g. Ctrl+C at the CLI's C/D/A/N prompt) must not read as the person answering
        // "don't check this screen" -- the asker propagates it as OperationCanceledException (see
        // ConsoleRecordingInput.AskAsync), which must reach Recorder's own cancellation handling.
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource
            {
                LargeTextCaptureBehavior = (_, _, _) => Task.FromResult(LargeTextCapture.Skipped(LargeTextCapture.DidNotGrowLive)),
            };
            var control = new RecorderControl();
            using var askGate = new SemaphoreSlim(0);
            Task<LargeTextRestartChoice> Ask(string _, string __, bool ___, Platform ____, CancellationToken ct)
            {
                var tcs = new TaskCompletionSource<LargeTextRestartChoice>();
                ct.Register(() => tcs.TrySetCanceled(ct));
                askGate.Release(); // let the test know the prompt is up before it cancels
                return tcs.Task;
            }
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, policy: LargeTextRestartPolicy.Ask, ask: Ask, largeText: true);
            using var cts = new CancellationTokenSource();
            var run = recorder.RunAsync(cts.Token);
            await Task.Delay(200);
            control.RequestScanNow();
            await askGate.WaitAsync(TimeSpan.FromSeconds(5));
            cts.Cancel();
            var report = await run;

            Assert.NotNull(report.EndedEarlyReason);
            Assert.Contains("cancelled", report.EndedEarlyReason);
            Assert.Equal(0, source.BeginLargeTextCalls);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// IScreenSource.CaptureScreenReaderAsync (iOS's Accessibility Inspector route -- see
    /// IosScreenSource.CaptureScreenReaderAsync) is called for a screen's normal capture, and whatever it
    /// returns is attached to that screen's ScreenSnapshot before the rules run, so it ends up on the
    /// resulting ScreenResult.ScreenReaderCapture -- the same shape ScanService.ScanAsync uses for `scan`.
    /// Verified end to end on the iOS Simulator too (see the PR description); this checks Recorder's own
    /// wiring in isolation, without a device.
    /// </summary>
    [Fact]
    public async Task ScreenReaderCapture_AttachedToTheNormalCapture()
    {
        var dir = TempDir();
        try
        {
            var capture = new ScreenReaderCapture(ScreenReaderSource.AccessibilityInspector, "1", DateTimeOffset.Now, [], Complete: true, NotCompleteReason: null);
            var source = new FakeScreenSource { ScreenReaderCaptureBehavior = _ => capture };
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "the screen to be captured");
            control.Stop();
            var report = await run;

            Assert.Single(report.Screens);
            Assert.Equal(1, source.ScreenReaderCaptureCalls);
            Assert.Same(capture, report.Screens[0].ScreenReaderCapture);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The default no-op (IScreenSource.CaptureScreenReaderAsync's own default, and what
    /// FakeScreenSource returns with no ScreenReaderCaptureBehavior set) must never overwrite a snapshot that
    /// already carries a capture -- the case a real Android source is in: its TalkBack capture, when
    /// requested, already arrives attached to the snapshot CaptureAsync returns.</summary>
    [Fact]
    public async Task ScreenReaderCapture_NullResult_NeverOverwritesWhatCaptureAsyncAlreadyAttached()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource(); // ScreenReaderCaptureBehavior unset -> null, the default no-op
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "the screen to be captured");
            control.Stop();
            var report = await run;

            Assert.Single(report.Screens);
            Assert.Equal(1, source.ScreenReaderCaptureCalls);
            Assert.Null(report.Screens[0].ScreenReaderCapture);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Never attempted for a large-text capture (that path calls
    /// IScreenSource.CaptureLargeTextAsync/CompleteLargeTextAsync, never CaptureAsync directly) -- a rescan
    /// under a changed text size isn't what --screen-reader asked to capture again, the same choice
    /// ScanService's own appearance/orientation rescans make for their second captures.</summary>
    [Fact]
    public async Task ScreenReaderCapture_NeverCalledForALargeTextCapture()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource
            {
                ScreenReaderCaptureBehavior = _ => new ScreenReaderCapture(ScreenReaderSource.AccessibilityInspector, "1", DateTimeOffset.Now, [], true, null),
                LargeTextCaptureBehavior = (_, _, _) => Task.FromResult(LargeTextCapture.Skipped(LargeTextCapture.DidNotGrowLive)),
            };
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, autoScan: false, control: control, policy: LargeTextRestartPolicy.Never, largeText: true);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.LargeTextCaptureCalls >= 1, "the live large-text attempt to run");
            control.Stop();
            var report = await run;

            Assert.Single(report.Screens);
            // Exactly one normal capture happened (the live large-text attempt is a separate call, not
            // CaptureAsync), so exactly one screen-reader capture attempt, not two.
            Assert.Equal(1, source.ScreenReaderCaptureCalls);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

/// <summary>
/// Same-screen replacement (a rescan, within one run, replaces the earlier capture rather than adding a
/// duplicate entry -- never across separate runs) and `record --continue`'s
/// <see cref="RecordContinuation"/> (appending to, and recognizing rescans of, screens kept from an earlier
/// session of the same run). <see cref="ScanServiceTests"/>'s ResolveContinuationTests covers building a
/// RecordContinuation from a saved run; these cover Recorder's own behaviour once it has one.
/// </summary>
public class RecorderContinuationAndRescanTests
{
    private static Recorder MakeRecorder(
        FakeScreenSource source, string outDir, RecorderControl? control = null, NoopLog? log = null, bool largeText = false,
        LargeTextRestartPolicy policy = LargeTextRestartPolicy.Never, LargeTextRestartAsker? ask = null, RecordContinuation? continuation = null) =>
        new(source, outDir, "1.0.0", null, null, [], largeText, null, autoScanOnScreenChange: false, control ?? new RecorderControl(),
            log ?? new NoopLog(), policy, ask, revisitSkippedScreensAsk: null, continuation);

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cf-recorder-continue-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task SameScreenRescan_ReplacesEarlierCapture_DeletesOldFilesAndNotesTheRescan()
    {
        // The fake source stays on the same screen throughout (never Advance()d): two "Scan this screen now"
        // presses with nothing navigated in between are a rescan of the same screen, not two different ones.
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource();
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, control: control);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            var firstCaptureDir = Path.Combine(dir, "screens", "01");
            var firstMarker = Path.Combine(firstCaptureDir, "capture.marker");
            await TestWait.UntilAsync(() => File.Exists(firstMarker), "the first capture's marker file");
            Assert.True(File.Exists(firstMarker));

            control.RequestScanNow(); // rescan of the identical screen
            await TestWait.UntilAsync(() => source.CaptureCount >= 2, "the rescan capture");
            control.Stop();
            var report = await run;

            Assert.Single(report.Screens); // not counted twice
            Assert.Equal("Screen A", report.Screens[0].ScreenName); // kept the original display name, not "Screen A (2)"
            Assert.NotNull(report.Screens[0].RescannedAt);
            Assert.False(Directory.Exists(firstCaptureDir)); // the earlier capture's files were deleted
            Assert.True(File.Exists(Path.Combine(dir, "screens", "02", "capture.marker"))); // the newer capture is kept
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task DifferentScreens_AreNeverTreatedAsARescan()
    {
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource();
            source.Screens.Add(("Screen B", "b"));
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, control: control);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "screen 1 to be captured");
            source.Advance();
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 2, "screen 2 to be captured");
            control.Stop();
            var report = await run;

            Assert.Equal(2, report.Screens.Count);
            Assert.All(report.Screens, s => Assert.Null(s.RescannedAt));
            Assert.True(Directory.Exists(Path.Combine(dir, "screens", "01")));
            Assert.True(Directory.Exists(Path.Combine(dir, "screens", "02")));
            // Guided-check answers key off ScreenId, never list position -- two different screens must never
            // collide on the same id.
            Assert.NotEqual(report.Screens[0].ScreenId, report.Screens[1].ScreenId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task SameScreenRescan_KeepsTheSameScreenId()
    {
        // A guided-check answer recorded against the earlier capture (see ScreenResult.ScreenId's remarks)
        // must still resolve to the rescanned one -- verified here by reading results.json between the two
        // scans, rather than only the final report, so both ids are actually captured and compared.
        var dir = TempDir();
        try
        {
            var source = new FakeScreenSource();
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, control: control);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            var resultsPath = Path.Combine(dir, "results.json");
            // Wait for results.json to exist AND be fully written/parseable with a screen in it -- the
            // capture.marker file (written earlier, by the capture step itself) is not proof of that, and
            // reading too early raced with the save in practice (FileNotFoundException/empty read).
            string? firstScreenId = null;
            await TestWait.UntilAsync(() =>
            {
                try
                {
                    var report = File.Exists(resultsPath) ? Swipewalk.Core.Reports.JsonReport.Deserialize(File.ReadAllText(resultsPath)) : null;
                    firstScreenId = report?.Screens.Count > 0 ? report.Screens[0].ScreenId : null;
                    return firstScreenId is not null;
                }
                catch (IOException)
                {
                    return false; // still being written
                }
            }, "the first capture's results.json to be written with a screen id");
            Assert.False(string.IsNullOrEmpty(firstScreenId));

            control.RequestScanNow(); // rescan of the identical screen
            await TestWait.UntilAsync(() => source.CaptureCount >= 2, "the rescan capture");
            control.Stop();
            var report = await run;

            Assert.Single(report.Screens);
            Assert.Equal(firstScreenId, report.Screens[0].ScreenId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static ScreenResult PriorScreen(string name) => new() { Platform = Platform.Android, ScreenName = name, Findings = [] };

    [Fact]
    public async Task Continue_AppendsToPriorResults_AndReportsBothSessions()
    {
        var dir = TempDir();
        try
        {
            var earlierSession = new RecordingSession(
                new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 1, 9, 5, 0, TimeSpan.Zero));
            // A different screen from what the fake source will capture this session ("Screen A"), so it's
            // appended rather than recognized as a rescan.
            var continuation = new RecordContinuation(
                PriorResults: [PriorScreen("Home")],
                PriorScreens: [new RecordedScreenState(1, new ScreenFingerprint("Home", ["Home"]))],
                PriorSessions: [earlierSession],
                LearnedRestartReason: null,
                NextScreenNumber: 2,
                StateWasSaved: true);

            var source = new FakeScreenSource();
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, control: control, continuation: continuation);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "the screen to be captured");
            control.Stop();
            var report = await run;

            Assert.Equal(2, report.Screens.Count);
            Assert.Equal("Home", report.Screens[0].ScreenName);
            Assert.Equal("Screen A", report.Screens[1].ScreenName);
            // The new screen's folder continues from NextScreenNumber (2), not from PriorResults.Count + 1
            // (which would also be 2 here, so this also guards against ever going back to 1 and colliding).
            Assert.True(File.Exists(Path.Combine(dir, "screens", "02", "capture.marker")));

            Assert.Equal(2, report.Sessions.Count);
            Assert.Equal(earlierSession, report.Sessions[0]);
            Assert.True(report.Sessions[1].StartedAt > earlierSession.EndedAt);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Continue_SameScreenAsAPriorSession_ReplacesThatCaptureAndDeletesItsFiles()
    {
        var dir = TempDir();
        try
        {
            // Simulates screens/01 having been written by an earlier (now-finished) process.
            var earlierCaptureDir = Path.Combine(dir, "screens", "01");
            Directory.CreateDirectory(earlierCaptureDir);
            File.WriteAllText(Path.Combine(earlierCaptureDir, "capture.marker"), "from an earlier session");

            var continuation = new RecordContinuation(
                PriorResults: [PriorScreen("Screen A")],
                // Matches exactly what the fake source's default screen ("Screen A", "a") fingerprints to.
                PriorScreens: [new RecordedScreenState(1, new ScreenFingerprint("Screen A", ["Screen A", "a"]))],
                PriorSessions: [new RecordingSession(DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now.AddMinutes(-55))],
                LearnedRestartReason: null,
                NextScreenNumber: 2,
                StateWasSaved: true);

            var source = new FakeScreenSource(); // stays on ("Screen A", "a") -- the same screen as the prior session's
            var control = new RecorderControl();
            var recorder = MakeRecorder(source, dir, control: control, continuation: continuation);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "the screen to be captured");
            control.Stop();
            var report = await run;

            Assert.Single(report.Screens); // replaced, not appended
            Assert.Equal("Screen A", report.Screens[0].ScreenName);
            Assert.NotNull(report.Screens[0].RescannedAt);
            Assert.False(Directory.Exists(earlierCaptureDir)); // the prior session's capture was deleted
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Continue_LearnedRestartReason_CarriesOver_NoFreshLiveAttemptNeeded()
    {
        // An earlier session already learned (via its own live attempt) that this app needs a restart to check
        // large text further; continuing must ask before touching the size at all, the same as any later screen
        // within a single session -- never repeat the disruptive live attempt.
        var dir = TempDir();
        try
        {
            var continuation = new RecordContinuation(
                PriorResults: [PriorScreen("Home")],
                PriorScreens: [new RecordedScreenState(1, new ScreenFingerprint("Home", ["Home"]))],
                PriorSessions: [new RecordingSession(DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now.AddMinutes(-55))],
                LearnedRestartReason: LargeTextCapture.DidNotGrowLive,
                NextScreenNumber: 2,
                StateWasSaved: true);

            var source = new FakeScreenSource
            {
                LargeTextCaptureBehavior = (_, _, _) => Task.FromResult(LargeTextCapture.Skipped(LargeTextCapture.DidNotGrowLive)),
            };
            var control = new RecorderControl();
            var learnedFlags = new List<bool>();
            Task<LargeTextRestartChoice> Ask(string _, string __, bool learnedFromEarlierScreen, Platform ____, CancellationToken ___)
            {
                learnedFlags.Add(learnedFromEarlierScreen);
                return Task.FromResult(LargeTextRestartChoice.SkipForThisScreen);
            }
            var recorder = MakeRecorder(
                source, dir, control: control, largeText: true, policy: LargeTextRestartPolicy.Ask, ask: Ask, continuation: continuation);
            var run = recorder.RunAsync(CancellationToken.None);
            await Task.Delay(200);
            control.RequestScanNow();
            await TestWait.UntilAsync(() => source.CaptureCount >= 1, "the screen to be captured");
            control.Stop();
            var report = await run;

            Assert.Equal(2, report.Screens.Count);
            Assert.Equal([true], learnedFlags); // told up front, not "just learned it"
            Assert.Equal(0, source.LargeTextCaptureCalls); // no fresh live attempt
            Assert.Equal(LargeTextCapture.DeclinedByPerson, report.Screens[1].LargeTextSkippedReason);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
