using Swipewalk.Core.Imaging;
using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class IconContrastRuleTests
{
    private const int Width = 20;
    private const int Height = 20;
    private static readonly Rgb White = new(0xFF, 0xFF, 0xFF);

    private static RgbaImage BuildImage(Rgb background, Rgb blockColor)
    {
        var pixels = new byte[Width * Height * 4];
        for (var i = 0; i < Width * Height; i++)
        {
            pixels[i * 4] = background.R;
            pixels[i * 4 + 1] = background.G;
            pixels[i * 4 + 2] = background.B;
            pixels[i * 4 + 3] = 255;
        }
        // A block in the middle representing the icon's glyph pixels.
        for (var y = 5; y < 15; y++)
            for (var x = 5; x < 15; x++)
            {
                var i = (y * Width + x) * 4;
                pixels[i] = blockColor.R;
                pixels[i + 1] = blockColor.G;
                pixels[i + 2] = blockColor.B;
            }
        return new RgbaImage(Width, Height, pixels);
    }

    private static AccessibilityNode IconButton(
        string? label = "Search", bool isInteractive = true, bool isAccessible = true, bool isEnabled = true,
        Bounds? bounds = null) => new()
    {
        Role = "button",
        Label = label,
        IsInteractive = isInteractive,
        IsAccessible = isAccessible,
        IsEnabled = isEnabled,
        Bounds = bounds ?? new Bounds(0, 0, Width, Height),
    };

    private static List<Finding> Evaluate(Platform platform, RgbaImage? screenshot, AccessibilityNode node) =>
        new IconContrastRule().Evaluate(new ScreenSnapshot
        {
            Platform = platform,
            ScreenName = "Screen",
            Root = new AccessibilityNode { Role = "window", Children = [node] },
            Screenshot = screenshot,
        }).ToList();

    [Fact]
    public void LowContrastIcon_iOS_NeedsReviewUnderNonTextContrast()
    {
        // #AAAAAA on white is about 2.32:1, below the 3:1 minimum.
        var image = BuildImage(White, new Rgb(0xAA, 0xAA, 0xAA));

        var findings = Evaluate(Platform.iOS, image, IconButton());

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.NonTextContrast], finding.Criteria);
    }

    [Fact]
    public void HighContrastIcon_NoFinding()
    {
        var image = BuildImage(White, new Rgb(0, 0, 0));

        var findings = Evaluate(Platform.iOS, image, IconButton());

        Assert.Empty(findings);
    }

    [Fact]
    public void AndroidPlatform_NeverEvaluated()
    {
        // Android icon contrast is covered by Google ATF's ImageContrastCheck instead (see AtfIssueRule).
        var image = BuildImage(White, new Rgb(0xAA, 0xAA, 0xAA));

        var findings = Evaluate(Platform.Android, image, IconButton());

        Assert.Empty(findings);
    }

    [Fact]
    public void NonInteractiveImage_NotFlagged()
    {
        // A purely decorative or informative (but non-interactive) image is out of scope for this rule;
        // 1.4.11 only requires contrast for UI components and their state.
        var image = BuildImage(White, new Rgb(0xAA, 0xAA, 0xAA));

        var findings = Evaluate(Platform.iOS, image, IconButton(isInteractive: false));

        Assert.Empty(findings);
    }

    [Fact]
    public void TextButton_NotFlagged()
    {
        var image = BuildImage(White, new Rgb(0xAA, 0xAA, 0xAA));
        var node = new AccessibilityNode
        {
            Role = "button", VisibleText = "Save", IsInteractive = true, Bounds = new Bounds(0, 0, Width, Height),
        };

        var findings = Evaluate(Platform.iOS, image, node);

        Assert.Empty(findings);
    }

    [Fact]
    public void HiddenFromAccessibilityTree_NotFlagged()
    {
        var image = BuildImage(White, new Rgb(0xAA, 0xAA, 0xAA));

        var findings = Evaluate(Platform.iOS, image, IconButton(isAccessible: false));

        Assert.Empty(findings);
    }

    [Fact]
    public void DisabledControl_NotFlagged()
    {
        var image = BuildImage(White, new Rgb(0xAA, 0xAA, 0xAA));

        var findings = Evaluate(Platform.iOS, image, IconButton(isEnabled: false));

        Assert.Empty(findings);
    }

    [Fact]
    public void ZeroSizeBounds_NotFlagged()
    {
        var image = BuildImage(White, new Rgb(0xAA, 0xAA, 0xAA));

        var findings = Evaluate(Platform.iOS, image, IconButton(bounds: new Bounds(0, 0, 0, 0)));

        Assert.Empty(findings);
    }

    [Fact]
    public void NoScreenshot_NoFindings()
    {
        var findings = Evaluate(Platform.iOS, null, IconButton());

        Assert.Empty(findings);
    }

    [Fact]
    public void BlockedScreenshot_NoFindings()
    {
        // Black screenshot with a node that has meaningful visible text elsewhere on the tree is what
        // ScreenSnapshot.ScreenshotBlocked detects; TextContrastRule already reports this screen-level, so
        // icon-contrast stays silent rather than duplicate it.
        var black = new RgbaImage(Width, Height, Enumerable.Range(0, Width * Height * 4).Select(i => i % 4 == 3 ? (byte)255 : (byte)0).ToArray());
        var snapshot = new ScreenSnapshot
        {
            Platform = Platform.iOS,
            ScreenName = "Screen",
            Root = new AccessibilityNode
            {
                Role = "window",
                Children =
                [
                    IconButton(),
                    new AccessibilityNode { Role = "text", VisibleText = "Password", IsAccessible = true, Bounds = new Bounds(0, 0, 20, 10) },
                ],
            },
            Screenshot = black,
        };

        Assert.Empty(new IconContrastRule().Evaluate(snapshot));
    }
}
