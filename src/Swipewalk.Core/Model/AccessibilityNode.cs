using Swipewalk.Core.Imaging;

namespace Swipewalk.Core.Model;

/// <summary>The platform a snapshot was captured from.</summary>
public enum Platform
{
    Android,
    iOS,
    Windows,
}

/// <summary>
/// Screen-space rectangle in density-independent units: Android dp, iOS points, Windows DIPs.
/// Collectors convert from raw pixels; <see cref="ScreenSnapshot.PixelScale"/> maps back to screenshot pixels.
/// </summary>
public readonly record struct Bounds(double X, double Y, double Width, double Height);

/// <summary>
/// One element in a platform-neutral accessibility tree. Collectors translate
/// AccessibilityNodeInfo (Android), UIAccessibility (iOS) and UI Automation (Windows)
/// into this shape so rules can be written once.
/// </summary>
public sealed record AccessibilityNode
{
    /// <summary>Normalized role, e.g. "button", "image", "text", "textfield", "heading".</summary>
    public required string Role { get; init; }

    /// <summary>The platform's original class or control type, kept for diagnostics.</summary>
    public string? NativeType { get; init; }

    /// <summary>
    /// Name set explicitly for assistive technology (Android content description, iOS accessibilityLabel,
    /// UIA Name). Null when the platform would fall back to <see cref="VisibleText"/> or child content.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>Text rendered on screen by the element itself, when the platform exposes it separately.</summary>
    public string? VisibleText { get; init; }

    public string? Value { get; init; }
    public string? Hint { get; init; }

    /// <summary>Test/automation identifier. Never announced by screen readers.</summary>
    public string? AutomationId { get; init; }

    public Bounds Bounds { get; init; }
    public bool IsInteractive { get; init; }
    public bool IsFocusable { get; init; }

    /// <summary>Scrolling container (list, scroll view). Screen readers stop on its items, not on the container.</summary>
    public bool IsScrollable { get; init; }
    public bool IsEnabled { get; init; } = true;

    /// <summary>False when the element is hidden from assistive technology.</summary>
    public bool IsAccessible { get; init; } = true;

    /// <summary>
    /// True when <see cref="Hint"/> or <see cref="VisibleText"/> is currently shown as a placeholder rather
    /// than an entered value, from Android's <c>AccessibilityNodeInfo#isShowingHintText()</c> (API 26).
    /// Read only through the Android instrumentation harness (harness/android; see
    /// Swipewalk.Collectors.Android.AndroidHarness); <c>uiautomator dump</c> does not expose it. Null when
    /// the harness did not run for this capture (older Android, harness unavailable, or any other
    /// platform) -- see <see cref="Rules.MissingNameRule"/> and KnownLimitations "android-edittext-text".
    /// </summary>
    public bool? IsShowingHintText { get; init; }

    /// <summary>
    /// True when the platform reports this element as a heading, from Android's
    /// <c>AccessibilityNodeInfo#isHeading()</c> (API 28), read only through the Android harness (see
    /// <see cref="IsShowingHintText"/>). Null when not read. Not yet used by a rule (see KnownLimitations
    /// "no-heading-checks": full 1.3.1 heading/landmark structure needs more than this one flag).
    /// </summary>
    public bool? IsHeading { get; init; }

    /// <summary>
    /// A window or pane's title, from Android's <c>AccessibilityNodeInfo#getPaneTitle()</c> (API 28), read
    /// only through the Android harness (see <see cref="IsShowingHintText"/>). Makes 2.4.2 Page Titled
    /// checkable on Android in principle; no rule reads it yet. Null when not read or not set.
    /// </summary>
    public string? PaneTitle { get; init; }

    /// <summary>
    /// A supplementary description of this element's state beyond its role (e.g. "3 of 7 selected"), from
    /// Android's <c>AccessibilityNodeInfo#getStateDescription()</c> (API 30), read only through the Android
    /// harness (see <see cref="IsShowingHintText"/>). Strengthens what a 4.1.2 Name, Role, Value check could
    /// verify; no rule reads it yet. Null when not read or not set.
    /// </summary>
    public string? StateDescription { get; init; }

    /// <summary>
    /// Android's own importance-for-accessibility flag, from <c>AccessibilityNodeInfo#isImportantForAccessibility()</c>
    /// (API 24), read only through the Android harness (see <see cref="IsShowingHintText"/>). Distinct from
    /// <see cref="IsAccessible"/>, which today is inferred from the compressed <c>uiautomator dump</c>
    /// (an approximation of what TalkBack can reach, not this flag itself). Null when not read.
    /// </summary>
    public bool? IsImportantForAccessibility { get; init; }

    public IReadOnlyList<AccessibilityNode> Children { get; init; } = [];

    public IEnumerable<AccessibilityNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var node in child.DescendantsAndSelf())
                yield return node;
    }

    /// <summary>Depth-first walk yielding each node with its child-index path ("" for this node, then "0", "0/1", ...).</summary>
    public IEnumerable<(AccessibilityNode Node, string Path)> DescendantsAndSelfWithPath(string path = "")
    {
        yield return (this, path);
        for (var i = 0; i < Children.Count; i++)
        {
            var childPath = path.Length == 0 ? $"{i}" : $"{path}/{i}";
            foreach (var entry in Children[i].DescendantsAndSelfWithPath(childPath))
                yield return entry;
        }
    }
}

/// <summary>A captured screen: its accessibility tree plus an optional screenshot.</summary>
public sealed record ScreenSnapshot
{
    public required Platform Platform { get; init; }
    public required string ScreenName { get; init; }
    public required AccessibilityNode Root { get; init; }
    public AppFramework Framework { get; init; } = AppFramework.Unknown;

    /// <summary>
    /// The framework's own version, when detected (for example ".NET MAUI 10.0.60", stored as "10.0.60" with
    /// any "+commit" suffix stripped). Null when the framework is <see cref="AppFramework.Unknown"/>, or when
    /// it is known but the version could not be read. Selects between known-fixed-version and
    /// unknown-version wording in the iOS MAUI text-resize rule messages; see <see cref="FrameworkVersions"/>.
    /// </summary>
    public string? FrameworkVersion { get; init; }

    /// <summary>The device the screen was captured on, when known.</summary>
    public DeviceInfo? Device { get; init; }

    /// <summary>Scale from the tree's units to screenshot pixels.</summary>
    public double PixelScale { get; init; } = 1.0;

    public string? ScreenshotPath { get; init; }

    /// <summary>Decoded screenshot pixels, for checks such as text contrast. Null when no screenshot was taken.</summary>
    public RgbaImage? Screenshot { get; init; }

    /// <summary>
    /// True when the app blocked screenshots (for example Android FLAG_SECURE on banking or password screens): the
    /// image is black although the tree shows text. Pixel-based checks are skipped.
    /// </summary>
    public bool ScreenshotBlocked =>
        Screenshot is { } image
        && Root.DescendantsAndSelf().Any(n => n.IsAccessible && !string.IsNullOrWhiteSpace(n.VisibleText))
        && image.BlackShare() >= 0.97;

    /// <summary>
    /// The same screen captured again with the system text size enlarged (Android font scale 2.0,
    /// iOS largest accessibility text size), for resize checks. Null when not captured.
    /// </summary>
    public ScreenSnapshot? LargeText { get; init; }

    /// <summary>Human-readable description of the large-text setting used, e.g. "Android font scale 2.0".</summary>
    public string? LargeTextSetting { get; init; }

    /// <summary>
    /// The text-size scale tested by <see cref="LargeText"/>, as a multiple of normal size (Android font
    /// scale 2.0 = 200%; iOS accessibility size AX3 is about 2.35 = 235%). Null for captures made before
    /// this was recorded. WCAG 1.4.4 Resize Text only requires 200%: rules use this to tell a WCAG failure
    /// from a platform-guideline advisory about text sizes beyond 200%.
    /// </summary>
    public double? LargeTextScale { get; init; }

    /// <summary>
    /// How the large-text capture was produced, e.g. "system setting" (Settings on Android/iOS Simulator, or
    /// driven through the iOS harness on a physical iPhone) or "per-app launch setting" (physical iPhone
    /// fallback: a per-process launch argument, used when the Settings automation itself failed). Null when
    /// not captured or not recorded (older captures, or platforms where only one method exists).
    /// </summary>
    public string? LargeTextMethod { get; init; }

    /// <summary>
    /// True when <see cref="LargeText"/> was captured live (the app picked up the larger text size without
    /// restarting); false when it only appeared after the app under test was terminated and relaunched (some
    /// frameworks, e.g. .NET MAUI, apply Dynamic Type / font scale only at launch); null when not recorded.
    /// A separate rule reports "applies only after restart" using this field; this model only carries the data.
    /// </summary>
    public bool? LargeTextAppliedLive { get; init; }

    /// <summary>
    /// True when a force-stop + relaunch was actually captured and compared against the pre-relaunch
    /// screen, so <see cref="LargeTextAppliedLive"/> being false or null reflects a confirmed result (text
    /// grew after the restart, or still did not grow even after it). False or null when that after-restart
    /// comparison could not be completed (the app didn't return to the same screen or come back to front
    /// after being relaunched) -- <see cref="LargeText"/> may still hold the live, pre-relaunch capture in
    /// that case, but whether restarting the app would help is unknown, not confirmed. Rules fall back to
    /// the more conservative "unknown" wording when this is null, including for captures made before this
    /// was recorded. Only collectors that run the restart escalation (Android, iOS) set this; set alongside
    /// <see cref="LargeTextAppliedLive"/> wherever that is decided after a restart attempt.
    /// </summary>
    public bool? LargeTextRestartCaptured { get; init; }

    /// <summary>
    /// True when the system text-size change made the app show a different screen than the one being
    /// captured (typically its own first/launch screen), so the person's navigation place was lost and they
    /// have to navigate back to continue. Seen on Android with the default .NET MAUI template: MainActivity's
    /// ConfigurationChanges list omits FontScale, so a font-scale change recreates the activity and the app
    /// rebuilds its first page (App.CreateWindow) (verified 2026-09-23 on the emulator with BuggyApp, .NET
    /// MAUI 10.0.110). False or null when this was not observed (for example the app stayed on the same
    /// screen). This field is about the lost navigation place only -- whether text on any screen reaches the
    /// larger size is reported separately, see <see cref="Rules.TextResizeRule"/> and
    /// <see cref="Rules.TextResizeLiveUpdateRule"/>. Set by <c>Swipewalk.Engine.Recorder</c> and
    /// <c>Swipewalk.Engine.ScanService</c> (Android only, on the one screen where it was observed -- never on
    /// a later screen just because an earlier one exhibited it); see <see cref="Rules.TextResizeNavigationRule"/>.
    /// </summary>
    public bool? LargeTextWentToAnotherScreen { get; init; }

    /// <summary>
    /// The same screen captured again after rotating the device to the other orientation (portrait &lt;-&gt;
    /// landscape; see <c>Swipewalk.Engine.ScanOptions.OrientationBoth</c>), for
    /// <see cref="Rules.OrientationRestrictedRule"/> -- the same nested-capture shape as <see cref="LargeText"/>,
    /// attached by <c>Swipewalk.Engine.ScanService</c> before rules run so the rule can compare the two
    /// captures' dimensions. Null when not captured.
    /// </summary>
    public ScreenSnapshot? Orientation { get; init; }

    /// <summary>The orientation this capture (not <see cref="Orientation"/>) was taken in -- "portrait" or
    /// "landscape" (see <c>Swipewalk.Core.Reports.OrientationLabels</c>). Null unless <see cref="Orientation"/>
    /// is also set.</summary>
    public string? OrientationLabel { get; init; }

    /// <summary>The orientation <see cref="Orientation"/> was captured in. Null unless <see cref="Orientation"/>
    /// is also set.</summary>
    public string? OtherOrientationLabel { get; init; }

    /// <summary>
    /// Further captures of this same screen, taken a few seconds apart with no input (see
    /// <c>Swipewalk.Engine.ScanOptions.AutoUpdateCheck</c>), for <see cref="Rules.AutoUpdatingContentRule"/> --
    /// evidence for WCAG 2.2.2 Pause, Stop, Hide: moving, blinking or scrolling content that starts on its own,
    /// lasts more than 5 seconds and is shown alongside other content, or auto-updating content shown
    /// alongside other content, needs a way to pause, stop or hide it. Ordered, each after the last, by
    /// roughly <see cref="AutoUpdateIntervalSeconds"/> plus however long a capture itself takes on this
    /// platform (fast on Android, sometimes tens of seconds on iOS) -- see
    /// <see cref="Reports.ScreenResult.AutoUpdateElapsedSeconds"/> for the REAL measured span, which is what
    /// gets reported, not this nominal figure -- the same nested-capture shape as <see cref="LargeText"/> and
    /// <see cref="Orientation"/>, except a list (more than one extra capture is needed to tell "changed once
    /// and settled" -- a spinner, a one-off load -- from "still changing after more than one interval has
    /// passed", see <see cref="AutoUpdateChangeDetector"/>). Empty when not requested for this screen.
    /// </summary>
    public IReadOnlyList<ScreenSnapshot> AutoUpdateCaptures { get; init; } = [];

    /// <summary>Seconds between this capture and the first entry of <see cref="AutoUpdateCaptures"/>, and
    /// between each entry after that. Null unless <see cref="AutoUpdateCaptures"/> is non-empty.</summary>
    public double? AutoUpdateIntervalSeconds { get; init; }

    /// <summary>
    /// Real screen-reader evidence for this screen (TalkBack, Xcode's Accessibility Inspector, or a
    /// recorded VoiceOver session) -- see <see cref="Model.ScreenReaderCapture"/>. Null (the common case
    /// today) when no such capture was made for this screen; that is different from a capture that ran and
    /// found nothing, which would have a non-null value with an empty <see cref="ScreenReaderCapture.Items"/>.
    /// Compared against the predicted transcript (<see cref="ScreenReader.ScreenReaderPredictor.Predict"/>)
    /// by <see cref="ScreenReader.ScreenReaderCaptureComparer"/> and turned into findings by
    /// <see cref="Rules.ScreenReaderCaptureRule"/>.
    /// </summary>
    public ScreenReaderCapture? ScreenReaderCapture { get; init; }

    /// <summary>Issues reported by the platform's own accessibility engine for this screen.</summary>
    public IReadOnlyList<EngineIssue> EngineIssues { get; init; } = [];

    /// <summary>
    /// Issues reported by Google's Accessibility Test Framework (ATF), via the Android instrumentation
    /// harness (harness/android; see Swipewalk.Collectors.Android.AndroidHarness). Empty both when the
    /// harness ran and found nothing, and when it didn't run at all -- see <see cref="AtfRan"/> to tell
    /// those apart. Always empty on platforms other than Android.
    /// </summary>
    public IReadOnlyList<AtfIssue> AtfIssues { get; init; } = [];

    /// <summary>
    /// True only when <see cref="Collectors.Android.AndroidCollector.Load"/> (or a snapshot built the same
    /// way) confirmed the Android ATF harness actually produced a result for this capture -- never assumed
    /// from <see cref="AtfIssues"/> being non-empty, so a clean screen (harness ran, found nothing) is not
    /// confused with one the harness never touched (a hand-built snapshot, an older capture, or one Windows
    /// or iOS platform never runs it on). Defaults to false, the safe assumption for any snapshot that
    /// doesn't explicitly know about this. See <see cref="AtfSkippedReason"/> for why, when false.
    /// </summary>
    public bool AtfRan { get; init; }

    /// <summary>
    /// Why the Android ATF harness's checks did not run for this screen (for example "the harness could
    /// not be built" or "adb shell am instrument failed"), so the report and the WCAG coverage section can
    /// say Google's checks did not run instead of silently reporting zero issues. Meaningful only when
    /// <see cref="AtfRan"/> is false; Swipewalk never fails a scan because of this -- see KnownLimitations
    /// "android-atf-harness".
    /// </summary>
    public string? AtfSkippedReason { get; init; }

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// App identifier detected from the capture itself (Android package, iOS bundle id) -- separate from
    /// whichever app id, if any, was passed in when the scan started (that one takes priority; see
    /// Swipewalk.Engine.ScanOptions.AppId and how it's combined with this field into
    /// Swipewalk.Core.Reports.ScanReport.AppId). Lets run history group and compare runs of the same app
    /// without a typed <c>--package</c>/<c>--bundle-id</c>. Null when it could not be told from the capture,
    /// e.g. an Android dump with no usable package attribute, or an iOS capture with no saved bundle id.
    /// </summary>
    public string? AppId { get; init; }
}
