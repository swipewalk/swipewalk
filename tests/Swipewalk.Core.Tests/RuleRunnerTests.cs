using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class RuleRunnerTests
{
    private static readonly ScreenSnapshot Snapshot = new()
    {
        Platform = Platform.Android,
        ScreenName = "Checkout",
        ScreenshotPath = "checkout.png",
        Root = new AccessibilityNode { Role = "window", Children = [new AccessibilityNode { Role = "button" }] },
    };

    private sealed class FakeRule(string id, Func<ScreenSnapshot, IEnumerable<Finding>> evaluate) : IRule
    {
        public string Id => id;
        public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot) => evaluate(snapshot);
    }

    private static Finding ButtonFinding(FindingKind kind, params WcagCriterion[] criteria) => new()
    {
        RuleId = "fake",
        Kind = kind,
        Message = "Automated check found an issue.",
        Criteria = criteria,
        PlatformGuideline = kind == FindingKind.PlatformAdvisory ? "Android: touch targets at least 48×48 dp" : null,
        NodePath = "0",
        Role = "button",
    };

    [Fact]
    public void Run_CollectsFindingsFromAllRules_AndCopiesScreenInfo()
    {
        var runner = new RuleRunner(
        [
            new FakeRule("a", _ => [ButtonFinding(FindingKind.WcagIssue, WcagCriteria.NameRoleValue)]),
            new FakeRule("b", _ => [ButtonFinding(FindingKind.PlatformAdvisory)]),
            new FakeRule("c", _ => []),
        ]);

        var result = runner.Run(Snapshot);

        Assert.Equal(Platform.Android, result.Platform);
        Assert.Equal("Checkout", result.ScreenName);
        Assert.Equal("checkout.png", result.ScreenshotPath);
        Assert.Equal([FindingKind.WcagIssue, FindingKind.PlatformAdvisory], result.Findings.Select(f => f.Kind));
    }

    [Fact]
    public void Run_Throws_WhenWcagFindingHasNoCriterion()
    {
        var runner = new RuleRunner([new FakeRule("bad", _ => [ButtonFinding(FindingKind.WcagIssue)])]);

        var ex = Assert.Throws<InvalidOperationException>(() => runner.Run(Snapshot));
        Assert.Contains("bad", ex.Message);
    }

    [Fact]
    public void Run_Throws_WhenAdvisoryCitesWcagCriterion()
    {
        var runner = new RuleRunner(
            [new FakeRule("mixed", _ => [ButtonFinding(FindingKind.PlatformAdvisory, WcagCriteria.TargetSizeMinimum)])]);

        Assert.Throws<InvalidOperationException>(() => runner.Run(Snapshot));
    }
}
