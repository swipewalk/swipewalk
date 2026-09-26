using Swipewalk.Core.Imaging;
using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class OrientationRestrictedRuleTests
{
    private static RgbaImage Image(int width, int height) => new(width, height, new byte[width * height * 4]);

    private static ScreenSnapshot Snapshot(
        RgbaImage? screenshot, ScreenSnapshot? orientation = null, string? label = null, string? otherLabel = null) => new()
    {
        Platform = Platform.Android,
        ScreenName = "Home",
        Root = new AccessibilityNode { Role = "window" },
        Screenshot = screenshot,
        Orientation = orientation,
        OrientationLabel = label,
        OtherOrientationLabel = otherLabel,
    };

    private static List<Finding> Evaluate(ScreenSnapshot snapshot) => new OrientationRestrictedRule().Evaluate(snapshot).ToList();

    [Fact]
    public void NoOrientationCapture_NoFinding()
    {
        Assert.Empty(Evaluate(Snapshot(Image(1080, 2400))));
    }

    [Fact]
    public void DeviceRotatedAndContentFollowed_NoFinding()
    {
        var other = new ScreenSnapshot { Platform = Platform.Android, ScreenName = "Home", Root = new AccessibilityNode { Role = "window" }, Screenshot = Image(2400, 1080) };
        var snapshot = Snapshot(Image(1080, 2400), other, "portrait", "landscape");

        Assert.Empty(Evaluate(snapshot));
    }

    [Fact]
    public void DeviceRotatedButContentStayedTheSameShape_NeedsReviewCiting1_3_4()
    {
        var other = new ScreenSnapshot { Platform = Platform.Android, ScreenName = "Home", Root = new AccessibilityNode { Role = "window" }, Screenshot = Image(1080, 2350) };
        var snapshot = Snapshot(Image(1080, 2400), other, "portrait", "landscape");

        var findings = Evaluate(snapshot);

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.Orientation], finding.Criteria);
        Assert.Contains("portrait", finding.Message);
        Assert.Contains("landscape", finding.Message);
        Assert.Contains("essential", finding.Message);
    }

    [Fact]
    public void InconclusiveComparison_NoScreenshotOrBoundsEitherSide_NoFinding()
    {
        var other = new ScreenSnapshot { Platform = Platform.Android, ScreenName = "Home", Root = new AccessibilityNode { Role = "window" }, Screenshot = null };
        var snapshot = Snapshot(null, other, "portrait", "landscape");

        Assert.Empty(Evaluate(snapshot));
    }
}
