using Swipewalk.Collectors.Android;
using Swipewalk.Collectors.Ios;
using Swipewalk.Core.Model;
using Swipewalk.Collectors;

namespace Swipewalk.Core.Tests;

/// <summary>
/// Covers the poll-then-degrade decision behind the large-text capture fix: a font/content-size change can
/// restart the app or briefly show another window, and the scan should wait for it to come back to front
/// instead of failing outright. These run without a device or simulator by faking the foreground check.
/// </summary>
public class ForegroundWaitTests
{
    /// <summary>Long enough that a loaded CI machine can't reach it: tests that check what happens when the
    /// app comes back must not depend on how fast the machine polls. They finish as soon as it does.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Short, but with room for several polls even on a slow shared runner, for the one test that
    /// is about the deadline itself.</summary>
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(1);

    [Fact]
    public async Task UntilInFrontAsync_AlreadyInFront_ReturnsTrueWithoutPolling()
    {
        var calls = 0;
        var result = await ForegroundWait.UntilInFrontAsync(() =>
        {
            calls++;
            return Task.FromResult(true);
        }, Timeout, PollInterval);

        Assert.True(result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task UntilInFrontAsync_ComesBackWithinTimeout_ReturnsTrue()
    {
        var calls = 0;
        var result = await ForegroundWait.UntilInFrontAsync(() =>
        {
            calls++;
            return Task.FromResult(calls >= 3); // in front on the third check
        }, Timeout, PollInterval);

        Assert.True(result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task UntilInFrontAsync_NeverComesBack_ReturnsFalseAfterTimeout()
    {
        var calls = 0;
        var result = await ForegroundWait.UntilInFrontAsync(() =>
        {
            calls++;
            return Task.FromResult(false);
        }, ShortTimeout, PollInterval);

        Assert.False(result);
        Assert.True(calls > 1, "should have polled more than once before giving up");
    }

    [Fact]
    public async Task UntilInFrontAsync_AlreadyCancelled_ChecksOnceThenGivesUp()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var calls = 0;

        var result = await ForegroundWait.UntilInFrontAsync(() =>
        {
            calls++;
            return Task.FromResult(false);
        }, Timeout, PollInterval, cts.Token);

        Assert.False(result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task UntilInFrontAsync_CancelledWhileWaiting_StopsPolling()
    {
        // Polling is verified here rather than by racing a deadline: the token is cancelled from inside the
        // check itself, so the result is the same on any machine, however slow.
        using var cts = new CancellationTokenSource();
        var calls = 0;

        var result = await ForegroundWait.UntilInFrontAsync(() =>
        {
            calls++;
            if (calls == 3)
                cts.Cancel();
            return Task.FromResult(false);
        }, Timeout, PollInterval, cts.Token);

        Assert.False(result);
        Assert.Equal(3, calls);
    }
}

/// <summary>The shared "skipped, with a reason" result that scan and record both report the same way.</summary>
public class LargeTextCaptureTests
{
    [Fact]
    public void Captured_HasSnapshotAndNoReason()
    {
        var snapshot = new Model.ScreenSnapshot { Platform = Model.Platform.Android, ScreenName = "s", Root = new Model.AccessibilityNode { Role = "screen" } };

        var result = LargeTextCapture.Captured(snapshot);

        Assert.Same(snapshot, result.Snapshot);
        Assert.Null(result.SkippedReason);
    }

    [Fact]
    public void Skipped_HasReasonAndNoSnapshot()
    {
        var result = LargeTextCapture.Skipped(LargeTextCapture.NotInFront);

        Assert.Null(result.Snapshot);
        Assert.Equal(LargeTextCapture.NotInFront, result.SkippedReason);
    }
}

/// <summary>
/// Covers the decision <see cref="IosCollector.CaptureLargeTextAsync"/> makes once a post-content-size-change
/// capture attempt has finished (or never succeeded): the one-shot iOS capture test activates the app itself
/// rather than reporting "not in front" the way the record-mode harness does, so a missing capture (not a
/// thrown exception) is the only signal that the app never came back to front.
/// </summary>
public class IosCollectorLargeTextEvaluateTests
{
    private static ScreenSnapshot Snapshot(string title) => new()
    {
        Platform = Platform.iOS,
        ScreenName = "",
        Root = new AccessibilityNode { Role = "window", Children = [new AccessibilityNode { Role = "text", VisibleText = title, Bounds = new Bounds(0, 0, 100, 20) }] },
    };

    [Fact]
    public void Evaluate_NoCapture_IsSkippedNotInFront()
    {
        var result = IosCollector.Evaluate(Snapshot("Checkout"), null);

        Assert.Null(result.Snapshot);
        Assert.Equal(LargeTextCapture.NotInFront, result.SkippedReason);
    }

    [Fact]
    public void Evaluate_SameScreenAtLargerSize_IsCaptured()
    {
        var normal = Snapshot("Checkout");
        var large = Snapshot("Checkout");

        var result = IosCollector.Evaluate(normal, large);

        Assert.Same(large, result.Snapshot);
        Assert.Null(result.SkippedReason);
    }

    [Fact]
    public void Evaluate_DifferentScreenAtLargerSize_IsSkippedDifferentScreen()
    {
        var normal = Snapshot("Checkout");
        var large = Snapshot("Home");

        var result = IosCollector.Evaluate(normal, large);

        Assert.Null(result.Snapshot);
        Assert.Equal(LargeTextCapture.DifferentScreen, result.SkippedReason);
    }
}

/// <summary>
/// Covers <see cref="IosCollector.CapturedAfterRestart"/>: the decision every iOS terminate + relaunch
/// escalation (physical iPhone, Simulator, and the per-app launch-argument fallback) reaches once it has a
/// post-restart capture. Regression coverage for a bug where the collector always reported
/// <c>LargeTextAppliedLive = false</c> after a same-screen restart even when the text never actually grew
/// (e.g. a framework/app that doesn't support Dynamic Type at all, or the phone's own text size already
/// stuck at the larger size before the check ran) -- misreporting "applied after a restart" when nothing was
/// ever applied, and suppressing the WCAG 1.4.4 "did not get taller" finding that should have fired instead.
/// </summary>
public class IosCollectorCapturedAfterRestartTests
{
    private static ScreenSnapshot Snapshot(string title, double height) => new()
    {
        Platform = Platform.iOS,
        ScreenName = "",
        Root = new AccessibilityNode { Role = "window", Children = [new AccessibilityNode { Role = "text", VisibleText = title, Bounds = new Bounds(0, 0, 100, height) }] },
    };

    [Fact]
    public void SameScreenTextGrew_IsCapturedWithAppliedLiveFalse()
    {
        // Matches observed growth after a genuine terminate + relaunch at AX3.
        var before = Snapshot("Pay a parking ticket", 27.5);
        var afterRestart = Snapshot("Pay a parking ticket", 120);

        var result = IosCollector.CapturedAfterRestart(before, afterRestart, "system setting", "different screen");

        Assert.NotNull(result.Snapshot);
        Assert.Same(afterRestart.Root, result.Snapshot!.Root);
        Assert.Null(result.SkippedReason);
        Assert.Equal("system setting", result.Snapshot.LargeTextMethod);
        Assert.False(result.Snapshot.LargeTextAppliedLive);
    }

    [Fact]
    public void SameScreenTextUnchanged_IsCapturedWithAppliedLiveNull()
    {
        // The bug this guards against: the app (or a phone whose text size was already stuck large before
        // the check started) never actually applied the setting, even after a genuine restart. The capture
        // is still kept -- TextResizeRule reports the 1.4.4 finding from it -- but AppliedLive must not
        // claim "false" (applied, just not live), since nothing was applied at all.
        var before = Snapshot("Pay a parking ticket", 27.5);
        var afterRestart = Snapshot("Pay a parking ticket", 27.5);

        var result = IosCollector.CapturedAfterRestart(before, afterRestart, "system setting", "different screen");

        Assert.NotNull(result.Snapshot);
        Assert.Same(afterRestart.Root, result.Snapshot!.Root);
        Assert.Null(result.SkippedReason);
        Assert.Equal("system setting", result.Snapshot.LargeTextMethod);
        Assert.Null(result.Snapshot.LargeTextAppliedLive);
    }

    [Fact]
    public void DifferentScreen_IsSkippedWithGivenReason()
    {
        var before = Snapshot("Checkout", 20);
        var afterRestart = Snapshot("Home", 20);

        var result = IosCollector.CapturedAfterRestart(before, afterRestart, "system setting", "different screen");

        Assert.Null(result.Snapshot);
        Assert.Equal("different screen", result.SkippedReason);
    }

    [Fact]
    public void LiveTestedFalse_TextGrew_IsCapturedWithAppliedLiveNull()
    {
        // Regression coverage for a false positive: the per-app launch-argument fallback relaunches the app
        // fresh with the larger size already set, so it never observed the app at the normal size first --
        // live-vs-restart was never actually tested, even though the after-restart capture shows growth.
        // Reporting "applied after a restart" here would claim something that was never observed.
        var before = Snapshot("Pay a parking ticket", 27.5);
        var afterRestart = Snapshot("Pay a parking ticket", 229);

        var result = IosCollector.CapturedAfterRestart(
            before, afterRestart, "per-app launch setting", LargeTextCapture.DifferentScreen, liveTested: false);

        Assert.Equal("per-app launch setting", result.Snapshot!.LargeTextMethod);
        Assert.Null(result.Snapshot.LargeTextAppliedLive);
        Assert.True(result.Snapshot.LargeTextRestartCaptured);
    }

    [Fact]
    public void LiveTestedFalse_TextUnchanged_IsCapturedWithAppliedLiveNull()
    {
        var before = Snapshot("Pay a parking ticket", 27.5);
        var afterRestart = Snapshot("Pay a parking ticket", 27.5);

        var result = IosCollector.CapturedAfterRestart(
            before, afterRestart, "per-app launch setting", LargeTextCapture.DifferentScreen, liveTested: false);

        Assert.Equal("per-app launch setting", result.Snapshot!.LargeTextMethod);
        Assert.Null(result.Snapshot.LargeTextAppliedLive);
        Assert.True(result.Snapshot.LargeTextRestartCaptured);
    }

    [Fact]
    public void LiveTestedDefaultsToTrue_UsesGivenMethod()
    {
        var before = Snapshot("Pay a parking ticket", 27.5);
        var afterRestart = Snapshot("Pay a parking ticket", 229);

        var result = IosCollector.CapturedAfterRestart(before, afterRestart, "per-app launch setting", LargeTextCapture.DifferentScreen);

        Assert.Equal("per-app launch setting", result.Snapshot!.LargeTextMethod);
        Assert.False(result.Snapshot.LargeTextAppliedLive);
    }
}

/// <summary>
/// Covers the decision behind Android's live-vs-restart escalation (<see cref="AndroidScreenSource.CaptureLargeTextAsync"/>):
/// with MAUI's MainActivity declaring ConfigChanges.FontScale, Android doesn't recreate the activity on a
/// font-scale change and text stays frozen until the app is force-stopped and relaunched; with the default
/// template the OS recreates the activity and text grows without a forced restart. These run without a device
/// by exercising the pure decision functions directly, the same pattern as <see cref="IosCollectorLargeTextEvaluateTests"/>.
/// </summary>
public class AndroidScreenSourceLargeTextDecisionTests
{
    private static ScreenSnapshot Snapshot(string title, double height = 20) => new()
    {
        Platform = Platform.Android,
        ScreenName = "",
        Root = new AccessibilityNode { Role = "window", Children = [new AccessibilityNode { Role = "text", VisibleText = title, Bounds = new Bounds(0, 0, 100, height) }] },
    };

    [Fact]
    public void DecideAfterFirstCapture_DifferentScreen_IsSkippedDifferentScreen()
    {
        var before = Snapshot("Checkout");
        var large = Snapshot("Home");

        var result = AndroidScreenSource.DecideAfterFirstCapture(before, large);

        Assert.NotNull(result);
        Assert.Null(result.Snapshot);
        Assert.Equal(LargeTextCapture.DifferentScreen, result.SkippedReason);
    }

    [Fact]
    public void DecideAfterFirstCapture_SameScreenTextGrew_IsCapturedLive()
    {
        // The default MAUI template: the OS recreates the activity on the font-scale change without a
        // forced restart, so this counts as live from the user's point of view.
        var before = Snapshot("Checkout", 20);
        var large = Snapshot("Checkout", 48); // matches an observed growth ratio

        var result = AndroidScreenSource.DecideAfterFirstCapture(before, large);

        Assert.NotNull(result);
        Assert.NotNull(result.Snapshot);
        Assert.Same(large.Root, result.Snapshot!.Root); // same capture, only Method/AppliedLive are added
        Assert.Null(result.SkippedReason);
        Assert.Equal("system setting", result.Snapshot.LargeTextMethod);
        Assert.True(result.Snapshot.LargeTextAppliedLive);
    }

    [Fact]
    public void DecideAfterFirstCapture_SameScreenTextUnchanged_ReturnsNullToEscalate()
    {
        // MainActivity declares ConfigChanges.FontScale: no activity recreation, text stays frozen.
        // Returning null tells the caller to force-stop + relaunch and try again.
        var before = Snapshot("Checkout", 20);
        var large = Snapshot("Checkout", 20);

        var result = AndroidScreenSource.DecideAfterFirstCapture(before, large);

        Assert.Null(result);
    }

    [Fact]
    public void DecideAfterRestart_SameScreenTextGrew_IsCapturedNotLive()
    {
        var before = Snapshot("Checkout", 20);
        var afterRestart = Snapshot("Checkout", 48);

        var result = AndroidScreenSource.DecideAfterRestart(before, afterRestart);

        Assert.NotNull(result.Snapshot);
        Assert.Same(afterRestart.Root, result.Snapshot!.Root); // same capture, only Method/AppliedLive are added
        Assert.Null(result.SkippedReason);
        Assert.Equal("system setting", result.Snapshot.LargeTextMethod);
        Assert.False(result.Snapshot.LargeTextAppliedLive);
    }

    [Fact]
    public void DecideAfterRestart_SameScreenTextUnchanged_IsCapturedWithAppliedLiveNull()
    {
        // Regression coverage: a force-stop + relaunch that still doesn't grow the text (the app never
        // applies the font scale at all, or the device's font scale was already stuck large before the
        // check started) must not be reported as "applied after a restart" -- that falsely implies the
        // restart fixed it. The after-restart capture is still kept so TextResizeRule reports the WCAG
        // 1.4.4 "did not get taller" finding.
        var before = Snapshot("Checkout", 20);
        var afterRestart = Snapshot("Checkout", 20);

        var result = AndroidScreenSource.DecideAfterRestart(before, afterRestart);

        Assert.NotNull(result.Snapshot);
        Assert.Same(afterRestart.Root, result.Snapshot!.Root);
        Assert.Null(result.SkippedReason);
        Assert.Equal("system setting", result.Snapshot.LargeTextMethod);
        Assert.Null(result.Snapshot.LargeTextAppliedLive);
    }

    [Fact]
    public void DecideAfterRestart_DifferentScreen_IsSkippedWithRestartReason()
    {
        var before = Snapshot("Checkout");
        var afterRestart = Snapshot("Home");

        var result = AndroidScreenSource.DecideAfterRestart(before, afterRestart);

        Assert.Null(result.Snapshot);
        // Now a single constant shared with the iOS escalation (see LargeTextCapture.DifferentScreenAfterRestart)
        // so Swipewalk.Engine.ScanService can recognize it across the assembly boundary and point to record
        // mode instead of "check by hand".
        Assert.Equal(LargeTextCapture.DifferentScreenAfterRestart, result.SkippedReason);
    }
}
