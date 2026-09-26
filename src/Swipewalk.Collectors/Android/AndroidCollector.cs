using System.Text.Json;
using System.Text.RegularExpressions;
using Swipewalk.Core.Imaging;
using Swipewalk.Core.Model;

namespace Swipewalk.Collectors.Android;

/// <summary>
/// Captures the foreground screen of an Android device or emulator over adb. Raw artifacts are
/// saved to a capture directory so a scan can be re-run offline with <see cref="Load"/>.
/// </summary>
public static partial class AndroidCollector
{
    public const string FullDumpFile = "uiautomator.xml";
    public const string CompressedDumpFile = "uiautomator-compressed.xml";
    public const string ScreenshotFile = "screenshot.png";
    public const string DensityFile = "density.txt";

    /// <summary>
    /// Sidecar written by <see cref="CaptureAsync"/> with the package that was actually in front at capture
    /// time (from <c>dumpsys window</c>, the same source <see cref="EnsureInFrontAsync"/> checks against),
    /// read back by <see cref="Load"/> in preference to guessing one from the dump. Only written when
    /// <c>CaptureAsync</c> wasn't given an expected package -- with one given, that's already the known-correct
    /// app. Ground truth beats a guess: a system overlay (a permission prompt, a share sheet) dumped alongside
    /// the app could otherwise out-vote it in <see cref="UiAutomatorParser.GuessPackage"/> if it happens to have
    /// more interactive elements. Absent for a capture made before this existed, or one where the foreground
    /// window couldn't be read.
    /// </summary>
    public const string PackageFile = "package.txt";

    /// <summary>Sidecar with the Android instrumentation harness's raw JSON output (see
    /// <see cref="AndroidHarness"/>), written by <see cref="CaptureAsync"/> when the harness ran, and read
    /// back by <see cref="Load"/>. Mutually exclusive with <see cref="AtfSkipReasonFile"/>.</summary>
    internal const string AtfResultFile = "atf-harness.json";

    /// <summary>Sidecar with why the harness didn't run for this capture (see <see cref="ScreenSnapshot.AtfSkippedReason"/>),
    /// written by <see cref="CaptureAsync"/> instead of <see cref="AtfResultFile"/> when it failed. Absent for a
    /// capture made before this existed; see <see cref="Load"/>.</summary>
    internal const string AtfSkipReasonFile = "atf-skip-reason.txt";

    /// <summary>Sidecar with the Android TalkBack screen-reader capture's raw JSON (see
    /// <see cref="AndroidHarness.RunScreenReaderCaptureAsync"/>), written by <see cref="CaptureAsync"/> when
    /// <c>captureScreenReader</c> was true and the harness produced a result (even an empty one -- see
    /// <see cref="ScreenSnapshot.ScreenReaderCapture"/>'s remarks on why that differs from no capture at
    /// all). Mutually exclusive with <see cref="ScreenReaderSkipReasonFile"/>; both absent means
    /// <c>captureScreenReader</c> was false (the default) for this capture.</summary>
    internal const string ScreenReaderResultFile = "screen-reader-capture.json";

    /// <summary>Sidecar with why the Android TalkBack screen-reader capture didn't run for this capture
    /// (see <see cref="AndroidHarness.ScreenReaderHarnessResult.SkipReason"/>), written instead of
    /// <see cref="ScreenReaderResultFile"/> when <c>captureScreenReader</c> was true but the harness
    /// couldn't produce a result (for example TalkBack isn't installed). <see cref="Load"/> still builds a
    /// non-null, incomplete <see cref="ScreenSnapshot.ScreenReaderCapture"/> from this, with the reason in
    /// <see cref="Model.ScreenReaderCapture.NotCompleteReason"/>, so a report can say why rather than look
    /// like a clean capture that found nothing.</summary>
    internal const string ScreenReaderSkipReasonFile = "screen-reader-skip-reason.txt";

    /// <summary>Dumps the UI tree and screenshot into <paramref name="captureDir"/>.</summary>
    /// <param name="maskStatusBar">
    /// Blank the system status bar in the screenshot, so notifications and other personal information on a real
    /// phone don't end up in shared reports. The status bar is outside the app and not scanned.
    /// </param>
    /// <param name="expectedPackage">
    /// When set, capture only while this app is in front (checked before and after the screenshot); otherwise
    /// throw <see cref="TargetNotInFrontException"/> and keep nothing.
    /// </param>
    /// <param name="androidHarnessDir">Path to harness/android; null auto-detects it (see
    /// <see cref="AndroidHarness.FindHarnessDir()"/>). Passing a path that doesn't exist, or leaving the
    /// harness unbuildable, only skips Google's ATF checks (see <see cref="AtfSkipReasonFile"/>) -- it never
    /// fails the capture.
    /// </param>
    /// <param name="captureScreenReader">Also run the Android TalkBack screen-reader capture (see
    /// <see cref="AndroidHarness.RunScreenReaderCaptureAsync"/>) -- opt-in and false by default: it costs
    /// roughly 1-2 seconds per element (a 30-element screen ~1 minute), on top of everything else this
    /// method does. A harness problem only skips this, never the rest of the capture -- see
    /// <see cref="ScreenReaderSkipReasonFile"/>.
    /// </param>
    public static async Task CaptureAsync(
        string captureDir, string? serial = null, bool maskStatusBar = true, string? expectedPackage = null,
        string? androidHarnessDir = null, bool captureScreenReader = false)
    {
        Directory.CreateDirectory(captureDir);
        serial = await new Adb(serial).ResolveSerialAsync();
        var adb = new Adb(serial);
        await EnsureUnlockedAsync(adb);
        await EnsureInFrontAsync(adb, expectedPackage);
        await Devices.SaveAsync(await Devices.AndroidInfoAsync(serial), captureDir);

        await adb.RunAsync("shell", "uiautomator", "dump", "/sdcard/cf-full.xml");
        await adb.RunAsync("shell", "uiautomator", "dump", "--compressed", "/sdcard/cf-compressed.xml");
        await File.WriteAllTextAsync(Path.Combine(captureDir, FullDumpFile), await adb.RunAsync("exec-out", "cat", "/sdcard/cf-full.xml"));
        await File.WriteAllTextAsync(Path.Combine(captureDir, CompressedDumpFile), await adb.RunAsync("exec-out", "cat", "/sdcard/cf-compressed.xml"));
        var screenshot = await adb.RunBinaryAsync("exec-out", "screencap", "-p");
        if (maskStatusBar && StatusBarFrame(await adb.RunAsync("shell", "dumpsys", "window")) is var (top, bottom))
        {
            var image = PngDecoder.Decode(new MemoryStream(screenshot));
            image.Fill(0, top, image.Width, bottom - top, new Rgb(0x60, 0x60, 0x60));
            screenshot = PngEncoder.Encode(image);
        }
        var foregroundAfter = await EnsureInFrontAsync(adb, expectedPackage); // the user may have switched apps during the capture
        await File.WriteAllBytesAsync(Path.Combine(captureDir, ScreenshotFile), screenshot);
        await adb.RunAsync("shell", "rm", "/sdcard/cf-full.xml", "/sdcard/cf-compressed.xml");

        var density = ParseDensity(await adb.RunAsync("shell", "wm", "density"));
        await File.WriteAllTextAsync(Path.Combine(captureDir, DensityFile), density.ToString());

        // expectedPackage is already known-correct; only record the guess-avoiding ground truth when the
        // caller didn't give one (see PackageFile).
        if (expectedPackage is null && foregroundAfter is not null)
            await File.WriteAllTextAsync(Path.Combine(captureDir, PackageFile), foregroundAfter);

        // Google's Accessibility Test Framework, via the Android instrumentation harness (see
        // AndroidHarness): best-effort, always -- see AtfResultFile/AtfSkipReasonFile and KnownLimitations
        // "android-atf-harness". A harness problem is recorded as a skip reason, never thrown.
        var atfPackage = expectedPackage ?? foregroundAfter;
        if (atfPackage is not null)
        {
            var harness = await AndroidHarness.RunAsync(adb, atfPackage, density, androidHarnessDir);
            if (harness.SkipReason is { } reason)
                await File.WriteAllTextAsync(Path.Combine(captureDir, AtfSkipReasonFile), reason);
            else
                await File.WriteAllTextAsync(Path.Combine(captureDir, AtfResultFile), JsonSerializer.Serialize(harness));
        }
        else
        {
            await File.WriteAllTextAsync(Path.Combine(captureDir, AtfSkipReasonFile),
                "the app in front could not be identified, so the Android accessibility harness was not run");
        }

        // Android TalkBack screen-reader capture: opt-in (see captureScreenReader's remarks), separate from
        // the ATF harness above -- see ScreenReaderResultFile/ScreenReaderSkipReasonFile.
        if (captureScreenReader)
        {
            var screenReaderPackage = expectedPackage ?? foregroundAfter;
            if (screenReaderPackage is not null)
            {
                var screenReader = await AndroidHarness.RunScreenReaderCaptureAsync(adb, screenReaderPackage, androidHarnessDir);
                if (screenReader.SkipReason is { } reason)
                    await File.WriteAllTextAsync(Path.Combine(captureDir, ScreenReaderSkipReasonFile), reason);
                else
                    await File.WriteAllTextAsync(Path.Combine(captureDir, ScreenReaderResultFile), JsonSerializer.Serialize(screenReader));
            }
            else
            {
                await File.WriteAllTextAsync(Path.Combine(captureDir, ScreenReaderSkipReasonFile),
                    "the app in front could not be identified, so the Android screen-reader capture was not run");
            }
        }
    }

    /// <summary>Builds a snapshot from a capture directory written by <see cref="CaptureAsync"/>.</summary>
    public static ScreenSnapshot Load(string captureDir, string screenName, string? package = null)
    {
        var density = int.Parse(File.ReadAllText(Path.Combine(captureDir, DensityFile)).Trim());
        var compressedPath = Path.Combine(captureDir, CompressedDumpFile);
        var screenshotPath = Path.Combine(captureDir, ScreenshotFile);

        // package given wins; else prefer the ground truth CaptureAsync saved (PackageFile) over guessing
        // from the dump's node counts (UiAutomatorParser.GuessPackage, still the fallback for a capture made
        // before PackageFile existed, or a hand-built one such as a test fixture).
        package ??= LoadPackageFile(captureDir);
        var (atfExtras, atfIssues, atfRan, atfSkippedReason) = LoadAtfHarness(captureDir);
        var root = UiAutomatorParser.Parse(
            File.ReadAllText(Path.Combine(captureDir, FullDumpFile)),
            File.Exists(compressedPath) ? File.ReadAllText(compressedPath) : null,
            density,
            package,
            atfExtras);

        return new ScreenSnapshot
        {
            Platform = Platform.Android,
            ScreenName = screenName,
            Root = root,
            Framework = DetectFramework(root),
            Device = Devices.Load(captureDir),
            PixelScale = density / 160.0,
            ScreenshotPath = File.Exists(screenshotPath) ? screenshotPath : null,
            Screenshot = File.Exists(screenshotPath) ? PngDecoder.Decode(screenshotPath) : null,
            // The root's NativeType is always the package UiAutomatorParser.Parse resolved -- the one given
            // here (explicit, or from PackageFile above), or else its own guess from the dump when neither is
            // available (see UiAutomatorParser.GuessPackage) -- so this is known whenever the dump has a
            // usable package attribute. Lets run history group and compare runs of the same app without a
            // typed app id (see Swipewalk.Engine.RunHistory). Null in the rare case the dump has none at all.
            AppId = root.NativeType,
            AtfIssues = atfIssues,
            AtfRan = atfRan,
            AtfSkippedReason = atfSkippedReason,
            ScreenReaderCapture = LoadScreenReaderCapture(captureDir, File.ReadAllText(Path.Combine(captureDir, FullDumpFile)), root.NativeType),
        };
    }

    /// <summary>
    /// Reads back what <see cref="CaptureAsync"/> wrote for the Android TalkBack screen-reader capture (see
    /// <see cref="ScreenReaderResultFile"/>/<see cref="ScreenReaderSkipReasonFile"/>). Null (matching
    /// <see cref="ScreenSnapshot.ScreenReaderCapture"/>'s own contract) when <c>captureScreenReader</c>
    /// wasn't requested for this capture -- neither file exists then. Matches each captured item to a node
    /// path by the same identity <see cref="UiAutomatorParser.KeyPathsByPackage"/> computes from the raw
    /// dump; an item whose key isn't found (a node the harness saw that this dump's tree doesn't, or vice
    /// versa) is kept with <see cref="MatchConfidence.None"/> rather than dropped, so the report can still
    /// show what TalkBack said even when it couldn't be lined up to a scanned element.
    /// </summary>
    private static ScreenReaderCapture? LoadScreenReaderCapture(string captureDir, string fullXml, string? package)
    {
        var resultPath = Path.Combine(captureDir, ScreenReaderResultFile);
        var skipPath = Path.Combine(captureDir, ScreenReaderSkipReasonFile);
        if (!File.Exists(resultPath) && !File.Exists(skipPath))
            return null;

        // Scope is a constant fact about the TalkBack route itself (see ScreenReaderCaptureScope's own
        // remarks), not something that varies per capture -- set here directly rather than carried through
        // the harness's own JSON contract, on every path (skipped, unreadable, and the real result below).
        if (File.Exists(skipPath))
            return new ScreenReaderCapture(ScreenReaderSource.TalkBack, ToolVersion: "unknown", DateTimeOffset.UtcNow, [], Complete: false,
                NotCompleteReason: File.ReadAllText(skipPath).Trim(), Scope: ScreenReaderCaptureScope.FocusableElementsOnly);

        var harness = JsonSerializer.Deserialize<AndroidHarness.ScreenReaderHarnessResult>(File.ReadAllText(resultPath));
        if (harness is null)
            return new ScreenReaderCapture(ScreenReaderSource.TalkBack, ToolVersion: "unknown", DateTimeOffset.UtcNow, [], Complete: false,
                NotCompleteReason: "the screen-reader capture result could not be read", Scope: ScreenReaderCaptureScope.FocusableElementsOnly);

        var keyPaths = UiAutomatorParser.KeyPathsByPackage(fullXml, package);
        var items = harness.Items.Select(item =>
        {
            var matched = keyPaths.TryGetValue(item.Key, out var path);
            return new ScreenReaderCaptureItem(
                item.Order, item.SpokenText, Label: null, Value: null, Traits: null, Hint: null, Identifier: null, ClassName: null,
                Timestamp: null, MatchedNodePath: matched ? path : null, MatchConfidence: matched ? MatchConfidence.Exact : MatchConfidence.None);
        }).ToList();

        return new ScreenReaderCapture(
            ScreenReaderSource.TalkBack, harness.ToolVersion ?? "unknown", DateTimeOffset.UtcNow, items, harness.Complete, harness.NotCompleteReason,
            harness.Language, Scope: ScreenReaderCaptureScope.FocusableElementsOnly);
    }

    /// <summary>
    /// Reads back what <see cref="CaptureAsync"/> wrote for the Android instrumentation harness (see
    /// <see cref="AndroidHarness"/>): either <see cref="AtfResultFile"/> (harness ran -- <c>Ran</c> true) or
    /// <see cref="AtfSkipReasonFile"/> (it didn't, with why). Neither file present means a capture made
    /// before this existed, or a hand-built one (e.g. a test fixture); treated the same as an unrecorded skip
    /// (<c>Ran</c> false) so the report doesn't silently claim Google's checks ran and found nothing.
    /// </summary>
    private static (IReadOnlyDictionary<string, AndroidHarness.NodeExtras>? Extras, IReadOnlyList<AtfIssue> Issues, bool Ran, string? SkippedReason) LoadAtfHarness(string captureDir)
    {
        var resultPath = Path.Combine(captureDir, AtfResultFile);
        if (File.Exists(resultPath))
        {
            var harness = JsonSerializer.Deserialize<AndroidHarness.HarnessResult>(File.ReadAllText(resultPath));
            return harness is null ? (null, [], false, null) : (harness.Nodes, harness.AtfIssues, true, null);
        }
        var skipPath = Path.Combine(captureDir, AtfSkipReasonFile);
        return (null, [], false,
            File.Exists(skipPath) ? File.ReadAllText(skipPath).Trim() : "no Android accessibility harness result was recorded for this capture");
    }

    private static string? LoadPackageFile(string captureDir)
    {
        var path = Path.Combine(captureDir, PackageFile);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    /// <summary>Returns the foreground package read along the way (null when it couldn't be read), so callers
    /// with no <paramref name="expectedPackage"/> can still record it (see <see cref="PackageFile"/>).</summary>
    private static async Task<string?> EnsureInFrontAsync(Adb adb, string? expectedPackage)
    {
        var foreground = ForegroundPackage(await adb.RunAsync("shell", "dumpsys", "window"));
        if (expectedPackage is not null && foreground != expectedPackage)
            throw new TargetNotInFrontException($"{foreground ?? "Another window"} is in front instead of {expectedPackage}; nothing was captured.");
        return foreground;
    }

    /// <summary>The package in front on the device, when known.</summary>
    internal static async Task<string?> ForegroundPackageAsync(Adb adb) => ForegroundPackage(await adb.RunAsync("shell", "dumpsys", "window"));

    /// <summary>The package in front on <paramref name="serial"/> (or the only connected device/emulator), when
    /// known. Public so callers outside this assembly (e.g. <c>ScanService</c>) can say which app is about to be
    /// scanned when none was named with --package.</summary>
    public static Task<string?> ForegroundPackageAsync(string? serial = null) => ForegroundPackageAsync(new Adb(serial));

    /// <summary>The device to use: <paramref name="serial"/>, or the only connected device -- see
    /// <see cref="Adb.ResolveSerialAsync"/>. Public so callers outside this assembly (e.g. <c>ScanService</c>'s
    /// appearance rescan) can resolve it once and reuse it for several adb calls against the same device.</summary>
    public static Task<string> ResolveSerialAsync(string? serial = null) => new Adb(serial).ResolveSerialAsync();

    /// <summary>
    /// Brings <paramref name="package"/> to the front before a scan or the start of a recording, so pre-flight's
    /// "another app is in front" is no longer a dead end when the app is installed (see <see cref="BringToFront"/>).
    /// Already in front: does nothing, so "scan the screen shown now" means exactly that. Running in the
    /// background: brought forward with an <c>am start</c> against its launcher activity, the same call
    /// <see cref="AppInstaller.LaunchAndroidAsync"/> makes after a fresh install -- for a task Android already
    /// has for the app, this resumes it rather than starting a second instance in the common case (a normal
    /// launch-mode activity), and is logged. Not running: started the same way, and the return value tells the
    /// caller to record that fact (the first screen shown is the app's own, possibly onboarding, not wherever
    /// it was left). Not installed: throws with the existing "install a build first" guidance. Either way,
    /// waits for the app to actually reach the front (<see cref="ForegroundWait"/>) before returning; if it
    /// never does, throws <see cref="TargetNotInFrontException"/> rather than let a capture silently scan the
    /// wrong app.
    /// </summary>
    /// <returns>True when the app had to be started from not running (vs. merely brought forward, or already
    /// in front).</returns>
    public static async Task<bool> EnsureAppInFrontAsync(
        string package, string? serial, IProgress<string>? log = null, CancellationToken cancellationToken = default)
    {
        serial = await new Adb(serial).ResolveSerialAsync();
        var adb = new Adb(serial);
        var window = await adb.RunAsync("shell", "dumpsys", "window");
        var installed = (await adb.RunAsync("shell", "pm", "list", "packages", package)).Contains($"package:{package}", StringComparison.Ordinal);
        // pidof exits non-zero (adb then throws) when nothing matches, same as the shell command itself --
        // that's "not running", not a real failure.
        string pidof;
        try
        {
            pidof = installed ? await adb.RunAsync("shell", "pidof", package) : "";
        }
        catch (InvalidOperationException)
        {
            pidof = "";
        }
        var state = DecideForegroundState(window, package, installed, pidof);

        var action = BringToFront.Decide(state);
        if (action == BringToFront.Action.None)
            return false;
        if (action == BringToFront.Action.Fail)
            throw new InvalidOperationException($"{package} is not installed. Install the app on the device first.");

        await AppInstaller.LaunchAndroidAsync(package, serial);
        if (!await ForegroundWait.UntilInFrontAsync(
                async () => await ForegroundPackageAsync(adb) == package,
                ForegroundWait.DefaultTimeout, ForegroundWait.DefaultPollInterval, cancellationToken))
            throw new TargetNotInFrontException($"{package} did not come to the front after being started; nothing was captured.");

        var launched = action == BringToFront.Action.Launch;
        log?.Report(launched ? BringToFront.StartedLog(package) : BringToFront.BroughtToFrontLog(package));
        return launched;
    }

    /// <summary>
    /// Pure decision behind <see cref="EnsureAppInFrontAsync"/>, from three cheap adb reads already used
    /// elsewhere (the focused window, <c>pm list packages</c>, <c>pidof</c>): testable without a device (see
    /// AndroidCollectorTests).
    /// </summary>
    internal static AppForegroundState DecideForegroundState(string dumpsysWindow, string package, bool installed, string pidofOutput) =>
        !installed ? AppForegroundState.NotInstalled
        : ForegroundPackage(dumpsysWindow) == package ? AppForegroundState.InFront
        : pidofOutput.Trim().Length > 0 ? AppForegroundState.Background
        : AppForegroundState.NotRunning;

    /// <summary>Package of the focused window from <c>dumpsys window</c> (mCurrentFocus).</summary>
    internal static string? ForegroundPackage(string dumpsysWindow)
    {
        var match = FocusPattern().Match(dumpsysWindow);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"mCurrentFocus=Window\{\S+ \S+ ([\w.]+)/")]
    private static partial Regex FocusPattern();

    /// <summary>Vertical extent of the status bar from <c>dumpsys window</c> (Android 13 and 16 formats).</summary>
    internal static (int Top, int Bottom)? StatusBarFrame(string dumpsysWindow)
    {
        var match = StatusBarPattern().Match(dumpsysWindow);
        return match.Success ? (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value)) : null;
    }

    [GeneratedRegex(@"InsetsSource\b[^\n]*?type=(?:ITYPE_STATUS_BAR|statusBars) frame=\[\d+,(\d+)\]\[\d+,(\d+)\]")]
    private static partial Regex StatusBarPattern();

    /// <summary>Refuses to capture a sleeping or locked device, which would scan the lock screen instead of the app.</summary>
    private static async Task EnsureUnlockedAsync(Adb adb)
    {
        var power = await adb.RunAsync("shell", "dumpsys", "power");
        var window = await adb.RunAsync("shell", "dumpsys", "window");
        if (!IsAwakeAndUnlocked(power, window))
            throw new InvalidOperationException(
                "The device's screen is off or locked. Unlock it, open the app screen to scan, and try again " +
                "(Developer options > Stay awake keeps the screen on while charging).");
    }

    internal static bool IsAwakeAndUnlocked(string dumpsysPower, string dumpsysWindow) =>
        dumpsysPower.Contains("mWakefulness=Awake", StringComparison.Ordinal)
        && !dumpsysWindow.Contains("isKeyguardShowing=true", StringComparison.Ordinal)
        && !dumpsysWindow.Contains("mDreamingLockscreen=true", StringComparison.Ordinal);

    /// <summary>MAUI's Android navigation host views have recognizable resource ids.</summary>
    internal static AppFramework DetectFramework(AccessibilityNode root) =>
        root.DescendantsAndSelf().Any(n => n.AutomationId is "navigationlayout_content" or "navigationlayout_appbar" or "nav_host")
            ? AppFramework.Maui
            : AppFramework.Unknown;

    /// <summary>Parses <c>wm density</c> output; an override density wins over the physical one.</summary>
    internal static int ParseDensity(string output)
    {
        var overrideMatch = OverrideDensity().Match(output);
        var match = overrideMatch.Success ? overrideMatch : PhysicalDensity().Match(output);
        return match.Success
            ? int.Parse(match.Groups[1].Value)
            : throw new FormatException($"Unexpected 'wm density' output: {output}");
    }

    [GeneratedRegex(@"Override density:\s*(\d+)")]
    private static partial Regex OverrideDensity();

    [GeneratedRegex(@"Physical density:\s*(\d+)")]
    private static partial Regex PhysicalDensity();
}
