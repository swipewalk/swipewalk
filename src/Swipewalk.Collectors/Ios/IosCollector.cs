using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Swipewalk.Core.Imaging;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Rules;
using Swipewalk.Core.ScreenReader;

namespace Swipewalk.Collectors.Ios;

/// <summary>
/// Captures the foreground screen of an app on an iOS Simulator or a physical iPhone by running the XCUITest
/// harness in harness/ios. On a simulator the harness writes tree.json and screenshot.png straight into the
/// capture directory (simulators share the Mac's file system); on a physical device the files come back as
/// result-bundle attachments (<see cref="ExportAttachmentsAsync"/>).
/// </summary>
public static class IosCollector
{
    public const string TreeFile = "tree.json";
    public const string ScreenshotFile = "screenshot.png";
    public const string HarnessProject = "harness/ios/SwipewalkHarness.xcodeproj";

    /// <summary>Sidecar written by <see cref="CaptureAsync"/> next to tree.json when the framework was
    /// detected from the installed app bundle (Simulator only; see <see cref="DetectFrameworkAsync"/>), and
    /// read back by <see cref="Load"/>. Absent when detection wasn't attempted (a physical device) or found
    /// nothing (a native app), the same as an older capture made before this existed.</summary>
    internal const string FrameworkFile = "framework.json";

    /// <summary>
    /// Sidecar written by <see cref="CaptureAsync"/> next to tree.json with the bundle id used for the capture,
    /// and read back by <see cref="Load"/> into <see cref="ScreenSnapshot.AppId"/>. Live scans always know the
    /// bundle id already (it must be passed to capture at all), but a saved capture replayed later with
    /// <c>scan --from</c> has no other record of it; absent for captures made before this existed.
    /// </summary>
    internal const string BundleIdFile = "bundle-id.txt";

    internal sealed record DetectedFramework(AppFramework Framework, string? Version);

    /// <param name="team">Apple developer team id used to sign the harness for a physical device.</param>
    /// <param name="forceResultBundle">Use the physical-device result path on a simulator (for testing).</param>
    /// <param name="profile">Provisioning profile (UUID or name) to sign with; by default one is chosen.</param>
    /// <param name="bundlePrefix">Harness bundle id prefix, to fit a company wildcard profile.</param>
    /// <param name="relaunch">Terminate + relaunch the app under test instead of just activating it (some
    /// frameworks, e.g. .NET MAUI, only apply a system text-size change at launch).</param>
    /// <param name="launchArg">Space-separated launch arguments for the app under test (implies relaunch);
    /// used for the physical-device large-text fallback when driving Settings itself fails.</param>
    /// <param name="log">Receives <see cref="BringToFront.BroughtToFrontLog"/> when the harness had to bring a
    /// backgrounded app forward (see the "launched"/"broughtForward" keys the harness's <c>app(_:)</c> writes
    /// into tree.json, read back below).</param>
    /// <returns>True when the app under test had to be started from not running (see <see cref="BringToFront"/>);
    /// false when it was already in front, was merely brought forward, or this wasn't a plain scan capture.</returns>
    public static async Task<bool> CaptureAsync(
        string captureDir, string bundleId, string? device = null, string? harnessProject = null,
        string? team = null, bool forceResultBundle = false, string? profile = null, string? bundlePrefix = null,
        bool relaunch = false, string? launchArg = null, IProgress<string>? log = null)
    {
        captureDir = Path.GetFullPath(captureDir);
        Directory.CreateDirectory(captureDir);
        var udid = device ?? await BootedSimulatorAsync()
            ?? throw new InvalidOperationException("No booted iOS Simulator found; boot one or pass --device <udid>.");

        var found = (await Devices.IosAsync()).FirstOrDefault(d => d.Id == udid);
        if (found is not null)
            await Devices.SaveAsync(found, captureDir);

        // Physical devices can't write to the Mac: results come back as attachments in the result bundle.
        var viaResultBundle = found?.IsPhysical == true || forceResultBundle;
        var resultBundle = viaResultBundle
            ? Path.Combine(Path.GetTempPath(), $"swipewalk-{Guid.NewGuid():N}.xcresult")
            : null;
        SigningPlan? signing = null;
        if (found?.IsPhysical == true)
        {
            var (plan, problem) = SigningPlan.Create(team, profile, bundlePrefix, udid);
            signing = plan ?? throw new InvalidOperationException(problem);
            if (team is not null)
                SigningTeams.Remember(team);
        }
        var info = HarnessProcess(harnessProject, udid, bundleId, captureDir, "ScanTests/testCaptureScreen", serve: false,
            resultBundle, signing);
        if (relaunch)
            info.Environment["TEST_RUNNER_CF_RELAUNCH"] = "1";
        if (!string.IsNullOrEmpty(launchArg))
            info.Environment["TEST_RUNNER_CF_LAUNCH_ARG"] = launchArg;
        var (exitCode, output) = await RunAsync(info);
        if (resultBundle is not null)
        {
            if (Directory.Exists(resultBundle))
                await ExportAttachmentsAsync(resultBundle, captureDir);
            TryDelete(resultBundle);
        }
        var launched = false;
        if (exitCode == 0 && File.Exists(Path.Combine(captureDir, TreeFile)))
        {
            MaskStatusBar(captureDir);
            await File.WriteAllTextAsync(Path.Combine(captureDir, BundleIdFile), bundleId);
            // Physical devices can't be read from the Mac; DetectFrameworkAsync only works against a Simulator.
            if (found?.IsPhysical != true)
                await WriteFrameworkFileAsync(captureDir, udid, bundleId);
            (launched, var broughtForward) = ReadForegroundFlags(captureDir);
            if (broughtForward)
                log?.Report(BringToFront.BroughtToFrontLog(bundleId));
            else if (launched)
                log?.Report(BringToFront.StartedLog(bundleId));
        }
        if (exitCode != 0 || !File.Exists(Path.Combine(captureDir, TreeFile)))
        {
            if (output.Contains("Failed to initialize for UI testing", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The iPhone did not allow UI automation. Keep it unlocked and, when the scan starts, approve the Face ID / " +
                    "passcode prompt on the device. If no prompt appears, turn on Settings > Developer > Enable UI Automation.");
            var errors = string.Join('\n', output.Split('\n').Where(l => l.Contains("error", StringComparison.OrdinalIgnoreCase)).Take(10));
            throw new InvalidOperationException(
                $"iOS harness failed (xcodebuild exit {exitCode}). Is {bundleId} installed and running on {udid}?" +
                (signing is not null ? $" Harness signing: {signing.Description}." : "") +
                $"\n{errors}");
        }
        return launched;
    }

    /// <summary>Reads the "launched"/"broughtForward" keys the harness's <c>app(_:)</c> writes into tree.json
    /// (see <see cref="CaptureAsync"/>); both false for a capture made before these existed, or one not taken
    /// through <c>testCaptureScreen</c> (a record-mode peek/capture, which never brings the app forward).</summary>
    internal static (bool Launched, bool BroughtForward) ReadForegroundFlags(string captureDir)
    {
        // Same MaxDepth as XcuiTreeParser.Parse, and the same JsonException-to-InvalidOperationException
        // wrapping (XcuiTreeParser.UnreadableTreeMessage, so the two sites can't drift apart): this reads the
        // same tree.json, which can be far deeper than JsonDocument's 64-level default (a WKWebView's mirrored
        // DOM). In practice MaskStatusBar (called first, see CaptureAsync) already parses this file
        // successfully before this runs, but this stays defensive on its own.
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(captureDir, TreeFile)), XcuiTreeParser.TreeJsonOptions);
            var launched = doc.RootElement.TryGetProperty("launched", out var l) && l.GetBoolean();
            var broughtForward = doc.RootElement.TryGetProperty("broughtForward", out var b) && b.GetBoolean();
            return (launched, broughtForward);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{XcuiTreeParser.UnreadableTreeMessage} (details: {ex.Message})", ex);
        }
    }

    /// <summary>How long to keep retrying the capture after a content-size change before giving up as not-in-front.</summary>
    private static readonly TimeSpan LargeTextRetryTimeout = TimeSpan.FromSeconds(150);

    private static readonly TimeSpan LargeTextRetryPollInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Captures the screen again at accessibility text size AX3 (about 235%) into <paramref name="captureDir"/>,
    /// then restores the original size. Degrades to a <see cref="LargeTextCapture"/> skip instead of failing the
    /// scan: <see cref="LargeTextCapture.DifferentScreen"/> when the app shows a different screen at the larger
    /// size, and <see cref="LargeTextCapture.NotInFront"/> when it never comes back (on a physical device this can
    /// also mean the harness couldn't be signed to change its text size; see <see cref="CapturePhysicalLargeTextAsync"/>).
    /// Unlike the record-mode harness,
    /// the one-shot capture test (<c>ScanTests/testCaptureScreen</c>) activates the app itself rather than reporting
    /// "not in front" and leaving it alone, so it never throws <see cref="TargetNotInFrontException"/> here; a
    /// capture failure after the size change is the only signal available that the app hasn't come back, so it's
    /// retried for a while before giving up.
    /// </summary>
    /// <param name="team">Apple developer team id used to sign the harness for a physical device.</param>
    /// <param name="profile">Provisioning profile (UUID or name) to sign with; by default one is chosen.</param>
    /// <param name="bundlePrefix">Harness bundle id prefix, to fit a company wildcard profile.</param>
    /// <param name="log">Receives <see cref="PhysicalDeviceTextSizeNotice"/> once, before a physical iPhone's
    /// own text size is changed.</param>
    /// <param name="confirmRestart">Scan mode's ask-before-restart hook (see <see cref="ScanLargeTextRestartAsk"/>):
    /// asked just before the terminate + relaunch escalation, once the live attempt finds the text didn't
    /// visibly grow. Null proceeds with the restart automatically, the same as before this parameter existed.</param>
    public static async Task<LargeTextCapture> CaptureLargeTextAsync(
        string captureDir, string bundleId, ScreenSnapshot normal, string? device = null, string? harnessProject = null,
        string? team = null, string? profile = null, string? bundlePrefix = null, IProgress<string>? log = null,
        CancellationToken cancellationToken = default, ScanLargeTextRestartAsk? confirmRestart = null)
    {
        var udid = device ?? await BootedSimulatorAsync()
            ?? throw new InvalidOperationException("No booted iOS Simulator found.");
        if ((await Devices.IosAsync()).Any(d => d.Id == udid && d.IsPhysical))
            return await CapturePhysicalLargeTextAsync(
                captureDir, bundleId, normal, udid, harnessProject, team, profile, bundlePrefix, log, cancellationToken, confirmRestart);

        var original = (await Simctl("ui", udid, "content_size")).Trim();
        // Check before touching anything: if the Simulator's text size is already enlarged, a normal-size
        // capture isn't at default size, and enlarging it further would compare large-vs-large. Nothing has
        // been changed yet, so there's nothing to restore.
        if (BaselineTextSize.IsSimulatorEnlarged(original))
            return LargeTextCapture.SkippedBaselineEnlarged(original);
        if (original.Length > 0)
            TextSizeRestore.Remember(udid, original);
        // Set once the escalation below relaunches the app while content_size is still enlarged. Some
        // frameworks (.NET MAUI among them) only re-read the system text size at launch and
        // cache it; restoring content_size in the `finally` block changes the setting back, but the app's own
        // process keeps rendering at the larger size until it is relaunched again. Left alone, a later scan's
        // baseline capture (a bare activate(), not a fresh launch -- see ScanService) would silently reuse
        // that stale, already-enlarged process and measure "no growth" against an already-grown baseline
        // (reproduced 2026-09-22: a study's three apps all measured the same height before and after AX3,
        // because each app was still the relaunched-at-AX3 process left running by an earlier check).
        var relaunchedAtLargerSize = false;
        try
        {
            await Simctl("ui", udid, "content_size", IosScreenSource.LargeContentSize);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            ScreenSnapshot? large = null;
            await ForegroundWait.UntilInFrontAsync(async () =>
                {
                    try
                    {
                        await CaptureAsync(captureDir, bundleId, udid, harnessProject);
                        large = Load(captureDir, normal.ScreenName);
                        return true;
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }
                },
                LargeTextRetryTimeout, LargeTextRetryPollInterval, cancellationToken);
            var live = Evaluate(normal, large);
            if (live.Snapshot is not { } liveSnapshot)
                return live;
            if (TextGrowth.LooksGrown(normal, liveSnapshot))
                return LargeTextCapture.Captured(liveSnapshot with { LargeTextMethod = "system setting", LargeTextAppliedLive = true });

            // Text didn't visibly grow while the app just came back to front: some frameworks (.NET MAUI
            // among them) only apply the new content size at launch. Terminate + relaunch the app in the
            // Simulator and try again, the same escalation the physical-device flow uses -- unless the person
            // asked not to (see ScanLargeTextRestartAsk).
            if (confirmRestart is not null && await confirmRestart(LargeTextCapture.DidNotGrowLive, cancellationToken) is { } declinedReason)
                return LargeTextCapture.Skipped(declinedReason);
            relaunchedAtLargerSize = true;
            await SimctlRelaunchAsync(udid, bundleId);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            ScreenSnapshot afterRestart;
            try
            {
                await CaptureAsync(captureDir, bundleId, udid, harnessProject);
                afterRestart = Load(captureDir, normal.ScreenName);
            }
            catch (InvalidOperationException)
            {
                return LargeTextCapture.Skipped(LargeTextCapture.NotInFront);
            }
            return CapturedAfterRestart(normal, afterRestart, "system setting", LargeTextCapture.DifferentScreenAfterRestart);
        }
        finally
        {
            if (original.Length > 0)
            {
                await Simctl("ui", udid, "content_size", original);
                TextSizeRestore.Forget(udid);
            }
            if (LargeTextReset.Decide(relaunchedAtLargerSize, recordMode: false) == LargeTextResetAction.Terminate)
                await SimctlTerminateAsync(udid, bundleId);
        }
    }

    /// <summary>
    /// Physical-iPhone large-text flow: read the original Settings state through the harness and remember it
    /// (<see cref="TextSizeRestore"/>, keyed by UDID) BEFORE anything is changed, then drive Settings to AX3 →
    /// return to the app under test (activate; same screen) → capture. If the text visibly grew ("live"), use
    /// that capture; MAUI and some other frameworks only apply the new size at launch, so if it didn't grow,
    /// terminate + relaunch the app and capture again ("after restart"); if that shows a different screen, skip
    /// with a clear reason instead of reporting the wrong screen. Always restores the exact original state in
    /// <c>finally</c>, forgetting the marker only once the harness confirms the restore. If reading the
    /// original size fails, nothing has been changed yet, so this falls back to a per-app launch-argument
    /// override instead of guessing -- the same fallback used when driving Settings fails while *applying*
    /// AX3 (the marker is already recorded by then, and the <c>finally</c> below still restores Settings
    /// itself, in case the failed apply attempt left it partway changed) or the harness can't be reached at
    /// all; the override affects only that process and needs no Settings restore beyond relaunching once
    /// more without it. The scan never fails because of large text:
    /// every path here returns a <see cref="LargeTextCapture"/>, never throws.
    /// </summary>
    private static async Task<LargeTextCapture> CapturePhysicalLargeTextAsync(
        string captureDir, string bundleId, ScreenSnapshot normal, string udid, string? harnessProject,
        string? team, string? profile, string? bundlePrefix, IProgress<string>? log, CancellationToken cancellationToken,
        ScanLargeTextRestartAsk? confirmRestart = null)
    {
        var (plan, problem) = SigningPlan.Create(team, profile, bundlePrefix, udid);
        if (plan is null)
            return LargeTextCapture.Skipped($"the iOS harness could not be signed for this iPhone to change its text size: {problem}");
        if (team is not null)
            SigningTeams.Remember(team);
        log?.Report(PhysicalDeviceTextSizeNotice.Text);

        // Read first, without changing anything. A failure here (harness/driver error, or an unparseable
        // reading) leaves the phone untouched, so there's nothing to remember and nothing to restore --
        // fall back to the per-app launch argument, the same as a failure driving Settings further below.
        TextSizeState original;
        try
        {
            original = await ReadTextSizeAsync(harnessProject, udid, plan, cancellationToken)
                ?? throw new InvalidOperationException("the iOS harness reported this iPhone's text size in an unexpected format");
        }
        catch (InvalidOperationException)
        {
            return await CapturePhysicalLargeTextFallbackAsync(captureDir, bundleId, normal, udid, harnessProject, team, profile, bundlePrefix, cancellationToken);
        }

        if (BaselineTextSize.IsPhysicalEnlarged(original))
            // Nothing has been changed yet (the read above doesn't touch Settings), so there's nothing to restore.
            return LargeTextCapture.SkippedBaselineEnlarged(original.ToString());

        // The marker is written now, before AX3 is applied below -- not after, as the combined read+apply
        // step used to do. A failure applying AX3, or anywhere after, now leaves a marker pointing at the
        // true original for the next preflight/doctor check to restore, instead of a phone changed with
        // nothing recorded.
        TextSizeRestore.Remember(udid, original.ToString());
        // Set once the escalation below relaunches the app under test while Settings is still at AX3 --
        // the same staleness risk as the Simulator path above (see its comment), just reached through
        // Settings instead of `simctl ui content_size`.
        var relaunchedAtLargerSize = false;
        try
        {
            try
            {
                await SetTextSizeAsync(harnessProject, udid, plan, TextSizeState.Ax3, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Applying AX3 itself failed (a driver error, not a growth question): the marker above
                // already protects the phone (the `finally` below, or the next preflight/doctor check,
                // restores it), so fall back to a per-app launch argument instead of failing the whole scan.
                return await CapturePhysicalLargeTextFallbackAsync(captureDir, bundleId, normal, udid, harnessProject, team, profile, bundlePrefix, cancellationToken);
            }

            await CaptureAsync(captureDir, bundleId, udid, harnessProject, team, forceResultBundle: true, profile, bundlePrefix);
            var live = Load(captureDir, normal.ScreenName);
            if (TextGrowth.LooksGrown(normal, live))
                return LargeTextCapture.Captured(live with { LargeTextMethod = "system setting", LargeTextAppliedLive = true });

            // Text didn't visibly grow while the app just came back to front: some frameworks (.NET MAUI
            // among them) only apply the new size at launch. Terminate + relaunch and try again
            // -- unless the person asked not to (see ScanLargeTextRestartAsk).
            if (confirmRestart is not null && await confirmRestart(LargeTextCapture.DidNotGrowLive, cancellationToken) is { } declinedReason)
                return LargeTextCapture.Skipped(declinedReason);
            relaunchedAtLargerSize = true;
            await CaptureAsync(captureDir, bundleId, udid, harnessProject, team, forceResultBundle: true, profile, bundlePrefix, relaunch: true);
            var afterRestart = Load(captureDir, normal.ScreenName);
            return CapturedAfterRestart(normal, afterRestart, "system setting", LargeTextCapture.DifferentScreenAfterRestart);
        }
        catch (InvalidOperationException)
        {
            return LargeTextCapture.Skipped(LargeTextCapture.NotInFront);
        }
        finally
        {
            try
            {
                await RestoreTextSizeAsync(harnessProject, udid, plan, original.ToString(), cancellationToken);
                TextSizeRestore.Forget(udid);
                // Settings is back to normal, but a relaunched process caches whatever size it launched
                // with (see above): a physical device has no separate "stop" primitive reachable from here,
                // so scan mode's equivalent of the Simulator's outright terminate is one more relaunch, now
                // that there's nothing enlarged left for it to pick up. Captures into a throwaway directory
                // (not `captureDir`, whose tree.json/screenshot.png back the snapshot already returned above)
                // and discards the result; best effort -- if it fails, the app is left running at the stale
                // size until the next normal launch.
                if (LargeTextReset.Decide(relaunchedAtLargerSize, recordMode: false) == LargeTextResetAction.Terminate)
                {
                    var resetDir = Path.Combine(Path.GetTempPath(), $"swipewalk-textsize-reset-{Guid.NewGuid():N}");
                    try { await CaptureAsync(resetDir, bundleId, udid, harnessProject, team, forceResultBundle: true, profile, bundlePrefix, relaunch: true); }
                    catch (InvalidOperationException) { /* best effort: nothing else can be done from here */ }
                    finally { TryDelete(resetDir); }
                }
            }
            catch (InvalidOperationException)
            {
                // Leave the marker: the next preflight/doctor check restores it (see Preflight.IosAsync).
            }
        }
    }

    /// <summary>Per-app launch-argument fallback (see <see cref="CapturePhysicalLargeTextAsync"/>): affects only
    /// the app under test's own process, so there's nothing in Settings to restore, only the app to relaunch
    /// once more without the argument.</summary>
    private const string LaunchArgumentContentSizeCategory = "-UIPreferredContentSizeCategoryName UICTContentSizeCategoryAccessibilityExtraLarge";

    private static async Task<LargeTextCapture> CapturePhysicalLargeTextFallbackAsync(
        string captureDir, string bundleId, ScreenSnapshot normal, string udid, string? harnessProject,
        string? team, string? profile, string? bundlePrefix, CancellationToken cancellationToken)
    {
        try
        {
            await CaptureAsync(captureDir, bundleId, udid, harnessProject, team, forceResultBundle: true, profile, bundlePrefix,
                launchArg: LaunchArgumentContentSizeCategory);
            var large = Load(captureDir, normal.ScreenName);
            // This fallback relaunches the app fresh with the launch argument already set: it was never seen
            // running at the normal size first, so live-vs-restart was never tested (see CapturedAfterRestart).
            return CapturedAfterRestart(normal, large, "per-app launch setting", LargeTextCapture.DifferentScreen, liveTested: false);
        }
        catch (InvalidOperationException)
        {
            return LargeTextCapture.Skipped("could not change this iPhone's text size (Settings automation failed) and the per-app launch-argument fallback also failed");
        }
        finally
        {
            // Relaunch without the launch argument so the process isn't left rendering at the fallback's
            // enlarged size. Captures into a throwaway directory, not `captureDir` (whose tree.json/screenshot.png
            // back the snapshot already returned above), and discards the result.
            var resetDir = Path.Combine(Path.GetTempPath(), $"swipewalk-textsize-reset-{Guid.NewGuid():N}");
            try { await CaptureAsync(resetDir, bundleId, udid, harnessProject, team, forceResultBundle: true, profile, bundlePrefix, relaunch: true); }
            catch (InvalidOperationException) { /* best effort: the app is left with the fallback's content size until next launched normally */ }
            finally { TryDelete(resetDir); }
        }
    }

    /// <summary>
    /// Runs a Settings text-size one-shot test method (ScanTests/testTextSizeRead|Restore) and returns its
    /// textsize.json as a dictionary, or throws with the harness's machine-readable error. Always uses a
    /// result bundle (CF_ATTACH=1): only used for physical devices, whose files the Mac can't read directly.
    /// </summary>
    private static async Task<Dictionary<string, string>> RunTextSizeStepAsync(
        string? harnessProject, string udid, string testMethod, SigningPlan signing, string? target, CancellationToken cancellationToken)
    {
        var workDir = Path.Combine(Path.GetTempPath(), $"swipewalk-textsize-{Guid.NewGuid():N}");
        var resultBundle = Path.Combine(Path.GetTempPath(), $"swipewalk-{Guid.NewGuid():N}.xcresult");
        Directory.CreateDirectory(workDir);
        try
        {
            var info = HarnessProcess(harnessProject, udid, "-", workDir, "ScanTests/" + testMethod, serve: false, resultBundle, signing);
            if (target is not null)
                info.Environment["TEST_RUNNER_CF_TEXTSIZE_TARGET"] = target;
            var (exitCode, output) = await RunAsync(info);
            if (Directory.Exists(resultBundle))
                await ExportAttachmentsAsync(resultBundle, workDir, [TextSizeFile]);
            var jsonPath = Path.Combine(workDir, TextSizeFile);
            if (exitCode != 0 || !File.Exists(jsonPath))
            {
                var errors = string.Join('\n', output.Split('\n').Where(l => l.Contains("error", StringComparison.OrdinalIgnoreCase)).Take(5));
                throw new InvalidOperationException($"iOS text-size step {testMethod} failed (xcodebuild exit {exitCode}). {errors}");
            }
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(jsonPath));
            var result = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
            if (result.GetValueOrDefault("ok") == "false")
                throw new InvalidOperationException($"iOS text-size step {testMethod} failed: {result.GetValueOrDefault("error", "unknown error")}");
            return result;
        }
        finally
        {
            TryDelete(workDir);
            if (Directory.Exists(resultBundle))
                TryDelete(resultBundle);
        }
    }

    private const string TextSizeFile = "textsize.json";

    /// <summary>
    /// Restores a physical iPhone's Settings text-size state to exactly <paramref name="state"/> (a
    /// <see cref="TextSizeState"/>, as stored by <see cref="TextSizeRestore"/>) through the harness. Used by
    /// <c>swipewalk doctor</c>/preflight to finish an interrupted large-text check (see
    /// Swipewalk.Collectors.Preflight.Preflight.IosAsync) the same way the Android and Simulator paths do.
    /// </summary>
    public static async Task RestoreTextSizeAsync(string? harnessProject, string udid, SigningPlan signing, string state, CancellationToken cancellationToken = default) =>
        await RunTextSizeStepAsync(harnessProject, udid, "testTextSizeRestore", signing, state, cancellationToken);

    /// <summary>
    /// Moves a physical iPhone's Settings text-size state to exactly <paramref name="target"/> through the
    /// harness -- e.g. AX3, to start a large-text check, once <see cref="ReadTextSizeAsync"/> has already
    /// found and remembered the true original (see <see cref="TextSizeRestore"/>). Uses the same underlying
    /// harness step as <see cref="RestoreTextSizeAsync"/> (ScanTests/testTextSizeRestore): the harness makes
    /// no distinction between "set" and "restore", both are exactly "make Settings show this state",
    /// verified by read-back. There used to be a separate combined read+apply step
    /// (testTextSizeSetAX3/textsize-set) that read the original and moved to AX3 in one harness round trip;
    /// it was removed because a failure partway through it could leave the phone changed with no marker
    /// recorded yet -- callers now always read and remember first, as two separate, individually-retryable
    /// steps.
    /// </summary>
    public static Task SetTextSizeAsync(string? harnessProject, string udid, SigningPlan signing, TextSizeState target, CancellationToken cancellationToken = default) =>
        RunTextSizeStepAsync(harnessProject, udid, "testTextSizeRestore", signing, target.ToString(), cancellationToken);

    /// <summary>
    /// Reads (without changing) a physical iPhone's current Settings &gt; Accessibility &gt; Display &amp; Text
    /// Size &gt; Larger Text state through the harness, for verifying the device was left as found after a
    /// large-text check (e.g. after <c>swipewalk doctor</c> or a manual confirmation), and as the first step
    /// of <see cref="CapturePhysicalLargeTextAsync"/>'s own read-then-remember-then-apply sequence.
    /// </summary>
    public static async Task<TextSizeState?> ReadTextSizeAsync(string? harnessProject, string udid, SigningPlan signing, CancellationToken cancellationToken = default)
    {
        var result = await RunTextSizeStepAsync(harnessProject, udid, "testTextSizeRead", signing, target: null, cancellationToken);
        return TextSizeState.Parse(result.GetValueOrDefault("state"));
    }

    /// <summary>
    /// The Simulator's current dark/light appearance ("dark" or "light"), via `xcrun simctl ui &lt;udid&gt;
    /// appearance`. Simulator only -- there is no equivalent for a physical iPhone yet (see
    /// <see cref="AppearanceLabels.PhysicalIphoneNotSupportedReason"/> and docs/limitations.md).
    /// </summary>
    public static async Task<string> ReadAppearanceAsync(string udid) => (await Simctl("ui", udid, "appearance")).Trim();

    /// <summary>Sets the Simulator's appearance ("dark" or "light").</summary>
    public static Task SetAppearanceAsync(string udid, string appearance) => Simctl("ui", udid, "appearance", appearance);

    /// <summary>
    /// Rotates the Simulator to <paramref name="orientation"/> ("portrait" or "landscapeLeft") for the
    /// orientation rescan (<c>scan --orientation both</c>). There is no <c>simctl</c> equivalent of
    /// <see cref="SetAppearanceAsync"/> for orientation (checked: `simctl ui &lt;udid&gt; --help` lists
    /// appearance/increase_contrast/content_size only, no orientation option, on Xcode 27), so this runs the
    /// harness's one-shot <c>ScanTests/testSetOrientation</c>, which sets <c>XCUIDevice.shared.orientation</c>
    /// -- the same one-small-command shape as the text-size steps below, but with no signing (Simulator only:
    /// a physical iPhone is never passed here -- see
    /// <see cref="Reports.OrientationLabels.PhysicalIphoneNotSupportedReason"/>) and no output file (success
    /// is exit code 0).
    /// </summary>
    public static async Task SetOrientationAsync(string? harnessProject, string udid, string orientation, CancellationToken cancellationToken = default)
    {
        var workDir = Path.Combine(Path.GetTempPath(), $"swipewalk-orientation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        try
        {
            var info = HarnessProcess(harnessProject, udid, "-", workDir, "ScanTests/testSetOrientation", serve: false);
            info.Environment["TEST_RUNNER_CF_ORIENTATION"] = orientation;
            var (exitCode, output) = await RunAsync(info);
            if (exitCode != 0)
            {
                var errors = string.Join('\n', output.Split('\n').Where(l => l.Contains("error", StringComparison.OrdinalIgnoreCase)).Take(5));
                throw new InvalidOperationException($"Could not rotate the Simulator to {orientation} (xcodebuild exit {exitCode}). {errors}");
            }
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    /// <summary>
    /// Asks whether to use the Accessibility Inspector route for a screen-reader capture (<c>--screen-reader</c>
    /// on iOS -- see <see cref="RunInspectorCaptureAsync"/>). The Inspector route needs two things Swipewalk
    /// cannot do itself over the AX API: the macOS Accessibility permission for whichever app is responsible
    /// for this process (which lets that app, and anything it runs, operate other apps on the Mac -- not
    /// only the Inspector, though Swipewalk itself only ever uses it for the Inspector), and a one-time
    /// manual step (opening the Inspector, choosing the target device in its toolbar, and clicking the first
    /// element on the app's screen so the walk starts from the top) -- so this is a person-answered prompt,
    /// not a plain bool. Implemented by the CLI (a console prompt) or a future desktop dialog; returns
    /// whether to proceed (having already, if true, made sure the permission is granted and the one-time
    /// step is done). Null is "no interactive prompt available" (a non-interactive run), resolved as decline
    /// -- the predicted transcript still applies then, same as any other Swipewalk confirmation with nothing
    /// to ask on.
    /// </summary>
    public delegate Task<bool> IosInspectorGuide(CancellationToken cancellationToken);

    /// <summary>
    /// Evidence for <paramref name="snapshot"/> from Xcode's Accessibility Inspector, walked over the macOS
    /// Accessibility API (harness/mac-inspector-walk/InspectorWalk.swift, via <see cref="IosInspectorWalk"/>)
    /// and matched to this screen's tree (<see cref="IosInspectorCapture"/>). VoiceOver itself is never
    /// turned on -- the Inspector reports the same accessibility properties VoiceOver would read (label,
    /// value, traits, identifier, hint, class), and its own navigation order, without VoiceOver running,
    /// which is why this is a distinct <see cref="ScreenReaderSource"/> from a real recorded voice, alongside
    /// Android's on-device TalkBack capture (<see cref="Android.AndroidHarness.RunScreenReaderCaptureAsync"/>).
    /// Never throws: every path -- <paramref name="guide"/> null, declined, or the walk itself failing --
    /// returns a <see cref="ScreenReaderCapture"/> that is simply incomplete, with why in
    /// <see cref="Model.ScreenReaderCapture.NotCompleteReason"/>.
    /// </summary>
    public static async Task<ScreenReaderCapture> RunInspectorCaptureAsync(
        ScreenSnapshot snapshot, IosInspectorGuide? guide, CancellationToken cancellationToken = default)
    {
        if (guide is null)
            return IosInspectorCapture.Skipped(
                "no interactive prompt was available to ask about the Accessibility Inspector route for this run; the manual VoiceOver route still applies");
        if (!await guide(cancellationToken))
            return IosInspectorCapture.Skipped(
                "the Accessibility Inspector route was declined, or the macOS Accessibility permission it needs was not granted, for this run; the manual VoiceOver route still applies");

        var raw = await IosInspectorWalk.RunAsync(cancellationToken: cancellationToken);
        return IosInspectorCapture.Build(snapshot, raw);
    }

    /// <summary>
    /// Asks whether, and for how long, to run a VoiceOver-captions session for <paramref name="snapshot"/>'s
    /// screen (<c>--voiceover-captions</c> -- see <see cref="Model.ScreenReaderCapture"/>'s
    /// <c>VoiceOverCaptions</c> source): a person turns VoiceOver and its Caption Panel on themselves, swipes
    /// through the screen, and tells Swipewalk when they're done, while this Mac polls screenshots over the
    /// cable and reads the on-screen caption -- VoiceOver is never scripted. Implemented by the CLI (a console
    /// prompt) or a future desktop dialog; returns the completed evidence (built via
    /// <see cref="IosVoiceOverCaptionCapture.Build"/>, so the guide implementation has whatever device/session
    /// details it needs, e.g. the target UDID, in its own closure), or null when declined or unavailable (a
    /// non-interactive run). Never throws itself; whatever it returns (or null) is used as-is.
    /// </summary>
    public delegate Task<ScreenReaderCapture?> IosVoiceOverCaptionGuide(ScreenSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>
    /// Evidence for <paramref name="snapshot"/> from a person's own real VoiceOver session, captured passively
    /// (see <see cref="IosVoiceOverCaptionGuide"/>). Never throws: every path -- <paramref name="guide"/> null,
    /// declined, or the session itself failing -- returns a <see cref="ScreenReaderCapture"/> that is simply
    /// incomplete, with why in <see cref="Model.ScreenReaderCapture.NotCompleteReason"/>.
    /// </summary>
    public static async Task<ScreenReaderCapture> RunVoiceOverCaptionCaptureAsync(
        ScreenSnapshot snapshot, IosVoiceOverCaptionGuide? guide, CancellationToken cancellationToken = default)
    {
        if (guide is null)
            return IosVoiceOverCaptionCapture.Skipped(
                "no interactive prompt was available to ask about a VoiceOver-captions capture for this run; the predicted transcript still applies");
        var result = await guide(snapshot, cancellationToken);
        return result ?? IosVoiceOverCaptionCapture.Skipped(
            "the VoiceOver-captions capture was declined, or not run, for this screen; the predicted transcript still applies");
    }

    /// <summary>The pure decision behind <see cref="CaptureLargeTextAsync"/>, once a capture attempt has finished
    /// (or never succeeded): no snapshot means the app never came back to front; a snapshot of a different screen
    /// means it restarted or navigated away.</summary>
    internal static LargeTextCapture Evaluate(ScreenSnapshot normal, ScreenSnapshot? large) =>
        large is null
            ? LargeTextCapture.Skipped(LargeTextCapture.NotInFront)
            : Swipewalk.Core.ScreenReader.ScreenIdentity.IsSameScreen(normal, large)
                ? LargeTextCapture.Captured(large)
                : LargeTextCapture.Skipped(LargeTextCapture.DifferentScreen);

    /// <summary>
    /// The pure decision after a terminate + relaunch escalation (physical iPhone, Simulator, and the per-app
    /// launch-argument fallback all reach this once they have a post-restart capture): a different screen
    /// means the app lost its place after restarting, so there's nothing to report safely. The same screen
    /// with text that actually grew (<see cref="TextGrowth.LooksGrown"/>) means the restart picked up the
    /// size, recorded as applied only after a restart, not live (<c>LargeTextAppliedLive = false</c>) -- but
    /// only when <paramref name="liveTested"/> is true: the per-app launch-argument fallback launches the app
    /// fresh with the larger size already set, so whether it would have applied the setting live was never
    /// tested, and claiming "applied after a restart" there would be a false positive (the app was never seen
    /// running at the normal size and then picking it up). The same screen with text that still didn't grow,
    /// or any screen when <paramref name="liveTested"/> is false, means recorded as unknown/not-applied
    /// (<c>null</c>) rather than claiming a restart fixed (or didn't fix) something that was never observed,
    /// with the after-restart capture still kept as <see cref="ScreenSnapshot.LargeText"/> so
    /// <c>Swipewalk.Core.Rules.TextResizeRule</c> can report the WCAG 1.4.4 "did not get taller" finding.
    /// Mirrors <see cref="Swipewalk.Collectors.Android.AndroidScreenSource.DecideAfterRestart"/>.
    /// </summary>
    /// <param name="liveTested">False for the per-app launch-argument fallback, whose relaunch never ran the
    /// app at the normal size first, so live-vs-restart was never actually compared.</param>
    internal static LargeTextCapture CapturedAfterRestart(
        ScreenSnapshot before, ScreenSnapshot afterRestart, string method, string differentScreenReason, bool liveTested = true) =>
        Swipewalk.Core.ScreenReader.ScreenIdentity.IsSameScreen(before, afterRestart)
            ? LargeTextCapture.Captured(afterRestart with
                {
                    LargeTextMethod = method,
                    LargeTextAppliedLive = liveTested && TextGrowth.LooksGrown(before, afterRestart) ? false : null,
                    LargeTextRestartCaptured = true,
                })
            : LargeTextCapture.Skipped(differentScreenReason);

    internal static async Task<string> Simctl(params string[] args)
    {
        var info = new ProcessStartInfo("xcrun") { RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("simctl");
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        var (exitCode, output) = await RunAsync(info);
        return exitCode == 0 ? output : throw new InvalidOperationException($"simctl {string.Join(' ', args)} failed: {output.Trim()}");
    }

    /// <summary>
    /// Detects .NET MAUI from the app installed on a Simulator (<c>simctl get_app_container ... app</c> gives
    /// the bundle directory; see <see cref="DetectFrameworkInDirectory"/>). Best-effort: any failure (the app
    /// isn't installed, `simctl` unavailable) leaves the framework <see cref="AppFramework.Unknown"/> rather
    /// than failing the capture. Simulator only -- a physical iPhone's bundle isn't reachable from the Mac;
    /// see <c>AppInstaller</c> for the physical-device path (detecting from an installed .app/.ipa instead).
    /// </summary>
    internal static async Task<DetectedFramework> DetectFrameworkAsync(string udid, string bundleId)
    {
        try
        {
            var container = (await Simctl("get_app_container", udid, bundleId, "app")).Trim();
            return DetectFrameworkInDirectory(container);
        }
        catch (InvalidOperationException)
        {
            return new DetectedFramework(AppFramework.Unknown, null);
        }
    }

    /// <summary>Runs <see cref="DetectFrameworkAsync"/> and saves the result for <see cref="Load"/> to pick
    /// up; writes nothing when detection found no known framework, same as an older capture without this file.</summary>
    private static async Task WriteFrameworkFileAsync(string captureDir, string udid, string bundleId)
    {
        var detected = await DetectFrameworkAsync(udid, bundleId);
        if (detected.Framework == AppFramework.Unknown)
            return;
        await File.WriteAllTextAsync(Path.Combine(captureDir, FrameworkFile), JsonSerializer.Serialize(detected));
    }

    /// <summary>
    /// Looks for Microsoft.Maui.Controls.dll directly in an app bundle directory: on both the Simulator and a
    /// device .ipa/.app tested (TipCalc, DeveloperBalance, Calculator -- the MAUI samples -- and BuggyApp, all
    /// on .NET MAUI 10.0.60), the app's managed assemblies sit flat in the bundle root, not in a subfolder.
    /// Also used for an extracted Simulator .app (from a .zip) or the Payload/*.app inside a device .ipa; see
    /// <c>AppInstaller</c>.
    /// </summary>
    internal static DetectedFramework DetectFrameworkInDirectory(string appDirectory)
    {
        var dll = Path.Combine(appDirectory, "Microsoft.Maui.Controls.dll");
        return File.Exists(dll) ? new DetectedFramework(AppFramework.Maui, ReadInformationalVersion(dll)) : new DetectedFramework(AppFramework.Unknown, null);
    }

    /// <summary>
    /// Reads System.Reflection.AssemblyInformationalVersionAttribute from a .NET assembly (e.g.
    /// "10.0.60+bf6156897c887d33dcd40592db3bcb6471916e03") and strips the "+commit" suffix, leaving
    /// "10.0.60". Null when the attribute is missing or the file can't be read as a .NET assembly.
    /// </summary>
    internal static string? ReadInformationalVersion(string assemblyPath)
    {
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
                return null;
            var reader = peReader.GetMetadataReader();
            var assembly = reader.GetAssemblyDefinition();
            foreach (var handle in assembly.GetCustomAttributes())
            {
                var attribute = reader.GetCustomAttribute(handle);
                if (CustomAttributeTypeName(reader, attribute) != "System.Reflection.AssemblyInformationalVersionAttribute")
                    continue;
                var value = attribute.DecodeValue(InformationalVersionAttributeTypeProvider.Instance);
                if (value.FixedArguments is [{ Value: string raw }, ..])
                    return raw.Split('+', 2)[0];
            }
        }
        catch (Exception ex) when (ex is BadImageFormatException or IOException or UnauthorizedAccessException)
        {
            // Not a readable .NET assembly; detection must never fail the capture over this.
        }
        return null;
    }

    private static string? CustomAttributeTypeName(MetadataReader reader, CustomAttribute attribute)
    {
        if (attribute.Constructor.Kind != HandleKind.MemberReference)
            return null;
        var memberRef = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
        if (memberRef.Parent.Kind != HandleKind.TypeReference)
            return null;
        var typeRef = reader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
        return $"{reader.GetString(typeRef.Namespace)}.{reader.GetString(typeRef.Name)}";
    }

    /// <summary>Minimal custom-attribute type provider: <see cref="ReadInformationalVersion"/> only needs the
    /// attribute's single string fixed argument, never a typed value.</summary>
    private sealed class InformationalVersionAttributeTypeProvider : ICustomAttributeTypeProvider<string>
    {
        public static readonly InformationalVersionAttributeTypeProvider Instance = new();
        public string GetSystemType() => "System.Type";
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => "?";
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => "?";
        public string GetTypeFromSerializedName(string name) => name;
        public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;
        public bool IsSystemType(string type) => type == "System.Type";
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
        public string GetSZArrayType(string elementType) => elementType + "[]";
    }

    /// <summary>Terminates and relaunches the app under test on a Simulator, for the "text didn't grow live,
    /// try again after a restart" escalation. Terminate is best-effort (the app may not be running); launch
    /// is not, since a failed launch means the escalation attempt itself has nothing to capture.</summary>
    private static async Task SimctlRelaunchAsync(string udid, string bundleId)
    {
        var terminate = new ProcessStartInfo("xcrun") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "simctl", "terminate", udid, bundleId })
            terminate.ArgumentList.Add(arg);
        await RunAsync(terminate); // ignore result: fails harmlessly when the app wasn't running
        await Simctl("launch", udid, bundleId);
    }

    /// <summary>
    /// Terminates and relaunches the app under test on a physical device via devicectl directly, instead of
    /// through the harness's own <c>XCUIApplication.terminate()</c>/<c>launch()</c> (the "relaunch" harness
    /// command). Found 2026-09-23 on a physical iPhone: after the large-text check restores the system text
    /// size, the harness's terminate()+launch() reports success (the app reaches <c>.runningForeground</c>) but
    /// the app keeps rendering at the enlarged size -- confirmed by screenshot, and confirmed fixed by a manual
    /// <c>devicectl device process terminate</c>+<c>launch</c> instead, which shows the normal size immediately.
    /// The asymmetry (the same command reliably relaunches the app *into* the larger size a moment earlier, in
    /// <see cref="IosScreenSource"/>'s <c>BeginLargeTextCoreAsync</c>, but not back out of it) points at a
    /// physical-device-only timing/race in XCUITest's own terminate()+launch(), not a harness logic error --
    /// devicectl's <c>--terminate-existing</c> talks to CoreDevice directly and, per that verification, is
    /// reliable where the harness's own call is not. Kept in <see cref="IosCollector"/> (not the harness) so
    /// the harness stays a thin command executor; best-effort like <see cref="SimctlRelaunchAsync"/> -- a
    /// failure here just leaves the app at its current size until the next relaunch.
    /// </summary>
    internal static async Task DevicectlRelaunchAsync(string udid, string bundleId)
    {
        var info = new ProcessStartInfo("xcrun") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "devicectl", "device", "process", "launch", "--device", udid, "--terminate-existing", bundleId })
            info.ArgumentList.Add(arg);
        await RunAsync(info); // best-effort: a failure here just leaves the app at its current size
    }

    /// <summary>Relaunches the app under test so it picks up the current system text size, choosing
    /// <see cref="DevicectlRelaunchAsync"/> (physical) or <see cref="SimctlRelaunchAsync"/> (Simulator) -- see
    /// <see cref="DevicectlRelaunchAsync"/> for why a physical device doesn't go through the harness's own
    /// terminate()/launch() for this. Used by <see cref="IosScreenSource"/>'s record-mode large-text flow;
    /// scan mode's physical path still relaunches through the harness (<see cref="CaptureAsync"/>'s
    /// <c>relaunch</c> parameter), which combines the relaunch with the capture that follows it in one harness
    /// session -- untouched here since scan mode's large-text flow wasn't part of what was reproduced and
    /// verified.</summary>
    internal static Task RelaunchOnDeviceAsync(string udid, string bundleId, bool isPhysical) =>
        isPhysical ? DevicectlRelaunchAsync(udid, bundleId) : SimctlRelaunchAsync(udid, bundleId);

    /// <summary>Best-effort terminate of the app under test on a Simulator, used after the "no growth even
    /// after a restart" escalation (see <see cref="CaptureLargeTextAsync"/>) so the process isn't left running
    /// at the larger content size once the system setting has been restored -- a later plain scan only
    /// activates an already-running app rather than relaunching it, and would otherwise inherit its stale,
    /// still-enlarged rendering.</summary>
    private static async Task SimctlTerminateAsync(string udid, string bundleId)
    {
        var terminate = new ProcessStartInfo("xcrun") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "simctl", "terminate", udid, bundleId })
            terminate.ArgumentList.Add(arg);
        await RunAsync(terminate); // ignore result: fails harmlessly when the app wasn't running
    }

    public static ScreenSnapshot Load(string captureDir, string screenName)
    {
        var parsed = XcuiTreeParser.Parse(File.ReadAllText(Path.Combine(captureDir, TreeFile)));
        var screenshotPath = Path.Combine(captureDir, ScreenshotFile);
        var framework = LoadFramework(captureDir);
        var bundleIdPath = Path.Combine(captureDir, BundleIdFile);
        return new ScreenSnapshot
        {
            Platform = Platform.iOS,
            ScreenName = screenName,
            Root = parsed.Root,
            PixelScale = parsed.Scale,
            ScreenshotPath = File.Exists(screenshotPath) ? screenshotPath : null,
            Screenshot = File.Exists(screenshotPath) ? PngDecoder.Decode(screenshotPath) : null,
            EngineIssues = parsed.EngineIssues,
            Device = Devices.Load(captureDir),
            Framework = framework?.Framework ?? AppFramework.Unknown,
            FrameworkVersion = framework?.Version,
            AppId = File.Exists(bundleIdPath) ? File.ReadAllText(bundleIdPath).Trim() : null,
        };
    }

    /// <summary>Reads the <see cref="FrameworkFile"/> sidecar written by <see cref="CaptureAsync"/>; null when
    /// absent (a physical-device capture, detection found nothing, or an older capture made before this
    /// existed) -- callers then keep <see cref="AppFramework.Unknown"/>, the same as before detection existed.</summary>
    private static DetectedFramework? LoadFramework(string captureDir)
    {
        var path = Path.Combine(captureDir, FrameworkFile);
        return File.Exists(path) ? JsonSerializer.Deserialize<DetectedFramework>(File.ReadAllText(path)) : null;
    }

    /// <summary>
    /// Blanks the status bar in the screenshot so the time, carrier and notification icons of a real phone
    /// don't end up in shared reports. The status bar is outside the app and not scanned.
    /// </summary>
    internal static void MaskStatusBar(string captureDir)
    {
        var screenshotPath = Path.Combine(captureDir, ScreenshotFile);
        if (!File.Exists(screenshotPath))
            return;
        var parsed = XcuiTreeParser.Parse(File.ReadAllText(Path.Combine(captureDir, TreeFile)));
        if (parsed.StatusBar is not { Height: > 0 } bar)
            return;
        var image = PngDecoder.Decode(screenshotPath);
        image.Fill(0, (int)(bar.Y * parsed.Scale), image.Width, (int)Math.Ceiling(bar.Height * parsed.Scale), new Rgb(0x60, 0x60, 0x60));
        File.WriteAllBytes(screenshotPath, PngEncoder.Encode(image));
    }

    /// <summary>xcodebuild invocation running one harness test on a simulator.</summary>
    /// <param name="signing">Signing for a physical device; null for simulators (no signing).</param>
    internal static ProcessStartInfo HarnessProcess(
        string? harnessProject, string udid, string bundleId, string output, string test, bool serve,
        string? resultBundle = null, SigningPlan? signing = null)
    {
        var project = harnessProject ?? FindHarness()
            ?? throw new InvalidOperationException($"Could not find {HarnessProject}; pass --harness <path to .xcodeproj>.");
        var info = new ProcessStartInfo("xcodebuild") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[]
                 {
                     "test", "-project", project, "-scheme", "Harness", "-destination", $"id={udid}",
                     "-only-testing:HarnessUITests/" + test,
                     "-derivedDataPath", Path.Combine(Path.GetTempPath(), "swipewalk-harness"),
                 })
            info.ArgumentList.Add(arg);
        if (signing is null)
        {
            info.ArgumentList.Add("CODE_SIGNING_ALLOWED=NO");
        }
        else
        {
            info.ArgumentList.Add($"DEVELOPMENT_TEAM={signing.Team.TeamId}");
            info.ArgumentList.Add($"CF_BUNDLE_PREFIX={signing.BundlePrefix}");
            if (signing.Profile is { } profile)
            {
                // Manual signing with an installed profile: no Apple ID or network needed.
                info.ArgumentList.Add("CODE_SIGN_STYLE=Manual");
                info.ArgumentList.Add("CODE_SIGN_IDENTITY=Apple Development");
                info.ArgumentList.Add($"PROVISIONING_PROFILE_SPECIFIER={profile.Uuid}");
            }
            else
            {
                info.ArgumentList.Add("-allowProvisioningUpdates");
                info.ArgumentList.Add("-allowProvisioningDeviceRegistration"); // first run: registers the device with the team
            }
        }
        if (resultBundle is not null)
        {
            info.ArgumentList.Add("-resultBundlePath");
            info.ArgumentList.Add(resultBundle);
            info.Environment["TEST_RUNNER_CF_ATTACH"] = "1";
        }
        info.Environment["TEST_RUNNER_CF_BUNDLE_ID"] = bundleId;
        info.Environment["TEST_RUNNER_CF_OUTPUT"] = output;
        info.Environment["TEST_RUNNER_CF_MODE"] = serve ? "serve" : "capture";
        return info;
    }

    /// <summary>Copies the harness's attachments (tree.json/screenshot.png by default, or <paramref name="files"/>) out of a result bundle.</summary>
    private static async Task ExportAttachmentsAsync(string resultBundle, string captureDir, string[]? files = null)
    {
        files ??= [TreeFile, ScreenshotFile];
        var exportDir = Path.Combine(Path.GetTempPath(), $"swipewalk-attachments-{Guid.NewGuid():N}");
        var info = new ProcessStartInfo("xcrun") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "xcresulttool", "export", "attachments", "--path", resultBundle, "--output-path", exportDir })
            info.ArgumentList.Add(arg);
        var (exitCode, output) = await RunAsync(info);
        if (exitCode != 0)
            throw new InvalidOperationException($"Could not read the iOS test results: {output.Trim()}");

        // manifest.json lists each test's attachments: exportedFileName and suggestedHumanReadableName.
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(exportDir, "manifest.json")));
        foreach (var attachment in manifest.RootElement.EnumerateArray()
                     .SelectMany(t => t.GetProperty("attachments").EnumerateArray()))
        {
            var suggested = attachment.GetProperty("suggestedHumanReadableName").GetString() ?? "";
            var target = files.FirstOrDefault(n => suggested.StartsWith(Path.GetFileNameWithoutExtension(n), StringComparison.Ordinal)
                                                     && suggested.EndsWith(Path.GetExtension(n), StringComparison.Ordinal));
            if (target is not null)
                File.Copy(Path.Combine(exportDir, attachment.GetProperty("exportedFileName").GetString()!), Path.Combine(captureDir, target), overwrite: true);
        }
        TryDelete(exportDir);
    }

    private static void TryDelete(string directory)
    {
        try { Directory.Delete(directory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string? FindHarness() =>
        FindHarness(Directory.GetCurrentDirectory(), AppContext.BaseDirectory, Path.Combine(Path.GetTempPath(), "swipewalk-harness-src"));

    internal static string? FindHarness(string currentDirectory, string baseDirectory, string buildCopy)
    {
        // Working in a Swipewalk checkout: build the harness in place.
        for (var dir = new DirectoryInfo(currentDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, HarnessProject);
            if (Directory.Exists(candidate))
                return candidate;
        }

        // Installed copies carry the harness source: next to the assemblies (dotnet tool), or in the app bundle's
        // Resources folder (desktop app). Build a copy in a writable folder: the install folder may be read-only,
        // and changing files inside a signed app bundle breaks its signature.
        var bundled = new[] { baseDirectory, Path.Combine(baseDirectory, "..", "Resources") }
            .Select(root => Path.GetFullPath(Path.Combine(root, "harness", "ios")))
            .FirstOrDefault(dir => Directory.Exists(Path.Combine(dir, Path.GetFileName(HarnessProject))));
        if (bundled is null)
            return null;
        SyncDirectory(bundled, buildCopy);
        return Path.Combine(buildCopy, Path.GetFileName(HarnessProject));
    }

    /// <summary>Copies files that are missing or different, so unchanged files keep their dates and xcodebuild stays incremental.</summary>
    internal static void SyncDirectory(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            if (File.Exists(destination) && File.ReadAllBytes(destination).AsSpan().SequenceEqual(File.ReadAllBytes(file)))
                continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    /// <summary>The UDID of the currently booted Simulator, or null if none is booted. Public so callers
    /// outside this assembly (e.g. <c>ScanService</c>'s appearance rescan) can resolve the same device
    /// <see cref="CaptureAsync"/> would default to, before it captures anything.</summary>
    public static async Task<string?> BootedSimulatorAsync()
    {
        var info = new ProcessStartInfo("xcrun") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "simctl", "list", "devices", "booted", "-j" })
            info.ArgumentList.Add(arg);
        var (exitCode, output) = await RunAsync(info);
        if (exitCode != 0)
            return null;

        using var doc = JsonDocument.Parse(output);
        return doc.RootElement.GetProperty("devices").EnumerateObject()
            .SelectMany(runtime => runtime.Value.EnumerateArray())
            .Select(d => d.GetProperty("udid").GetString())
            .FirstOrDefault();
    }

    /// <summary>
    /// Runs a process and waits for it to exit. <paramref name="timeout"/> is null for anything that can
    /// legitimately run long (a harness build, a full <c>xcodebuild test</c> invocation) -- those keep waiting
    /// exactly as before. Callers that invoke a normally-fast helper repeatedly (see the devicectl calls in
    /// <see cref="HarnessChannel"/>'s <c>DeviceChannel</c>, "about a second per call") pass a bound instead.
    /// 2026-09-23: a record run froze on a physical iPhone during the large-text step (Settings visibly
    /// moving, the text never changing size, the whole recording then unresponsive) and was never reproduced
    /// on demand. This method having no timeout at all was the one unbounded wait on that path: without it, a
    /// single devicectl call that didn't return -- for whatever reason, e.g. the device being busy with the
    /// large-text step's own repeated Settings/app-switching automation -- would block forever, which would
    /// defeat <see cref="IosHarnessSession"/>'s own 90-second per-command deadline (its poll loop would never
    /// get another chance to check that deadline because it was stuck awaiting this one call) and freeze the
    /// whole recording, in the CLI and the desktop app, with no error. That's a plausible, unconfirmed
    /// explanation for what was observed, not a proven one. Throws <see cref="InvalidOperationException"/> on
    /// timeout, after killing the process tree, so a caller in a tight retry loop (like <c>ExistsAsync</c>)
    /// can catch it and treat it as "try again" instead of the caller hanging with nothing bounding it.
    /// </summary>
    internal static async Task<(int ExitCode, string Output)> RunAsync(ProcessStartInfo info, TimeSpan? timeout = null)
    {
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {info.FileName}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (timeout is { } bound)
        {
            using var cts = new CancellationTokenSource(bound);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                throw new InvalidOperationException($"{info.FileName} did not finish within {bound.TotalSeconds:0}s.");
            }
        }
        else
        {
            await process.WaitForExitAsync();
        }
        return (process.ExitCode, await stdout + await stderr);
    }
}
