using Swipewalk.Collectors;
using Swipewalk.Collectors.Ios;

namespace Swipewalk.Core.Tests;

/// <summary>
/// <see cref="BaselineTextSize"/>: the safeguard that checks a device's text size right after each
/// large-text flow reads it and before it changes anything, so a normal-size capture taken while the device
/// is already enlarged isn't mistaken for a default-size capture (which would make a large-vs-large
/// comparison wrongly look like text "did not grow").
/// </summary>
public class BaselineTextSizeTests
{
    [Theory]
    [InlineData(1.0)] // default
    [InlineData(1.005)] // tiny float noise around the default
    [InlineData(0.85)] // Android's "small" step is below the default, not enlarged
    public void IsAndroidEnlarged_AtOrBelowDefault_ReturnsFalse(double fontScale) =>
        Assert.False(BaselineTextSize.IsAndroidEnlarged(fontScale));

    [Theory]
    [InlineData(1.15)]
    [InlineData(1.3)]
    [InlineData(2.0)]
    public void IsAndroidEnlarged_AboveDefault_ReturnsTrue(double fontScale) =>
        Assert.True(BaselineTextSize.IsAndroidEnlarged(fontScale));

    [Theory]
    [InlineData("extra-small")]
    [InlineData("small")]
    [InlineData("medium")]
    [InlineData("large")] // the default
    public void IsSimulatorEnlarged_AtOrBelowDefaultCategory_ReturnsFalse(string contentSize) =>
        Assert.False(BaselineTextSize.IsSimulatorEnlarged(contentSize));

    [Theory]
    [InlineData("extra-large")]
    [InlineData("extra-extra-extra-large")]
    [InlineData("accessibility-medium")]
    [InlineData("accessibility-extra-extra-extra-large")]
    public void IsSimulatorEnlarged_AboveDefaultCategory_ReturnsTrue(string contentSize) =>
        Assert.True(BaselineTextSize.IsSimulatorEnlarged(contentSize));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unexpected-value")]
    public void IsSimulatorEnlarged_UnrecognizedOrEmpty_ReturnsFalse(string? contentSize) =>
        Assert.False(BaselineTextSize.IsSimulatorEnlarged(contentSize));

    [Fact]
    public void IsPhysicalEnlarged_DefaultState_ReturnsFalse()
    {
        // "off:3/7": the owner's phone at its ordinary, day-to-day text size.
        Assert.False(BaselineTextSize.IsPhysicalEnlarged(new TextSizeState(false, 3, 7)));
    }

    [Fact]
    public void IsPhysicalEnlarged_SliderAboveDefaultStep_ReturnsTrue()
    {
        Assert.True(BaselineTextSize.IsPhysicalEnlarged(new TextSizeState(false, 4, 7)));
    }

    [Fact]
    public void IsPhysicalEnlarged_SliderBelowDefaultStep_ReturnsFalse()
    {
        // Smaller than default is not "enlarged".
        Assert.False(BaselineTextSize.IsPhysicalEnlarged(new TextSizeState(false, 0, 7)));
    }

    [Fact]
    public void IsPhysicalEnlarged_LargerAccessibilitySizesOn_ReturnsTrueEvenAtLowSliderIndex()
    {
        // Turning "Larger Accessibility Sizes" on is itself an enlarged configuration, regardless of where
        // the slider then sits within the resulting 12-step range.
        Assert.True(BaselineTextSize.IsPhysicalEnlarged(new TextSizeState(true, 0, 12)));
    }

    [Fact]
    public void Warning_NamesTheValueAndTellsThePersonWhatToDo()
    {
        var message = BaselineTextSize.Warning("font_scale 1.3");

        Assert.Equal(
            "The device's text size was already enlarged (font_scale 1.3) when the scan started, so the normal-size " +
            "capture isn't at the default size. Set the text size back to the default and scan again.",
            message);
    }

    [Fact]
    public void SkippedBaselineEnlarged_HasNoSnapshotAndBothReasonFields()
    {
        var result = LargeTextCapture.SkippedBaselineEnlarged("on:9/12");

        Assert.Null(result.Snapshot);
        Assert.Equal(BaselineTextSize.AlreadyEnlargedReason, result.SkippedReason);
        Assert.Equal(BaselineTextSize.Warning("on:9/12"), result.BaselineTextSizeNote);
    }
}
