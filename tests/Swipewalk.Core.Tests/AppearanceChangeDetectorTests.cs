using Swipewalk.Core.Imaging;
using Swipewalk.Core.Model;

namespace Swipewalk.Core.Tests;

public class AppearanceChangeDetectorTests
{
    private static RgbaImage SolidImage(byte level)
    {
        var pixels = new byte[4 * 4 * 4]; // 4x4
        for (var i = 0; i < pixels.Length; i += 4)
            (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (level, level, level, 255);
        return new RgbaImage(4, 4, pixels);
    }

    private static ScreenSnapshot Snapshot(RgbaImage? screenshot, string text = "Hello") => new()
    {
        Platform = Platform.Android,
        ScreenName = "Home",
        Root = new AccessibilityNode { Role = "text", VisibleText = text },
        Screenshot = screenshot,
    };

    [Fact]
    public void LooksUnchanged_TrueWhenSameTreeAndBrightnessBarelyDiffers()
    {
        var before = Snapshot(SolidImage(240)); // light background
        var after = Snapshot(SolidImage(238)); // essentially the same

        Assert.True(AppearanceChangeDetector.LooksUnchanged(before, after));
    }

    [Fact]
    public void LooksUnchanged_FalseWhenBrightnessActuallyChanged()
    {
        var before = Snapshot(SolidImage(240)); // light
        var after = Snapshot(SolidImage(20)); // dark

        Assert.False(AppearanceChangeDetector.LooksUnchanged(before, after));
    }

    [Fact]
    public void LooksUnchanged_FalseWhenTheScreenItselfIsDifferent()
    {
        // Same brightness, but different visible text -- likely a different screen (e.g. the app restarted
        // to its launch screen), not the same screen in the other appearance.
        var before = Snapshot(SolidImage(240), "Home");
        var after = Snapshot(SolidImage(240), "Login");

        Assert.False(AppearanceChangeDetector.LooksUnchanged(before, after));
    }

    [Fact]
    public void LooksUnchanged_FalseWhenAScreenshotIsMissing()
    {
        var before = Snapshot(SolidImage(240));
        var after = Snapshot(null);

        Assert.False(AppearanceChangeDetector.LooksUnchanged(before, after));
    }
}
