using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Model;

public enum FindingKind
{
    /// <summary>A possible failure of one or more WCAG 2.2 success criteria.</summary>
    WcagIssue,

    /// <summary>Automated checks can't decide; a person must review against the cited WCAG criteria.</summary>
    NeedsReview,

    /// <summary>Below a platform guideline (Apple HIG, Android, Windows) but not a WCAG criterion.</summary>
    PlatformAdvisory,
}

/// <summary>One issue automated checks found on a screen.</summary>
public sealed record Finding
{
    public required string RuleId { get; init; }
    public required FindingKind Kind { get; init; }
    public required string Message { get; init; }

    /// <summary>WCAG criteria this finding maps to. Required unless the kind is <see cref="FindingKind.PlatformAdvisory"/>.</summary>
    public IReadOnlyList<WcagCriterion> Criteria { get; init; } = [];

    /// <summary>The platform guideline cited by an advisory, e.g. "Android: touch targets at least 48×48 dp".</summary>
    public string? PlatformGuideline { get; init; }

    /// <summary>Child-index path from the root, e.g. "0/2/1"; the root is "".</summary>
    public required string NodePath { get; init; }

    public required string Role { get; init; }
    public string? Label { get; init; }
    public Bounds Bounds { get; init; }

    /// <summary>Who reported the finding: "Swipewalk" or a platform engine such as "Apple accessibility audit".</summary>
    public string Source { get; init; } = DefaultSource;

    public const string DefaultSource = "Swipewalk";

    /// <summary>Rule-specific data for reports and downstream tools, e.g. measured colors or visible text.</summary>
    public IReadOnlyDictionary<string, string> Details { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Ids of standards whose referenced WCAG version and level include this finding's criteria
    /// (see Swipewalk.Core.Standards.KnownStandards). Filled in by the rule runner.
    /// </summary>
    public IReadOnlyList<string> RelevantStandards { get; init; } = [];

    /// <summary>Suggested fix, example code and likely causes for the app's framework. Filled in by the rule runner.</summary>
    public Reports.Fix? Fix { get; init; }

    /// <summary>Platform engines that reported the same issue on the same element.</summary>
    public IReadOnlyList<string> AlsoReportedBy { get; init; } = [];

    /// <summary>
    /// Which appearance this finding came from, when the screen was checked in both (see
    /// <c>Swipewalk.Engine.ScanOptions.AppearanceBoth</c> and <see cref="Reports.AppearanceMerge"/>):
    /// <c>"dark"</c> or <c>"light"</c> for a finding seen in only one of the two captures, or
    /// <see cref="Reports.AppearanceLabels.Both"/> when the same rule reported the same element in each. The
    /// WCAG mapping above is unchanged either way -- both are user-selectable modes, so a contrast failure in
    /// either one is a real WCAG failure. Null when the appearance rescan did not run for this screen.
    /// </summary>
    public string? Appearance { get; init; }

    /// <summary>
    /// Which orientation this finding came from, when the screen was checked in both (see
    /// <c>Swipewalk.Engine.ScanOptions.OrientationBoth</c> and <see cref="Reports.OrientationMerge"/>):
    /// <c>"portrait"</c> or <c>"landscape"</c> for a finding seen in only one of the two captures, or
    /// <see cref="Reports.OrientationLabels.Both"/> when the same rule reported the same element in each.
    /// Only set when the device actually rotated (see <see cref="ScreenResult.OrientationUnchanged"/>); null
    /// when the orientation rescan did not run for this screen, or the screen did not visibly rotate (that
    /// case is reported once, on the screen itself, by <see cref="Rules.OrientationRestrictedRule"/>, not
    /// per finding).
    /// </summary>
    public string? Orientation { get; init; }
}

/// <summary>The findings for one scanned screen.</summary>
public sealed record ScreenResult
{
    /// <summary>
    /// Stable id for this screen within its run, assigned once when the screen is first captured (see
    /// <see cref="Rules.RuleRunner.Run"/>, the only place that constructs a genuinely new
    /// <see cref="ScreenResult"/>) and kept across a same-screen replacement (record mode's
    /// rescan-keeps-the-newer-capture behavior -- see <c>Engine.Recorder</c>) and across
    /// <c>record --continue</c>. Guided-check answers key off this, never off list position or
    /// <see cref="ScreenName"/> (names repeat: two screens titled "Home", or the same app's first screen
    /// appearing again after a restart). Defaults to empty, never a random id: a fresh capture gets a real one
    /// explicitly from <see cref="Rules.RuleRunner.Run"/>; a results.json saved before this field existed has
    /// none on disk, and <see cref="Reports.JsonReport.Deserialize"/> fills that gap deterministically (by
    /// screen position) instead -- a random default here would hand the SAME old file a DIFFERENT id on every
    /// separate load, silently orphaning guided-check answers saved against an earlier one.
    /// </summary>
    public string ScreenId { get; init; } = "";

    public required Platform Platform { get; init; }
    public required string ScreenName { get; init; }
    public AppFramework Framework { get; init; } = AppFramework.Unknown;

    /// <summary>The framework's own version, when detected (see <see cref="ScreenSnapshot.FrameworkVersion"/>).</summary>
    public string? FrameworkVersion { get; init; }

    public DeviceInfo? Device { get; init; }
    public string? ScreenshotPath { get; init; }
    public required IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>Screenshot at enlarged system text size (record mode), and the setting used.</summary>
    public string? LargeTextScreenshotPath { get; init; }
    public double LargeTextPixelScale { get; init; } = 1.0;
    public string? LargeTextSetting { get; init; }

    /// <summary>The text-size scale tested (see <see cref="ScreenSnapshot.LargeTextScale"/>). Null when not captured
    /// or not recorded (older captures).</summary>
    public double? LargeTextScale { get; init; }

    /// <summary>How the large-text capture was produced (see <see cref="ScreenSnapshot.LargeTextMethod"/>).</summary>
    public string? LargeTextMethod { get; init; }

    /// <summary>Whether the large-text capture was live or only appeared after a restart (see
    /// <see cref="ScreenSnapshot.LargeTextAppliedLive"/>); the report shows this next to the method.</summary>
    public bool? LargeTextAppliedLive { get; init; }

    /// <summary>Whether a force-stop + relaunch comparison was actually captured (see
    /// <see cref="ScreenSnapshot.LargeTextRestartCaptured"/>); the report shows this next to the method. Null
    /// when not captured or not recorded (older results), so old results.json files still load.</summary>
    public bool? LargeTextRestartCaptured { get; init; }

    /// <summary>
    /// Why the large-text check was skipped for this screen (a physical device, a different screen at the
    /// larger size, or the app not coming back to front), when it was attempted but not captured. Null when
    /// large text was captured (see <see cref="LargeTextSetting"/>) or was never requested for this screen.
    /// </summary>
    public string? LargeTextSkippedReason { get; init; }

    /// <summary>
    /// Warning when the device's text size was already enlarged before this screen's normal-size capture, so
    /// that capture isn't at the platform's default size (Android font_scale, iOS Simulator content_size, or
    /// a physical iPhone's Larger Text state -- see Swipewalk.Collectors.BaselineTextSize). When set, the
    /// large-text comparison for this screen was skipped (see <see cref="LargeTextSkippedReason"/>) rather
    /// than risk a large-vs-large comparison that would wrongly look like text "did not grow"; the normal-size
    /// findings above are unaffected. Null when the baseline was at default, or wasn't checked.
    /// </summary>
    public string? BaselineTextSizeNote { get; init; }

    /// <summary>
    /// Set when Swipewalk had to start the app itself because it wasn't running (see
    /// Swipewalk.Collectors.BringToFront): this screen (or, in record mode, the first screen of the recording)
    /// is the app's own first screen -- possibly onboarding or a terms screen -- not wherever it was last left.
    /// Null when the app was already running (whether already in front or brought forward from the background
    /// -- neither of which restarts it, so its state is usually preserved) or this screen wasn't the one that
    /// triggered a launch.
    /// </summary>
    public string? AppLaunchedNote { get; init; }

    /// <summary>Whether Google's Accessibility Test Framework actually ran for this screen (see
    /// <see cref="ScreenSnapshot.AtfRan"/>); defaults to false.</summary>
    public bool AtfRan { get; init; }

    /// <summary>
    /// Why Google's Accessibility Test Framework (Android instrumentation harness, harness/android) did not
    /// run for this screen, meaningful only when <see cref="AtfRan"/> is false (see
    /// <see cref="ScreenSnapshot.AtfSkippedReason"/>). Reports and the WCAG coverage section use this to say
    /// Google's checks didn't run instead of silently showing zero issues; see KnownLimitations
    /// "android-atf-harness" and <see cref="Coverage.ScreenActivityBuilder"/>.
    /// </summary>
    public string? AtfSkippedReason { get; init; }

    /// <summary>Scale from tree units to screenshot pixels, for drawing overlays.</summary>
    public double PixelScale { get; init; } = 1.0;

    /// <summary>Predicted (not recorded) screen-reader output in swipe order.</summary>
    public IReadOnlyList<Announcement> PredictedTranscript { get; init; } = [];

    /// <summary>Real screen-reader evidence for this screen, carried through from
    /// <see cref="ScreenSnapshot.ScreenReaderCapture"/> (see its remarks). Null when none was captured.</summary>
    public ScreenReaderCapture? ScreenReaderCapture { get; init; }

    /// <summary>App identifier detected from this screen's capture (see <see cref="ScreenSnapshot.AppId"/>).
    /// Carried through to <see cref="Reports.ScanReport.AppId"/> for run history grouping.</summary>
    public string? AppId { get; init; }

    /// <summary>
    /// Record mode only: set when this capture replaced an earlier capture of the same screen within this same
    /// run (see Swipewalk.Core.ScreenReader.ScreenIdentity.IsSameScreen) -- a newer capture of the same screen
    /// replaces the older one, but only within the same run; separate runs are never merged. The earlier capture's
    /// files are deleted and its findings are not kept, so they aren't counted twice; this timestamp is when
    /// the newer (kept) capture was taken, so the report can still say the screen was scanned again and when.
    /// Null for a screen's first (or only) capture in a run, and always null for a scan.
    /// </summary>
    public DateTimeOffset? RescannedAt { get; init; }

    /// <summary>
    /// The appearance ("dark" or "light") the primary capture above was taken in, when the dark/light
    /// rescan (<c>Swipewalk.Engine.ScanOptions.AppearanceBoth</c>) ran for this screen. Null when it wasn't
    /// requested for this run.
    /// </summary>
    public string? Appearance { get; init; }

    /// <summary>The other appearance actually captured and compared against. Null when the rescan wasn't
    /// requested, or was requested but skipped -- see <see cref="AppearanceSkippedReason"/>.</summary>
    public string? OtherAppearance { get; init; }

    /// <summary>Screenshot of the screen in <see cref="OtherAppearance"/>, alongside the primary one above.</summary>
    public string? OtherAppearanceScreenshotPath { get; init; }

    public double OtherAppearancePixelScale { get; init; } = 1.0;

    /// <summary>
    /// Why the other-appearance capture wasn't made for this screen, when the rescan was requested: no
    /// Simulator or device was found, the iOS harness could not be signed or driven (a physical iPhone), or a
    /// physical iPhone was left on Automatic (day/night) appearance with no fixed "current" theme to switch
    /// from (see docs/limitations.md). Null when it was captured (see <see cref="OtherAppearance"/>), or
    /// wasn't requested for this run.
    /// </summary>
    public string? AppearanceSkippedReason { get; init; }

    /// <summary>
    /// True when the other-appearance capture looked the same as the primary one -- same accessibility tree,
    /// near-identical screenshot brightness (see <see cref="Model.AppearanceChangeDetector"/>) -- meaning the
    /// screen most likely did not visibly change appearance. This can't say why: the app may force one theme
    /// regardless of the system setting, use fixed colors, or only read the theme at launch and need a
    /// restart. Findings from that capture are still reported, since automated checks did run against it, but
    /// the report says the check may not reflect the other appearance. Null when not checked, or when a real
    /// change was seen.
    /// </summary>
    public bool? AppearanceUnchanged { get; init; }

    /// <summary>
    /// The orientation ("portrait" or "landscape") the primary capture above was taken in, when the
    /// orientation rescan (<c>Swipewalk.Engine.ScanOptions.OrientationBoth</c>) ran for this screen. Null
    /// when it wasn't requested for this run.
    /// </summary>
    public string? Orientation { get; init; }

    /// <summary>The other orientation actually captured and compared against. Null when the rescan wasn't
    /// requested, or was requested but skipped -- see <see cref="OrientationSkippedReason"/>.</summary>
    public string? OtherOrientation { get; init; }

    /// <summary>Screenshot of the screen rotated to <see cref="OtherOrientation"/>, alongside the primary one above.</summary>
    public string? OtherOrientationScreenshotPath { get; init; }

    public double OtherOrientationPixelScale { get; init; } = 1.0;

    /// <summary>
    /// Why the other-orientation capture wasn't made for this screen, when the rescan was requested: no
    /// Simulator or device was found, or the iOS harness could not be signed or driven (a physical iPhone;
    /// see docs/limitations.md). Null when it was captured (see <see cref="OtherOrientation"/>), or wasn't
    /// requested for this run.
    /// </summary>
    public string? OrientationSkippedReason { get; init; }

    /// <summary>
    /// True when the screen still looked the same shape (same aspect ratio, measured from each capture's own
    /// screenshot, or from its tree's root bounds for whichever capture has no usable screenshot) after the
    /// device was rotated to <see cref="OtherOrientation"/> -- the app's content is restricted to one
    /// orientation, reported for review as WCAG 1.3.4 Orientation by <see cref="Rules.OrientationRestrictedRule"/>
    /// (the exception for an essential orientation means this is never a confirmed failure). False when the
    /// two captures show a genuinely different aspect ratio (the app rotated; every rule's findings are
    /// tagged by orientation instead -- see <see cref="Finding.Orientation"/>). Null when the rescan didn't
    /// run for this screen, or ran but one of the two captures gave no usable evidence (no screenshot and no
    /// usable bounds) -- see <see cref="OrientationSkippedReason"/> for that second case, since a comparison
    /// that couldn't be made is reported as not done, never guessed.
    /// </summary>
    public bool? OrientationUnchanged { get; init; }

    /// <summary>
    /// Total captures taken for the auto-updating-content check (<c>Swipewalk.Engine.ScanOptions.AutoUpdateCheck</c>,
    /// scan only for now), including the primary one -- set once the check actually ran (whether or not it
    /// found sustained change; see <see cref="Rules.AutoUpdatingContentRule"/>). Null when it wasn't requested
    /// for this run, or was requested but not completed -- see <see cref="AutoUpdateSkippedReason"/>.
    /// </summary>
    public int? AutoUpdateCaptureCount { get; init; }

    /// <summary>The requested wait between each extra capture in the auto-updating-content check
    /// (<c>Swipewalk.Engine.ScanOptions.AutoUpdateIntervalSeconds</c>) -- NOT how far apart the captures
    /// actually ended up, since a full capture itself takes time on top of the wait (see
    /// <see cref="AutoUpdateElapsedSeconds"/> for that). Null unless <see cref="AutoUpdateCaptureCount"/> is set.</summary>
    public double? AutoUpdateIntervalSeconds { get; init; }

    /// <summary>
    /// The REAL elapsed time, measured from each capture's own timestamp, between the primary capture and the
    /// last of the extra captures -- what <see cref="Rules.AutoUpdatingContentRule"/>'s finding message and
    /// the report's before/after caption state, instead of assuming <see cref="AutoUpdateIntervalSeconds"/> x
    /// the capture count (a full capture costs time too: a few seconds on Android, sometimes tens of seconds
    /// on iOS via the XCUITest harness). Null unless <see cref="AutoUpdateCaptureCount"/> is set.
    /// </summary>
    public double? AutoUpdateElapsedSeconds { get; init; }

    /// <summary>Screenshot of the last of the extra captures, alongside the primary one above, so the report
    /// can show a before/after close-up. Null when the check didn't run, or ran with no screenshot captured.</summary>
    public string? AutoUpdateScreenshotPath { get; init; }

    public double AutoUpdateScreenshotPixelScale { get; init; } = 1.0;

    /// <summary>
    /// Why the auto-updating-content check wasn't completed for this screen, when it was requested. Null
    /// when it completed (see <see cref="AutoUpdateCaptureCount"/>), or wasn't requested for this run.
    /// </summary>
    public string? AutoUpdateSkippedReason { get; init; }

    /// <summary>
    /// Tree-based suggestions (never a verdict) that some WCAG criteria might not apply to this screen, from
    /// <see cref="Coverage.ApplicabilityRules.Evaluate"/> at capture time -- see
    /// <see cref="Coverage.ProposedNotApplicable"/>. Shown to the tester in the guided-checks UI/CLI as a
    /// prompt to confirm or dismiss; only becomes a final <see cref="Coverage.ScreenCriterionStatus.NotApplicableHere"/>
    /// once a <see cref="Coverage.GuidedAnswer"/> with <see cref="Coverage.GuidedAnswerResult.ConfirmedNotApplicable"/>
    /// exists for the same (screen, criterion).
    /// </summary>
    public IReadOnlyList<Coverage.ProposedNotApplicable> ProposedNotApplicable { get; init; } = [];
}
