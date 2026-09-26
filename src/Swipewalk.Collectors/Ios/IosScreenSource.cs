using System.Diagnostics;
using System.Text.Json;
using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;

namespace Swipewalk.Collectors.Ios;

/// <summary>
/// Record-mode source for an iOS Simulator or physical device, built on <see cref="IosHarnessSession"/>. By
/// default (manual capture, <c>liveSession: false</c>) every action -- a capture, the once-at-the-start
/// ensure-front check, or a large-text step -- opens its own session and closes it right after, so iOS's
/// "Automation Running" banner is visible only while that action runs, not while the person is navigating
/// between captures. Passing <c>liveSession: true</c> (auto-scan on screen change) keeps a single session open
/// for the whole recording instead, the original design, because auto-scan already polls the screen every
/// ~1.5-2s and there is no gap to hide the banner in anyway. Simulators share the Mac's file system; physical
/// devices are reached through devicectl in the harness runner's container, for either mode.
/// </summary>
public sealed class IosScreenSource : IScreenSource
{
    /// <summary>AX3: the smallest accessibility text size at or above 200% for body text (about 235%).</summary>
    public const string LargeContentSize = "accessibility-extra-large";

    public const string LargeTextDescription = "iOS accessibility text size AX3 (about 235%)";

    /// <summary>AX3 as a multiple of normal size, for body text. Used both on a Simulator (via `simctl ui
    /// content_size`) and, through the harness's Settings driver, on a physical iPhone.</summary>
    public const double LargeTextScaleValue = 2.35;

    private readonly string _udid;
    private readonly string? _harnessProject;
    private readonly SigningPlan? _signing;
    private readonly DeviceInfo? _device;
    private readonly IosCollector.DetectedFramework _framework;

    /// <summary>Set only when built with <c>liveSession: true</c> (auto-scan): one session kept open for the
    /// whole recording, instead of one per action. Null is what makes every public method below open (and
    /// dispose) its own ephemeral session via <see cref="RunAsync{T}"/>.</summary>
    private IosHarnessSession? _persistent;

    private string? _originalContentSize;

    /// <summary>The physical iPhone's text size before <see cref="BeginLargeTextAsync"/> enlarged it, so
    /// <see cref="CompleteLargeTextAsync"/> can restore exactly that value. Set only on that path -- the
    /// Simulator path reuses <see cref="_originalContentSize"/> instead.</summary>
    private TextSizeState? _pendingPhysicalOriginal;

    private readonly bool _captureScreenReader;
    private readonly IosCollector.IosInspectorGuide? _inspectorGuide;

    /// <summary>
    /// Built lazily, on the first <see cref="CaptureScreenReaderAsync"/> call: caches
    /// <see cref="_inspectorGuide"/>'s answer for the whole recording, so every later screen reuses it silently
    /// instead of asking again -- the one-time permission-and-setup step is per recording, not per screen. See
    /// <see cref="CachedInspectorGuide"/>.
    /// </summary>
    private CachedInspectorGuide? _cachedInspectorGuide;

    private IosScreenSource(
        string bundleId, string udid, string? harnessProject, SigningPlan? signing, DeviceInfo? device, IosCollector.DetectedFramework framework,
        bool captureScreenReader, IosCollector.IosInspectorGuide? inspectorGuide)
    {
        TargetApp = bundleId;
        _udid = udid;
        _harnessProject = harnessProject;
        _signing = signing;
        _device = device;
        _framework = framework;
        _captureScreenReader = captureScreenReader;
        _inspectorGuide = inspectorGuide;
    }

    public Platform Platform => Platform.iOS;

    public string TargetApp { get; }

    public string LargeTextSetting => LargeTextDescription;

    public double LargeTextScale => LargeTextScaleValue;

    /// <summary>Per-app launch-argument fallback for a physical iPhone (see <see cref="CapturePhysicalLargeTextFallbackAsync"/>).</summary>
    private const string LaunchArgumentContentSizeCategory = "-UIPreferredContentSizeCategoryName UICTContentSizeCategoryAccessibilityExtraLarge";

    /// <inheritdoc/>
    public bool ChangesPhysicalDeviceTextSize => _device?.IsPhysical == true;

    /// <summary>iOS opening a session (a capture, ensure-front check or large-text step) is expensive enough
    /// -- and shows the "Automation Running" banner -- that <see cref="Swipewalk.Engine.Recorder"/>'s poll loop
    /// should not call <see cref="PeekAsync"/> continuously just to notice the person is still navigating; see
    /// <see cref="IScreenSource.PeekIsExpensive"/>. Only true when this source was built for manual capture
    /// (<c>liveSession: false</c>, the default) -- an auto-scan source keeps one session open for the whole
    /// recording anyway, so polling it costs nothing extra and stays cheap.</summary>
    public bool PeekIsExpensive => _persistent is null;

    /// <summary>
    /// Resolves the device and (for a physical iPhone) how the harness will be signed, and detects the app's
    /// framework -- none of which needs a live session. With <paramref name="liveSession"/> true (auto-scan),
    /// also starts and waits for one session that is kept open for the whole recording, the same as before
    /// this class had a manual-capture mode; that build can take about a minute the first time. With it false
    /// (manual capture, the default), nothing is built or started here -- the first action (normally
    /// <see cref="EnsureInFrontAsync"/>, called once before the recording loop starts) does that instead.
    /// </summary>
    /// <param name="captureScreenReader">Record mode's equivalent of <c>--screen-reader</c> on iOS: when true,
    /// <see cref="CaptureScreenReaderAsync"/> walks Xcode's Accessibility Inspector for each normally captured
    /// screen (see that method's remarks). False (the default) never attempts it, whatever
    /// <paramref name="inspectorGuide"/> is.</param>
    /// <param name="inspectorGuide">The CLI/desktop prompt for the Inspector route's one-time
    /// permission-and-setup step (see <see cref="IosCollector.RunInspectorCaptureAsync"/>), asked at most once
    /// for the whole recording -- see <see cref="CaptureScreenReaderAsync"/>. Null (the default) declines
    /// without asking, same as any other unset confirmation.</param>
    public static async Task<IosScreenSource> StartAsync(
        string bundleId, string? device = null, string? harnessProject = null,
        string? team = null, string? profile = null, string? bundlePrefix = null, bool liveSession = false,
        bool captureScreenReader = false, IosCollector.IosInspectorGuide? inspectorGuide = null)
    {
        var udid = device ?? await IosCollector.BootedSimulatorAsync()
            ?? throw new InvalidOperationException("No booted iOS Simulator found; boot one or pass --device <udid>.");
        var info = (await Devices.IosAsync()).FirstOrDefault(d => d.Id == udid);

        SigningPlan? signing = null;
        if (info?.IsPhysical == true)
        {
            var (plan, problem) = SigningPlan.Create(team, profile, bundlePrefix, udid);
            signing = plan ?? throw new InvalidOperationException(problem);
        }

        // Detected once for the whole recording (the app under test doesn't change mid-session); physical
        // devices can't be read from the Mac, so they keep the default Unknown/null (see IosCollector.CaptureAsync).
        var framework = info?.IsPhysical == true
            ? new IosCollector.DetectedFramework(AppFramework.Unknown, null)
            : await IosCollector.DetectFrameworkAsync(udid, bundleId);

        var source = new IosScreenSource(bundleId, udid, harnessProject, signing, info, framework, captureScreenReader, inspectorGuide);
        if (liveSession)
            source._persistent = await IosHarnessSession.StartAsync(bundleId, udid, info?.IsPhysical == true, harnessProject, signing, CancellationToken.None);
        return source;
    }

    /// <inheritdoc/>
    public Task<bool> EnsureInFrontAsync(IProgress<string>? log = null, CancellationToken cancellationToken = default) =>
        RunAsync(session => EnsureInFrontCoreAsync(session, log, cancellationToken), cancellationToken);

    private async Task<bool> EnsureInFrontCoreAsync(IosHarnessSession session, IProgress<string>? log, CancellationToken cancellationToken)
    {
        var dir = await session.SendCommandAsync("ensure-front", cancellationToken);
        using var doc = JsonDocument.Parse(await session.ReadAsync($"{dir}/launched.json"));
        var launched = doc.RootElement.TryGetProperty("launched", out var l) && l.GetBoolean();
        var broughtForward = doc.RootElement.TryGetProperty("broughtForward", out var b) && b.GetBoolean();
        if (broughtForward)
            log?.Report(BringToFront.BroughtToFrontLog(TargetApp));
        else if (launched)
            log?.Report(BringToFront.StartedLog(TargetApp));
        return launched;
    }

    public Task<AccessibilityNode> PeekAsync(CancellationToken cancellationToken = default) =>
        RunAsync(session => PeekCoreAsync(session, cancellationToken), cancellationToken);

    private static async Task<AccessibilityNode> PeekCoreAsync(IosHarnessSession session, CancellationToken cancellationToken)
    {
        var dir = await session.SendCommandAsync("peek", cancellationToken);
        return XcuiTreeParser.Parse(await session.ReadAsync($"{dir}/{IosCollector.TreeFile}")).Root;
    }

    public Task<ScreenSnapshot> CaptureAsync(string captureDir, string screenName, CancellationToken cancellationToken = default) =>
        RunAsync(session => CaptureCoreAsync(session, captureDir, screenName, cancellationToken), cancellationToken);

    private async Task<ScreenSnapshot> CaptureCoreAsync(IosHarnessSession session, string captureDir, string screenName, CancellationToken cancellationToken)
    {
        var dir = await session.SendCommandAsync("capture", cancellationToken);
        await session.FetchAsync(dir, captureDir, IosCollector.TreeFile, IosCollector.ScreenshotFile);
        IosCollector.MaskStatusBar(captureDir);
        if (_device is not null)
            await Devices.SaveAsync(_device, captureDir);
        var snapshot = IosCollector.Load(captureDir, screenName);
        return _framework.Framework == AppFramework.Unknown
            ? snapshot
            : snapshot with { Framework = _framework.Framework, FrameworkVersion = _framework.Version };
    }

    /// <summary>
    /// Record mode's iOS screen-reader evidence (see <see cref="IScreenSource.CaptureScreenReaderAsync"/>):
    /// walks Xcode's Accessibility Inspector on the Mac -- a separate, Mac-side step from the device capture
    /// <see cref="CaptureAsync"/> just did -- via <c>IosCollector.RunInspectorCaptureAsync</c>, the same
    /// orchestration <c>scan</c> uses. The one-time guide it needs (the macOS Accessibility permission, then
    /// choosing the device and clicking the first element in the Inspector) is asked at most once for the
    /// whole recording: <see cref="_cachedInspectorGuide"/> (<see cref="CachedInspectorGuide"/>) caches the
    /// answer the first time this runs, so every later "Scan this screen now" reuses it silently -- its
    /// <see cref="CachedInspectorGuide.AskAsync"/> is what's passed down to <c>RunInspectorCaptureAsync</c>,
    /// which only ever calls the real <see cref="_inspectorGuide"/> once. A walk
    /// that comes back short or incomplete (the Inspector's selection went stale, e.g. after navigating to a
    /// screen it didn't follow) is reported through <paramref name="log"/> for that one screen, prompting the
    /// person to click an element in the Inspector and scan again -- the guide itself is never re-asked, since
    /// doing so would re-show the permission explanation for something already granted.
    /// </summary>
    public Task<ScreenReaderCapture?> CaptureScreenReaderAsync(ScreenSnapshot snapshot, IProgress<string>? log = null, CancellationToken cancellationToken = default) =>
        _captureScreenReader
            ? CaptureScreenReaderCoreAsync(snapshot, log, cancellationToken)
            : Task.FromResult<ScreenReaderCapture?>(null);

    private async Task<ScreenReaderCapture?> CaptureScreenReaderCoreAsync(ScreenSnapshot snapshot, IProgress<string>? log, CancellationToken cancellationToken)
    {
        _cachedInspectorGuide ??= new CachedInspectorGuide(_inspectorGuide, answer => log?.Report(answer
            ? "  Accessibility Inspector evidence will be captured for each screen you scan for the rest of this recording."
            : "  Accessibility Inspector evidence won't be captured for this recording (declined, or the macOS " +
              "Accessibility permission wasn't granted); the manual VoiceOver route still applies -- see the report."));

        var capture = await IosCollector.RunInspectorCaptureAsync(snapshot, _cachedInspectorGuide.AskAsync, cancellationToken);
        // Only for a walk actually attempted (the guide having been accepted), not for the "declined"/"no
        // guide" skip above, which already logged its own one-time reason -- logging the same skip again for
        // every later screen would be noise, not new information.
        if (!capture.Complete && _cachedInspectorGuide.Answer == true)
            log?.Report($"  Accessibility Inspector evidence for this screen is incomplete: {capture.NotCompleteReason}");
        return capture;
    }

    public Task<LargeTextCapture> CaptureLargeTextAsync(string captureDir, string screenName, CancellationToken cancellationToken = default) =>
        _device?.IsPhysical == true
            ? RunAsync(session => CapturePhysicalLargeTextAsync(session, captureDir, screenName, cancellationToken), cancellationToken)
            : RunAsync(session => CaptureSimulatorLargeTextAsync(session, captureDir, screenName, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public Task BeginLargeTextAsync(string reason, CancellationToken cancellationToken = default) =>
        RunAsync(session => BeginLargeTextCoreAsync(session, cancellationToken), cancellationToken);

    private async Task BeginLargeTextCoreAsync(IosHarnessSession session, CancellationToken cancellationToken)
    {
        if (_device?.IsPhysical == true)
        {
            // Read first, without changing anything, and remember the marker BEFORE AX3 is applied below --
            // not after, as a combined read+apply step used to do. A failure applying AX3, or anywhere
            // after, now leaves a marker pointing at the true original for the next preflight/doctor check
            // to restore, instead of a phone changed with nothing recorded. A TextSizeReadFailedException
            // here means the read itself failed, before anything changed; Recorder's AskAndMaybeBeginAsync
            // reports that specifically and turns it into a clean "not checked" outcome for this screen
            // instead of losing the whole recording. Any other exception from here on means something may
            // already be changed, so Recorder also asks AbandonLargeTextAsync to put it back.
            TextSizeState original;
            try
            {
                var readResult = await SendTextSizeCoreAsync(session, "textsize-read", cancellationToken);
                original = TextSizeState.Parse(readResult.GetValueOrDefault("state"))
                    ?? throw new InvalidOperationException("the iOS harness reported this iPhone's text size in an unexpected format");
            }
            catch (InvalidOperationException ex)
            {
                throw new TextSizeReadFailedException($"could not read this iPhone's current text size ({ex.Message.Split('\n')[0]})");
            }
            _pendingPhysicalOriginal = original;
            TextSizeRestore.Remember(_udid, original.ToString());
            await SendTextSizeCoreAsync(session, "textsize-restore", cancellationToken, target: TextSizeState.Ax3.ToString());
        }
        else
        {
            if (_originalContentSize is null)
            {
                try
                {
                    _originalContentSize = (await SimctlAsync("ui", _udid, "content_size")).Trim();
                }
                catch (InvalidOperationException ex)
                {
                    throw new TextSizeReadFailedException($"could not read the Simulator's current text size ({ex.Message.Split('\n')[0]})");
                }
            }
            if (_originalContentSize.Length > 0)
                TextSizeRestore.Remember(_udid, _originalContentSize);
            await SimctlAsync("ui", _udid, "content_size", LargeContentSize);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
        // Unlike Android (whose activity is recreated by the setting change itself for one of its two
        // reasons), neither the Simulator nor a physical iPhone re-applies the new size to an app that's
        // already running -- some frameworks, .NET MAUI among them, only read Dynamic Type at launch -- so a
        // restart is always needed here regardless of reason. WentToAnotherScreenLive can still occur on iOS
        // (the live attempt showed a different screen, e.g. the app navigated on its own) even though it's
        // rarer than on Android; either way this relaunch is required to show the enlarged size at all.
        // Relaunched directly (devicectl/simctl), not through the harness's own "relaunch" command -- see
        // IosCollector.DevicectlRelaunchAsync for why a physical device needs that.
        await IosCollector.RelaunchOnDeviceAsync(_udid, TargetApp, _device?.IsPhysical == true);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LargeTextCapture> CompleteLargeTextAsync(
        string captureDir, string screenName, ScreenSnapshot before, string reason, bool liveAttemptFailedHere,
        CancellationToken cancellationToken = default) =>
        RunAsync(session => CompleteLargeTextCoreAsync(session, captureDir, screenName, before, liveAttemptFailedHere, cancellationToken), cancellationToken);

    private async Task<LargeTextCapture> CompleteLargeTextCoreAsync(
        IosHarnessSession session, string captureDir, string screenName, ScreenSnapshot before, bool liveAttemptFailedHere,
        CancellationToken cancellationToken)
    {
        LargeTextCapture result;
        try
        {
            var large = await CaptureCoreAsync(session, captureDir, screenName, cancellationToken);
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
            // Same session as the capture above: restoring is the very next thing that happens, with no
            // person interaction in between -- capturing at the larger size, then restoring, are one
            // continuous disruption to the app, not two separate ones to hide a session start between.
            await RestoreAfterLargeTextCoreAsync(session, CancellationToken.None);
        }
        return result;
    }

    /// <inheritdoc/>
    public Task AbandonLargeTextAsync(string reason, CancellationToken cancellationToken = default) =>
        // Always CancellationToken.None, both for the session this opens and for the restore work inside it:
        // restoring the device is cleanup that should run to completion even when the caller's own token is
        // why it's running at all (e.g. Ctrl+C, or the recording ending early with an unfinished check).
        RunAsync(session => RestoreAfterLargeTextCoreAsync(session, CancellationToken.None), CancellationToken.None);

    /// <summary>Shared restore step behind <see cref="CompleteLargeTextAsync"/>'s <c>finally</c> and
    /// <see cref="AbandonLargeTextAsync"/>. Ignores the large-text reason that started the check -- unlike
    /// Android, iOS always needs the same relaunch regardless of which reason started the check (see
    /// <see cref="BeginLargeTextCoreAsync"/>).</summary>
    private async Task RestoreAfterLargeTextCoreAsync(IosHarnessSession session, CancellationToken cancellationToken)
    {
        if (_device?.IsPhysical == true)
        {
            try
            {
                if (_pendingPhysicalOriginal is { } original)
                    await SendTextSizeCoreAsync(session, "textsize-restore", cancellationToken, target: original.ToString());
                TextSizeRestore.Forget(_udid);
            }
            catch (InvalidOperationException)
            {
                // Leave the marker: the next preflight/doctor check restores it (see Preflight.IosAsync).
            }
        }
        else
        {
            await RestoreContentSizeAsync();
        }
        // Restore alone isn't enough to show it (the same reason a restart was needed in
        // BeginLargeTextCoreAsync): relaunch so the app is back in front, correctly sized, for the person to
        // keep going. Relaunched directly (devicectl/simctl), not through the harness's own "relaunch" command:
        // found 2026-09-23 that the harness's terminate()+launch() reports success on a physical iPhone but the
        // app keeps rendering at the enlarged size -- see IosCollector.DevicectlRelaunchAsync.
        try
        {
            await IosCollector.RelaunchOnDeviceAsync(_udid, TargetApp, _device?.IsPhysical == true);
            await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Best effort: the person is told the size was restored either way; if the app didn't come
            // back, the next "Scan this screen now" will surface that on its own.
        }
    }

    private async Task<LargeTextCapture> CaptureSimulatorLargeTextAsync(
        IosHarnessSession session, string captureDir, string screenName, CancellationToken cancellationToken)
    {
        var before = new ScreenSnapshot { Platform = Platform.iOS, ScreenName = "", Root = await PeekCoreAsync(session, cancellationToken) };
        _originalContentSize ??= (await SimctlAsync("ui", _udid, "content_size")).Trim();
        // Check before touching anything: if the Simulator's text size is already enlarged, a normal-size
        // capture isn't at default size, and enlarging it further would compare large-vs-large. Nothing has
        // been changed yet, so there's nothing to restore.
        if (BaselineTextSize.IsSimulatorEnlarged(_originalContentSize))
            return LargeTextCapture.SkippedBaselineEnlarged(_originalContentSize);
        if (_originalContentSize.Length > 0)
            TextSizeRestore.Remember(_udid, _originalContentSize);

        // Set once the escalation below relaunches the app while content_size is still enlarged: some
        // frameworks (.NET MAUI among them) only re-read the system text size at launch, so restoring
        // content_size in the `finally` block below doesn't undo what the relaunched process is already
        // showing. Record mode still needs the app usable afterwards, so it's relaunched once more --
        // unlike scan mode's outright terminate -- and the caller is told (see ApplyResetAsync).
        var relaunchedAtLargerSize = false;
        LargeTextCapture result;
        try
        {
            result = await EvaluateAsync();
        }
        catch (TargetNotInFrontException)
        {
            // The app left the foreground again during the capture itself; degrade instead of failing.
            result = LargeTextCapture.Skipped(LargeTextCapture.NotInFront);
        }
        finally
        {
            await RestoreContentSizeAsync();
            await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
        }
        return await ApplyResetAsync(session, result, relaunchedAtLargerSize, cancellationToken);

        async Task<LargeTextCapture> EvaluateAsync()
        {
            await SimctlAsync("ui", _udid, "content_size", LargeContentSize);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            // The content-size change can restart the app or briefly show another window (the same
            // weakness as Android's font scale); give it a chance to come back to front rather than
            // failing the whole scan.
            if (!await ForegroundWait.UntilInFrontAsync(
                    () => IsInFrontCoreAsync(session, cancellationToken), ForegroundWait.DefaultTimeout, ForegroundWait.DefaultPollInterval, cancellationToken))
                return LargeTextCapture.Skipped(LargeTextCapture.NotInFront);
            var large = await CaptureCoreAsync(session, captureDir, screenName, cancellationToken);
            var sameScreen = ScreenIdentity.IsSameScreen(before, large);
            if (sameScreen && TextGrowth.LooksGrown(before, large))
                return LargeTextCapture.Captured(large with { LargeTextMethod = "system setting", LargeTextAppliedLive = true });

            // Text didn't visibly grow (or a different screen came up, typically because the app restarted
            // on its own) while the app just came back to front: some frameworks (.NET MAUI among them)
            // only apply the new content size at launch. This source is used only for recording, so it
            // never force-stops + relaunches on its own here -- that would interrupt the person's
            // navigation without asking. Report what happened and let Swipewalk.Engine.Recorder ask; if the
            // person says yes, BeginLargeTextAsync/CompleteLargeTextAsync perform the restart once they've
            // navigated back.
            return LargeTextCapture.Skipped(sameScreen ? LargeTextCapture.DidNotGrowLive : LargeTextCapture.WentToAnotherScreenLive);
        }
    }

    /// <summary>
    /// Physical-iPhone large-text flow for record mode, over the same serve-mode commands `scan` uses through
    /// <see cref="IosCollector"/>: read the original Settings state and remember it (<see cref="TextSizeRestore"/>,
    /// keyed by UDID) BEFORE anything is changed, then drive Settings to AX3 (the harness activates the app
    /// under test again itself once done with Settings) → capture. If the text visibly grew, use it; otherwise
    /// relaunch the app (some frameworks, e.g. .NET MAUI, only apply the new size at launch) and capture again,
    /// skipping with a clear reason if that shows a different screen. Always restores the exact original state,
    /// forgetting the marker only once the harness confirms the restore. If reading the original size fails,
    /// nothing has been changed yet, so this skips straight to the per-app launch-argument fallback; the
    /// same fallback runs if driving Settings fails while *applying* AX3 (the marker is already recorded
    /// by then, and the <c>finally</c> below still
    /// attempts a Settings restore in case that failed attempt left it partway changed). Never throws for a
    /// large-text reason: always returns a <see cref="LargeTextCapture"/>.
    /// </summary>
    private async Task<LargeTextCapture> CapturePhysicalLargeTextAsync(
        IosHarnessSession session, string captureDir, string screenName, CancellationToken cancellationToken)
    {
        var before = new ScreenSnapshot { Platform = Platform.iOS, ScreenName = "", Root = await PeekCoreAsync(session, cancellationToken) };

        // Read first, without changing anything: a failure here leaves the phone untouched, so fall back to
        // the per-app launch argument, the same as a failure driving Settings further below.
        TextSizeState original;
        try
        {
            var readResult = await SendTextSizeCoreAsync(session, "textsize-read", cancellationToken);
            original = TextSizeState.Parse(readResult.GetValueOrDefault("state"))
                ?? throw new InvalidOperationException("the iOS harness reported this iPhone's text size in an unexpected format");
        }
        catch (InvalidOperationException)
        {
            return await CapturePhysicalLargeTextFallbackAsync(session, captureDir, screenName, before, cancellationToken);
        }

        if (BaselineTextSize.IsPhysicalEnlarged(original))
            // Nothing has been changed yet (the read above doesn't touch Settings), so there's nothing to restore.
            return LargeTextCapture.SkippedBaselineEnlarged(original.ToString());

        // The marker is written now, before AX3 is applied below -- not after, as a combined read+apply step
        // used to do. A failure applying AX3, or anywhere after, now leaves a marker pointing at the true
        // original for the next preflight/doctor check to restore, instead of a phone changed with nothing
        // recorded.
        TextSizeRestore.Remember(_udid, original.ToString());

        // See CaptureSimulatorLargeTextAsync: set once the escalation below relaunches the app while
        // Settings is still at AX3, so the app itself needs relaunching again (not just the Settings
        // restore below) to pick up the normal size.
        var relaunchedAtLargerSize = false;
        LargeTextCapture result;
        try
        {
            try
            {
                await SendTextSizeCoreAsync(session, "textsize-restore", cancellationToken, target: TextSizeState.Ax3.ToString());
            }
            catch (InvalidOperationException)
            {
                // Applying AX3 itself failed (a driver error, not a growth question): the marker above
                // already protects the phone (the `finally` below, or the next preflight/doctor check,
                // restores it), so fall back to a per-app launch argument instead of losing this check.
                return await CapturePhysicalLargeTextFallbackAsync(session, captureDir, screenName, before, cancellationToken);
            }
            result = await EvaluateAsync();
        }
        catch (TargetNotInFrontException)
        {
            result = LargeTextCapture.Skipped(LargeTextCapture.NotInFront);
        }
        finally
        {
            try
            {
                await SendTextSizeCoreAsync(session, "textsize-restore", cancellationToken, target: original.ToString());
                TextSizeRestore.Forget(_udid);
            }
            catch (InvalidOperationException)
            {
                // Leave the marker: the next preflight/doctor check restores it (see Preflight.IosAsync).
            }
        }
        return await ApplyResetAsync(session, result, relaunchedAtLargerSize, cancellationToken);

        async Task<LargeTextCapture> EvaluateAsync()
        {
            var live = await CaptureCoreAsync(session, captureDir, screenName, cancellationToken);
            var sameScreen = ScreenIdentity.IsSameScreen(before, live);
            if (sameScreen && TextGrowth.LooksGrown(before, live))
                return LargeTextCapture.Captured(live with { LargeTextMethod = "system setting", LargeTextAppliedLive = true });

            // As on the Simulator: never force-stop + relaunch on its own here -- report what happened and
            // let Swipewalk.Engine.Recorder ask first.
            return LargeTextCapture.Skipped(sameScreen ? LargeTextCapture.DidNotGrowLive : LargeTextCapture.WentToAnotherScreenLive);
        }
    }

    private async Task<LargeTextCapture> CapturePhysicalLargeTextFallbackAsync(
        IosHarnessSession session, string captureDir, string screenName, ScreenSnapshot before, CancellationToken cancellationToken)
    {
        LargeTextCapture result;
        try
        {
            await session.SendCommandAsync("relaunch", cancellationToken, launchArg: LaunchArgumentContentSizeCategory);
            var large = await CaptureCoreAsync(session, captureDir, screenName, cancellationToken);
            // This fallback relaunches the app fresh with the launch argument already set: it was never seen
            // running at the normal size first, so live-vs-restart was never tested (see IosCollector.CapturedAfterRestart).
            result = IosCollector.CapturedAfterRestart(before, large, "per-app launch setting", LargeTextCapture.DifferentScreen, liveTested: false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TargetNotInFrontException)
        {
            result = LargeTextCapture.Skipped("could not change this iPhone's text size (Settings automation failed) and the per-app launch-argument fallback also failed");
        }
        // Unlike the other escalations, this fallback's whole approach *is* a relaunch (with the launch
        // argument), so there's no separate "did the escalation happen" branch to gate on: the app is
        // relaunched here unconditionally, without the argument, to clear it -- and the user is told, since
        // it's disruptive either way (see ApplyResetAsync).
        return await ApplyResetAsync(session, result, relaunchedAtLargerSize: true, cancellationToken);
    }

    /// <summary>
    /// Record mode's clean-up step after a large-text escalation, once the system text size has been
    /// restored: relaunches the app once more (<see cref="LargeTextResetAction.Relaunch"/>) if it was left
    /// running at the larger size, so it's back in front, correctly sized, for the user to keep going, and
    /// attaches <see cref="LargeTextCapture.RestartedToRestoreNotice"/> so the caller can surface it. Best
    /// effort: a failed relaunch here just leaves the app at the stale size until the user relaunches it
    /// themselves, same as before this clean-up step existed.
    /// </summary>
    private async Task<LargeTextCapture> ApplyResetAsync(IosHarnessSession session, LargeTextCapture result, bool relaunchedAtLargerSize, CancellationToken cancellationToken)
    {
        if (LargeTextReset.Decide(relaunchedAtLargerSize, recordMode: true) != LargeTextResetAction.Relaunch)
            return result;
        try
        {
            await session.SendCommandAsync("relaunch", cancellationToken);
            return result with { ResetNotice = LargeTextCapture.RestartedToRestoreNotice };
        }
        catch (Exception ex) when (ex is InvalidOperationException or TargetNotInFrontException)
        {
            return result;
        }
    }

    /// <summary>Sends a textsize-* serve command and parses its textsize.json result, throwing with the
    /// harness's machine-readable error on failure.</summary>
    private static async Task<Dictionary<string, string>> SendTextSizeCoreAsync(
        IosHarnessSession session, string action, CancellationToken cancellationToken, string? target = null)
    {
        var dir = await session.SendCommandAsync(action, cancellationToken, target: target);
        using var doc = JsonDocument.Parse(await session.ReadAsync($"{dir}/textsize.json"));
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
    }

    /// <summary>Cheap foreground check reusing the "peek" command, for <see cref="ForegroundWait"/>. Takes the
    /// session already open for the surrounding large-text step, rather than opening a new one per poll --
    /// this can run several times in a row while waiting for the app to settle.</summary>
    private static async Task<bool> IsInFrontCoreAsync(IosHarnessSession session, CancellationToken cancellationToken)
    {
        try
        {
            await PeekCoreAsync(session, cancellationToken);
            return true;
        }
        catch (TargetNotInFrontException)
        {
            return false;
        }
    }

    /// <summary>
    /// Ends the recording session: for auto-scan (<c>liveSession: true</c>), disposes the one
    /// <see cref="IosHarnessSession"/> kept open for the whole recording, which sends "stop" so the XCTest
    /// session (and the "Automation Running" state iOS shows for its whole lifetime on a physical device) is
    /// torn down. For manual capture (the default), there is nothing to dispose here: every action already
    /// opened and closed its own session via <see cref="RunAsync{T}"/>, so no session is ever left running
    /// between captures in the first place. Either way, restores the Simulator's content size as a last-resort
    /// safety net if a large-text check somehow left it enlarged. Runs on every normal path out of record mode
    /// (finish, cancel, an error -- see the `await using` in Swipewalk.Engine.ScanService.RecordAsync).
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await RestoreContentSizeAsync();
        if (_persistent is { } persistent)
            await persistent.DisposeAsync();
    }

    /// <summary>Runs <paramref name="action"/> against the recording's one long-lived session
    /// (<c>liveSession: true</c>, auto-scan), or against a fresh session opened just for this call and
    /// disposed right after (the default): the "start a session for the action, end it right after" behaviour
    /// that keeps iOS's automation banner off screen between captures. Killing the harness process is handled
    /// inside <see cref="IosHarnessSession.StartAsync"/>/<see cref="IosHarnessSession.DisposeAsync"/>, so a
    /// cancellation partway through (e.g. Ctrl+C) never leaves an orphaned xcodebuild/XCUITest runner process
    /// behind.</summary>
    private async Task<T> RunAsync<T>(Func<IosHarnessSession, Task<T>> action, CancellationToken cancellationToken)
    {
        if (_persistent is { } persistent)
            return await action(persistent);
        await using var session = await IosHarnessSession.StartAsync(TargetApp, _udid, _device?.IsPhysical == true, _harnessProject, _signing, cancellationToken);
        return await action(session);
    }

    /// <summary>Non-generic overload of <see cref="RunAsync{T}"/>, for actions with nothing to return.</summary>
    private async Task RunAsync(Func<IosHarnessSession, Task> action, CancellationToken cancellationToken)
    {
        if (_persistent is { } persistent)
        {
            await action(persistent);
            return;
        }
        await using var session = await IosHarnessSession.StartAsync(TargetApp, _udid, _device?.IsPhysical == true, _harnessProject, _signing, cancellationToken);
        await action(session);
    }

    private async Task RestoreContentSizeAsync()
    {
        if (_originalContentSize is { Length: > 0 } original)
        {
            await SimctlAsync("ui", _udid, "content_size", original);
            TextSizeRestore.Forget(_udid);
        }
    }

    private static async Task<string> SimctlAsync(params string[] args)
    {
        var info = new ProcessStartInfo("xcrun") { RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("simctl");
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        var (exitCode, output) = await IosCollector.RunAsync(info);
        return exitCode == 0 ? output : throw new InvalidOperationException($"simctl {string.Join(' ', args)} failed: {output.Trim()}");
    }
}
