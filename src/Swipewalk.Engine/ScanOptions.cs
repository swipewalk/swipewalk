using Swipewalk.Core.Model;

namespace Swipewalk.Engine;

public enum TargetPlatform
{
    Android,
    Ios,
}

/// <summary>Everything a scan or recording needs. Built from command-line options, swipewalk.json, or the desktop app.</summary>
public sealed record ScanOptions
{
    public required TargetPlatform Platform { get; init; }

    /// <summary>adb serial or iOS UDID; null for the only connected Android device / the booted Simulator.</summary>
    public string? Device { get; init; }

    /// <summary>Android package of the app under test.</summary>
    public string? Package { get; init; }

    /// <summary>iOS bundle id of the app under test.</summary>
    public string? BundleId { get; init; }

    /// <summary>Already-built app to install first (.apk, Android App Bundle .aab, Simulator .app/.zip, device .ipa).</summary>
    public string? InstallFile { get; init; }

    /// <summary>
    /// Android only: path to bundletool (a .jar, or an executable such as Homebrew's wrapper), used to install
    /// an <see cref="InstallFile"/> ending in .aab. Found automatically when not given -- on PATH, then bundled
    /// with the installed .NET Android SDK workload -- see <see cref="Swipewalk.Collectors.Android.Bundletool"/>.
    /// Ignored for a .apk/.ipa/.app install.
    /// </summary>
    public string? BundletoolPath { get; init; }

    /// <summary>
    /// Android only: keystore to sign a .aab's device-specific .apks with, instead of the standard Android
    /// debug key (~/.android/debug.keystore, created by Swipewalk with keytool if it doesn't already exist) -- see
    /// <see cref="Swipewalk.Collectors.AppInstaller.AndroidBundleSigning"/>. Needs
    /// <see cref="AndroidKeystoreAlias"/> too, and the SWIPEWALK_KEYSTORE_PASSWORD environment variable
    /// (SWIPEWALK_KEY_PASSWORD as well if the key's own password differs) -- a keystore password never goes on
    /// the command line or in swipewalk.json. Ignored unless <see cref="InstallFile"/> ends in .aab.
    /// </summary>
    public string? AndroidKeystore { get; init; }

    /// <summary>Key alias inside <see cref="AndroidKeystore"/>; required together with it.</summary>
    public string? AndroidKeystoreAlias { get; init; }

    public AppFramework? Framework { get; init; }

    /// <summary>
    /// Framework version, when known (for example from detecting .NET MAUI in a physical iPhone's .app/.ipa
    /// before installing it; see <see cref="Swipewalk.Collectors.AppInstaller.InstalledIosApp"/>). iOS
    /// Simulator scans detect this later, from the running app, and ignore this field. Null unless set here.
    /// </summary>
    public string? FrameworkVersion { get; init; }
    public string? Standard { get; init; }
    public string ScreenName { get; init; } = "Screen 1";
    public bool LargeText { get; init; }

    /// <summary>
    /// scan only for now: also capture the screen in the device's other dark/light appearance (Android
    /// `cmd uimode night`; iOS Simulator `simctl ui appearance`; a physical iPhone through the harness driving
    /// Settings &gt; Appearance) and run every rule on that capture too, so a contrast failure
    /// that only shows up in one theme is still found regardless of which theme the device happened to be in
    /// (see docs/case-study.md: the same screen scanned clean in dark mode and found 5 contrast failures in
    /// light mode). The device's original appearance is restored afterward, including on an error or
    /// cancellation. Off by default: it doubles the capture and rule-running work for a screen. A physical
    /// iPhone left on Automatic (day/night scheduling) has no fixed "current" appearance to switch from, so
    /// the check is skipped there with a reason instead.
    /// </summary>
    public bool AppearanceBoth { get; init; }

    /// <summary>
    /// scan only for now: also capture the screen rotated to the device's other orientation (portrait &lt;-&gt;
    /// landscape; Android `settings put system accelerometer_rotation/user_rotation`; iOS via the harness's
    /// <c>XCUIDevice.shared.orientation</c> -- there is no `simctl` equivalent, and it is the same call for
    /// the Simulator and a physical iPhone, signed for the latter) and check whether the screen's content
    /// actually followed, for WCAG 1.3.4 Orientation (see
    /// <c>Swipewalk.Core.Rules.OrientationRestrictedRule</c>). When it did rotate, every rule runs on that
    /// capture too and findings are tagged by orientation (see <c>Swipewalk.Core.Reports.OrientationMerge</c>),
    /// the same shape as <see cref="AppearanceBoth"/>. The device's original orientation is restored
    /// afterward (and, on Android only, its rotation-lock state too), including on an error or cancellation.
    /// Off by default: it doubles the capture and rule-running work for a screen. On a physical iPhone with
    /// Control Center's rotation lock on, the rotation call can report success without the interface actually
    /// turning -- there is no public API here to read the lock's own state, so
    /// <c>Swipewalk.Collectors.PhysicalDeviceOrientationNotice</c> prints a plain notice naming the
    /// possibility; it does not check or rule out the lock itself.
    /// </summary>
    public bool OrientationBoth { get; init; }

    /// <summary>
    /// scan only for now: take a few further captures of this screen a few seconds apart, with no input, and
    /// check whether content kept changing on its own across more than one interval -- evidence for WCAG
    /// 2.2.2 Pause, Stop, Hide (see <c>Swipewalk.Core.Rules.AutoUpdatingContentRule</c>). Unlike
    /// <see cref="AppearanceBoth"/> and <see cref="OrientationBoth"/>, this never changes the device: no
    /// restore step is needed, and it works on a physical device too. Off by default: it adds
    /// <see cref="AutoUpdateExtraCaptures"/> extra captures to the screen, each costing the wait
    /// (<see cref="AutoUpdateIntervalSeconds"/>) plus a full capture -- fast on Android (a few seconds each),
    /// much slower on iOS (a full XCUITest harness capture, which can take tens of seconds), so the real added
    /// time varies a lot by platform; the report states the real elapsed time measured, not an assumed one.
    /// </summary>
    public bool AutoUpdateCheck { get; init; }

    /// <summary>Seconds to wait before each extra capture in the auto-updating-content check, on top of
    /// however long the capture itself takes (see <see cref="AutoUpdateCheck"/>). Default 3.</summary>
    public double AutoUpdateIntervalSeconds { get; init; } = 3.0;

    /// <summary>
    /// Extra captures taken beyond the primary one. Minimum 2 (enforced by the CLI and the desktop app, not
    /// here): with only one, the check can't tell a settled one-off change (a spinner, a one-off load) from
    /// content that is still changing after more than one interval has passed -- see
    /// <c>Swipewalk.Core.Model.AutoUpdateChangeDetector</c>. Default 2 (3 captures total); the real time they
    /// span is the interval plus however long each capture itself takes, not just the interval -- see
    /// <c>Swipewalk.Core.Reports.ScreenResult.AutoUpdateElapsedSeconds</c>.
    /// </summary>
    public int AutoUpdateExtraCaptures { get; init; } = 2;

    public bool KeepStatusBar { get; init; }
    public bool SkipChecks { get; init; }

    /// <summary>
    /// record: scan automatically when the screen changes, without pressing "Scan this screen now" / Enter.
    /// Off by default: only an explicit scan (button or Enter) captures a screen, so a recording never
    /// captures a screen the person is still navigating through (see Swipewalk.Engine.Recorder).
    /// </summary>
    public bool AutoScanOnScreenChange { get; init; }

    /// <summary>
    /// record: what to do when a large-text check finds that the larger size needs a restart to show (see
    /// Swipewalk.Engine.LargeTextRestartPolicy). The caller (CLI, desktop app) resolves its own default --
    /// "ask" when it can ask, "never" otherwise -- before setting this; the engine itself never guesses at
    /// whether a console or UI is available.
    /// </summary>
    public LargeTextRestartPolicy LargeTextRestartPolicy { get; init; } = LargeTextRestartPolicy.Never;

    /// <summary>Re-scan a saved capture directory instead of a live device.</summary>
    public string? FromCapture { get; init; }

    /// <summary>Screens the user means to cover (record); missing ones are listed in the report.</summary>
    public IReadOnlyList<string> ExpectedScreens { get; init; } = [];

    public required string OutputDirectory { get; init; }

    // iOS signing and harness
    public string? Team { get; init; }
    public string? Profile { get; init; }
    public string? HarnessBundlePrefix { get; init; }
    public string? HarnessProject { get; init; }
    public bool ForceResultBundle { get; init; }

    /// <summary>Path to harness/android, for Google's Accessibility Test Framework (see
    /// Swipewalk.Collectors.Android.AndroidHarness); null auto-detects it. A missing or unbuildable harness
    /// only skips those checks (see KnownLimitations "android-atf-harness"), never the scan itself.</summary>
    public string? AndroidHarnessDir { get; init; }

    /// <summary>
    /// Android only for now: also drive TalkBack over every screen captured in this run and read back what
    /// it actually says, compared against Swipewalk's predicted transcript (see
    /// Swipewalk.Collectors.Android.AndroidHarness.RunScreenReaderCaptureAsync). Opt-in and false by
    /// default: it costs roughly 1-2 seconds per element (a 30-element screen ~1 minute) on top of an
    /// ordinary capture. A harness problem (TalkBack not installed, its settings screen not found) only
    /// skips this for the affected screen, never the rest of the scan.
    /// </summary>
    public bool ScreenReaderCapture { get; init; }

    /// <summary>The app identifier for the platform (package or bundle id), when known.</summary>
    public string? AppId => Platform == TargetPlatform.Ios ? BundleId : Package;
}
