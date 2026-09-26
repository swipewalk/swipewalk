using Swipewalk.Collectors.Ios;
using Swipewalk.Core.Model;

namespace Swipewalk.Core.Tests;

/// <summary>
/// <see cref="IosVoiceOverCaptionCapture.Build"/> matches a person-driven VoiceOver session's own captions to
/// <see cref="ScreenSnapshot"/> tree nodes by focus-rect overlap, then by the caption text containing a
/// candidate's accessible name -- pure, without a Mac, device or VoiceOver session. See
/// <see cref="IosVoiceOverCaptionWalkTests"/> for the process boundary (real OCR/detection against synthetic
/// screenshots) this deliberately stays on the pure side of.
/// </summary>
public class IosVoiceOverCaptionCaptureTests
{
    private static AccessibilityNode Button(double x, double y, double w, double h, string? label) => new()
    {
        Role = "button",
        Label = label,
        IsInteractive = true,
        IsFocusable = true,
        Bounds = new Bounds(x, y, w, h),
    };

    private static ScreenSnapshot Snapshot(double pixelScale, params AccessibilityNode[] children) => new()
    {
        Platform = Platform.iOS,
        ScreenName = "Test",
        PixelScale = pixelScale,
        Root = new AccessibilityNode { Role = "container", Children = children },
    };

    private static VoiceOverCaptionItem Item(int order, string spokenText, double elapsedSeconds = 1, VoiceOverRect? focusRect = null) =>
        new(order, spokenText, elapsedSeconds, focusRect);

    private static VoiceOverCaptionCaptureRawResult Result(params VoiceOverCaptionItem[] items) =>
        new(Ok: true, Error: null, items, Complete: false, NotCompleteReason: "a person drove this VoiceOver session by hand", ToolVersion: "0.1-draft", ImageWidth: 1170, ImageHeight: 2532);

    [Fact]
    public void NotOk_ReturnsSkippedCaptureWithTheError()
    {
        var raw = new VoiceOverCaptionCaptureRawResult(Ok: false, Error: "the device disconnected", [], Complete: false, "the device disconnected", null, null, null);

        var capture = IosVoiceOverCaptionCapture.Build(Snapshot(1, Button(0, 0, 100, 30, "Search")), raw);

        Assert.Equal(ScreenReaderSource.VoiceOverCaptions, capture.Source);
        Assert.Empty(capture.Items);
        Assert.False(capture.Complete);
        Assert.Equal("the device disconnected", capture.NotCompleteReason);
    }

    [Fact]
    public void NoItems_KeepsCompleteAndReasonFromTheRawResult()
    {
        var raw = new VoiceOverCaptionCaptureRawResult(Ok: true, Error: null, [], Complete: false, NotCompleteReason: "no VoiceOver captions were captured", ToolVersion: "0.1-draft", null, null);

        var capture = IosVoiceOverCaptionCapture.Build(Snapshot(1, Button(0, 0, 100, 30, "Search")), raw);

        Assert.Empty(capture.Items);
        Assert.False(capture.Complete);
        Assert.Equal("no VoiceOver captions were captured", capture.NotCompleteReason);
    }

    [Fact]
    public void NeverReportsComplete_EvenWhenEveryItemMatchesConfidently()
    {
        // Design decision (see IosVoiceOverCaptionCapture.Build's remarks): unlike a scripted walk, nothing
        // here can tell whether the person covered the whole screen, so Complete is never true for this source
        // regardless of how well matched the items are.
        var snapshot = Snapshot(1, Button(0, 0, 100, 30, "Search"));
        var raw = Result(Item(1, "Search, Button", focusRect: new VoiceOverRect(0, 0, 100, 30)));

        var capture = IosVoiceOverCaptionCapture.Build(snapshot, raw);

        Assert.False(capture.Complete);
        Assert.NotNull(capture.NotCompleteReason);
    }

    [Fact]
    public void FocusRectStronglyOverlappingANode_MatchesLikely_AfterUndoingPixelScale()
    {
        // pixelScale 3: the tree's own Bounds are in points, the focus rect comes back in screenshot pixels.
        var snapshot = Snapshot(3, Button(x: 10, y: 20, w: 100, h: 40, label: "Submit"));
        var raw = Result(Item(1, "Submit, Button", focusRect: new VoiceOverRect(X: 30, Y: 60, W: 300, H: 120)));

        var capture = IosVoiceOverCaptionCapture.Build(snapshot, raw);

        var only = Assert.Single(capture.Items);
        Assert.Equal("0", only.MatchedNodePath);
        Assert.Equal(MatchConfidence.Likely, only.MatchConfidence);
        Assert.Equal("Submit, Button", only.SpokenText);
    }

    [Fact]
    public void FocusRectOverANonMatchingArea_FallsBackToText()
    {
        var snapshot = Snapshot(1,
            Button(x: 0, y: 0, w: 100, h: 30, label: "Search"),
            Button(x: 0, y: 500, w: 100, h: 30, label: "Cancel"));
        // The rect is nowhere near either button (far off-screen), so the overlap floor rejects it, and the
        // caption text uniquely names "Cancel".
        var raw = Result(Item(1, "Cancel, Button", focusRect: new VoiceOverRect(X: 900, Y: 900, W: 10, H: 10)));

        var capture = IosVoiceOverCaptionCapture.Build(snapshot, raw);

        var only = Assert.Single(capture.Items);
        Assert.Equal("1", only.MatchedNodePath);
        Assert.Equal(MatchConfidence.Weak, only.MatchConfidence);
    }

    [Fact]
    public void NoFocusRectAndAmbiguousText_IsUnmatched()
    {
        var snapshot = Snapshot(1, Button(0, 0, 100, 30, "Search"), Button(0, 40, 100, 30, "Search"));
        var raw = Result(Item(1, "Search, Button"));

        var capture = IosVoiceOverCaptionCapture.Build(snapshot, raw);

        var only = Assert.Single(capture.Items);
        Assert.Null(only.MatchedNodePath);
        Assert.Equal(MatchConfidence.None, only.MatchConfidence);
    }

    [Fact]
    public void ConsecutiveItems_KeepTheirOwnOrderAndTimestampFromElapsedSeconds()
    {
        var snapshot = Snapshot(1, Button(0, 0, 100, 30, "Search"), Button(0, 40, 100, 30, "Cancel"));
        var startedAt = new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);
        var raw = Result(
            Item(1, "Search, Button", elapsedSeconds: 2.5),
            Item(2, "Cancel, Button", elapsedSeconds: 5.0));

        var capture = IosVoiceOverCaptionCapture.Build(snapshot, raw, startedAt);

        Assert.Equal(2, capture.Items.Count);
        Assert.Equal(1, capture.Items[0].Order);
        Assert.Equal(startedAt.AddSeconds(2.5), capture.Items[0].Timestamp);
        Assert.Equal(2, capture.Items[1].Order);
        Assert.Equal(startedAt.AddSeconds(5.0), capture.Items[1].Timestamp);
    }
}
