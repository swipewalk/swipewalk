using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Swipewalk.Core.Model;

namespace Swipewalk.Collectors.Android;

/// <summary>
/// Runs the Android instrumentation harness in harness/android: a thin UiAutomation reader that also runs
/// Google's Accessibility Test Framework (ATF) 4.1.1 against an UNMODIFIED app -- the Android equivalent of
/// how <c>Swipewalk.Collectors.Ios.IosCollector</c> runs Apple's XCUITest audit harness. Called from
/// <see cref="AndroidCollector.CaptureAsync"/>/<see cref="AndroidCollector.Load"/>, so both `scan` and
/// `record` get it for free; never throws -- every path returns a <see cref="HarnessResult"/>, using
/// <see cref="HarnessResult.SkipReason"/> to say why nothing came back (see KnownLimitations
/// "android-atf-harness"). Kept in its own file, not AndroidCollector.cs, so this can be reviewed and
/// changed on its own.
///
/// The default path (<see cref="EnsureInstalledAsync"/> with no explicit harness directory) installs a
/// prebuilt APK shipped with Swipewalk (<see cref="FindBundledApk()"/>): no JDK, Gradle or network access,
/// only <c>adb</c> -- CI (.github/workflows/ci.yml, release.yml) builds that APK and both
/// Swipewalk.Collectors.csproj and the desktop app's csproj bundle it, mirroring how harness/ios's source
/// ships. Building harness/android from source with the Gradle wrapper is only the fallback for a
/// from-source checkout with nothing built yet, or an explicit <c>--android-harness &lt;path&gt;</c>.
/// </summary>
public static partial class AndroidHarness
{
    /// <summary>Test package the harness's androidTest APK installs as (AGP's default "&lt;namespace&gt;.test"
    /// for harness/android/harness, whose namespace is "org.swipewalk.harness"; verified by installing it).</summary>
    public const string TestPackage = "org.swipewalk.harness.test";

    private const string InstrumentationRunner = TestPackage + "/androidx.test.runner.AndroidJUnitRunner";
    private const string TestMethod = "org.swipewalk.harness.HarnessTest#capture";
    private const string ScreenReaderTestMethod = "org.swipewalk.harness.HarnessTest#captureScreenReader";
    private const string RestoreScreenReaderSettingsTestMethod = "org.swipewalk.harness.HarnessTest#restoreScreenReaderSettings";

    /// <summary>File the harness writes into its own external files directory (see harness/android's
    /// HarnessTest.kt); no root or extra permission needed to read another app's own external files dir
    /// with adb on a development device/emulator.</summary>
    private static string ResultDevicePath(string testPackage) => $"/sdcard/Android/data/{testPackage}/files/harness-result.json";

    /// <summary>File <see cref="RunScreenReaderCaptureAsync"/>'s instrumentation test writes its result to
    /// (see harness/android's HarnessTest.kt/TalkBackCollector.kt).</summary>
    private static string ScreenReaderResultDevicePath(string testPackage) => $"/sdcard/Android/data/{testPackage}/files/screen-reader-result.json";

    /// <summary>The marker file harness/android's <c>TalkBackCollector.kt</c> leaves behind while it has
    /// accessibility settings changed, so a leftover from a crashed run can be detected without running any
    /// instrumentation (see <see cref="HasLeftoverScreenReaderSettingsAsync"/>) -- filename matches
    /// <c>TalkBackCollector.RestoreMarkerFile</c>.</summary>
    private static string ScreenReaderRestoreMarkerDevicePath(string testPackage) => $"/sdcard/Android/data/{testPackage}/files/screen-reader-restore-state.json";

    /// <summary>
    /// Cheap check (one adb round trip, no instrumentation) for whether a screen-reader capture left
    /// accessibility settings changed after crashing or being killed -- see
    /// <see cref="RestoreLeftoverScreenReaderSettingsAsync"/>, which actually repairs it, and
    /// <see cref="Preflight.Preflight.AndroidAsync"/>, which uses this to decide whether that's worth doing
    /// (mirrors the iOS large-text <c>TextSizeRestore.Pending</c> check). False (nothing to report) when the
    /// harness test package was never installed, same as when it's installed but has no marker. Only a
    /// confirmed "No such file" from <c>ls</c> counts as false: any other adb failure (device disconnected,
    /// unauthorized, offline mid-command, ...) means this genuinely doesn't know the marker's state, and
    /// treats that the same as "still there" -- callers use false here to decide it's safe to forget the
    /// host record and remove the privileged helper app (see <see cref="UninstallTtsEngineAsync"/>'s
    /// remarks), so an uncertain answer must never read as false.
    /// </summary>
    internal static async Task<bool> HasLeftoverScreenReaderSettingsAsync(Adb adb)
    {
        try
        {
            await adb.RunAsync("shell", "ls", ScreenReaderRestoreMarkerDevicePath(TestPackage));
            return true;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No such file", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            // Some other adb failure -- unknown, not confirmed clean; see this method's remarks.
            return true;
        }
    }

    public const string HarnessRelativePath = "harness/android";
    private const string ApkRelativePath = "harness/build/outputs/apk/androidTest/debug/harness-debug-androidTest.apk";

    /// <summary>File name of the prebuilt APK shipped next to the assemblies (dotnet tool) or in the
    /// desktop app's Resources bundle -- see Swipewalk.Collectors.csproj/Swipewalk.Desktop's csproj, both
    /// built by CI (.github/workflows/ci.yml, release.yml) before packing/publishing, the same way
    /// harness/ios's source ships for iOS. When present, a scan never needs a JDK, Gradle or network
    /// access -- only <c>adb</c>. See <see cref="FindBundledApk()"/> and KnownLimitations "android-atf-harness".</summary>
    internal const string BundledApkFileName = "harness-debug-androidTest.apk";

    /// <summary>Package the standalone TTS-engine app (harness/android/ttsengine) installs as -- a real
    /// app, not an androidTest APK; see <c>TalkBackCollector.kt</c>'s remarks for why a TTS engine has to
    /// be one. Only installed/used for a screen-reader capture (<c>--screen-reader</c>).</summary>
    public const string TtsEnginePackage = "org.swipewalk.harness.ttsengine";

    private const string TtsEngineApkRelativePath = "ttsengine/build/outputs/apk/debug/ttsengine-debug.apk";

    /// <summary>File name of the prebuilt TTS-engine APK shipped alongside <see cref="BundledApkFileName"/>
    /// -- see <see cref="FindBundledTtsEngineApk()"/>.</summary>
    internal const string BundledTtsEngineApkFileName = "ttsengine-debug.apk";

    /// <summary>Never auto-granted; needed so harness/android/ttsengine's <c>SafetyTimerReceiver</c> can
    /// restore accessibility settings with a plain <c>Settings.Secure.putString</c> call entirely
    /// on-device, with no adb/host connection needed, if a capture is interrupted before it restores them
    /// itself -- see that class's remarks. Granting it is idempotent (a no-op if already granted).</summary>
    private const string WriteSecureSettingsPermission = "android.permission.WRITE_SECURE_SETTINGS";

    /// <summary>
    /// Whether the harness is installed and confirmed current this process, cached after the first
    /// successful check/install (per cache key: <paramref name="harnessDir"/>, or a fixed key for the
    /// auto-detected/bundled path) so every capture in a scan or recording doesn't repeat a version check
    /// or retry a slow Gradle build once it's known to work or fail -- record mode calls this once per
    /// screen. Cleared only by process restart; a fixed problem (e.g. installing a JDK) takes effect on
    /// the next `swipewalk` run.
    /// </summary>
    private static readonly Dictionary<string, bool> InstallAttempted = new(StringComparer.Ordinal);

    private const string AutoCacheKey = "<auto>";

    /// <summary>Everything the harness produced for one capture, or why it didn't run. Bounds in
    /// <see cref="AtfIssues"/> are already converted to dp using the capture's density.</summary>
    public sealed record HarnessResult(
        IReadOnlyDictionary<string, NodeExtras> Nodes,
        IReadOnlyList<AtfIssue> AtfIssues,
        string? SkipReason)
    {
        public static readonly HarnessResult Empty = new(new Dictionary<string, NodeExtras>(), [], null);
        public static HarnessResult Skipped(string reason) => new(new Dictionary<string, NodeExtras>(), [], reason);
    }

    /// <summary>Extra per-node properties the harness reads that <c>uiautomator dump</c> does not (see
    /// AccessibilityNode.cs). Keyed by <see cref="UiAutomatorParser"/>'s node identity so
    /// <see cref="UiAutomatorParser.Parse"/> can attach them while building the tree.</summary>
    /// <param name="HintText">From <c>AccessibilityNodeInfo#getHintText()</c> (API 26): the field's hint,
    /// independent of <see cref="IsShowingHintText"/> -- present even while a value is showing, and on a
    /// device/version whose <c>uiautomator dump</c> omits the <c>hint</c> attribute even though the field
    /// has one (see KnownLimitations "android-edittext-text"). <see cref="UiAutomatorParser.Parse"/> uses
    /// this to fill in <see cref="AccessibilityNode.Hint"/> when the dump's own attribute is empty.</param>
    public sealed record NodeExtras(bool? IsShowingHintText, string? HintText, bool? IsHeading, string? PaneTitle, string? StateDescription, bool? IsImportantForAccessibility);

    /// <summary>One item the Android TalkBack capture read back, before it's matched to a node path (see
    /// <see cref="AndroidCollector.Load"/>, which uses <see cref="UiAutomatorParser.KeyPathsByPackage"/> for
    /// that -- this type only carries what the harness itself produced).</summary>
    /// <param name="Key">Same node-identity scheme as <see cref="NodeExtras"/>'s (class, raw-pixel bounds,
    /// resource id, text, content description), computed by harness/android's <c>TalkBackCollector.kt</c>.</param>
    public sealed record ScreenReaderHarnessItem(int Order, string SpokenText, string Key);

    /// <summary>Everything a TalkBack capture produced for one screen, or why it didn't run/finish -- see
    /// harness/android's <c>TalkBackCollector.kt</c> for how each field is decided. Never thrown from
    /// <see cref="RunScreenReaderCaptureAsync"/>: a failed or skipped capture is never a reason to fail the
    /// scan (see KnownLimitations, and this method's own remarks).</summary>
    /// <param name="MissingCount">How many walked elements TalkBack said nothing for at all within the
    /// harness's per-element timeout -- kept apart from <see cref="Items"/> (which requires real spoken
    /// text) so a report can say "TalkBack was silent for N elements" rather than just omitting them
    /// invisibly.</param>
    /// <param name="Language">The device's system language (BCP-47, e.g. "es-ES") when the capture ran --
    /// see <see cref="Swipewalk.Core.Model.ScreenReaderCapture.Language"/> for why the comparator needs
    /// this. Null when the capture didn't run far enough to read it (see <see cref="SkipReason"/>).</param>
    public sealed record ScreenReaderHarnessResult(
        string? ToolVersion, IReadOnlyList<ScreenReaderHarnessItem> Items, int MissingCount, bool Complete, string? NotCompleteReason, string? SkipReason, string? Language = null)
    {
        public static ScreenReaderHarnessResult Skipped(string reason) => new(null, [], 0, false, null, reason);
    }

    /// <summary>
    /// Runs the Android TalkBack screen-reader capture (harness/android's <c>TalkBackCollector.kt</c>)
    /// against <paramref name="package"/>, which must already be the foreground app: enables TalkBack,
    /// points `tts_default_synth` at harness/android/ttsengine's TTS engine (a real installed app, not
    /// part of this androidTest APK -- see that file's remarks for why), walks every focusable/interactive
    /// element performing accessibility focus, reads back what TalkBack actually said from that engine's
    /// logcat output, then restores every accessibility setting it changed. Costs roughly 1-2 seconds per
    /// element, so this is only called when screen-reader capture was explicitly asked for
    /// (<c>--screen-reader</c>), never on an ordinary scan. Never throws: a harness problem (TalkBack not
    /// installed, the TTS engine not routing, a build/install failure) comes back as
    /// <see cref="ScreenReaderHarnessResult.SkipReason"/>, same contract as <see cref="RunAsync"/> for the
    /// ATF harness. Because a person may be relying on TalkBack for real while this runs, every setting
    /// change is protected on three independent paths -- see `TalkBackCollector.kt`'s remarks and
    /// <see cref="ScreenReaderSettingsRestore"/> for the fourth, host-side copy pre-flight uses.
    /// </summary>
    internal static async Task<ScreenReaderHarnessResult> RunScreenReaderCaptureAsync(Adb adb, string package, string? harnessDir = null)
    {
        var serial = await adb.ResolveSerialAsync();
        try
        {
            await EnsureInstalledAsync(adb, harnessDir);
            await EnsureTtsEngineInstalledAsync(adb, harnessDir);
            // Repair a leftover from an earlier run that crashed before its own restore ran, before this
            // one takes its own "what was the device like before" snapshot (see TalkBackCollector.kt's
            // remarks and RestoreLeftoverScreenReaderSettingsAsync, which pre-flight also calls).
            // uninstallEngineAfterward: false -- the capture that follows needs the engine installed again
            // immediately, so uninstalling here would just be undone a few lines down.
            await RestoreLeftoverScreenReaderSettingsAsync(adb, harnessDir, uninstallEngineAfterward: false);

            // Host-side copy of "what to put back", written BEFORE anything changes on the device -- not
            // after a successful round trip, which would miss exactly the case this exists for (this
            // process dying partway through). Swipewalk.Collectors.Preflight.Preflight.AndroidAsync reads
            // this back directly, with no harness/instrumentation needed, if the device-side marker
            // TalkBackCollector.kt writes is ever lost. Read straight from the device rather than trusting
            // any earlier snapshot, since RestoreLeftoverScreenReaderSettingsAsync just ran above.
            var original = new ScreenReaderSettingsRestore.Snapshot(
                EnabledServices: await ReadSecureSettingAsync(adb, "enabled_accessibility_services"),
                AccessibilityEnabled: await ReadSecureSettingAsync(adb, "accessibility_enabled"),
                TouchExploration: await ReadSecureSettingAsync(adb, "touch_exploration_enabled"),
                TtsDefaultSynth: await ReadSecureSettingAsync(adb, "tts_default_synth"));
            ScreenReaderSettingsRestore.Remember(serial, original);

            var output = await adb.RunAsync(
                "shell", "am", "instrument", "-w", "-e", "class", ScreenReaderTestMethod, "-e", "package", package, InstrumentationRunner);
            // `am instrument` returning at all does NOT by itself mean the capture's own `finally` block
            // ran: an ordinary test failure inside it still runs `finally` (which deletes the device-side
            // marker only once every setting reads back as restored -- see TalkBackCollector.kt's
            // remarks), but the instrumentation process being killed or crashing outright can return here
            // too, WITHOUT `finally` ever
            // running -- in which case the marker is still there, TalkBack is still on, and the settings
            // are still changed. Only the marker's absence is trusted as "restore succeeded"; check it
            // fresh rather than assuming the line above proves it, since it's the same check the safety net
            // itself (RestoreLeftoverScreenReaderSettingsAsync/doctor) uses to decide whether it has work
            // to do -- both must agree on the same evidence.
            if (!await HasLeftoverScreenReaderSettingsAsync(adb))
            {
                // Genuinely restored: safe to forget the host-side copy and remove the app that held the
                // privileged permission -- see UninstallTtsEngineAsync's remarks. Nothing privileged stays
                // installed between captures; the next one (or record's next screen) reinstalls it.
                ScreenReaderSettingsRestore.Forget(serial);
                await UninstallTtsEngineAsync(adb);
            }
            // Otherwise: leave the host record and the app in place. The marker, the host record and the
            // still-armed on-device safety timer are exactly what the next --screen-reader run's own
            // leftover repair, `doctor`, or (if nothing else runs first) the timer itself will use to put
            // the device back -- uninstalling now would remove the timer along with them.
            if (!output.Contains("OK (1 test)", StringComparison.Ordinal))
                return ScreenReaderHarnessResult.Skipped($"the Android screen-reader capture failed: {SummarizeFailure(output)}");
            var json = await adb.RunAsync("shell", "cat", ScreenReaderResultDevicePath(TestPackage));
            return ParseScreenReaderResult(json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Deliberately NOT forgetting the host-side record here: an exception this early could mean
            // the `am instrument` call itself never got to run (and never changed anything, so there's
            // nothing to restore) or that it ran and changed settings before failing -- either way, the
            // safer default is to leave the record for pre-flight to check, which is a harmless no-op if
            // the device turns out to already match it.
            return ScreenReaderHarnessResult.Skipped($"the Android screen-reader capture did not run: {ex.Message}");
        }
    }

    private static async Task<string> ReadSecureSettingAsync(Adb adb, string key)
    {
        var value = (await adb.RunAsync("shell", "settings", "get", "secure", key)).Trim();
        return value is "null" or "" ? ScreenReaderSettingsRestore.Snapshot.Null : value;
    }

    /// <summary>
    /// Best-effort repair for a screen-reader capture that crashed or was killed before it restored the
    /// accessibility settings it changed (mirrors the iOS large-text <c>TextSizeRestore</c> safeguard,
    /// checked the same way in pre-flight -- see <see cref="Swipewalk.Collectors.Preflight.Preflight.AndroidAsync"/>). A no-op,
    /// successfully, when nothing is pending; never throws.
    /// </summary>
    /// <param name="uninstallEngineAfterward">Whether to remove harness/android/ttsengine (see
    /// <see cref="UninstallTtsEngineAsync"/>) once the marker this repair deletes is confirmed gone (real
    /// evidence of success, not just that the instrument call returned). False from
    /// <see cref="RunScreenReaderCaptureAsync"/>'s own pre-capture repair (the engine is needed again a few
    /// lines later for the capture that follows); true (the default) for a standalone call such as
    /// pre-flight/`doctor`'s, where nothing else is about to reinstall it.</param>
    internal static async Task RestoreLeftoverScreenReaderSettingsAsync(Adb adb, string? harnessDir = null, bool uninstallEngineAfterward = true)
    {
        try
        {
            await EnsureInstalledAsync(adb, harnessDir);
            await EnsureTtsEngineInstalledAsync(adb, harnessDir);
            await adb.RunAsync("shell", "am", "instrument", "-w", "-e", "class", RestoreScreenReaderSettingsTestMethod, InstrumentationRunner);
            // Only trust that the repair actually ran if the marker it deletes only once every setting
            // reads back as restored is now gone (see RunScreenReaderCaptureAsync's remarks on why the
            // instrument call returning isn't enough by itself) -- otherwise leave the app installed for
            // the next attempt.
            if (uninstallEngineAfterward && !await HasLeftoverScreenReaderSettingsAsync(adb))
                await UninstallTtsEngineAsync(adb);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best effort only: nothing more useful to do here than leave it for the next attempt.
        }
    }

    /// <summary>Parses harness/android's screen-reader JSON (see <c>TalkBackCollector.toJson</c>).</summary>
    internal static ScreenReaderHarnessResult ParseScreenReaderResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var toolVersion = GetNullableString(root, "talkBackVersion");
        var complete = root.TryGetProperty("complete", out var c) && c.GetBoolean();
        var missingCount = root.TryGetProperty("missingCount", out var m) ? m.GetInt32() : 0;
        var notCompleteReason = GetNullableString(root, "notCompleteReason");
        var language = GetNullableString(root, "language");
        var items = new List<ScreenReaderHarnessItem>();
        if (root.TryGetProperty("items", out var itemsElement))
        {
            foreach (var item in itemsElement.EnumerateArray())
            {
                var order = item.GetProperty("order").GetInt32();
                var spokenText = item.GetProperty("spokenText").GetString() ?? "";
                var key = item.GetProperty("key").GetString() ?? "";
                items.Add(new ScreenReaderHarnessItem(order, spokenText, key));
            }
        }
        return new ScreenReaderHarnessResult(toolVersion, items, missingCount, complete, notCompleteReason, SkipReason: null, language);
    }

    /// <summary>
    /// Runs the harness against <paramref name="package"/> and returns its result, or a
    /// <see cref="HarnessResult.SkipReason"/> if the harness could not be built, installed or run -- this
    /// method never throws, so a broken harness never fails a scan (see KnownLimitations
    /// "android-atf-harness"). <paramref name="density"/> converts the harness's raw-pixel bounds to dp,
    /// matching every other Android bounds in the model.
    /// </summary>
    internal static async Task<HarnessResult> RunAsync(Adb adb, string package, int density, string? harnessDir = null)
    {
        try
        {
            await EnsureInstalledAsync(adb, harnessDir);
            var output = await adb.RunAsync(
                "shell", "am", "instrument", "-w", "-e", "class", TestMethod, "-e", "package", package, InstrumentationRunner);
            if (!output.Contains("OK (1 test)", StringComparison.Ordinal))
                return HarnessResult.Skipped($"the Android accessibility harness failed: {SummarizeFailure(output)}");
            // Only now, not right after install: the harness's external files directory (where both the
            // version marker and harness-result.json live) doesn't exist until the app has actually run
            // once and called getExternalFilesDir() itself -- writing the marker straight after `adb
            // install` failed silently (parent directory didn't exist yet) until this was moved here.
            await WriteVersionMarkerAsync(adb, CurrentToolVersion);
            var json = await adb.RunAsync("shell", "cat", ResultDevicePath(TestPackage));
            return Parse(json, density);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never fail a scan because of the harness (missing JDK, a build error, a device policy
            // blocking install, an unreadable result file, ...): see KnownLimitations "android-atf-harness".
            return HarnessResult.Skipped($"the Android accessibility harness did not run: {ex.Message}");
        }
    }

    /// <summary>
    /// A short, useful reason from `am instrument` output on a non-"OK (1 test)" run: JUnit's on-device
    /// runner prints "Error in &lt;test&gt;:" or "Failure in &lt;test&gt;:" followed by the exception's own
    /// class and message on the next line(s) -- the first line alone (as instrumentation output always also
    /// contains the word "Error" in unrelated INSTRUMENTATION_STATUS keys) is not useful by itself.
    /// </summary>
    internal static string SummarizeFailure(string output)
    {
        var lines = output.Split('\n');
        var headerIndex = Array.FindIndex(lines, l => l.Contains("Error in ", StringComparison.Ordinal) || l.Contains("Failure in ", StringComparison.Ordinal));
        if (headerIndex < 0)
            return output.Trim() is { Length: > 0 } trimmed ? trimmed : "no output from adb shell am instrument";
        var detail = lines.Skip(headerIndex).Take(3).Select(l => l.Trim()).Where(l => l.Length > 0);
        return string.Join(" ", detail);
    }

    /// <summary>
    /// Ensures the harness's androidTest APK is installed and current, preferring a prebuilt APK shipped
    /// with Swipewalk (<see cref="FindBundledApk()"/> -- no JDK, Gradle or network needed) over building
    /// from source, which is now only the fallback for a repo checkout with no bundled APK, or an explicit
    /// <paramref name="harnessDir"/> (<c>--android-harness</c>), which always builds from that source even
    /// when a bundled APK exists -- the point of naming a source directory is to test it.
    /// </summary>
    /// <remarks>
    /// A device can keep an old harness install across Swipewalk updates (a device isn't reset between
    /// scans), so an install is also considered stale, and reinstalled, when the on-device version marker
    /// (<see cref="TryReadVersionMarkerAsync"/>) doesn't match this build's own assembly version
    /// (<see cref="CurrentToolVersion"/>) -- see KnownLimitations "android-atf-harness". Cached per process
    /// after the first check (see <see cref="InstallAttempted"/>); throws <see cref="InvalidOperationException"/>
    /// on any failure, which <see cref="RunAsync"/> turns into a skip reason.
    /// </remarks>
    private static async Task EnsureInstalledAsync(Adb adb, string? harnessDir)
    {
        var cacheKey = harnessDir ?? AutoCacheKey;
        if (InstallAttempted.TryGetValue(cacheKey, out var previouslyOk))
        {
            if (!previouslyOk)
                throw new InvalidOperationException("the harness could not be installed earlier in this run; not retrying");
            return;
        }

        var currentVersion = CurrentToolVersion;
        var installed = (await adb.RunAsync("shell", "pm", "list", "packages", TestPackage)).Contains($"package:{TestPackage}", StringComparison.Ordinal);
        if (installed && await TryReadVersionMarkerAsync(adb) == currentVersion)
        {
            InstallAttempted[cacheKey] = true;
            return;
        }

        try
        {
            var apk = harnessDir is not null
                ? await BuildFromSourceAsync(harnessDir)
                : FindBundledApk()
                    ?? await BuildFromSourceAsync(FindHarnessDir()
                        ?? throw new InvalidOperationException($"No bundled harness APK and could not find {HarnessRelativePath}; pass --android-harness <path>."));
            await adb.RunAsync("install", "-r", "-t", apk);
            // The version marker is written after the first successful `am instrument` run instead of
            // here (see RunAsync): the harness's external files directory doesn't exist until the app has
            // actually run once and called getExternalFilesDir() itself, so writing it immediately after
            // install would silently fail (no parent directory yet).
            InstallAttempted[cacheKey] = true;
        }
        catch
        {
            InstallAttempted[cacheKey] = false;
            throw;
        }
    }

    /// <summary>Builds the androidTest APK from harness/android source if there's no prebuilt one there
    /// already (a prior build in this checkout), and returns its path.</summary>
    private static async Task<string> BuildFromSourceAsync(string harnessDir)
    {
        var apk = Path.Combine(harnessDir, ApkRelativePath);
        if (!File.Exists(apk))
            await BuildAsync(harnessDir);
        if (!File.Exists(apk))
            throw new InvalidOperationException($"Gradle did not produce {ApkRelativePath}.");
        return apk;
    }

    /// <summary>
    /// Ensures harness/android/ttsengine is installed and that it holds <see cref="WriteSecureSettingsPermission"/>
    /// (granting it is idempotent, so this is safe to call every time rather than tracking it separately).
    /// Same prebuilt-preferred, cached-per-process shape as <see cref="EnsureInstalledAsync"/>, under its
    /// own cache key so the two APKs' install state is never confused.
    /// </summary>
    private static async Task EnsureTtsEngineInstalledAsync(Adb adb, string? harnessDir)
    {
        var cacheKey = "ttsengine:" + (harnessDir ?? AutoCacheKey);
        if (InstallAttempted.TryGetValue(cacheKey, out var previouslyOk))
        {
            if (!previouslyOk)
                throw new InvalidOperationException("the TTS-engine app could not be installed earlier in this run; not retrying");
            return;
        }

        try
        {
            var installed = (await adb.RunAsync("shell", "pm", "list", "packages", TtsEnginePackage)).Contains($"package:{TtsEnginePackage}", StringComparison.Ordinal);
            if (!installed)
            {
                var apk = harnessDir is not null
                    ? await BuildTtsEngineFromSourceAsync(harnessDir)
                    : FindBundledTtsEngineApk()
                        ?? await BuildTtsEngineFromSourceAsync(FindHarnessDir()
                            ?? throw new InvalidOperationException($"No bundled TTS-engine APK and could not find {HarnessRelativePath}; pass --android-harness <path>."));
                await adb.RunAsync("install", "-r", "-t", apk);
            }
            // Idempotent: granting an already-granted permission, or an app op that's already allowed, is
            // a no-op, so these run every time rather than being cached -- cheap (one adb round trip
            // each) and self-healing if any was ever somehow lost (e.g. the app was reinstalled, which
            // resets granted permissions and app ops).
            await adb.RunAsync("shell", "pm", "grant", TtsEnginePackage, WriteSecureSettingsPermission);
            // The safety timer's exact alarm (see SafetyTimerReceiver.kt's remarks) needs this app op
            // allowed -- SCHEDULE_EXACT_ALARM isn't grantable with `pm grant` like a normal permission.
            await adb.RunAsync("shell", "appops", "set", TtsEnginePackage, "SCHEDULE_EXACT_ALARM", "allow");
            // A freshly-installed app with no launcher activity (nothing the system ever sees "opened")
            // lands in Android's most-restricted App Standby Bucket ("never"), which measurably delayed
            // even an inexact, deviceidle-whitelisted alarm by minutes on a Pixel 4a -- forcing it
            // "active" here is belt-and-braces alongside the exact alarm switch above.
            await adb.RunAsync("shell", "am", "set-standby-bucket", TtsEnginePackage, "active");
            await adb.RunAsync("shell", "dumpsys", "deviceidle", "whitelist", $"+{TtsEnginePackage}");
            InstallAttempted[cacheKey] = true;
        }
        catch
        {
            InstallAttempted[cacheKey] = false;
            throw;
        }
    }

    /// <summary>
    /// Removes harness/android/ttsengine and, with it, the <see cref="WriteSecureSettingsPermission"/>
    /// grant -- uninstalling an app is expected to also clear the app-op/allowlist changes
    /// <see cref="EnsureTtsEngineInstalledAsync"/> made, though that wasn't checked entry by entry.
    /// Nothing privileged is left on the device once a capture's own restore is known to have
    /// succeeded. Called
    /// only from a point where that's true (see call sites' remarks); NOT called when a restore failed or
    /// is still pending, since the on-device safety timer that can restore settings on its own, and
    /// `doctor`'s own leftover-repair path, both live inside this app -- see
    /// <see cref="ScreenReaderSettingsRestore"/> and KnownLimitations "android-screen-reader-capture". Best
    /// effort and never throws: a failed uninstall just leaves the app for the next run to find already
    /// there (harmless -- <see cref="EnsureTtsEngineInstalledAsync"/> treats that as nothing to do). Clears
    /// the install-attempted cache for every cache key naming this app -- not just the one for whichever
    /// harness directory (or the auto-detected default) is in play right now -- so a later call in the
    /// same process, under a different cache key, doesn't wrongly assume it's still installed.
    /// </summary>
    internal static async Task UninstallTtsEngineAsync(Adb adb)
    {
        try
        {
            await adb.RunAsync("uninstall", TtsEnginePackage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best effort only -- see this method's remarks.
        }
        foreach (var key in InstallAttempted.Keys.Where(k => k.StartsWith("ttsengine:", StringComparison.Ordinal)).ToList())
            InstallAttempted.Remove(key);
    }

    private static async Task<string> BuildTtsEngineFromSourceAsync(string harnessDir)
    {
        var apk = Path.Combine(harnessDir, TtsEngineApkRelativePath);
        if (!File.Exists(apk))
            await BuildAsync(harnessDir, ":ttsengine:assembleDebug");
        if (!File.Exists(apk))
            throw new InvalidOperationException($"Gradle did not produce {TtsEngineApkRelativePath}.");
        return apk;
    }

    /// <summary>Prebuilt TTS-engine APK shipped next to <see cref="BundledApkFileName"/> -- see that
    /// method's own remarks; same search locations.</summary>
    internal static string? FindBundledTtsEngineApk() => FindBundledTtsEngineApk(AppContext.BaseDirectory);

    internal static string? FindBundledTtsEngineApk(string baseDirectory)
    {
        foreach (var root in new[] { baseDirectory, Path.Combine(baseDirectory, "..", "Resources") })
        {
            var candidate = Path.GetFullPath(Path.Combine(root, HarnessRelativePath, BundledTtsEngineApkFileName));
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>
    /// This build's own version, used to tell a current harness install from a stale one left by an
    /// earlier Swipewalk version on a device that's reused across scans (see
    /// <see cref="EnsureInstalledAsync"/>). Not the installed APK's own Android version -- the shipped APK
    /// has none set, and this only needs to change whenever Swipewalk itself is rebuilt, which the
    /// assembly's own version (from Directory.Build.props's &lt;Version&gt;) already does automatically.
    /// </summary>
    internal static string CurrentToolVersion => typeof(AndroidHarness).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

    private static string VersionMarkerDevicePath(string testPackage) => $"/sdcard/Android/data/{testPackage}/files/harness-version.txt";

    /// <summary>Null when the marker file doesn't exist (a harness installed before this existed) or
    /// couldn't be read, the same as a version that won't match <see cref="CurrentToolVersion"/> --
    /// either way, treated as stale.</summary>
    private static async Task<string?> TryReadVersionMarkerAsync(Adb adb)
    {
        try { return (await adb.RunAsync("shell", "cat", VersionMarkerDevicePath(TestPackage))).Trim(); }
        catch (InvalidOperationException) { return null; }
    }

    /// <summary>Best effort: if writing the marker fails, the next capture in this process (see
    /// <see cref="InstallAttempted"/>) already treats the harness as installed and skips this entirely, so
    /// the only cost is that a later Swipewalk run re-checks (and likely reinstalls) instead of trusting a
    /// cached "current" state -- never a reason to fail the capture that just installed the harness.</summary>
    private static async Task WriteVersionMarkerAsync(Adb adb, string version)
    {
        try { await adb.RunAsync("shell", "echo", "-n", version, ">", VersionMarkerDevicePath(TestPackage)); }
        catch (InvalidOperationException) { }
    }

    /// <summary>
    /// Prebuilt APK shipped next to the assemblies (dotnet tool: <see cref="AppContext.BaseDirectory"/>) or
    /// in the desktop app's bundle (Contents/Resources, alongside how harness/ios ships -- see
    /// <c>Swipewalk.Collectors.Ios.IosCollector.FindHarness</c>). Null in a from-source checkout that
    /// hasn't built one (see <see cref="BuildFromSourceAsync"/>).
    /// </summary>
    internal static string? FindBundledApk() => FindBundledApk(AppContext.BaseDirectory);

    internal static string? FindBundledApk(string baseDirectory)
    {
        foreach (var root in new[] { baseDirectory, Path.Combine(baseDirectory, "..", "Resources") })
        {
            var candidate = Path.GetFullPath(Path.Combine(root, HarnessRelativePath, BundledApkFileName));
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>Builds the androidTest APK with the Gradle wrapper committed in harness/android.</summary>
    private static Task BuildAsync(string harnessDir) => BuildAsync(harnessDir, ":harness:assembleDebugAndroidTest");

    private static async Task BuildAsync(string harnessDir, string gradleTask)
    {
        var gradlew = Path.Combine(harnessDir, OperatingSystem.IsWindows() ? "gradlew.bat" : "gradlew");
        if (!File.Exists(gradlew))
            throw new InvalidOperationException($"No Gradle wrapper at {gradlew}.");
        var info = new ProcessStartInfo(gradlew) { WorkingDirectory = harnessDir, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(gradleTask);
        info.ArgumentList.Add("--console=plain");
        if (FindJavaHome() is { } javaHome)
            info.Environment["JAVA_HOME"] = javaHome;
        if (FindAndroidSdk() is { } sdk)
            info.Environment["ANDROID_HOME"] = sdk;
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {gradlew}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Building the Android harness failed (gradle exit {process.ExitCode}): {(await stderr).Trim()}");
    }

    /// <summary>Same search order as <c>Swipewalk.Collectors.Ios.IosCollector.FindHarness</c> for
    /// harness/ios: walk up from the current directory looking for harness/android/gradlew, so this works
    /// both from a checkout and (once the harness is bundled the same way the iOS one is) from an installed
    /// copy.</summary>
    internal static string? FindHarnessDir() => FindHarnessDir(Directory.GetCurrentDirectory(), AppContext.BaseDirectory);

    internal static string? FindHarnessDir(string currentDirectory, string baseDirectory)
    {
        for (var dir = new DirectoryInfo(currentDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, HarnessRelativePath);
            if (File.Exists(Path.Combine(candidate, "gradlew")))
                return candidate;
        }
        var bundled = Path.GetFullPath(Path.Combine(baseDirectory, HarnessRelativePath));
        return File.Exists(Path.Combine(bundled, "gradlew")) ? bundled : null;
    }

    /// <summary>
    /// JDK to build with, preferring one already on the machine over asking the owner to install another:
    /// JAVA_HOME if set, then Android Studio's bundled JBR (present on any Mac with Android Studio
    /// installed), then null -- the Gradle wrapper script falls back to a `java` already on PATH in that
    /// case, which is enough on a machine with just a JDK and no Android Studio.
    /// </summary>
    internal static string? FindJavaHome()
    {
        var envHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (envHome is { Length: > 0 } && Directory.Exists(envHome))
            return envHome;
        var studioJbr = "/Applications/Android Studio.app/Contents/jbr/Contents/Home";
        return OperatingSystem.IsMacOS() && Directory.Exists(studioJbr) ? studioJbr : null;
    }

    private static string? FindAndroidSdk() => Adb.SdkRoots().FirstOrDefault();

    /// <summary>Parses the harness's JSON (see harness/android's HarnessTest.kt/AtfCollector.kt) into a
    /// <see cref="HarnessResult"/>, converting ATF issue bounds from raw pixels to dp.</summary>
    internal static HarnessResult Parse(string json, int density)
    {
        using var doc = JsonDocument.Parse(json);
        var scale = density / 160.0;

        var nodes = new Dictionary<string, NodeExtras>(StringComparer.Ordinal);
        if (doc.RootElement.TryGetProperty("nodes", out var nodesElement))
        {
            foreach (var node in nodesElement.EnumerateArray())
            {
                var key = node.GetProperty("key").GetString();
                if (key is null)
                    continue;
                nodes[key] = new NodeExtras(
                    GetNullableBool(node, "isShowingHintText"),
                    GetNullableString(node, "hintText"),
                    GetNullableBool(node, "isHeading"),
                    GetNullableString(node, "paneTitle"),
                    GetNullableString(node, "stateDescription"),
                    GetNullableBool(node, "isImportantForAccessibility"));
            }
        }

        var issues = new List<AtfIssue>();
        if (doc.RootElement.TryGetProperty("atfIssues", out var issuesElement))
        {
            foreach (var issue in issuesElement.EnumerateArray())
            {
                var checkName = issue.GetProperty("checkName").GetString() ?? "unknown";
                // ATF's own message strings can contain literal <tt>...</tt> markup meant for a UI that
                // renders it; Swipewalk's report is plain text, so strip the tags rather than show them escaped.
                var message = (GetNullableString(issue, "message") ?? "").Replace("<tt>", "").Replace("</tt>", "");
                var bounds = GetNullableString(issue, "bounds") is { } b ? ParseBounds(b, scale) : default;
                var label = GetNullableString(issue, "text") ?? GetNullableString(issue, "contentDescription")
                    ?? ShortResourceId(GetNullableString(issue, "resourceId"));
                issues.Add(new AtfIssue(checkName, message, NodePath: null, label, bounds));
            }
        }

        return new HarnessResult(nodes, issues, SkipReason: null);
    }

    /// <summary>"com.app:id/foo" → "foo", matching UiAutomatorParser's own resource-id shortening.</summary>
    private static string? ShortResourceId(string? resourceId)
    {
        if (resourceId is null)
            return null;
        var i = resourceId.IndexOf(":id/", StringComparison.Ordinal);
        return i >= 0 ? resourceId[(i + 4)..] : resourceId;
    }

    private static bool? GetNullableBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetBoolean() : null;

    private static string? GetNullableString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static Bounds ParseBounds(string value, double scale)
    {
        var match = BoundsPattern().Match(value);
        if (!match.Success)
            return default;
        var v = Enumerable.Range(1, 4).Select(i => double.Parse(match.Groups[i].Value)).ToArray();
        return new Bounds(v[0] / scale, v[1] / scale, (v[2] - v[0]) / scale, (v[3] - v[1]) / scale);
    }

    [GeneratedRegex(@"\[(-?\d+),(-?\d+)\]\[(-?\d+),(-?\d+)\]")]
    private static partial Regex BoundsPattern();
}
