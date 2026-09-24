using System.Buffers.Binary;
using System.IO.Compression;

namespace Swipewalk.Core.Imaging;

/// <summary>
/// Minimal PNG decoder for device screenshots (adb screencap, simctl): 8-bit RGB or RGBA,
/// non-interlaced. Other formats throw <see cref="NotSupportedException"/>.
/// </summary>
public static class PngDecoder
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    public static RgbaImage Decode(string path)
    {
        using var stream = File.OpenRead(path);
        return Decode(stream);
    }

    public static RgbaImage Decode(Stream stream)
    {
        using var reader = new BinaryReader(stream);
        if (!reader.ReadBytes(8).AsSpan().SequenceEqual(Signature))
            throw new InvalidDataException("Not a PNG file.");

        int width = 0, height = 0, channels = 0;
        using var idat = new MemoryStream();
        while (true)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4));
            var type = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
            var data = reader.ReadBytes(length);
            reader.ReadBytes(4); // CRC

            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0, 4));
                height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4, 4));
                var (bitDepth, colorType, interlace) = (data[8], data[9], data[12]);
                if (bitDepth != 8 || interlace != 0 || colorType is not (2 or 6))
                    throw new NotSupportedException(
                        $"PNG bit depth {bitDepth}, color type {colorType}, interlace {interlace} is not supported.");
                channels = colorType == 6 ? 4 : 3;
            }
            else if (type == "IDAT")
            {
                idat.Write(data);
            }
            else if (type == "IEND")
            {
                break;
            }
        }

        if (channels == 0)
            throw new InvalidDataException("PNG has no IHDR chunk.");

        idat.Position = 0;
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionMode.Decompress))
            zlib.CopyTo(inflated);

        return new RgbaImage(width, height, Unfilter(inflated.GetBuffer(), width, height, channels));
    }

    private static byte[] Unfilter(byte[] data, int width, int height, int channels)
    {
        var stride = width * channels;
        var previous = new byte[stride];
        var current = new byte[stride];
        var pixels = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            var offset = y * (stride + 1);
            var filter = data[offset];
            Array.Copy(data, offset + 1, current, 0, stride);

            for (var i = 0; i < stride; i++)
            {
                int left = i >= channels ? current[i - channels] : 0;
                int up = previous[i];
                int upLeft = i >= channels ? previous[i - channels] : 0;
                current[i] = (byte)(current[i] + filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException($"Unknown PNG filter {filter}."),
                });
            }

            for (var x = 0; x < width; x++)
            {
                var src = x * channels;
                var dst = (y * width + x) * 4;
                pixels[dst] = current[src];
                pixels[dst + 1] = current[src + 1];
                pixels[dst + 2] = current[src + 2];
                pixels[dst + 3] = channels == 4 ? current[src + 3] : (byte)255;
            }

            (previous, current) = (current, previous);
        }

        return pixels;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var (pa, pb, pc) = (Math.Abs(p - a), Math.Abs(p - b), Math.Abs(p - c));
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
