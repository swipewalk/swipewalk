using System.Globalization;
using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;

namespace Swipewalk.Collectors.Android;

/// <summary>Record-mode source for an Android device or emulator, over adb.</summary>
public sealed class AndroidScreenSource : IScreenSource
{
    public const double LargeFontScale = 2.0;

    private readonly string _serial;
    private readonly Adb _adb;
    private readonly DeviceInfo? _device;
    private readonly bool _recordMode;
    private readonly string? _androidHarnessDir;
    private readonly bool _captureScreenReader;

    private AndroidScreenSource(string serial, string targetApp, DeviceInfo? device, bool recordMode, string? androidHarnessDir, bool captureScreenReader)
    {
        _serial = serial;
        _adb = new Adb(serial);
        TargetApp = targetApp;
        _device = device;
        _recordMode = recordMode;
        _androidHarnessDir = androidHarnessDir;
        _captureScreenReader = captureScreenReader;
    }

    public string TargetApp { get; }

    /// <summary>
    /// Connects to the given device, or the only connected one, and records <paramref name="package"/>, or the
    /// app in front when recording starts. <paramref name="recordMode"/> is false for the one-shot `scan`
    /// flow: it changes what <see cref="CaptureLargeTextAsync"/> does with the app under test once the large-
    /// text escalation is over (see <see cref="LargeTextReset"/>) -- scan has no user to hand the app back to,
    /// so it stops it rather than relaunching it a second time. <paramref name="androidHarnessDir"/> is
    /// <see cref="Swipewalk.Engine.ScanOptions.AndroidHarnessDir"/>, threaded through to every
    /// <see cref="CaptureAsync"/> so Google's Accessibility Test Framework runs on record captures too, not
    /// only on `scan` (which calls <see cref="AndroidCollector.CaptureAsync"/> directly); null auto-detects
    /// the harness, same as scan. <paramref name="captureScreenReader"/> is likewise threaded through to
    /// every <see cref="CaptureAsync"/> call for the whole run -- see that parameter's remarks on
    /// <see cref="AndroidCollector.CaptureAsync"/> for its cost; opt-in and false by default.
    /// </summary>
    public static async Task<AndroidScreenSource> ConnectAsync(
        string? serial = null, string? package = null, bool recordMode = true, string? androidHarnessDir = null,
        bool captureScreenReader = false)
    {
        var resolved = await new Adb(serial).ResolveSerialAsync();
        package ??= await AndroidCollector.ForegroundPackageAsync(new Adb(resolved))
            ?? throw new InvalidOperationException("Could not tell which app is in front; open the app or pass --package.");
        var device = await Devices.AndroidInfoAsync(resolved);
        return new(resolved, package, device, recordMode, androidHarnessDir, captureScreenReader);
    }
    private string? _originalFontScale;

    public Platform Platform => Platform.Android;

    public const string LargeTextDescription = "Android font scale 2.0 (200%)";

    public string LargeTextSetting => LargeTextDescription;

    public double LargeTextScale => LargeFontScale;

    /// <inheritdoc/>
    public bool ChangesPhysicalDeviceTextSize => PhysicalDeviceTextSizeNotice.AppliesTo(_device);

    /// <inheritdoc/>
    public Task<bool> EnsureInFrontAsync(IProgress<string>? log = null, CancellationToken cancellationToken = default) =>
        AndroidCollector.EnsureAppInFrontAsync(TargetApp, _serial, log, cancellationToken);

    public async Task<AccessibilityNode> PeekAsync(CancellationToken cancellationToken = default)
    {
        var foreground = await AndroidCollector.ForegroundPackageAsync(_adb);
        if (foreground != TargetApp)
            throw new TargetNotInFrontException($"{foreground ?? "Another window"} is in front instead of {TargetApp}.");
        await _adb.RunAsync("shell", "uiautomator", "dump", "--compressed", "/sdcard/cf-peek.xml");
        var xml = await _adb.RunAsync("exec-out", "cat", "/sdcard/cf-peek.xml");
        var density = AndroidCollector.ParseDensity(await _adb.RunAsync("shell", "wm", "density"));
        return UiAutomatorParser.Parse(xml, compressedXml: null, density, TargetApp);
    }

    public async Task<ScreenSnapshot> CaptureAsync(string captureDir, string screenName, CancellationToken cancellationToken = default)
    {
        await AndroidCollector.CaptureAsync(
            captureDir, _serial, expectedPackage: TargetApp, androidHarnessDir: _androidHarnessDir,
            captureScreenReader: _captureScreenReader);
        return AndroidCollector.Load(captureDir, screenName, TargetApp);
    }

    /// <summary>Implicit <see cref="IScreenSource.CaptureLargeTextAsync"/> (record mode always calls through
    /// this three-parameter signature): the four-parameter overload below is what
    /// <c>Swipewalk.Engine.ScanService</c> calls instead, on this concrete type, for scan's own ask-before-
    /// restart flow.</summary>
    public Task<LargeTextCapture> CaptureLargeTextAsync(string captureDir, string screenName, CancellationToken cancellationToken = default) =>
        CaptureLargeTextAsync(captureDir, screenName, cancellationToken, confirmRestart: null);

    /// <param name="confirmRestart">Scan mode only (see <see cref="ScanLargeTextRestartAsk"/>): asked just
    /// before the force-stop + relaunch escalation, once the live attempt above finds the text didn't visibly
    /// grow. Null (record mode, or scan with no asker wired up) proceeds with the restart automatically, the
    /// same as before this parameter existed.</param>
    public async Task<LargeTextCapture> CaptureLargeTextAsync(
        string captureDir, string screenName, CancellationToken cancellationToken, ScanLargeTextRestartAsk? confirmRestart)
    {
        var before = await CaptureTreeOnlyAsync();
        _originalFontScale ??= NormalFontScale((await _adb.RunAsync("shell", "settings", "get", "system", "font_scale")).Trim());
        // Check before touching anything: if the phone's text size is already enlarged, a normal-size
        // capture isn't at default size, and enlarging it further would compare large-vs-large -- which
        // would wrongly look like text "did not grow" even when nothing is wrong. Nothing has been changed
        // yet, so there's nothing to restore.
        if (double.TryParse(_originalFontScale, CultureInfo.InvariantCulture, out var currentScale)
            && BaselineTextSize.IsAndroidEnlarged(currentScale))
            return LargeTextCapture.SkippedBaselineEnlarged($"font_scale {_originalFontScale}");
        TextSizeRestore.Remember(_serial, _originalFontScale);

        // Set once the escalation below force-stops + relaunches the app while font_scale is still
        // enlarged: some frameworks (a MAUI MainActivity declaring ConfigChanges.FontScale among them) only
        // read the system font scale at launch, so restoring font_scale in the `finally` block below
        // doesn't undo what the relaunched process is already showing (see LargeTextReset).
        var relaunchedAtLargerSize = false;
        LargeTextCapture result;
        try
        {
            result = await EvaluateAsync();
        }
        catch (TargetNotInFrontException)
        {
            // The app left the foreground again during a capture (e.g. a second restart);
            // degrade gracefully instead of failing the whole scan.
            result = LargeTextCapture.Skipped(LargeTextCapture.NotInFront);
        }
        finally
        {
            await RestoreFontScaleAsync();
            await Task.Delay(TimeSpan.FromSeconds(1.5), CancellationToken.None);
        }
        return await ApplyResetAsync(result, relaunchedAtLargerSize, cancellationToken);

        async Task<LargeTextCapture> EvaluateAsync()
        {
            await SetFontScaleAsync(LargeFontScale.ToString("0.0", CultureInfo.InvariantCulture));
            await Task.Delay(TimeSpan.FromSeconds(2.5), cancellationToken); // let the app re-layout
            // The font-scale change can restart the activity, which briefly shows the launcher or a
            // system window; give the app a chance to come back to front rather than failing the scan.
            if (!await WaitForForegroundAsync(cancellationToken))
                return LargeTextCapture.Skipped(LargeTextCapture.NotInFront);
            var large = await CaptureAsync(captureDir, screenName, cancellationToken);
            if (!_recordMode)
            {
                // Scan mode: unchanged from before this method distinguished record mode.
                if (DecideAfterFirstCapture(before, large) is { } decided)
                    return decided;
            }
            else
            {
                // Record mode never force-stops + relaunches the app on its own here: that would interrupt
                // the person's navigation without asking. Report what happened -- same screen, no growth
                // (DidNotGrowLive) or a different screen, typically the app's first (WentToAnotherScreenLive)
                // -- and let Swipewalk.Engine.Recorder ask; if the person says yes,
                // BeginLargeTextAsync/CompleteLargeTextAsync perform the same escalation below, once they've
                // navigated back.
                var sameScreen = ScreenIdentity.IsSameScreen(before, large);
                if (sameScreen && TextGrowth.LooksGrown(before, large))
                    return LargeTextCapture.Captured(large with { LargeTextMethod = "system setting", LargeTextAppliedLive = true });
                return LargeTextCapture.Skipped(sameScreen ? LargeTextCapture.DidNotGrowLive : LargeTextCapture.WentToAnotherScreenLive);
            }

            // Scan mode only, from here (record mode always returned above): text didn't visibly grow though
            // the app is still showing the same screen: some frameworks (e.g. a MAUI MainActivity that
            // declares ConfigChanges.FontScale) suppress the activity recreation that would otherwise pick up
            // the font-scale change, so text stays frozen until the app is relaunched. Force-stop + relaunch
            // and try again, the same escalation the iOS flow uses -- unless the person asked not to (see
            // ScanLargeTextRestartAsk): confirmRestart is called only here, right before the restart itself,
            // never for the DifferentScreen case above (nothing was restarted yet there).
            if (confirmRestart is not null && await confirmRestart(LargeTextCapture.DidNotGrowLive, cancellationToken) is { } declinedReason)
                return LargeTextCapture.Skipped(declinedReason);
            relaunchedAtLargerSize = true;
            await RelaunchAsync();
            await Task.Delay(TimeSpan.FromSeconds(2.5), cancellationToken);
            if (!await WaitForForegroundAsync(cancellationToken))
                return LargeTextCapture.Skipped(LargeTextCapture.NotInFront);
            var afterRestart = await CaptureAsync(captureDir, screenName, cancellationToken);
            return DecideAfterRestart(before, afterRestart);
        }
    }

    /// <inheritdoc/>
    public async Task BeginLargeTextAsync(string reason, CancellationToken cancellationToken = default)
    {
        // Read first, without changing anything, and remember the marker before font_scale is touched
        // below. A TextSizeReadFailedException here means the read itself failed, before anything changed;
        // Swipewalk.Engine.Recorder's AskAndMaybeBeginAsync reports that specifically, distinct from any
        // later failure (which may have already changed font_scale, and gets a best-effort restore instead).
        if (_originalFontScale is null)
        {
            try
            {
                _originalFontScale = NormalFontScale((await _adb.RunAsync("shell", "settings", "get", "system", "font_scale")).Trim());
            }
            catch (InvalidOperationException ex)
            {
                throw new TextSizeReadFailedException($"could not read this device's current text size ({ex.Message.Split('\n')[0]})");
            }
        }
        TextSizeRestore.Remember(_serial, _originalFontScale);
        await SetFontScaleAsync(LargeFontScale.ToString("0.0", CultureInfo.InvariantCulture));
        await Task.Delay(TimeSpan.FromSeconds(2.5), cancellationToken);
        await WaitForForegroundAsync(cancellationToken);
        if (reason == LargeTextCapture.DidNotGrowLive)
        {
            // WentToAnotherScreenLive already means the OS recreates the activity on its own when the
            // setting changes -- an explicit relaunch here would only cost another disruption for nothing.
            // DidNotGrowLive means it doesn't (e.g. android:configChanges="fontScale"), so the size never
            // takes effect without one.
            await RelaunchAsync();
            await Task.Delay(TimeSpan.FromSeconds(2.5), cancellationToken);
            await WaitForForegroundAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<LargeTextCapture> CompleteLargeTextAsync(
        string captureDir, string screenName, ScreenSnapshot before, string reason, bool liveAttemptFailedHere,
        CancellationToken cancellationToken = default)
    {
        LargeTextCapture result;
        try
        {
            var large = await CaptureAsync(captureDir, screenName, cancellationToken);
            var sameScreen = ScreenIdentity.IsSameScreen(before, large);
            var grown = sameScreen && TextGrowth.LooksGrown(before, large);
            result = sameScreen
                ? LargeTextCapture.Captured(large with
                    {
                        LargeTextMethod = "system setting",
                        LargeTextAppliedLive = liveAttemptFailedHere ? (grown ? (bool?)false : null) : null,
                        LargeTextRestartCaptured = true,
                    })
                : LargeTextCapture.Skipped(LargeTextCapture.DifferentScreen);
        }
        catch (TargetNotInFrontException)
        {
            result = LargeTextCapture.Skipped(LargeTextCapture.NotInFront);
        }
        finally
        {
            await RestoreAfterLargeTextAsync(reason);
        }
        return result;
    }

    /// <inheritdoc/>
    public Task AbandonLargeTextAsync(string reason, CancellationToken cancellationToken = default) => RestoreAfterLargeTextAsync(reason);

    /// <summary>Shared restore step behind <see cref="CompleteLargeTextAsync"/>'s <c>finally</c> and
    /// <see cref="AbandonLargeTextAsync"/>: always uses <see cref="CancellationToken.None"/> since restoring
    /// the device is cleanup that should run to completion even when the caller's own token is why it's
    /// running at all.</summary>
    private async Task RestoreAfterLargeTextAsync(string reason)
    {
        await RestoreFontScaleAsync();
        if (reason == LargeTextCapture.DidNotGrowLive)
        {
            // This app doesn't pick up a font-scale change without an explicit relaunch (see
            // BeginLargeTextAsync): restoring the setting alone would leave it still showing large text
            // until relaunched again, silently enlarging the next "normal" capture.
            // WentToAnotherScreenLive needs no equivalent step: the OS recreates the activity by itself
            // when font_scale changes, restoring included.
            try
            {
                await RelaunchAsync();
                await Task.Delay(TimeSpan.FromSeconds(2.5), CancellationToken.None);
                await WaitForForegroundAsync(CancellationToken.None);
            }
            catch (InvalidOperationException) { /* best effort: see ApplyResetAsync */ }
        }
        await Task.Delay(TimeSpan.FromSeconds(1.5), CancellationToken.None);
    }

    /// <summary>
    /// Clean-up step after a large-text escalation, once font_scale has been restored: if the app was force-
    /// stopped + relaunched at the larger scale (<see cref="LargeTextReset"/>), scan mode stops it again
    /// (there's no user waiting on it, and leaving it running at a stale enlarged size would fool a later
    /// plain scan the way the original bug did); record mode relaunches it once more so it's back in front,
    /// correctly sized, for the user to keep going, and the caller is told via <see cref="LargeTextCapture.ResetNotice"/>.
    /// Best effort throughout: a failure here is no worse than before this clean-up step existed.
    /// </summary>
    private async Task<LargeTextCapture> ApplyResetAsync(LargeTextCapture result, bool relaunchedAtLargerSize, CancellationToken cancellationToken)
    {
        switch (LargeTextReset.Decide(relaunchedAtLargerSize, _recordMode))
        {
            case LargeTextResetAction.Terminate:
                try { await _adb.RunAsync("shell", "am", "force-stop", TargetApp); }
                catch (InvalidOperationException) { /* best effort */ }
                return result;
            case LargeTextResetAction.Relaunch:
                try
                {
                    await RelaunchAsync();
                    return result with { ResetNotice = LargeTextCapture.RestartedToRestoreNotice };
                }
                catch (Exception ex) when (ex is InvalidOperationException or TargetNotInFrontException)
                {
                    return result;
                }
            default:
                return result;
        }
    }

    private Task<bool> WaitForForegroundAsync(CancellationToken cancellationToken) =>
        ForegroundWait.UntilInFrontAsync(
            async () => await AndroidCollector.ForegroundPackageAsync(_adb) == TargetApp,
            ForegroundWait.DefaultTimeout, ForegroundWait.DefaultPollInterval, cancellationToken);

    /// <summary>
    /// The pure decision behind the live-vs-restart escalation, once the capture at the larger font scale has
    /// finished: a different screen skips outright (the app likely restarted on its own); text that visibly
    /// grew on the same screen is live; unchanged text on the same screen needs the force-stop + relaunch
    /// escalation, signalled here by returning null so the caller can try again. Mirrors
    /// <see cref="Swipewalk.Collectors.Ios.IosCollector.Evaluate"/>.
    /// </summary>
    internal static LargeTextCapture? DecideAfterFirstCapture(ScreenSnapshot before, ScreenSnapshot large) =>
        !ScreenIdentity.IsSameScreen(before, large)
            ? LargeTextCapture.Skipped(LargeTextCapture.DifferentScreen)
            : TextGrowth.LooksGrown(before, large)
                ? LargeTextCapture.Captured(large with { LargeTextMethod = "system setting", LargeTextAppliedLive = true })
                : null;

    /// <summary>
    /// The pure decision after the force-stop + relaunch escalation: the same screen and text that actually
    /// grew (<see cref="TextGrowth.LooksGrown"/>) means the restart picked up the larger font scale (recorded
    /// as applied only after a restart, since the app itself never refreshed on its own -- the OS recreating
    /// the activity without a forced restart is the "live" case handled by <see cref="DecideAfterFirstCapture"/>
    /// instead); the same screen with text that still didn't grow means the app never applied the setting at
    /// all, recorded as unknown/not-applied (null) rather than falsely claiming a restart fixed it -- the after
    /// restart capture is still kept so <c>TextResizeRule</c> reports the WCAG 1.4.4 "did not get taller"
    /// finding; a different screen means the app lost its place and there's nothing to report safely.
    /// </summary>
    internal static LargeTextCapture DecideAfterRestart(ScreenSnapshot before, ScreenSnapshot afterRestart) =>
        ScreenIdentity.IsSameScreen(before, afterRestart)
            ? LargeTextCapture.Captured(afterRestart with
                {
                    LargeTextMethod = "system setting",
                    LargeTextAppliedLive = TextGrowth.LooksGrown(before, afterRestart) ? false : null,
                    LargeTextRestartCaptured = true,
                })
            : LargeTextCapture.Skipped(LargeTextCapture.DifferentScreenAfterRestart);

    /// <summary>
    /// Force-stops and relaunches the app under test, resolving its launcher activity the same way
    /// <see cref="AppInstaller.LaunchAndroidAsync"/> does after a fresh install. Unlike a force-stop (which is
    /// harmless if the app isn't running), a failed launch leaves nothing for the escalation attempt to
    /// capture, so it's allowed to throw -- the same shape as iOS's Simulator/physical relaunch.
    /// </summary>
    private async Task RelaunchAsync()
    {
        await _adb.RunAsync("shell", "am", "force-stop", TargetApp);
        var activity = (await _adb.RunAsync("shell", "cmd", "package", "resolve-activity", "--brief", TargetApp))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).LastOrDefault(l => l.Contains('/'));
        if (activity is null)
            throw new InvalidOperationException($"{TargetApp} has no launcher activity; open the screen to scan by hand.");
        await _adb.RunAsync("shell", "am", "start", "-W", "-n", activity);
    }

    private async Task<ScreenSnapshot> CaptureTreeOnlyAsync() =>
        new() { Platform = Platform.Android, ScreenName = "", Root = await PeekAsync() };

    public async ValueTask DisposeAsync() => await RestoreFontScaleAsync();

    private async Task RestoreFontScaleAsync()
    {
        if (_originalFontScale is { } original)
        {
            await SetFontScaleAsync(original);
            TextSizeRestore.Forget(_serial);
        }
    }

    /// <summary>The font_scale value to restore; "null" (never set) means the default size.</summary>
    internal static string NormalFontScale(string value) => value is "null" or "" ? "1.0" : value;

    private Task<string> SetFontScaleAsync(string value) => _adb.RunAsync("shell", "settings", "put", "system", "font_scale", value);
}
