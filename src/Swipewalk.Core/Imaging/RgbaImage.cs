namespace Swipewalk.Core.Imaging;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}

/// <summary>An 8-bit RGBA raster, row-major, 4 bytes per pixel.</summary>
public sealed class RgbaImage
{
    public RgbaImage(int width, int height, byte[] pixels)
    {
        if (pixels.Length != width * height * 4)
            throw new ArgumentException("Pixel buffer size does not match dimensions.", nameof(pixels));
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    /// <summary>Paints a rectangle (clipped to the image) with one opaque color, e.g. to mask private areas.</summary>
    public void Fill(int x, int y, int width, int height, Rgb color)
    {
        for (var py = Math.Max(0, y); py < Math.Min(Height, y + height); py++)
            for (var px = Math.Max(0, x); px < Math.Min(Width, x + width); px++)
            {
                var i = (py * Width + px) * 4;
                (Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]) = (color.R, color.G, color.B, 255);
            }
    }

    /// <summary>Share of pixels that are pure black (#000000).</summary>
    public double BlackShare()
    {
        var black = 0L;
        for (var i = 0; i < Pixels.Length; i += 4)
            if (Pixels[i] == 0 && Pixels[i + 1] == 0 && Pixels[i + 2] == 0)
                black++;
        return (double)black / (Width * Height);
    }

    public Rgb GetPixel(int x, int y)
    {
        var i = (y * Width + x) * 4;
        return new Rgb(Pixels[i], Pixels[i + 1], Pixels[i + 2]);
    }
}
