using System.Globalization;
using System.Xml.Linq;
using Swipewalk.Core.Imaging;

namespace Swipewalk.Core.Tests;

/// <summary>
/// The desktop app's own palette must meet WCAG contrast: 4.5:1 for text (1.4.3), 3:1 for control boundaries
/// (1.4.11), in light and dark mode. Reads the real Colors.xaml so a failing color can't slip in.
/// </summary>
public class DesktopPaletteTests
{
    private static readonly Dictionary<string, Rgb> Colors = Load();

    private static Dictionary<string, Rgb> Load()
    {
        string? path = null;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null && path is null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src/Swipewalk.Desktop/Resources/Styles/Colors.xaml");
            path = File.Exists(candidate) ? candidate : null;
        }
        XNamespace x = "http://schemas.microsoft.com/winfx/2009/xaml";
        return XDocument.Load(path ?? throw new FileNotFoundException("Colors.xaml"))
            .Root!.Elements().Where(e => e.Name.LocalName == "Color")
            .ToDictionary(e => (string)e.Attribute(x + "Key")!, e => Parse(e.Value.Trim()));
    }

    /// <summary>Named White/Black, #RGB, #RRGGBB or #AARRGGBB (alpha ignored: these colors are opaque).</summary>
    private static Rgb Parse(string hex)
    {
        if (hex is "White") return new Rgb(255, 255, 255);
        if (hex is "Black") return new Rgb(0, 0, 0);
        var digits = hex.TrimStart('#');
        if (digits.Length == 3)
            digits = string.Concat(digits.Select(c => $"{c}{c}"));
        var v = int.Parse(digits[^6..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public static TheoryData<string, string, double> Pairs() => new()
    {
        // Text on the page background (light: White, dark: OffBlack).
        { "Issue", "White", 4.5 }, { "Review", "White", 4.5 }, { "Advisory", "White", 4.5 },
        { "IssueDark", "OffBlack", 4.5 }, { "ReviewDark", "OffBlack", 4.5 }, { "AdvisoryDark", "OffBlack", 4.5 },
        // Placeholders.
        { "Gray500", "White", 4.5 }, { "Gray300", "OffBlack", 4.5 },
        // Secondary text (sidebar subtitle).
        { "Gray600", "White", 4.5 }, { "Gray300", "Black", 4.5 },
        // Primary buttons.
        { "White", "Primary", 4.5 }, { "PrimaryDarkText", "PrimaryDark", 4.5 },
        // Text field outlines.
        { "FieldBorder", "White", 3.0 }, { "FieldBorder", "OffBlack", 3.0 },
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void ColorPair_MeetsContrast(string foreground, string background, double minimum)
    {
        var ratio = Contrast.Ratio(Colors[foreground], Colors[background]);
        Assert.True(ratio >= minimum, $"{foreground} on {background} is {ratio:0.00}:1, below {minimum}:1");
    }
}
