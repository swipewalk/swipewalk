using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;

namespace Swipewalk.Core.Tests;

public class TextResizeLiveUpdateRuleTests
{
    private static AccessibilityNode Text(string text, double y, double height, double x = 0, double width = 300) => new()
    {
        Role = "text",
        VisibleText = text,
        Bounds = new Bounds(x, y, width, height),
    };

    private static ScreenSnapshot Snapshot(
        AccessibilityNode[] normal, AccessibilityNode[]? large, bool? appliedLive,
        AppFramework framework = AppFramework.Unknown, Platform platform = Platform.iOS, string? frameworkVersion = null) => new()
    {
        Platform = platform,
        ScreenName = "Screen",
        Framework = framework,
        FrameworkVersion = frameworkVersion,
        Root = new AccessibilityNode { Role = "window", Children = normal },
        LargeTextSetting = platform == Platform.iOS ? "iOS accessibility text size AX3 (about 235%)" : "Android font scale 2.0",
        LargeTextScale = platform == Platform.iOS ? 2.35 : 2.0,
        LargeTextAppliedLive = appliedLive,
        LargeText = large is null
            ? null
            : new ScreenSnapshot
            {
                Platform = platform,
                ScreenName = "Screen",
                Root = new AccessibilityNode { Role = "window", Children = large },
                LargeTextAppliedLive = appliedLive,
            },
    };

    private static List<Finding> Evaluate(ScreenSnapshot snapshot) => new TextResizeLiveUpdateRule().Evaluate(snapshot).ToList();

    [Fact]
    public void AppliedLive_ProducesNoAdvisory()
    {
        var findings = Evaluate(Snapshot(
            [Text("Title", 0, 20)], [Text("Title", 0, 47)], appliedLive: true));

        Assert.Empty(findings);
    }

    [Fact]
    public void AppliedAfterRestart_ProducesOnePlatformAdvisory()
    {
        var findings = Evaluate(Snapshot(
            [Text("Title", 0, 20)], [Text("Title", 0, 47)], appliedLive: false));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.PlatformAdvisory, finding.Kind);
        Assert.Empty(finding.Criteria);
        Assert.Equal(TextResizeRule.DynamicTypeGuideline, finding.PlatformGuideline);
        Assert.Equal("screen", finding.Role);
        Assert.Contains("didn't change size while the app was running", finding.Message);
        Assert.Contains("did after the app was restarted", finding.Message);
        Assert.Equal("largeText", finding.Details["screenshot"]);
    }

    [Fact]
    public void AppliedAfterRestart_OnIosMaui_MentionsTheFixedIssueAndTheVersion()
    {
        var findings = Evaluate(Snapshot(
            [Text("Title", 0, 20)], [Text("Title", 0, 47)], appliedLive: false,
            framework: AppFramework.Maui, platform: Platform.iOS));

        var finding = Assert.Single(findings);
        Assert.Contains("before 10.0.100", finding.Message);
        Assert.Contains("dotnet/maui#34445", finding.Message);
        Assert.Contains("Swipewalk can't read the app's MAUI version", finding.Message);
        Assert.Contains("custom handlers or code that sets FontSize", finding.Message);
        Assert.Equal(TextResizeRule.DynamicTypeGuideline, finding.PlatformGuideline);
    }

    [Fact]
    public void AppliedAfterRestart_OnIosMaui_KnownVersionBeforeFix_NamesTheVersionAndKeepsTheKnownIssue()
    {
        var findings = Evaluate(Snapshot(
            [Text("Title", 0, 20)], [Text("Title", 0, 47)], appliedLive: false,
            framework: AppFramework.Maui, platform: Platform.iOS, frameworkVersion: "10.0.60"));

        var finding = Assert.Single(findings);
        Assert.Contains("This app uses .NET MAUI 10.0.60", finding.Message);
        Assert.Contains("before 10.0.100", finding.Message);
        Assert.Contains("dotnet/maui#34445", finding.Message);
        Assert.Contains("updating Microsoft.Maui.Controls to 10.0.100 or later should make standard controls apply the new size while the app runs; rescan to confirm", finding.Message);
        Assert.DoesNotContain("Swipewalk can't read the app's MAUI version", finding.Message);
    }

    [Fact]
    public void AppliedAfterRestart_OnIosMaui_KnownVersionAtOrAfterFix_NamesTheVersionAndBlamesCustomCode()
    {
        var findings = Evaluate(Snapshot(
            [Text("Title", 0, 20)], [Text("Title", 0, 47)], appliedLive: false,
            framework: AppFramework.Maui, platform: Platform.iOS, frameworkVersion: "10.0.100"));

        var finding = Assert.Single(findings);
        Assert.Contains("This app uses .NET MAUI 10.0.100", finding.Message);
        Assert.Contains("includes the fix for live text-size changes", finding.Message);
        Assert.Contains("dotnet/maui#34445", finding.Message);
        Assert.Contains("custom handlers or code that sets FontSize are the likely cause", finding.Message);
        Assert.DoesNotContain("before 10.0.100 this is a known issue", finding.Message);
        Assert.DoesNotContain("Swipewalk can't read the app's MAUI version", finding.Message);
    }

    [Fact]
    public void AppliedAfterRestart_OnAndroidMaui_MentionsMainActivityConfigChanges()
    {
        var findings = Evaluate(Snapshot(
            [Text("Title", 0, 20)], [Text("Title", 0, 47)], appliedLive: false,
            framework: AppFramework.Maui, platform: Platform.Android));

        var finding = Assert.Single(findings);
        Assert.Contains("MainActivity", finding.Message);
        Assert.Contains("ConfigChanges.FontScale", finding.Message);
        Assert.DoesNotContain("dotnet/maui#34445", finding.Message);
        Assert.Equal(TextResizeLiveUpdateRule.AndroidConfigChangesGuideline, finding.PlatformGuideline);
    }

    [Fact]
    public void AppliedAfterRestart_OnAndroidOtherFramework_IsNeutral()
    {
        var findings = Evaluate(Snapshot(
            [Text("Title", 0, 20)], [Text("Title", 0, 47)], appliedLive: false,
            framework: AppFramework.Unknown, platform: Platform.Android));

        var finding = Assert.Single(findings);
        Assert.Contains("did not refresh", finding.Message);
        Assert.DoesNotContain("dotnet/maui", finding.Message);
        Assert.DoesNotContain("MainActivity", finding.Message);
        Assert.Equal(TextResizeLiveUpdateRule.AndroidConfigChangesGuideline, finding.PlatformGuideline);
    }

    [Fact]
    public void AppliedAfterRestart_OnIosOtherFramework_DoesNotClaimACause()
    {
        var findings = Evaluate(Snapshot(
            [Text("Title", 0, 20)], [Text("Title", 0, 47)], appliedLive: false,
            framework: AppFramework.Unknown, platform: Platform.iOS));

        var finding = Assert.Single(findings);
        Assert.DoesNotContain("dotnet/maui", finding.Message);
        Assert.DoesNotContain("known", finding.Message);
        Assert.Equal(TextResizeRule.DynamicTypeGuideline, finding.PlatformGuideline);
    }

    [Fact]
    public void AppliedLiveUnknown_ProducesNothing()
    {
        // Null: not recorded, or a platform (e.g. Android) whose own activity-recreation behavior makes
        // "live" ambiguous -- see the "android-font-scale-restart" limitation. Never guess.
        var findings = Evaluate(Snapshot(
            [Text("Title", 0, 20)], [Text("Title", 0, 20)], appliedLive: null));

        Assert.Empty(findings);
    }

    [Fact]
    public void NoLargeTextCapture_ProducesNothing()
    {
        var findings = Evaluate(Snapshot([Text("Title", 0, 20)], null, appliedLive: false));

        Assert.Empty(findings);
    }

    [Fact]
    public void AppliedAfterRestart_AndTextGrew_OnlyTheAdvisoryFires_NoResizeRuleFinding()
    {
        // The capture the flow ended with (snapshot.LargeText) is already the after-restart capture the
        // collectors substitute when LargeTextAppliedLive is false, and here the text did grow in it: the
        // "did not grow" 1.4.4 check (TextResizeRule) must find nothing, leaving only this advisory.
        var snapshot = Snapshot([Text("Pay a parking ticket", 0, 20)], [Text("Pay a parking ticket", 0, 47)], appliedLive: false);

        var resizeFindings = new TextResizeRule().Evaluate(snapshot).ToList();
        var liveUpdateFindings = new TextResizeLiveUpdateRule().Evaluate(snapshot).ToList();

        Assert.Empty(resizeFindings);
        var advisory = Assert.Single(liveUpdateFindings);
        Assert.Equal(FindingKind.PlatformAdvisory, advisory.Kind);
    }

    [Fact]
    public void AppliedAfterRestart_ButAfterRestartCaptureStillDidNotGrow_NoAdvisory_OnlyTheResizeRuleFindingFires()
    {
        // A restart happened (AppliedLive == false) but the after-restart capture still didn't grow: the
        // advisory's own claim ("it did after the app was restarted") would be false, so it must not fire.
        // Only TextResizeRule's 1.4.4 "did not get taller" finding is reported.
        var snapshot = Snapshot(
            [Text("Late fees apply", 0, 20)], [Text("Late fees apply", 0, 20)], appliedLive: false);

        var resizeFindings = new TextResizeRule().Evaluate(snapshot).ToList();
        var liveUpdateFindings = new TextResizeLiveUpdateRule().Evaluate(snapshot).ToList();

        var resizeFinding = Assert.Single(resizeFindings);
        Assert.Equal(FindingKind.NeedsReview, resizeFinding.Kind);
        Assert.Equal([Wcag.WcagCriteria.ResizeText], resizeFinding.Criteria);
        Assert.Empty(liveUpdateFindings);
    }

    [Fact]
    public void AppliedAfterRestart_ScreenLevelDidNotGrow_NoAdvisory_OnlyTheScreenLevelResizeFindingFires()
    {
        // The reported bug: a whole screen (>= 3 texts, most unchanged) that never picked up the setting,
        // even after a restart. TextResizeRule reports one screen-level "N of N did not get taller" finding;
        // the live-update advisory must not also claim the restart made it grow.
        var normal = new[] { Text("One", 0, 20), Text("Two", 30, 20), Text("Three", 60, 20), Text("Four", 90, 20) };
        var large = new[] { Text("One", 0, 20), Text("Two", 30, 20), Text("Three", 60, 20), Text("Four", 90, 20) };
        var snapshot = Snapshot(normal, large, appliedLive: false);

        var resizeFindings = new TextResizeRule().Evaluate(snapshot).ToList();
        var liveUpdateFindings = new TextResizeLiveUpdateRule().Evaluate(snapshot).ToList();

        var resizeFinding = Assert.Single(resizeFindings);
        Assert.Contains("4 of the 4 texts", resizeFinding.Message);
        Assert.Empty(liveUpdateFindings);
    }

    [Fact]
    public void AppliedAfterRestart_NoMatchingTextBetweenCaptures_NoAdvisory()
    {
        // Nothing to compare (e.g. the screen's text changed entirely between captures): no evidence the
        // after-restart capture grew, so the advisory must not guess that it did.
        var snapshot = Snapshot([Text("Old label", 0, 20)], [Text("New label", 0, 47)], appliedLive: false);

        Assert.Empty(new TextResizeLiveUpdateRule().Evaluate(snapshot));
    }

    [Fact]
    public void AppliedAfterRestart_ZeroSizeBoundsAreIgnored_NoAdvisoryWithoutOtherEvidence()
    {
        // A zero-size (e.g. collapsed/hidden) node in either capture must not be treated as evidence of
        // growth, and must not divide-by-zero.
        var snapshot = Snapshot(
            [Text("Hidden note", 0, 0)], [Text("Hidden note", 0, 0)], appliedLive: false);

        Assert.Empty(new TextResizeLiveUpdateRule().Evaluate(snapshot));
    }
}
