namespace Swipewalk.Core.Imaging;

/// <summary>WCAG 2.2 relative luminance and contrast ratio.</summary>
public static class Contrast
{
    public static double Ratio(Rgb a, Rgb b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    public static double Luminance(Rgb c) =>
        0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

    private static double Channel(byte value)
    {
        var c = value / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    /// Estimates text and background colors inside a region: the most common color is the background;
    /// the text color is the reasonably frequent color with the highest contrast against it (anti-aliased
    /// edge pixels are intermediate colors, so the glyph cores win). Null when the region is uniform.
    /// </summary>
    public static (Rgb Foreground, Rgb Background)? EstimateColors(RgbaImage image, int x, int y, int width, int height)
    {
        x = Math.Clamp(x, 0, image.Width);
        y = Math.Clamp(y, 0, image.Height);
        width = Math.Min(width, image.Width - x);
        height = Math.Min(height, image.Height - y);
        if (width <= 0 || height <= 0)
            return null;

        var counts = new Dictionary<Rgb, int>();
        for (var py = y; py < y + height; py++)
            for (var px = x; px < x + width; px++)
            {
                var c = image.GetPixel(px, py);
                counts[c] = counts.GetValueOrDefault(c) + 1;
            }

        var background = counts.MaxBy(kv => kv.Value).Key;
        var minCount = Math.Max(3, width * height / 500);
        var candidates = counts.Where(kv => kv.Value >= minCount && kv.Key != background).Select(kv => kv.Key).ToList();
        if (candidates.Count == 0)
            return null;

        var foreground = candidates.MaxBy(c => Ratio(c, background));
        return (foreground, background);
    }

    /// <summary>
    /// The closest color to <paramref name="foreground"/> (same hue, darkened on light backgrounds or
    /// lightened on dark ones) that reaches <paramref name="target"/> against <paramref name="background"/>.
    /// </summary>
    public static Rgb SuggestForeground(Rgb foreground, Rgb background, double target)
    {
        var darken = Luminance(background) > 0.18;
        for (var step = 0; step <= 100; step++)
        {
            var t = step / 100.0;
            var candidate = darken
                ? new Rgb(Scale(foreground.R, 1 - t), Scale(foreground.G, 1 - t), Scale(foreground.B, 1 - t))
                : new Rgb(Mix(foreground.R, t), Mix(foreground.G, t), Mix(foreground.B, t));
            if (Ratio(candidate, background) >= target)
                return candidate;
        }
        return darken ? new Rgb(0, 0, 0) : new Rgb(255, 255, 255);

        static byte Scale(byte c, double f) => (byte)Math.Floor(c * f);
        static byte Mix(byte c, double t) => (byte)Math.Ceiling(c + (255 - c) * t);
    }
}
