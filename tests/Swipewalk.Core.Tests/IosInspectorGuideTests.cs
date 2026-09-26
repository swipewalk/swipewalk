using Swipewalk.Collectors.Ios;
using Swipewalk.Core.Model;

namespace Swipewalk.Core.Tests;

/// <summary>
/// <see cref="IosCollector.RunInspectorCaptureAsync"/> is the orchestration point for --screen-reader's
/// Accessibility Inspector route: it must never throw, and must produce a clearly incomplete capture (not a
/// silent empty one) whenever the guided setup wasn't done, whatever the reason.
/// </summary>
public class IosInspectorGuideTests
{
    private static ScreenSnapshot Snapshot() => new()
    {
        Platform = Platform.iOS,
        ScreenName = "Test",
        Root = new AccessibilityNode { Role = "container" },
    };

    [Fact]
    public async Task NoGuide_NonInteractiveRun_IsIncompleteWithAClearReason()
    {
        var capture = await IosCollector.RunInspectorCaptureAsync(Snapshot(), guide: null);

        Assert.Equal(ScreenReaderSource.AccessibilityInspector, capture.Source);
        Assert.False(capture.Complete);
        Assert.Empty(capture.Items);
        Assert.Contains("no interactive prompt", capture.NotCompleteReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuideDeclines_IsIncompleteWithAClearReason_AndNeverRunsTheWalk()
    {
        var capture = await IosCollector.RunInspectorCaptureAsync(Snapshot(), _ => Task.FromResult(false));

        Assert.False(capture.Complete);
        Assert.Empty(capture.Items);
        Assert.Contains("declined", capture.NotCompleteReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuideConsents_ReachesTheRealWalk_RatherThanSkippingBeforeIt()
    {
        // What this test checks is that the guide answering true leads to a real walk attempt, not another
        // skip -- not what that walk itself finds, which depends on this machine's live Accessibility
        // permission and Accessibility Inspector state (see IosInspectorWalkTests' remarks): on a dev Mac
        // with the permission granted and the Inspector already pointed at a target, the walk can genuinely
        // succeed; otherwise it fails cleanly. Either way, the capture must not carry either of the two
        // reasons that mean the walk was never attempted at all.
        var capture = await IosCollector.RunInspectorCaptureAsync(Snapshot(), _ => Task.FromResult(true));

        var reason = capture.NotCompleteReason ?? "";
        Assert.DoesNotContain("no interactive prompt", reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("declined", reason, StringComparison.OrdinalIgnoreCase);
    }
}
