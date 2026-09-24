using Swipewalk.Collectors;
using Swipewalk.Collectors.Ios;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Rules;

namespace Swipewalk.Core.Tests;

/// <summary>
/// <see cref="TextSizeState"/>'s "on:9/12" wire format, round-tripped between the harness's Swift
/// TextSizeState, TextSizeRestore's marker file, and the one-shot/serve-mode textsize.json the harness
/// writes.
/// </summary>
public class TextSizeStateTests
{
    [Theory]
    [InlineData(true, 9, 12, "on:9/12")]
    [InlineData(false, 3, 7, "off:3/7")]
    [InlineData(true, 0, 1, "on:0/1")]
    public void ToString_MatchesTheHarnessWireFormat(bool toggleOn, int index, int steps, string expected)
    {
        var state = new TextSizeState(toggleOn, index, steps);

        Assert.Equal(expected, state.ToString());
    }

    [Theory]
    [InlineData("on:9/12", true, 9, 12)]
    [InlineData("off:3/7", false, 3, 7)]
    public void Parse_RoundTripsToString(string wire, bool toggleOn, int index, int steps)
    {
        var state = TextSizeState.Parse(wire);

        Assert.Equal(new TextSizeState(toggleOn, index, steps), state);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("maybe:9/12")]
    [InlineData("on:nine/12")]
    [InlineData("on:9")]
    [InlineData("on:9/12/1")]
    public void Parse_InvalidInput_ReturnsNull(string? wire)
    {
        Assert.Null(TextSizeState.Parse(wire));
    }

    [Fact]
    public void Ax3_IsSwitchOnIndex9Of12()
    {
        // AX3 (accessibilityExtraLarge, ~235%) is index 9 of 0...11 once "Larger
        // Accessibility Sizes" is on (12 steps: the 7 standard sizes plus AX1...AX5).
        Assert.Equal(new TextSizeState(true, 9, 12), TextSizeState.Ax3);
    }

    [Fact]
    public void RoundTripsThroughTextSizeRestore()
    {
        // TextSizeRestore stores an opaque string per device (Android font_scale, iOS Simulator content
        // size, or -- for a physical iPhone -- a TextSizeState). It must survive that round trip exactly,
        // since it is what the harness is told to restore to.
        var dir = Path.Combine(Path.GetTempPath(), "cf-restore-textsize-" + Guid.NewGuid().ToString("N"));
        try
        {
            var original = new TextSizeState(false, 2, 7);

            TextSizeRestore.Remember("00000000-TESTDEVICE", original.ToString(), dir);
            var pending = TextSizeRestore.Pending("00000000-TESTDEVICE", dir);

            Assert.Equal(original, TextSizeState.Parse(pending));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}

/// <summary>
/// <see cref="TextGrowth.LooksGrown"/>: the pure "did the text visibly grow" decision that gates the
/// live-vs-relaunch escalation on both iOS (Simulator and physical) and Android.
/// </summary>
public class TextGrowthTests
{
    private static ScreenSnapshot Screen(params (string Text, double Height)[] texts) => new()
    {
        Platform = Platform.iOS,
        ScreenName = "",
        Root = new AccessibilityNode
        {
            Role = "window",
            Children = [.. texts.Select(t => new AccessibilityNode { Role = "text", VisibleText = t.Text, Bounds = new Bounds(0, 0, 100, t.Height) })],
        },
    };

    [Fact]
    public void TextGrewPastMinimumGrowth_ReturnsTrue()
    {
        var before = Screen(("Pay a parking ticket", 20));
        var after = Screen(("Pay a parking ticket", 48)); // matches observed growth from 20.5pt -> 48.0pt

        Assert.True(TextGrowth.LooksGrown(before, after));
    }

    [Fact]
    public void TextUnchanged_ReturnsFalse()
    {
        // Observed on BuggyApp: its heading stayed at 27.5pt after activate() alone (MAUI
        // applies Dynamic Type only at launch).
        var before = Screen(("Pay a parking ticket", 27.5));
        var after = Screen(("Pay a parking ticket", 27.5));

        Assert.False(TextGrowth.LooksGrown(before, after));
    }

    [Fact]
    public void GrowthJustBelowThreshold_ReturnsFalse()
    {
        var before = Screen(("Label", 20));
        var after = Screen(("Label", 20 * (TextResizeRule.MinimumGrowth - 0.01)));

        Assert.False(TextGrowth.LooksGrown(before, after));
    }

    [Fact]
    public void NoMatchingText_ReturnsFalse()
    {
        var before = Screen(("Old label", 20));
        var after = Screen(("Completely different", 40));

        Assert.False(TextGrowth.LooksGrown(before, after));
    }

    [Fact]
    public void UsesTheMedianAcrossSeveralTexts_NotOutliers()
    {
        // Most text grew; one odd element (e.g. a fixed-size icon label) didn't. The median should still
        // say "grown".
        var before = Screen(("A", 20), ("B", 20), ("C", 20));
        var after = Screen(("A", 48), ("B", 47), ("C", 20));

        Assert.True(TextGrowth.LooksGrown(before, after));
    }
}

/// <summary>
/// <see cref="ScreenSnapshot.LargeTextMethod"/>/<see cref="ScreenSnapshot.LargeTextAppliedLive"/>/
/// <see cref="ScreenSnapshot.LargeTextRestartCaptured"/> flow through <see cref="RuleRunner"/> into
/// <see cref="ScreenResult"/> (and are absent when no large-text capture was made), and the HTML report
/// shows the method in the large-text caption.
/// </summary>
public class LargeTextMethodFieldTests
{
    private static readonly ScreenSnapshot Base = new()
    {
        Platform = Platform.iOS,
        ScreenName = "Home",
        Root = new AccessibilityNode { Role = "window" },
    };

    [Fact]
    public void RuleRunner_CopiesMethodAndLiveFields_WhenLargeTextWasCaptured()
    {
        var large = Base with
        {
            ScreenName = "Home",
            LargeTextMethod = "system setting",
            LargeTextAppliedLive = false,
        };
        var snapshot = Base with { LargeText = large, LargeTextSetting = "iOS accessibility text size AX3 (about 235%)", LargeTextMethod = "system setting", LargeTextAppliedLive = false };

        var result = new RuleRunner([]).Run(snapshot);

        Assert.Equal("system setting", result.LargeTextMethod);
        Assert.False(result.LargeTextAppliedLive);
    }

    [Fact]
    public void RuleRunner_LeavesMethodAndLiveNull_WhenLargeTextWasNotCaptured()
    {
        // Even if the fields were somehow set on the snapshot without a LargeText capture, the result
        // should not claim a method was used.
        var snapshot = Base with { LargeTextMethod = "system setting", LargeTextAppliedLive = true };

        var result = new RuleRunner([]).Run(snapshot);

        Assert.Null(result.LargeTextMethod);
        Assert.Null(result.LargeTextAppliedLive);
    }

    [Fact]
    public void RuleRunner_CopiesRestartCapturedField_WhenLargeTextWasCaptured()
    {
        var large = Base with { ScreenName = "Home", LargeTextMethod = "system setting" };
        var snapshot = Base with
        {
            LargeText = large, LargeTextSetting = "iOS accessibility text size AX3 (about 235%)",
            LargeTextMethod = "system setting", LargeTextRestartCaptured = true,
        };

        var result = new RuleRunner([]).Run(snapshot);

        Assert.True(result.LargeTextRestartCaptured);
    }

    [Fact]
    public void RuleRunner_LeavesRestartCapturedNull_WhenLargeTextWasNotCaptured()
    {
        // Even if the field were somehow set on the snapshot without a LargeText capture, the result should
        // not claim a restart was captured.
        var snapshot = Base with { LargeTextRestartCaptured = true };

        var result = new RuleRunner([]).Run(snapshot);

        Assert.Null(result.LargeTextRestartCaptured);
    }

    [Fact]
    public void HtmlReport_LargeTextCaption_MentionsTheMethod()
    {
        var largeScreenshot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "BuggyApp.Android", "large", "screenshot.png");
        Assert.True(File.Exists(largeScreenshot), $"Fixture missing: {largeScreenshot}");
        var report = new ScanReport
        {
            ToolVersion = "test",
            Screens =
            [
                new ScreenResult
                {
                    Platform = Platform.iOS, ScreenName = "Home", Findings = [],
                    LargeTextScreenshotPath = largeScreenshot,
                    LargeTextSetting = "iOS accessibility text size AX3 (about 235%)",
                    LargeTextMethod = "system setting",
                    LargeTextAppliedLive = false,
                },
            ],
        };

        var html = HtmlReport.Render(report);

        Assert.Contains("via system setting, applied after a restart", html);
    }

    [Fact]
    public void HtmlReport_LargeTextCaption_OmitsMethod_WhenNotRecorded()
    {
        var largeScreenshot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "BuggyApp.Android", "large", "screenshot.png");
        var report = new ScanReport
        {
            ToolVersion = "test",
            Screens =
            [
                new ScreenResult
                {
                    Platform = Platform.Android, ScreenName = "Home", Findings = [],
                    LargeTextScreenshotPath = largeScreenshot,
                    LargeTextSetting = "Android font scale 2.0 (200%)",
                },
            ],
        };

        var html = HtmlReport.Render(report);

        Assert.DoesNotContain("(via", html);
    }

    [Fact]
    public void HtmlReport_LargeTextCaption_NotesRestartCapture_WhenAppliedLiveIsUnknown()
    {
        // A force-stop + relaunch was captured but text still didn't grow (LargeTextAppliedLive stays null,
        // not falsely claimed as restart-applied): the caption still states a restart was captured, rather
        // than looking the same as a plain live capture.
        var largeScreenshot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "BuggyApp.Android", "large", "screenshot.png");
        var report = new ScanReport
        {
            ToolVersion = "test",
            Screens =
            [
                new ScreenResult
                {
                    Platform = Platform.iOS, ScreenName = "Home", Findings = [],
                    LargeTextScreenshotPath = largeScreenshot,
                    LargeTextSetting = "iOS accessibility text size AX3 (about 235%)",
                    LargeTextMethod = "system setting",
                    LargeTextRestartCaptured = true,
                },
            ],
        };

        var html = HtmlReport.Render(report);

        Assert.Contains("via system setting, captured after restarting the app", html);
    }

    [Fact]
    public void HtmlReport_LargeTextCaption_PerAppLaunchSettingFallback_DoesNotClaimAppliedAfterRestart()
    {
        // The per-app launch-argument fallback (IosCollector.CapturePhysicalLargeTextFallbackAsync) relaunches
        // the app fresh with the larger size already set, so live-vs-restart was never tested: LargeTextAppliedLive
        // is null even when the after-restart capture shows growth (see IosCollector.CapturedAfterRestart's
        // liveTested parameter). The caption must not claim "applied after a restart", which would misreport
        // something that was never observed; "per-app launch setting" states the method that was actually used.
        var largeScreenshot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "BuggyApp.Android", "large", "screenshot.png");
        var report = new ScanReport
        {
            ToolVersion = "test",
            Screens =
            [
                new ScreenResult
                {
                    Platform = Platform.iOS, ScreenName = "Home", Findings = [],
                    LargeTextScreenshotPath = largeScreenshot,
                    LargeTextSetting = "iOS accessibility text size AX3 (about 235%)",
                    LargeTextMethod = "per-app launch setting",
                    LargeTextAppliedLive = null,
                    LargeTextRestartCaptured = true,
                },
            ],
        };

        var html = HtmlReport.Render(report);

        Assert.DoesNotContain("applied after a restart", html);
        Assert.Contains("via per-app launch setting, captured after restarting the app", html);
    }
}
