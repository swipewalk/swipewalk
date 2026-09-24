using Swipewalk.Core.Imaging;
using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class TextContrastRuleTests
{
    private const int Width = 20;
    private const int Height = 10;
    private static readonly Rgb White = new(0xFF, 0xFF, 0xFF);

    private static RgbaImage BuildImage(Rgb background, (int X, int Y, int W, int H) block, Rgb blockColor)
    {
        var pixels = new byte[Width * Height * 4];
        for (var i = 0; i < Width * Height; i++)
        {
            pixels[i * 4] = background.R;
            pixels[i * 4 + 1] = background.G;
            pixels[i * 4 + 2] = background.B;
            pixels[i * 4 + 3] = 255;
        }
        for (var y = block.Y; y < block.Y + block.H; y++)
            for (var x = block.X; x < block.X + block.W; x++)
            {
                var i = (y * Width + x) * 4;
                pixels[i] = blockColor.R;
                pixels[i + 1] = blockColor.G;
                pixels[i + 2] = blockColor.B;
                pixels[i + 3] = 255;
            }
        return new RgbaImage(Width, Height, pixels);
    }

    private static AccessibilityNode TextNode(bool isAccessible = true, bool isEnabled = true, Bounds? bounds = null) => new()
    {
        Role = "text",
        VisibleText = "Hi",
        IsAccessible = isAccessible,
        IsEnabled = isEnabled,
        Bounds = bounds ?? new Bounds(0, 0, Width, Height),
    };

    private static List<Finding> Evaluate(RgbaImage? screenshot, AccessibilityNode node) => new TextContrastRule().Evaluate(new ScreenSnapshot
    {
        Platform = Platform.Android,
        ScreenName = "Screen",
        Root = new AccessibilityNode { Role = "window", Children = [node] },
        Screenshot = screenshot,
    }).ToList();

    [Fact]
    public void LowContrastGray_A_A_A_A_A_A_ReportsWcagIssue()
    {
        // #AAAAAA on white is about 2.32:1, below both the 4.5:1 and 3:1 thresholds.
        var image = BuildImage(White, (0, 0, 4, 2), new Rgb(0xAA, 0xAA, 0xAA));

        var findings = Evaluate(image, TextNode());

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.WcagIssue, finding.Kind);
        Assert.Equal([WcagCriteria.ContrastMinimum], finding.Criteria);
    }

    [Fact]
    public void Gray_767676_MeetsNormalTextMinimum_NoFinding()
    {
        // #767676 on white is about 4.54:1, at/above the 4.5:1 normal-text minimum.
        var image = BuildImage(White, (0, 0, 4, 2), new Rgb(0x76, 0x76, 0x76));

        var findings = Evaluate(image, TextNode());

        Assert.Empty(findings);
    }

    [Fact]
    public void RatioBetweenLargeAndNormalMinimums_NeedsReview()
    {
        // #8A8A8A on white is about 3.45:1: fails the normal-text minimum but meets the
        // large-text minimum, and text size can't be read from the accessibility tree.
        var image = BuildImage(White, (0, 0, 4, 2), new Rgb(0x8A, 0x8A, 0x8A));

        var findings = Evaluate(image, TextNode());

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.ContrastMinimum], finding.Criteria);
    }

    [Fact]
    public void NoScreenshot_ProducesNoFindings()
    {
        var findings = Evaluate(null, TextNode());

        Assert.Empty(findings);
    }

    [Fact]
    public void HiddenFromAccessibilityTree_IsStillMeasured()
    {
        // The rule reads screenshot pixels only; it does not check IsAccessible, so a low
        // contrast region is still reported even if hidden from assistive technology.
        var image = BuildImage(White, (0, 0, 4, 2), new Rgb(0xAA, 0xAA, 0xAA));

        var findings = Evaluate(image, TextNode(isAccessible: false));

        Assert.Single(findings);
    }

    [Fact]
    public void DisabledNode_IsExcluded()
    {
        var image = BuildImage(White, (0, 0, 4, 2), new Rgb(0xAA, 0xAA, 0xAA));

        var findings = Evaluate(image, TextNode(isEnabled: false));

        Assert.Empty(findings);
    }

    [Fact]
    public void ZeroSizeBounds_IsExcluded()
    {
        var image = BuildImage(White, (0, 0, 4, 2), new Rgb(0xAA, 0xAA, 0xAA));

        var findings = Evaluate(image, TextNode(bounds: new Bounds(0, 0, 0, 0)));

        Assert.Empty(findings);
    }

    [Fact]
    public void Ratio_BlackAndWhite_Is21To1()
    {
        var black = new Rgb(0, 0, 0);

        var ratio = Contrast.Ratio(black, White);

        Assert.Equal(21, ratio, 3);
    }

    [Fact]
    public void BlockedScreenshot_ReportsOneReviewItemInsteadOfMeasuring()
    {
        var black = new RgbaImage(20, 10, Enumerable.Range(0, 20 * 10 * 4).Select(i => i % 4 == 3 ? (byte)255 : (byte)0).ToArray());
        var snapshot = new ScreenSnapshot
        {
            Platform = Platform.Android,
            ScreenName = "Login",
            Root = new AccessibilityNode { Role = "window", Children = [new AccessibilityNode { Role = "text", VisibleText = "Password", Bounds = new Bounds(0, 0, 20, 10) }] },
            Screenshot = black,
        };

        var finding = Assert.Single(new TextContrastRule().Evaluate(snapshot));
        Assert.Equal("screen", finding.Role);
        Assert.Contains("blocked the screenshot", finding.Message);
    }
}
