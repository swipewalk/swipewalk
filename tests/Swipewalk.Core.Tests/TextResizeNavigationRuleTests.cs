using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;

namespace Swipewalk.Core.Tests;

public class TextResizeNavigationRuleTests
{
    // This rule fires from ScreenSnapshot.LargeTextWentToAnotherScreen alone; it doesn't inspect the tree
    // (there's nothing to point at -- the app landed on a different screen entirely). A root with hidden,
    // disabled and zero-size nodes is included below to confirm those don't change that.
    private static AccessibilityNode RootWithMixedNodes() => new()
    {
        Role = "window",
        Children =
        [
            new AccessibilityNode { Role = "text", VisibleText = "Welcome", Bounds = new Bounds(0, 0, 200, 20) },
            new AccessibilityNode { Role = "text", VisibleText = "Hidden", IsAccessible = false, Bounds = new Bounds(0, 20, 200, 20) },
            new AccessibilityNode { Role = "button", Label = "Disabled", IsEnabled = false, Bounds = new Bounds(0, 40, 100, 40) },
            new AccessibilityNode { Role = "text", VisibleText = "Zero size", Bounds = new Bounds(0, 80, 0, 0) },
        ],
    };

    private static ScreenSnapshot Snapshot(Platform platform, bool? wentToAnotherScreen) => new()
    {
        Platform = platform,
        ScreenName = "Settings",
        Root = RootWithMixedNodes(),
        LargeTextWentToAnotherScreen = wentToAnotherScreen,
    };

    private static List<Finding> Evaluate(ScreenSnapshot snapshot) => new TextResizeNavigationRule().Evaluate(snapshot).ToList();

    [Fact]
    public void AndroidWentToAnotherScreen_ProducesOnePlatformAdvisory()
    {
        var findings = Evaluate(Snapshot(Platform.Android, wentToAnotherScreen: true));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.PlatformAdvisory, finding.Kind);
        Assert.Empty(finding.Criteria);
        Assert.Equal(TextResizeNavigationRule.AndroidPreserveStateGuideline, finding.PlatformGuideline);
        Assert.Equal("screen", finding.Role);
        // "Captured a different screen", not "the app showed a different screen": the detection is a
        // heuristic (see the rule's own remarks), so the message states what Swipewalk saw, hedged with
        // "probably", not an assertion of fact (wcag-reviewer, 2026-09-23).
        Assert.Contains("captured a different screen", finding.Message);
        Assert.Contains("probably lost their place", finding.Message);
        Assert.DoesNotContain("did grow", finding.Message);
    }

    [Fact]
    public void NotObserved_ProducesNothing()
    {
        Assert.Empty(Evaluate(Snapshot(Platform.Android, wentToAnotherScreen: null)));
        Assert.Empty(Evaluate(Snapshot(Platform.Android, wentToAnotherScreen: false)));
    }

    [Fact]
    public void Ios_DoesNotFire_EvenWhenObserved()
    {
        // Restricted to Android: the verified evidence (and the Android configuration-changes guideline this
        // finding cites) is Android-specific; Swipewalk has not verified an equivalent scenario or citation on
        // iOS, so it does not guess one -- see TextResizeNavigationRule's class remarks.
        Assert.Empty(Evaluate(Snapshot(Platform.iOS, wentToAnotherScreen: true)));
    }

    [Fact]
    public void RuleId_IsTextResizeNavigation()
    {
        Assert.Equal("text-resize-navigation", new TextResizeNavigationRule().Id);
    }
}
