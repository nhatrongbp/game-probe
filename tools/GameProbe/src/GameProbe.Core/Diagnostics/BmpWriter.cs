using GameProbe.Core.Capture;

namespace GameProbe.Core.Diagnostics;

/// <summary>
/// Minimal 32-bit BMP writer for region captures. Alpha is ignored by BMP viewers,
/// but the BGR bytes are preserved for debugging.
/// </summary>
public static class BmpWriter
{
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;
    private const int PixelOffset = FileHeaderSize + InfoHeaderSize;

    public static void Write32Bgra(string path, RegionCaptureResult capture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(capture);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var imageSize = checked(capture.Width * capture.Height * 4);
        var fileSize = checked(PixelOffset + imageSize);

        using var stream = File.Create(fullPath);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write(PixelOffset);

        writer.Write(InfoHeaderSize);
        writer.Write(capture.Width);
        writer.Write(capture.Height); // positive: bottom-up rows
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(0); // BI_RGB
        writer.Write(imageSize);
        writer.Write(2835); // ~72 DPI
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        for (var y = capture.Height - 1; y >= 0; y--)
        {
            writer.Write(capture.Pixels, y * capture.Stride, capture.Stride);
        }
    }
}
