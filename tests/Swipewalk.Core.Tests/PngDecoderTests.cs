using System.Buffers.Binary;
using System.IO.Compression;
using Swipewalk.Core.Imaging;

namespace Swipewalk.Core.Tests;

public class PngDecoderTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Decode_ReversesAllFilterTypes(int channels)
    {
        const int width = 7, height = 10;
        var raw = new byte[width * height * channels];
        new Random(42).NextBytes(raw);

        var image = PngDecoder.Decode(new MemoryStream(Encode(raw, width, height, channels)));

        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * channels;
                Assert.Equal(new Rgb(raw[i], raw[i + 1], raw[i + 2]), image.GetPixel(x, y));
            }
    }

    [Fact]
    public void Encoder_RoundTripsThroughDecoder_AfterFill()
    {
        var image = new RgbaImage(4, 3, Enumerable.Repeat((byte)255, 4 * 3 * 4).ToArray());
        image.Fill(0, 0, 4, 1, new Rgb(0x60, 0x60, 0x60));

        var decoded = PngDecoder.Decode(new MemoryStream(PngEncoder.Encode(image)));

        Assert.Equal(new Rgb(0x60, 0x60, 0x60), decoded.GetPixel(3, 0));
        Assert.Equal(new Rgb(255, 255, 255), decoded.GetPixel(3, 1));
    }

    [Fact]
    public void Decode_Throws_ForNonPng()
    {
        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8])));
    }

    /// <summary>Test encoder: row y uses filter type y % 5 so every filter is exercised.</summary>
    private static byte[] Encode(byte[] raw, int width, int height, int channels)
    {
        var stride = width * channels;
        var filtered = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            var filter = (byte)(y % 5);
            filtered.WriteByte(filter);
            for (var i = 0; i < stride; i++)
            {
                int x = raw[y * stride + i];
                int a = i >= channels ? raw[y * stride + i - channels] : 0;
                int b = y > 0 ? raw[(y - 1) * stride + i] : 0;
                int c = y > 0 && i >= channels ? raw[(y - 1) * stride + i - channels] : 0;
                var predictor = filter switch
                {
                    0 => 0,
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    _ => Paeth(a, b, c),
                };
                filtered.WriteByte((byte)(x - predictor));
            }
        }

        var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(filtered.ToArray());

        var png = new MemoryStream();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8;
        ihdr[9] = (byte)(channels == 4 ? 6 : 2);
        WriteChunk(png, "IHDR", ihdr);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(System.Text.Encoding.ASCII.GetBytes(type));
        stream.Write(data);
        stream.Write(new byte[4]); // CRC is not verified by the decoder
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var (pa, pb, pc) = (Math.Abs(p - a), Math.Abs(p - b), Math.Abs(p - c));
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
