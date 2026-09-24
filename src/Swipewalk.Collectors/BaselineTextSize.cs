using Swipewalk.Collectors.Ios;

namespace Swipewalk.Collectors;

/// <summary>
/// Whether a device's <em>current</em> text size is already enlarged above the platform default, checked
/// right after each large-text flow reads the device's original size and before it changes anything. A
/// "normal size" capture taken while the device is already enlarged isn't actually at default size, so
/// enlarging it further and comparing would wrongly look like text "did not grow" (see <see cref="TextGrowth"/>)
/// even though nothing is actually wrong. Pure decisions only -- one per platform's baseline representation
/// (Android font_scale, iOS Simulator content_size, physical iPhone <see cref="TextSizeState"/>) -- so they
/// can be unit tested without a device.
/// </summary>
public static class BaselineTextSize
{
    /// <summary>Allowed float noise around Android's default font_scale of 1.0 (readings are normally exact
    /// values such as 1.0, 1.15, 1.3, ... but this guards against e.g. 1.0000001-style rounding).</summary>
    private const double AndroidTolerance = 0.02;

    /// <summary>True when an Android font_scale reading is enlarged above the default (1.0), allowing tiny
    /// float noise around it.</summary>
    public static bool IsAndroidEnlarged(double fontScale) => fontScale > 1.0 + AndroidTolerance;

    /// <summary>`simctl ui &lt;device&gt; content_size` categories, smallest to largest. "large" is iOS's
    /// default (UIContentSizeCategory.large); anything after it, including the accessibility sizes, is enlarged.</summary>
    private static readonly string[] SimulatorCategoriesSmallestToLargest =
    [
        "extra-small", "small", "medium", "large",
        "extra-large", "extra-extra-large", "extra-extra-extra-large",
        "accessibility-medium", "accessibility-large", "accessibility-extra-large",
        "accessibility-extra-extra-large", "accessibility-extra-extra-extra-large",
    ];

    private const string SimulatorDefaultCategory = "large";

    /// <summary>True when a Simulator's content_size category reads above the default ("large"). An
    /// unrecognized or empty value is treated as not enlarged: there is nothing safe to warn about.</summary>
    public static bool IsSimulatorEnlarged(string? contentSize)
    {
        if (string.IsNullOrWhiteSpace(contentSize))
            return false;
        var index = Array.IndexOf(SimulatorCategoriesSmallestToLargest, contentSize.Trim());
        if (index < 0)
            return false;
        return index > Array.IndexOf(SimulatorCategoriesSmallestToLargest, SimulatorDefaultCategory);
    }

    /// <summary>Default slider position on a physical iPhone with "Larger Accessibility Sizes" off: index 3
    /// of 7 steps (xSmall, Small, Medium, <b>Large</b>, xLarge, xxLarge, xxxLarge) -- iOS's "off:3/7".</summary>
    public const int PhysicalDefaultSliderIndex = 3;

    /// <summary>True when a physical iPhone's Settings &gt; Accessibility &gt; Display &amp; Text Size &gt;
    /// Larger Text reads above default: "Larger Accessibility Sizes" on (at any slider position), or the
    /// slider above the default step (index 3 of 7) with the switch off.</summary>
    public static bool IsPhysicalEnlarged(TextSizeState state) => state.ToggleOn || state.SliderIndex > PhysicalDefaultSliderIndex;

    /// <summary>
    /// The short reason recorded as <c>LargeTextSkippedReason</c> when the large-text comparison is skipped
    /// because the baseline was already enlarged (the same style as <see cref="LargeTextCapture.DifferentScreen"/>
    /// and <see cref="LargeTextCapture.NotInFront"/>: a lowercase phrase, no trailing period).
    /// </summary>
    public const string AlreadyEnlargedReason = "the device's text size was already enlarged before the scan started";

    /// <summary>
    /// The full warning logged and recorded on the screen result (<c>ScreenResult.BaselineTextSizeNote</c>)
    /// when the baseline was already enlarged: unlike <see cref="AlreadyEnlargedReason"/>, this names the
    /// actual value read and tells the person how to fix it.
    /// </summary>
    public static string Warning(string value) =>
        $"The device's text size was already enlarged ({value}) when the scan started, so the normal-size " +
        "capture isn't at the default size. Set the text size back to the default and scan again.";
}
