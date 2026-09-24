using Swipewalk.Core.Coverage;
using Swipewalk.Core.Model;

namespace Swipewalk.Core.Tests;

public class ScreenActivityBuilderTests
{
    private static ScreenResult Screen(
        Platform platform = Platform.Android, string? screenshotPath = null, string? largeTextSetting = null,
        string? largeTextSkippedReason = null, IReadOnlyList<Finding>? findings = null,
        ScreenReaderCapture? screenReaderCapture = null) => new()
    {
        Platform = platform,
        ScreenName = "Home",
        ScreenshotPath = screenshotPath,
        LargeTextSetting = largeTextSetting,
        LargeTextSkippedReason = largeTextSkippedReason,
        Findings = findings ?? [],
        ScreenReaderCapture = screenReaderCapture,
    };

    [Fact]
    public void TreeWalkingRules_AlwaysCountAsRan_EvenWithNoScreenshotOrLargeTextCapture()
    {
        var activity = ScreenActivityBuilder.For(Screen());

        Assert.Contains("missing-name", activity.RanRuleIds);
        Assert.Contains("target-size", activity.RanRuleIds);
        Assert.Contains("identifier-name", activity.RanRuleIds);
        Assert.Contains("label-in-name", activity.RanRuleIds);
    }

    [Fact]
    public void NoScreenshot_TextContrastIsSkipped_WithReason()
    {
        var activity = ScreenActivityBuilder.For(Screen(screenshotPath: null));

        Assert.DoesNotContain("text-contrast", activity.RanRuleIds);
        Assert.Equal("no usable screenshot", activity.SkippedRuleIds["text-contrast"]);
    }

    [Fact]
    public void WithScreenshot_TextContrastRan()
    {
        var activity = ScreenActivityBuilder.For(Screen(screenshotPath: "shot.png"));

        Assert.Contains("text-contrast", activity.RanRuleIds);
        Assert.False(activity.SkippedRuleIds.ContainsKey("text-contrast"));
    }

    [Fact]
    public void NoLargeTextCapture_AndNoSkipReasonRecorded_UsesNotRequestedReason()
    {
        // e.g. a plain `swipewalk scan` without --large-text: LargeTextSkippedReason is null because the
        // check was never attempted, not because it failed.
        var activity = ScreenActivityBuilder.For(Screen(largeTextSetting: null, largeTextSkippedReason: null));

        Assert.DoesNotContain("text-resize", activity.RanRuleIds);
        Assert.Equal("large-text check not requested", activity.SkippedRuleIds["text-resize"]);
        Assert.DoesNotContain("large-text-lost-content", activity.RanRuleIds);
        Assert.Equal("large-text check not requested", activity.SkippedRuleIds["large-text-lost-content"]);
    }

    [Fact]
    public void LargeTextAttemptedButSkipped_UsesTheRecordedSkipReason()
    {
        var activity = ScreenActivityBuilder.For(Screen(largeTextSetting: null, largeTextSkippedReason: "the app wasn't in front"));

        Assert.Equal("the app wasn't in front", activity.SkippedRuleIds["text-resize"]);
        Assert.Equal("the app wasn't in front", activity.SkippedRuleIds["large-text-lost-content"]);
    }

    [Fact]
    public void LargeTextCaptured_TextResizeRan()
    {
        var activity = ScreenActivityBuilder.For(Screen(largeTextSetting: "font scale 2.0"));

        Assert.Contains("text-resize", activity.RanRuleIds);
        Assert.False(activity.SkippedRuleIds.ContainsKey("text-resize"));
        Assert.Contains("large-text-lost-content", activity.RanRuleIds);
        Assert.False(activity.SkippedRuleIds.ContainsKey("large-text-lost-content"));
    }

    [Fact]
    public void IosScreen_EngineCountsAsRan_RegardlessOfFindings()
    {
        // The Apple audit always runs as part of the iOS collector, whether or not it reported anything.
        var activity = ScreenActivityBuilder.For(Screen(platform: Platform.iOS));

        Assert.Contains("engine", activity.RanRuleIds);
    }

    [Fact]
    public void AndroidScreen_EngineDoesNotCountAsRan()
    {
        // No platform engine exists for Android; zero engine findings there means the audit doesn't exist,
        // not that it ran and found nothing.
        var activity = ScreenActivityBuilder.For(Screen(platform: Platform.Android));

        Assert.DoesNotContain("engine", activity.RanRuleIds);
    }

    [Fact]
    public void NoScreenReaderCapture_SkippedWithNotRequestedReason()
    {
        // The common case: --screen-reader wasn't passed, so ScreenSnapshot never set this field (see its
        // own remarks on why null means "not attempted" rather than "ran and found nothing").
        var activity = ScreenActivityBuilder.For(Screen(screenReaderCapture: null));

        Assert.DoesNotContain("screen-reader-capture", activity.RanRuleIds);
        Assert.Contains("--screen-reader", activity.SkippedRuleIds["screen-reader-capture"]);
    }

    [Fact]
    public void ScreenReaderCaptureWithItems_CountsAsRan()
    {
        var item = new ScreenReaderCaptureItem(1, "Button", null, null, null, null, null, null, null, "0", MatchConfidence.Exact);
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow, [item], Complete: false, NotCompleteReason: "only focusable/interactive elements were captured");
        var activity = ScreenActivityBuilder.For(Screen(screenReaderCapture: capture));

        Assert.Contains("screen-reader-capture", activity.RanRuleIds);
        Assert.False(activity.SkippedRuleIds.ContainsKey("screen-reader-capture"));
    }

    [Fact]
    public void ScreenReaderCaptureWithNoItems_SkippedWithItsReason_NotCountedAsRan()
    {
        // A non-null capture with no items means either the harness genuinely never ran (TalkBack not
        // installed, its settings screen not found) or it ran and captured nothing walkable -- unlike
        // AtfRan for the ATF harness, there's no separate flag to tell those apart, so this must not be
        // silently counted as "ran" either way (a real bug found in review: an install failure was
        // previously indistinguishable from a clean run that found nothing).
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "unknown", DateTimeOffset.UtcNow, [],
            Complete: false, NotCompleteReason: "TalkBack (Android Accessibility Suite) is not installed on this device");
        var activity = ScreenActivityBuilder.For(Screen(screenReaderCapture: capture));

        Assert.DoesNotContain("screen-reader-capture", activity.RanRuleIds);
        Assert.Equal("TalkBack (Android Accessibility Suite) is not installed on this device", activity.SkippedRuleIds["screen-reader-capture"]);
    }

    [Fact]
    public void NoScreenReaderCapture_OnIos_SkippedAsAndroidOnly()
    {
        // The flag does nothing on iOS (Android-only for now); the reason must say that, not "pass
        // --screen-reader" as if the option existed there too.
        var activity = ScreenActivityBuilder.For(Screen(platform: Platform.iOS, screenReaderCapture: null));

        Assert.DoesNotContain("screen-reader-capture", activity.RanRuleIds);
        Assert.Equal("screen-reader capture is Android-only for now", activity.SkippedRuleIds["screen-reader-capture"]);
    }

    [Fact]
    public void MultipleScreens_BuildsOneActivityPerScreen_InOrder()
    {
        var screens = new[] { Screen(screenshotPath: "a.png"), Screen(screenshotPath: null) };

        var activity = ScreenActivityBuilder.For(screens);

        Assert.Equal(2, activity.Screens.Count);
        Assert.Contains("text-contrast", activity.Screens[0].RanRuleIds);
        Assert.True(activity.Screens[1].SkippedRuleIds.ContainsKey("text-contrast"));
    }
}
