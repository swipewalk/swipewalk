using Swipewalk.Collectors;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.ScreenReader;
using Swipewalk.Engine;

namespace Swipewalk.Core.Tests;

/// <summary>
/// The line ScanAsync prints before an Android scan with no --package, so the app actually in front
/// (which might not be the one the caller meant, e.g. TipCalc instead of BuggyApp) is named instead of scanned
/// silently. The live adb call that finds the foreground package needs a device, so only the message text
/// itself is unit-tested here; see AndroidCollector.ForegroundPackageAsync for the live lookup.
/// </summary>
public class ScanServiceTests
{
    [Fact]
    public void NoPackageGivenMessage_NamesTheForegroundApp_AndSuggestsPackageOption()
    {
        var message = ScanService.NoPackageGivenMessage("com.companyname.tipcalc");

        Assert.Equal("Scanning the app in front: com.companyname.tipcalc (pass --package to scan a specific app)", message);
    }

    [Fact]
    public void NoPackageGivenMessage_StillMentionsPackageOption_WhenTheForegroundAppIsUnknown()
    {
        var message = ScanService.NoPackageGivenMessage(null);

        Assert.Contains("--package", message);
        Assert.DoesNotContain("Scanning the app in front:", message);
    }
}

/// <summary>
/// <see cref="ScanService.AndroidLargeTextWentToAnotherScreen"/>: scan mode's equivalent of record's
/// LargeTextCapture.WentToAnotherScreenLive, feeding ScreenSnapshot.LargeTextWentToAnotherScreen for
/// Swipewalk.Core.Rules.TextResizeNavigationRule. The live adb/simctl
/// calls that produce the reason need a device, so only this pure decision is unit-tested here; the
/// Recorder-side wiring (and the resulting finding) is covered live, end to end, in RecorderTests.
/// </summary>
public class AndroidLargeTextWentToAnotherScreenTests
{
    [Fact]
    public void Android_DifferentScreenAtFirstAttempt_IsTrue()
    {
        Assert.True(ScanService.AndroidLargeTextWentToAnotherScreen(ios: false, LargeTextCapture.DifferentScreen));
    }

    [Theory]
    [InlineData(LargeTextCapture.DifferentScreenAfterRestart)] // Swipewalk's own restart, not the OS's -- different situation
    [InlineData(LargeTextCapture.DidNotGrowLive)]
    [InlineData(LargeTextCapture.NotInFront)]
    [InlineData(null)]
    public void Android_AnyOtherReason_IsFalse(string? reason)
    {
        Assert.False(ScanService.AndroidLargeTextWentToAnotherScreen(ios: false, reason));
    }

    [Fact]
    public void Ios_NeverTrue_EvenForTheSameReasonText()
    {
        // iOS's collector has no equivalent live-observe-before-any-restart signal; this must stay Android-only
        // regardless of what reason text happens to be passed.
        Assert.False(ScanService.AndroidLargeTextWentToAnotherScreen(ios: true, LargeTextCapture.DifferentScreen));
    }
}

/// <summary>
/// <see cref="ScanService.ResolveContinuation"/>: what `record --continue &lt;run&gt;` (or the desktop app's
/// Continue action) needs to resume an ended-early recording. Builds runs directly on
/// disk (run.json, results.json, and optionally record-state.json) rather than through a live Recorder, since
/// this is about reading a saved run back, not about recording one; RecorderTests covers Recorder's own side of
/// continuation (constructing a Recorder with a RecordContinuation and running it).
/// </summary>
public class ResolveContinuationTests
{
    private static RunHistory NewHistory(out string root)
    {
        root = Path.Combine(Path.GetTempPath(), $"cf-continue-{Guid.NewGuid():N}");
        return new RunHistory(root);
    }

    /// <summary>Writes a saved run directly (run.json + results.json, and record-state.json when given), the
    /// way Recorder/RunHistory leave one on disk, without needing a live device.</summary>
    private static RunRecord WriteRun(
        RunHistory history, string mode, string? endedEarlyReason, ScanReport report, RecordState? state = null, DateTimeOffset? startedAt = null)
    {
        var folder = history.NewRunFolder("org.example.app");
        report = report with { EndedEarlyReason = endedEarlyReason };
        File.WriteAllText(Path.Combine(folder, "results.json"), JsonReport.Serialize(report));
        File.WriteAllText(Path.Combine(folder, "report.html"), "<html></html>");
        if (state is not null)
            state.SaveAsync(folder).GetAwaiter().GetResult();
        var options = new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = folder, Package = "org.example.app" };
        return history.SaveAsync(
            new RunResult(report, Path.Combine(folder, "report.html"), Path.Combine(folder, "results.json")),
            options, mode, startedAt ?? DateTimeOffset.Now).GetAwaiter().GetResult();
    }

    private static ScanReport MinimalReport(params ScreenResult[] screens) => new()
    {
        ToolVersion = "1.0.0",
        Screens = screens,
    };

    private static ScreenResult Screen(string name) => new() { Platform = Platform.Android, ScreenName = name, Findings = [] };

    [Fact]
    public void EndedEarlyRecord_ResolvesByIdOrByFolder()
    {
        var history = NewHistory(out var root);
        try
        {
            var run = WriteRun(history, "record", "an error interrupted the recording", MinimalReport(Screen("Home")));

            var (byId, _, _) = ScanService.ResolveContinuation(history, run.Id);
            var (byFolder, _, _) = ScanService.ResolveContinuation(history, run.Folder);

            Assert.Equal(run.Id, byId.Id);
            Assert.Equal(run.Id, byFolder.Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NoSuchRun_Throws()
    {
        var history = NewHistory(out var root);
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ScanService.ResolveContinuation(history, "does-not-exist"));
            Assert.Contains("does-not-exist", ex.Message);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AScan_CannotBeContinued()
    {
        var history = NewHistory(out var root);
        try
        {
            var run = WriteRun(history, "scan", endedEarlyReason: null, MinimalReport(Screen("Home")));

            var ex = Assert.Throws<InvalidOperationException>(() => ScanService.ResolveContinuation(history, run.Id));
            Assert.Contains("not a recording", ex.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A recording that reached Finish is not offered Continue: it has nothing
    /// ended-early to resume.</summary>
    [Fact]
    public void ARecordingThatFinishedNormally_CannotBeContinued()
    {
        var history = NewHistory(out var root);
        try
        {
            var run = WriteRun(history, "record", endedEarlyReason: null, MinimalReport(Screen("Home")));

            var ex = Assert.Throws<InvalidOperationException>(() => ScanService.ResolveContinuation(history, run.Id));
            Assert.Contains("finished normally", ex.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A recording currently under way (here, or in another window -- see RunRecord.RecordingInProgress
    /// and RunHistory.List, which only leaves it true when the owning process is confirmed still running) isn't
    /// offered Continue either -- a different situation from "finished normally" above, so a different message:
    /// nothing ended early here, it just hasn't stopped yet.</summary>
    [Fact]
    public async Task ARecordingCurrentlyInProgress_CannotBeContinued()
    {
        var history = NewHistory(out var root);
        try
        {
            var folder = history.NewRunFolder("org.example.app");
            var report = MinimalReport(Screen("Home"));
            File.WriteAllText(Path.Combine(folder, "results.json"), JsonReport.Serialize(report));
            File.WriteAllText(Path.Combine(folder, "report.html"), "<html></html>");
            var options = new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = folder, Package = "org.example.app" };
            // recordingInProgress: true stamps this with the test process's own (live) pid and start time, the
            // same as RunHistory.StartRecordingAsync/OnScreenSaved would for a recording actually in progress.
            var run = await history.SaveAsync(
                new RunResult(report, Path.Combine(folder, "report.html"), Path.Combine(folder, "results.json")),
                options, "record", DateTimeOffset.Now, recordingInProgress: true);

            var ex = Assert.Throws<InvalidOperationException>(() => ScanService.ResolveContinuation(history, run.Id));
            Assert.Contains("still being recorded", ex.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EndedEarlyRecord_WithNoSavedRecordState_StartsFreshOnLearnedBehaviourAndIdentities()
    {
        // A run saved before record-state.json existed (or one whose file is missing/damaged): continuation
        // still works from just the report, starting fresh on learned large-text behaviour and screen
        // identities -- the caller (the CLI/desktop app) is the one that says so out loud; here we just check
        // nothing throws and nothing is guessed.
        var history = NewHistory(out var root);
        try
        {
            var run = WriteRun(history, "record", "the recording was cancelled", MinimalReport(Screen("Home"), Screen("Settings")));

            var (_, _, continuation) = ScanService.ResolveContinuation(history, run.Id);

            Assert.Equal(2, continuation.PriorResults.Count);
            Assert.Null(continuation.LearnedRestartReason);
            Assert.All(continuation.PriorIdentities, f => Assert.Equal(ScreenFingerprint.Unknown, f));
            Assert.Equal(3, continuation.NextScreenNumber); // never collides with screens/01, screens/02 on disk
            // False here, not just LearnedRestartReason being null, is what tells a caller (CLI/desktop) this
            // run genuinely has no saved resume state to say so about -- LearnedRestartReason null on its own
            // is ambiguous (see the next test: a saved state can also legitimately have nothing learned yet).
            Assert.False(continuation.StateWasSaved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EndedEarlyRecord_WithSavedRecordState_CarriesOverLearnedBehaviourAndIdentities()
    {
        var history = NewHistory(out var root);
        try
        {
            var fingerprint = new ScreenFingerprint("Home", ["Home", "Sign out"]);
            var state = new RecordState
            {
                NextScreenNumber = 2,
                LearnedRestartReason = LargeTextCapture.DidNotGrowLive,
                Screens = [new RecordedScreenState(1, fingerprint)],
            };
            var run = WriteRun(history, "record", "an error interrupted the recording", MinimalReport(Screen("Home")), state);

            var (_, _, continuation) = ScanService.ResolveContinuation(history, run.Id);

            Assert.Equal(LargeTextCapture.DidNotGrowLive, continuation.LearnedRestartReason);
            Assert.Equal(2, continuation.NextScreenNumber);
            var identity = Assert.Single(continuation.PriorIdentities);
            Assert.Equal(fingerprint.Title, identity.Title);
            Assert.Equal(fingerprint.Names, identity.Names);
            Assert.Equal([1], continuation.PriorScreenNumbers);
            Assert.True(continuation.StateWasSaved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EndedEarlyRecord_WithSavedStateButNothingLearnedYet_StillReportsStateWasSaved()
    {
        // A legitimate, common case: an earlier session saved its state, but the app hadn't needed a restart
        // to check large text yet on any of its screens (text grew live, or large text wasn't checked at all).
        // LearnedRestartReason is null here too, exactly like the no-saved-state case above -- StateWasSaved is
        // what tells them apart, so a caller doesn't wrongly say "starting fresh" when nothing was actually lost.
        var history = NewHistory(out var root);
        try
        {
            var state = new RecordState { NextScreenNumber = 2, LearnedRestartReason = null, Screens = [new RecordedScreenState(1, new ScreenFingerprint("Home", ["Home"]))] };
            var run = WriteRun(history, "record", "the recording was cancelled", MinimalReport(Screen("Home")), state);

            var (_, _, continuation) = ScanService.ResolveContinuation(history, run.Id);

            Assert.Null(continuation.LearnedRestartReason);
            Assert.True(continuation.StateWasSaved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EndedEarlyRecord_WithNoSavedSessions_SynthesizesOneFromTheRunsOwnTimes()
    {
        // A run saved before ScanReport.Sessions existed has an empty Sessions list on its report; continuation
        // still needs a first session to report "recorded across N sessions" honestly once a second one is
        // added -- built from the run's own StartedAt/FinishedAt (run.json always has both).
        var history = NewHistory(out var root);
        try
        {
            var startedAt = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
            var run = WriteRun(history, "record", "the recording was cancelled", MinimalReport(Screen("Home")), startedAt: startedAt);

            var (_, _, continuation) = ScanService.ResolveContinuation(history, run.Id);

            var session = Assert.Single(continuation.PriorSessions);
            Assert.Equal(startedAt, session.StartedAt);
            Assert.Equal(run.FinishedAt, session.EndedAt);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ContinuingOneRun_NeverTouchesASeparateRun()
    {
        // Separate runs are never merged, overwritten or replaced. Resolving (a read-only operation) one run
        // must not see or alter another saved run at all.
        var history = NewHistory(out var root);
        try
        {
            var other = WriteRun(history, "record", "the recording was cancelled", MinimalReport(Screen("Cart")));
            var target = WriteRun(history, "record", "the recording was cancelled", MinimalReport(Screen("Home")));
            var otherResultsBefore = File.ReadAllText(Path.Combine(other.Folder, "results.json"));
            var otherRunJsonBefore = File.ReadAllText(Path.Combine(other.Folder, "run.json"));

            var (run, _, continuation) = ScanService.ResolveContinuation(history, target.Id);

            Assert.Equal(target.Id, run.Id);
            Assert.Single(continuation.PriorResults);
            Assert.Equal("Home", continuation.PriorResults[0].ScreenName);
            Assert.Equal(otherResultsBefore, File.ReadAllText(Path.Combine(other.Folder, "results.json")));
            Assert.Equal(otherRunJsonBefore, File.ReadAllText(Path.Combine(other.Folder, "run.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
