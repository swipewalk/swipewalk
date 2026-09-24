using Swipewalk.Collectors;
using Swipewalk.Collectors.Android;
using Swipewalk.Collectors.Ios;
using Swipewalk.Collectors.Preflight;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Rules;

namespace Swipewalk.Engine;

/// <summary>
/// The scan workflow shared by the command line and the desktop app: install a build, check readiness,
/// scan one screen or record several, and write the report. Progress messages go to <c>log</c>.
/// </summary>
public sealed class ScanService(IProgress<string> log)
{
    public static string ToolVersion { get; } = typeof(ScanService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>
    /// Installs <see cref="ScanOptions.InstallFile"/> as-is (never rebuilt or re-signed), launches it on Android, and
    /// returns the options with the package / bundle id filled in when it can be read.
    /// </summary>
    public async Task<ScanOptions> InstallAsync(ScanOptions options)
    {
        if (options.InstallFile is not { } file)
            return options;
        log.Report($"Installing {Path.GetFileName(file)}...");
        if (options.Platform == TargetPlatform.Android)
        {
            var package = await AppInstaller.InstallAndroidAsync(file, options.Device) ?? options.Package;
            if (package is null)
            {
                log.Report("Installed. Open the app screen to scan.");
                return options;
            }
            await AppInstaller.LaunchAndroidAsync(package, options.Device);
            log.Report($"Installed and launched {package}.");
            return options with { Package = package };
        }

        var installed = await AppInstaller.InstallIosAsync(file, options.Device);
        var bundleId = installed.BundleId ?? options.BundleId
            ?? throw new InvalidOperationException("Installed, but the bundle id could not be read from the .ipa; set the bundle id.");
        log.Report($"Installed {bundleId}.");
        // An explicit --framework still wins; on a Simulator installed.Framework is always Unknown here (that
        // capture detects it later, from the running app -- see IosCollector.CaptureAsync).
        return options with
        {
            BundleId = bundleId,
            Framework = options.Framework ?? (installed.Framework == AppFramework.Unknown ? null : installed.Framework),
            FrameworkVersion = options.FrameworkVersion ?? installed.FrameworkVersion,
        };
    }

    public Task<IReadOnlyList<CheckResult>> CheckAsync(ScanOptions options) =>
        options.Platform == TargetPlatform.Ios
            ? Preflight.IosAsync(options.Device, options.BundleId, options.Team, options.HarnessProject, options.Profile, options.HarnessBundlePrefix)
            : Preflight.AndroidAsync(options.Device, options.Package, options.LargeText, options.ScreenReaderCapture, options.AndroidHarnessDir);

    /// <summary>
    /// The line printed before an Android scan with no --package, so nobody mistakes whatever happens to be in
    /// front for the app they meant (see <see cref="AndroidCollector.ForegroundPackageAsync(string?)"/>). Record
    /// mode doesn't need this: <c>Recorder</c> already announces <c>source.TargetApp</c> before it starts.
    /// </summary>
    public static string NoPackageGivenMessage(string? foregroundPackage) => foregroundPackage is not null
        ? $"Scanning the app in front: {foregroundPackage} (pass --package to scan a specific app)"
        : "Could not tell which app is in front; scanning it anyway (pass --package to scan a specific app)";

    /// <summary>
    /// Scan mode's equivalent of record mode's <c>LargeTextCapture.WentToAnotherScreenLive</c> (see
    /// <see cref="Swipewalk.Core.Rules.TextResizeNavigationRule"/>'s remarks): on Android, a different screen
    /// at the first live large-text attempt -- before Swipewalk has deliberately restarted anything itself --
    /// means the OS recreated the activity on its own when the font scale changed, reported by
    /// <see cref="AndroidScreenSource.CaptureLargeTextAsync(string, string, CancellationToken, ScanLargeTextRestartAsk?)"/>
    /// as <see cref="LargeTextCapture.DifferentScreen"/>. Never true for
    /// <see cref="LargeTextCapture.DifferentScreenAfterRestart"/> (Swipewalk's own restart losing the person's
    /// place afterwards -- a different situation, see <see cref="AndroidScreenSource.DecideAfterRestart"/>),
    /// and never on iOS (that collector has no equivalent live-observe-before-any-restart signal). Kept as its
    /// own pure method, separate from <see cref="ScanAsync"/>'s device calls, so it can be unit-tested without
    /// a device.
    /// </summary>
    public static bool AndroidLargeTextWentToAnotherScreen(bool ios, string? reason) =>
        !ios && reason == LargeTextCapture.DifferentScreen;

    /// <summary>Scans the screen currently shown (or a saved capture) and writes the report.</summary>
    /// <param name="largeTextRestartAsk">Asked before restarting the app to check large text further, when
    /// <see cref="ScanOptions.LargeTextRestartPolicy"/> is <see cref="LargeTextRestartPolicy.Ask"/>; the CLI
    /// prompts on the console, the desktop app shows a
    /// dialog -- the same asker shape record mode uses, via <see cref="ScanLargeTextRestart.Build"/>. Null with
    /// policy <see cref="LargeTextRestartPolicy.Ask"/> (e.g. a non-interactive run with no console/dialog
    /// wired up) resolves to "don't check", never to waiting forever.</param>
    public async Task<RunResult> ScanAsync(
        ScanOptions options, CancellationToken cancellationToken = default, LargeTextRestartAsker? largeTextRestartAsk = null)
    {
        var outDir = Path.GetFullPath(options.OutputDirectory);
        var captureDir = options.FromCapture is { } from ? Path.GetFullPath(from) : Path.Combine(outDir, "capture");
        var ios = options.Platform == TargetPlatform.Ios;

        // Set when Swipewalk had to start the app itself (see BringToFront): the report must say so, since the
        // screen captured is then the app's own first screen (possibly onboarding), not wherever it was left.
        string? appLaunchedNote = null;
        if (options.FromCapture is null)
        {
            if (ios)
            {
                log.Report("Capturing the current screen with the XCUITest harness (the first run builds it)...");
                var launched = await IosCollector.CaptureAsync(captureDir, options.BundleId!, options.Device, options.HarnessProject, options.Team,
                    options.ForceResultBundle, options.Profile, options.HarnessBundlePrefix, log: log);
                if (launched)
                    appLaunchedNote = BringToFront.LaunchedNote;
            }
            else
            {
                // With no --package, we scan whatever is in front; say so before capturing, since that app
                // might not be the one the caller meant (see AndroidCollector.ForegroundPackageAsync).
                if (options.Package is null)
                    log.Report(NoPackageGivenMessage(await AndroidCollector.ForegroundPackageAsync(options.Device)));
                log.Report("Capturing the current screen over adb...");
                // With a known package, bring it to the front first (already in front: nothing happens) --
                // see BringToFront -- then capture only while it's in front.
                if (options.Package is not null)
                {
                    var launched = await AndroidCollector.EnsureAppInFrontAsync(options.Package, options.Device, log, cancellationToken);
                    if (launched)
                        appLaunchedNote = BringToFront.LaunchedNote;
                }
                await AndroidCollector.CaptureAsync(captureDir, options.Device, maskStatusBar: !options.KeepStatusBar, expectedPackage: options.Package,
                    androidHarnessDir: options.AndroidHarnessDir, captureScreenReader: options.ScreenReaderCapture);
            }
        }

        // Passing options.Package (when given) makes AndroidCollector.Load filter to exactly that app rather
        // than guess from the dump (see UiAutomatorParser.GuessPackage); when it's null (no --package, or a
        // saved capture replayed without one), the guess is what carries the app's identity into
        // snapshot.AppId below.
        var snapshot = ios ? IosCollector.Load(captureDir, options.ScreenName) : AndroidCollector.Load(captureDir, options.ScreenName, options.Package);
        if (options.Framework is { } framework)
            snapshot = snapshot with { Framework = framework };
        // Set only for a physical iPhone installed from a .app/.ipa (InstallAsync detects it from the file,
        // since the running app can't be read from the Mac); a Simulator scan already got its version from
        // IosCollector.Load above.
        if (options.FrameworkVersion is { } frameworkVersion)
            snapshot = snapshot with { FrameworkVersion = frameworkVersion };

        var savedLarge = Path.Combine(captureDir, "large");
        string? largeTextSkippedReason = null;
        string? baselineTextSizeNote = null;
        if (options.FromCapture is not null && Directory.Exists(savedLarge))
        {
            snapshot = snapshot with
            {
                LargeText = ios ? IosCollector.Load(savedLarge, options.ScreenName) : AndroidCollector.Load(savedLarge, options.ScreenName, options.Package),
                LargeTextSetting = ios ? IosScreenSource.LargeTextDescription : AndroidScreenSource.LargeTextDescription,
                LargeTextScale = ios ? IosScreenSource.LargeTextScaleValue : AndroidScreenSource.LargeFontScale,
            };
        }
        else if (options.LargeText && options.FromCapture is null)
        {
            // Built once, whatever the outcome: ScanLargeTextRestart.Build turns options.LargeTextRestartPolicy
            // (Always: proceed automatically -- null, unchanged from before this hook existed; Never: skip
            // without asking; Ask: call largeTextRestartAsk) into the hook the collector calls just before it
            // would otherwise restart the app on its own.
            var confirmRestart = ScanLargeTextRestart.Build(
                options.LargeTextRestartPolicy, largeTextRestartAsk, options.ScreenName, options.Platform == TargetPlatform.Ios ? Platform.iOS : Platform.Android);

            LargeTextCapture large;
            string setting;
            double scale;
            if (ios)
            {
                log.Report($"Capturing again with {IosScreenSource.LargeTextDescription}...");
                large = await IosCollector.CaptureLargeTextAsync(savedLarge, options.BundleId!, snapshot, options.Device, options.HarnessProject,
                    options.Team, options.Profile, options.HarnessBundlePrefix, log, cancellationToken, confirmRestart);
                setting = IosScreenSource.LargeTextDescription;
                scale = IosScreenSource.LargeTextScaleValue;
            }
            else
            {
                // recordMode: false -- scan has no user to hand the app back to, so a large-text escalation
                // that needed a relaunch stops the app afterwards instead of relaunching it again (see
                // AndroidScreenSource.ApplyResetAsync / LargeTextReset).
                await using var source = await AndroidScreenSource.ConnectAsync(
                    options.Device, options.Package, recordMode: false, androidHarnessDir: options.AndroidHarnessDir,
                    captureScreenReader: options.ScreenReaderCapture);
                if (source.ChangesPhysicalDeviceTextSize)
                    log.Report(PhysicalDeviceTextSizeNotice.Text);
                log.Report($"Capturing again with {AndroidScreenSource.LargeTextDescription}...");
                large = await source.CaptureLargeTextAsync(savedLarge, options.ScreenName, cancellationToken, confirmRestart);
                setting = source.LargeTextSetting;
                scale = source.LargeTextScale;
            }
            if (large.Snapshot is null)
            {
                var reason = large.SkippedReason ?? LargeTextCapture.DifferentScreen;
                // large.SkippedReason itself (not the ?? DifferentScreen fallback above): that fallback is
                // only a display default for an unexpected null reason, and must not be misread as "the OS
                // recreated the activity" when nothing actually said so (wcag-reviewer, 2026-09-23).
                if (AndroidLargeTextWentToAnotherScreen(ios, large.SkippedReason))
                    snapshot = snapshot with { LargeTextWentToAnotherScreen = true };
                if (large.BaselineTextSizeNote is { } note)
                {
                    log.Report(note);
                    baselineTextSizeNote = note;
                }
                else if (reason == LargeTextCapture.DifferentScreenAfterRestart)
                    // Unlike record mode, scan has nobody to navigate back for it, so this line -- unlike the
                    // report/results.json, which only carry the reason text above -- points at record mode
                    // itself instead of a vague "check by hand". "not done", not "skipped": this can follow
                    // the person declining to check -- the person decides, nothing is silently skipped -- and
                    // matches Recorder's and the report's own wording for the same situation.
                    log.Report($"Large-text check not done: {reason}. To check this screen at large text, use record mode instead " +
                               "(`swipewalk record`, or Record in the desktop app): it lets you navigate back after the restart.");
                else
                    log.Report($"Large-text check not done: {reason}. Check large text by hand.");
                largeTextSkippedReason = reason;
            }
            else
                snapshot = snapshot with
                {
                    LargeText = large.Snapshot, LargeTextSetting = setting, LargeTextScale = scale,
                    LargeTextMethod = large.Snapshot.LargeTextMethod, LargeTextAppliedLive = large.Snapshot.LargeTextAppliedLive,
                    LargeTextRestartCaptured = large.Snapshot.LargeTextRestartCaptured,
                };
        }

        var screen = new RuleRunner(DefaultRules.All).Run(snapshot);
        if (largeTextSkippedReason is not null)
            screen = screen with { LargeTextSkippedReason = largeTextSkippedReason };
        if (baselineTextSizeNote is not null)
            screen = screen with { BaselineTextSizeNote = baselineTextSizeNote };
        if (appLaunchedNote is not null)
            screen = screen with { AppLaunchedNote = appLaunchedNote };

        if (options.AppearanceBoth && options.FromCapture is null)
        {
            var (other, primaryAppearance, otherAppearance, appearanceSkippedReason, appearanceUnchanged) =
                await RunAppearanceRescanAsync(ios, options, snapshot, captureDir, cancellationToken);
            if (other is not null && primaryAppearance is not null && otherAppearance is not null)
            {
                // Unchanged: the two captures are the same screen with barely different pixels, so nothing
                // was actually tested under the other appearance -- don't tag findings "both"/"only in X"
                // (AppearanceMerge would, since it has no way to tell "genuinely absent" from "never tried"),
                // just attach the other screenshot and say so.
                if (appearanceUnchanged)
                {
                    screen = screen with
                    {
                        Appearance = primaryAppearance,
                        OtherAppearance = otherAppearance,
                        OtherAppearanceScreenshotPath = other.ScreenshotPath,
                        OtherAppearancePixelScale = other.PixelScale,
                        AppearanceUnchanged = true,
                    };
                    log.Report($"The screen looked the same after switching to {otherAppearance} appearance; the app may force one theme, use fixed colors, or only read the theme at launch.");
                }
                else
                    screen = AppearanceMerge.Merge(screen, primaryAppearance, other, otherAppearance);
            }
            else if (appearanceSkippedReason is not null)
            {
                screen = screen with { AppearanceSkippedReason = appearanceSkippedReason };
                log.Report($"Appearance check not done: {appearanceSkippedReason}.");
            }
        }

        var report = new ScanReport
        {
            ToolVersion = ToolVersion,
            Screens = [screen],
            FocusStandard = options.Standard,
            // The app id passed in wins; otherwise whatever the capture itself told us (see snapshot.AppId /
            // AndroidCollector.Load, IosCollector.Load). Run history uses this to group and compare runs of
            // the same app without a typed --package/--bundle-id.
            AppId = options.AppId ?? screen.AppId,
        };
        var (html, json) = await ReportWriter.WriteAsync(report, outDir);
        return new RunResult(report, html, json);
    }

    /// <summary>
    /// <see cref="ScanOptions.AppearanceBoth"/>: captures the screen again in the device's other dark/light
    /// appearance and runs every rule on it, restoring the device's original appearance afterward no matter
    /// what happens (an error, a cancellation, or a clean finish) -- the same before-anything-changes,
    /// finally-restores shape as the large-text check's <see cref="AppearanceRestore"/> (mirroring
    /// <c>Swipewalk.Collectors.TextSizeRestore</c>). Not supported yet on a physical iPhone -- returns a skip
    /// reason instead of touching the device -- see <see cref="AppearanceLabels.PhysicalIphoneNotSupportedReason"/>.
    /// The other capture is already run through <see cref="RuleRunner"/>, so <see cref="ScanAsync"/> only has
    /// to merge the two <see cref="ScreenResult"/>s (see <see cref="AppearanceMerge"/>).
    /// </summary>
    /// <returns>
    /// The other appearance's rule results and the two appearance labels (all three non-null together, on
    /// success); a skip reason when nothing was captured; whether the two captures looked the same (see
    /// <see cref="AppearanceChangeDetector"/>) -- meaning the screen most likely did not visibly change.
    /// </returns>
    private async Task<(ScreenResult? Other, string? Appearance, string? OtherAppearance, string? SkippedReason, bool Unchanged)> RunAppearanceRescanAsync(
        bool ios, ScanOptions options, ScreenSnapshot snapshot, string captureDir, CancellationToken cancellationToken)
    {
        var otherCaptureDir = Path.Combine(captureDir, "appearance");
        if (ios)
        {
            var deviceId = options.Device ?? await IosCollector.BootedSimulatorAsync();
            var device = deviceId is null ? null : (await Devices.IosAsync()).FirstOrDefault(d => d.Id == deviceId);
            if (device is null || device.IsPhysical)
                return (null, null, null, AppearanceLabels.PhysicalIphoneNotSupportedReason, false);
            var udid = device.Id;

            var original = await IosCollector.ReadAppearanceAsync(udid);
            if (original is not (AppearanceLabels.Dark or AppearanceLabels.Light))
                return (null, null, null, $"could not read the Simulator's current appearance (\"{original}\")", false);

            AppearanceRestore.Remember(udid, original);
            try
            {
                var otherAppearance = original == AppearanceLabels.Dark ? AppearanceLabels.Light : AppearanceLabels.Dark;
                log.Report($"Switching to {otherAppearance} appearance and capturing again...");
                await IosCollector.SetAppearanceAsync(udid, otherAppearance);
                await IosCollector.CaptureAsync(otherCaptureDir, options.BundleId!, udid, options.HarnessProject, options.Team,
                    options.ForceResultBundle, options.Profile, options.HarnessBundlePrefix, log: log);
                var otherSnapshot = IosCollector.Load(otherCaptureDir, options.ScreenName);
                var otherScreen = new RuleRunner(DefaultRules.All).Run(otherSnapshot);
                return (otherScreen, original, otherAppearance, null, AppearanceChangeDetector.LooksUnchanged(snapshot, otherSnapshot));
            }
            finally
            {
                await IosCollector.SetAppearanceAsync(udid, original);
                AppearanceRestore.Forget(udid);
            }
        }
        else
        {
            var serial = await AndroidCollector.ResolveSerialAsync(options.Device);
            var original = await AndroidAppearance.ReadAsync(serial);
            AppearanceRestore.Remember(serial, original);
            try
            {
                var otherAppearance = original == AppearanceLabels.Dark ? AppearanceLabels.Light : AppearanceLabels.Dark;
                log.Report($"Switching to {otherAppearance} appearance and capturing again...");
                await AndroidAppearance.SetAsync(serial, otherAppearance);
                if (options.Package is not null)
                    await AndroidCollector.EnsureAppInFrontAsync(options.Package, serial, log, cancellationToken);
                // captureScreenReader is always false here: a full TalkBack walk is expensive (roughly 1-2s
                // per element) and --screen-reader already opts into it once for the primary capture; doubling
                // it silently for the appearance rescan would surprise a run that only asked for one of them.
                await AndroidCollector.CaptureAsync(otherCaptureDir, serial, maskStatusBar: !options.KeepStatusBar,
                    expectedPackage: options.Package, androidHarnessDir: options.AndroidHarnessDir, captureScreenReader: false);
                var otherSnapshot = AndroidCollector.Load(otherCaptureDir, options.ScreenName, options.Package);
                var otherScreen = new RuleRunner(DefaultRules.All).Run(otherSnapshot);
                return (otherScreen, original, otherAppearance, null, AppearanceChangeDetector.LooksUnchanged(snapshot, otherSnapshot));
            }
            finally
            {
                await AndroidAppearance.SetAsync(serial, original);
                AppearanceRestore.Forget(serial);
            }
        }
    }

    /// <summary>
    /// Records while the user navigates: each new screen of the app is scanned. Ends when <paramref name="control"/>
    /// is stopped or the token is cancelled; the report is rewritten after every screen.
    /// </summary>
    /// <param name="continuation">Resumes an earlier, ended-early session of the same run instead of starting a
    /// fresh one -- see <see cref="ResolveContinuation"/>. Null (the default) starts a
    /// fresh recording, as before.</param>
    public async Task<RunResult> RecordAsync(
        ScanOptions options, RecorderControl control, CancellationToken cancellationToken = default, Action<ScreenResult>? onScreen = null,
        LargeTextRestartAsker? largeTextRestartAsk = null, RevisitSkippedScreensAsker? revisitSkippedScreensAsk = null,
        RecordContinuation? continuation = null)
    {
        var outDir = Path.GetFullPath(options.OutputDirectory);
        if (options.Platform == TargetPlatform.Ios)
            // True either way: auto-scan builds it here, up front, for the one session kept open for the whole
            // recording; manual capture (the default) opens a session per action instead, so this build happens
            // on the very next thing that needs it -- checking the app is in front, right after this -- rather
            // than literally "starting" here, but a fresh derived-data build (the "first run") costs the same
            // either way, and later runs stay incremental (see IosHarnessSession).
            log.Report("Preparing the iOS harness (the first run builds it; this can take a minute)...");
        await using IScreenSource source = options.Platform == TargetPlatform.Ios
            ? await IosScreenSource.StartAsync(options.BundleId!, options.Device, options.HarnessProject, options.Team, options.Profile, options.HarnessBundlePrefix, options.AutoScanOnScreenChange)
            : await AndroidScreenSource.ConnectAsync(options.Device, options.Package, androidHarnessDir: options.AndroidHarnessDir,
                captureScreenReader: options.ScreenReaderCapture);

        var recorder = new Recorder(source, outDir, ToolVersion, options.Framework, options.FrameworkVersion, options.ExpectedScreens,
            options.LargeText, options.Standard, options.AutoScanOnScreenChange, control, log, options.LargeTextRestartPolicy, largeTextRestartAsk,
            revisitSkippedScreensAsk, continuation);
        if (onScreen is not null)
            recorder.ScreenScanned += onScreen;
        var report = await recorder.RunAsync(cancellationToken);
        var (html, json) = await ReportWriter.WriteAsync(report, outDir);
        return new RunResult(report, html, json);
    }

    /// <summary>
    /// Everything <c>record --continue &lt;run&gt;</c> (or the desktop app's Continue action on an ended-early
    /// run in History) needs to resume that run: the run's own metadata (for its app id/platform, and to write
    /// back into the same folder), the report it had saved so far, and a <see cref="RecordContinuation"/> built
    /// from it and its record-state.json (see <see cref="RecordState"/>). Throws
    /// <see cref="InvalidOperationException"/>, with a message fit to show the person directly, when the run
    /// can't be continued: no such run, not a recording, one that's currently being recorded (here or in
    /// another window -- see <see cref="RunRecord.RecordingInProgress"/>), or one that finished normally: a run
    /// that reached Finish isn't offered Continue, since there's nothing ended-early to resume (record a new
    /// run, or use the desktop app's Compare, to add more screens to the same app's
    /// coverage over time).
    /// </summary>
    public static (RunRecord Run, ScanReport Report, RecordContinuation Continuation) ResolveContinuation(RunHistory history, string runIdOrFolder)
    {
        var run = history.Resolve(runIdOrFolder)
            ?? throw new InvalidOperationException($"No saved run found for \"{runIdOrFolder}\" (see: swipewalk history).");
        if (run.Mode != "record")
            throw new InvalidOperationException($"{run.Id} was {ModeLabel(run.Mode)}, not a recording; only a recording can be continued.");
        // Checked before EndedEarlyReason: history.Resolve only leaves RecordingInProgress true when the
        // owning process is confirmed still running (see RunHistory.List), so this is a recording still under
        // way right now, not an ended-early one -- a different situation from "finished normally" below, and
        // worded accordingly.
        if (run.RecordingInProgress)
            throw new InvalidOperationException(
                $"{run.Id} is still being recorded (in another window or terminal). Stop that recording first; you can then continue it from where it stopped.");
        if (run.EndedEarlyReason is null)
            throw new InvalidOperationException(
                $"{run.Id} finished normally (Finish, or q on the CLI, was chosen); start a new recording instead of continuing it.");
        var report = RunHistory.Load(run)
            ?? throw new InvalidOperationException($"Could not read {run.ResultsPath}.");
        var state = RecordState.Load(run.Folder);
        var stateWasSaved = state is not null;
        state ??= new RecordState();
        var sessions = report.Sessions.Count > 0 ? report.Sessions : [new RecordingSession(run.StartedAt, run.FinishedAt)];
        var continuation = new RecordContinuation(
            report.Screens, state.Screens, sessions, state.LearnedRestartReason,
            Math.Max(state.NextScreenNumber, report.Screens.Count + 1), stateWasSaved);
        return (run, report, continuation);
    }

    /// <summary>Plain-words name for a run's <see cref="RunRecord.Mode"/>, for <see cref="ResolveContinuation"/>'s
    /// error message (so it reads "was a single-screen scan", "was a swipewalk run", not "was a run").</summary>
    private static string ModeLabel(string mode) => mode switch
    {
        "scan" => "a single-screen scan",
        "run" => "a `swipewalk run`",
        _ => $"a {mode}",
    };
}
