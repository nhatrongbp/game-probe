using GameProbe.Core.Capture;
using GameProbe.Core.Diagnostics;
using Xunit;

namespace GameProbe.Tests;

public sealed class BmpWriterTests
{
    [Fact]
    public void Write32Bgra_WritesValidHeaderAndPixels()
    {
        var pixels = new byte[]
        {
            1, 2, 3, 255,   // BGRA pixel (0,0)
            4, 5, 6, 255    // BGRA pixel (1,0)
        };

        var capture = new RegionCaptureResult(
            10,
            20,
            2,
            1,
            pixels,
            RegionCaptureMethod.BitBlt,
            0,
            DateTimeOffset.UnixEpoch);

        var directory = Path.Combine(
            Path.GetTempPath(),
            "GameProbe.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "region.bmp");

        try
        {
            BmpWriter.Write32Bgra(path, capture);

            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            Assert.Equal((byte)'B', reader.ReadByte());
            Assert.Equal((byte)'M', reader.ReadByte());
            Assert.Equal(54 + pixels.Length, reader.ReadInt32());
            reader.ReadInt16();
            reader.ReadInt16();
            Assert.Equal(54, reader.ReadInt32());

            Assert.Equal(40, reader.ReadInt32());
            Assert.Equal(2, reader.ReadInt32());
            Assert.Equal(1, reader.ReadInt32());
            Assert.Equal((short)1, reader.ReadInt16());
            Assert.Equal((short)32, reader.ReadInt16());
            Assert.Equal(0, reader.ReadInt32());
            Assert.Equal(pixels.Length, reader.ReadInt32());
            reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();

            Assert.Equal(pixels, reader.ReadBytes(pixels.Length));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
