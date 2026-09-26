using Swipewalk.Collectors;
using Swipewalk.Core.Coverage;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

/// <summary>
/// Guards against a rule being added to <see cref="DefaultRules.All"/> (and usually
/// <see cref="DefaultRules.Coverage"/>) without also being added to <see cref="ScreenActivityBuilder"/>'s
/// <c>AlwaysRun</c> set or one of its platform/capture gates. When that happens, the rule keeps producing
/// findings, but every screen's <see cref="ScreenActivity"/> never marks it as either ran or skipped, so a
/// WCAG criterion it maps to always reads "not tested in this run" -- silently, even while the rule is
/// working -- because <c>CriterionCoverageExtensions.ComputeRunStatus</c> only counts a rule as having run
/// where <see cref="ScreenActivity.RanRuleIds"/> says so. This happened twice before this test existed: to
/// <c>text-resize-live</c> (TextResizeLiveUpdateRule) and <c>text-resize-navigation</c> (TextResizeNavigationRule).
///
/// This doesn't hardcode ScreenActivityBuilder's internal gate conditions (that would just be a second copy
/// of the same logic, and as brittle as the thing it's guarding). Instead it builds <see cref="ScreenActivity"/>
/// for a representative screen in every platform x capture-state combination the builder branches on today
/// (Android/iOS, screenshot present/absent, large-text captured/skipped-with-reason/never-requested, ATF
/// ran/skipped) and asserts every rule id the app actually registers turns up in at least one combination's
/// <see cref="ScreenActivity.RanRuleIds"/> or <see cref="ScreenActivity.SkippedRuleIds"/>. A rule the builder
/// has simply never heard of -- the failure mode this test exists to catch -- appears in neither, on every
/// single combination, so it fails there regardless of which combinations a future capture state adds.
/// </summary>
public class RuleCoverageWiringTests
{
    /// <summary>
    /// Rule ids that are deliberately allowed to be untracked by <see cref="ScreenActivityBuilder"/>, with why.
    /// Empty today: <c>ScreenReaderCaptureRule</c> (the one rule with no collector wiring yet -- see
    /// <see cref="DefaultRules.All"/>'s own remarks) is excluded from both <see cref="DefaultRules.All"/> and
    /// <see cref="DefaultRules.Coverage"/> until it's registered, so it never reaches this test's rule-id list
    /// in the first place. If a future rule genuinely has no per-screen ran/skipped status to report (e.g. it
    /// only ever evaluates run-wide, not per screen), add its id here with a comment explaining why, rather than
    /// silently excluding it.
    /// </summary>
    private static readonly string[] DeliberatelyUntracked = [];

    private static ScreenResult Screen(
        Platform platform, string? screenshotPath, string? largeTextSetting, string? largeTextSkippedReason,
        bool atfRan, string? atfSkippedReason, IReadOnlyList<Finding>? findings = null,
        string? otherOrientation = null, string? orientationSkippedReason = null) => new()
    {
        Platform = platform,
        ScreenName = "Home",
        ScreenshotPath = screenshotPath,
        LargeTextSetting = largeTextSetting,
        LargeTextSkippedReason = largeTextSkippedReason,
        AtfRan = atfRan,
        AtfSkippedReason = atfSkippedReason,
        Findings = findings ?? [],
        OtherOrientation = otherOrientation,
        OrientationSkippedReason = orientationSkippedReason,
    };

    private static Finding OrientationFinding() => new()
    {
        RuleId = "orientation-restricted",
        Kind = FindingKind.NeedsReview,
        Message = "test finding",
        Criteria = [WcagCriteria.Orientation],
        NodePath = "",
        Role = "screen",
    };

    private static Finding NavigationFinding() => new()
    {
        RuleId = "text-resize-navigation",
        Kind = FindingKind.PlatformAdvisory,
        Message = "test finding",
        PlatformGuideline = "test guideline",
        NodePath = "",
        Role = "screen",
    };

    /// <summary>Every combination of the axes <see cref="ScreenActivityBuilder"/> branches on today. Adding a
    /// new axis there (a new field it reads) should mean adding it here too, but forgetting to do that only
    /// weakens this test's coverage of the new axis -- it doesn't produce a false pass or false failure for
    /// rules already wired to the existing axes.</summary>
    private static IEnumerable<ScreenResult> RepresentativeScreens()
    {
        foreach (var platform in new[] { Platform.Android, Platform.iOS })
        foreach (var screenshotPath in new[] { "shot.png", null })
        foreach (var (largeTextSetting, largeTextSkippedReason) in new (string?, string?)[]
                 {
                     ("font scale 2.0", null), // captured
                     (null, LargeTextCapture.DifferentScreen), // attempted, own live attempt showed elsewhere
                     (null, LargeTextCapture.DeclinedByPerson), // attempted, skipped for an unrelated reason
                     (null, null), // never requested
                 })
        foreach (var (atfRan, atfSkippedReason) in new (bool, string?)[] { (true, null), (false, "no harness result") })
            yield return Screen(platform, screenshotPath, largeTextSetting, largeTextSkippedReason, atfRan, atfSkippedReason);
    }

    private static (HashSet<string> Ran, Dictionary<string, string> Skipped) UnionAcrossRepresentativeScreens()
    {
        var ran = new HashSet<string>();
        var skipped = new Dictionary<string, string>();
        foreach (var screen in RepresentativeScreens())
        {
            var activity = ScreenActivityBuilder.For(screen);
            ran.UnionWith(activity.RanRuleIds);
            foreach (var (id, reason) in activity.SkippedRuleIds)
                skipped[id] = reason;
        }
        return (ran, skipped);
    }

    [Fact]
    public void EveryRegisteredRuleId_IsReportedAsRanOrSkippedOnAtLeastOneRepresentativeScreen()
    {
        var ruleIds = DefaultRules.All.Select(r => r.Id)
            .Concat(DefaultRules.Coverage.Select(r => r.RuleId))
            .Distinct()
            .Except(DeliberatelyUntracked)
            .ToList();
        Assert.NotEmpty(ruleIds); // sanity: DefaultRules.All/Coverage must not both be empty

        var (ran, skipped) = UnionAcrossRepresentativeScreens();

        foreach (var id in ruleIds)
        {
            var tracked = ran.Contains(id) || skipped.ContainsKey(id);
            Assert.True(tracked,
                $"Rule '{id}' is registered in DefaultRules.All/Coverage, but ScreenActivityBuilder.For(...) " +
                $"never reports it as ran or skipped, on any of {ran.Count + skipped.Count} rule-id slots seen " +
                "across every Android/iOS x screenshot x large-text x ATF combination this test tries. " +
                "Fix: in src/Swipewalk.Core/Coverage/ScreenActivityBuilder.cs, add this id to the AlwaysRun " +
                "array (if the rule evaluates unconditionally on every screen), or add it to an existing gate " +
                "or a new if/else that adds it to `ran` or records a reason in `skipped`, mirroring the gate " +
                "for whichever existing rule reads the same snapshot data. Until that's done, any WCAG " +
                "criterion this rule maps to will always show \"not tested in this run\" in every report, " +
                "even while the rule itself is finding real issues.");
        }
    }

    [Fact]
    public void TextResizeLiveAndNavigation_AreWiredIntoCoverage()
    {
        // Regression coverage for the two rule ids this exact bug hit: both were registered in
        // DefaultRules.All/Coverage without ever being added to ScreenActivityBuilder, so they never appeared
        // as ran or skipped on any screen.
        var captured = ScreenActivityBuilder.For(Screen(Platform.Android, "shot.png", "font scale 2.0", null, true, null));
        Assert.Contains("text-resize-live", captured.RanRuleIds);
        Assert.Contains("text-resize-navigation", captured.RanRuleIds);

        var neverRequested = ScreenActivityBuilder.For(Screen(Platform.Android, "shot.png", null, null, true, null));
        Assert.Equal("large-text check not requested", neverRequested.SkippedRuleIds["text-resize-live"]);
        Assert.Equal("large-text check not requested", neverRequested.SkippedRuleIds["text-resize-navigation"]);

        // text-resize-navigation still counts as having run when the large-text attempt itself is what
        // surfaced a different screen (its own reason for the check existing at all) -- unlike text-resize
        // and text-resize-live, which need a successful capture, not just an attempt.
        var wentToAnotherScreen = ScreenActivityBuilder.For(
            Screen(Platform.Android, "shot.png", null, LargeTextCapture.DifferentScreen, true, null));
        Assert.Equal(LargeTextCapture.DifferentScreen, wentToAnotherScreen.SkippedRuleIds["text-resize-live"]);
        Assert.Contains("text-resize-navigation", wentToAnotherScreen.RanRuleIds);

        // A large-text attempt that was skipped for an unrelated reason (declined, not in front, etc.), with
        // no text-resize-navigation finding on the screen, is NOT enough to say the rule ran: scan mode's own
        // "went to another screen" signal is a different, more specific reason (asserted above), so a generic
        // skip reason here, with no finding, means the rule is reported as skipped with that reason.
        var declined = ScreenActivityBuilder.For(
            Screen(Platform.Android, "shot.png", null, LargeTextCapture.DeclinedByPerson, true, null));
        Assert.Equal(LargeTextCapture.DeclinedByPerson, declined.SkippedRuleIds["text-resize-navigation"]);

        // But record mode's own "went to another screen" signal never becomes a stored skip reason -- it
        // surfaces only as this finding, set from the screen's own live attempt regardless of what the person
        // decides afterwards (see Swipewalk.Engine.Recorder) -- so a finding on the screen counts as ran even
        // when the final skip reason (declined here) doesn't say so on its own.
        var declinedWithFinding = ScreenActivityBuilder.For(
            Screen(Platform.Android, "shot.png", null, LargeTextCapture.DeclinedByPerson, true, null, [NavigationFinding()]));
        Assert.Contains("text-resize-navigation", declinedWithFinding.RanRuleIds);

        var iosScreen = ScreenActivityBuilder.For(Screen(Platform.iOS, "shot.png", "large text", null, false, null));
        Assert.Contains("text-resize-live", iosScreen.RanRuleIds); // large-text gate isn't platform-specific
        Assert.Equal(CoverageDisplay.TextResizeNavigationAndroidOnlyReason, iosScreen.SkippedRuleIds["text-resize-navigation"]);
    }

    [Fact]
    public void OrientationRestricted_IsWiredIntoCoverage()
    {
        // Regression coverage for the same bug this test file exists for (see class remarks), on the newest
        // rule: orientation-restricted (OrientationRestrictedRule).
        var notRequested = ScreenActivityBuilder.For(Screen(Platform.Android, "shot.png", null, null, true, null));
        Assert.Equal("orientation check not requested (pass --orientation both)", notRequested.SkippedRuleIds["orientation-restricted"]);

        // The screen rotated (or was checked and looked the same, with the rule's own finding to show for
        // it, or the comparison was inconclusive) -- either way, OtherOrientation being set means the check
        // was attempted and completed, so it counts as ran.
        var checkedAndRotated = ScreenActivityBuilder.For(
            Screen(Platform.Android, "shot.png", null, null, true, null, otherOrientation: OrientationLabels.Landscape));
        Assert.Contains("orientation-restricted", checkedAndRotated.RanRuleIds);

        var checkedAndNotRotated = ScreenActivityBuilder.For(
            Screen(Platform.Android, "shot.png", null, null, true, null, otherOrientation: OrientationLabels.Landscape, findings: [OrientationFinding()]));
        Assert.Contains("orientation-restricted", checkedAndNotRotated.RanRuleIds);

        // Attempted but skipped for a reason (a physical iPhone, today): recorded as skipped with that reason,
        // not silently dropped.
        var skippedWithReason = ScreenActivityBuilder.For(
            Screen(Platform.Android, "shot.png", null, null, true, null, orientationSkippedReason: OrientationLabels.PhysicalIphoneNotSupportedReason));
        Assert.Equal(OrientationLabels.PhysicalIphoneNotSupportedReason, skippedWithReason.SkippedRuleIds["orientation-restricted"]);
    }

    [Fact]
    public void LargeTextWentToAnotherScreenReason_MatchesTheCollectorsConstant()
    {
        // Swipewalk.Core cannot reference Swipewalk.Collectors (see TextResizeNavigationRule's remarks), so
        // ScreenActivityBuilder duplicates this exact string. This test is the cross-project check that keeps
        // the duplicate honest if LargeTextCapture.DifferentScreen's wording ever changes.
        Assert.Equal(LargeTextCapture.DifferentScreen, ScreenActivityBuilder.LargeTextWentToAnotherScreenReason);
    }
}
