using Swipewalk.Core.Imaging;
using Swipewalk.Core.Model;

namespace Swipewalk.Core.Tests;

public class OrientationChangeDetectorTests
{
    private static RgbaImage Image(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        return new RgbaImage(width, height, pixels);
    }

    private static ScreenSnapshot Snapshot(RgbaImage? screenshot, Bounds? bounds = null) => new()
    {
        Platform = Platform.Android,
        ScreenName = "Home",
        Root = new AccessibilityNode { Role = "window", Bounds = bounds ?? default },
        Screenshot = screenshot,
    };

    [Fact]
    public void Rotated_TrueWhenScreenshotAspectFlipsFromPortraitToLandscape()
    {
        var before = Snapshot(Image(1080, 2400)); // portrait
        var after = Snapshot(Image(2400, 1080)); // landscape

        Assert.True(OrientationChangeDetector.Rotated(before, after));
    }

    [Fact]
    public void Rotated_FalseWhenBothScreenshotsAreTheSameAspectCategory()
    {
        var before = Snapshot(Image(1080, 2400));
        var after = Snapshot(Image(1080, 2340)); // still taller than wide -- device rotated but content didn't

        Assert.False(OrientationChangeDetector.Rotated(before, after));
    }

    [Fact]
    public void Rotated_FallsBackToRootBoundsWhenNoScreenshot()
    {
        var before = Snapshot(null, new Bounds(0, 0, 1080, 2400));
        var after = Snapshot(null, new Bounds(0, 0, 2400, 1080));

        Assert.True(OrientationChangeDetector.Rotated(before, after));
    }

    [Fact]
    public void Rotated_NullWhenNeitherCaptureHasUsableEvidence()
    {
        var before = Snapshot(null);
        var after = Snapshot(null);

        Assert.Null(OrientationChangeDetector.Rotated(before, after));
    }

    [Fact]
    public void Rotated_NullWhenOnlyOneCaptureHasEvidence()
    {
        // Never guesses about the other capture just because this one has a screenshot.
        var before = Snapshot(Image(1080, 2400));
        var after = Snapshot(null);

        Assert.Null(OrientationChangeDetector.Rotated(before, after));
    }
}
