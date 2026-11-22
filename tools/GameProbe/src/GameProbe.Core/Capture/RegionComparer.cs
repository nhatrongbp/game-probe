namespace GameProbe.Core.Capture;

public readonly record struct RegionDiff(
    bool IsDifferent,
    int ChangedPixels,
    int MaxChannelDelta,
    int MinX,
    int MinY,
    int MaxX,
    int MaxY)
{
    public bool HasChangedArea => ChangedPixels > 0;

    public string BoundsText =>
        ChangedPixels == 0 ? "none" : $"({MinX},{MinY})-({MaxX},{MaxY})";
}

/// <summary>
/// Compares captured BGRA regions. RGB bytes are compared; alpha is ignored.
/// A pixel counts as changed when any RGB channel differs by more than
/// <paramref name="channelTolerance"/>.
/// </summary>
public static class RegionComparer
{
    public static RegionDiff Compare(
        RegionCaptureResult baseline,
        RegionCaptureResult current,
        int channelTolerance = 0,
        int minChangedPixels = 1)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        if (baseline.Width != current.Width || baseline.Height != current.Height)
        {
            throw new ArgumentException(
                $"Kích thước vùng khác nhau: baseline {baseline.Width}x{baseline.Height}, " +
                $"current {current.Width}x{current.Height}.");
        }

        if (channelTolerance is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(
                nameof(channelTolerance),
                "channelTolerance phải nằm trong khoảng 0..255.");
        }

        if (minChangedPixels < 1 || minChangedPixels > baseline.PixelCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minChangedPixels),
                $"minChangedPixels phải nằm trong khoảng 1..{baseline.PixelCount}.");
        }

        var a = baseline.Pixels;
        var b = current.Pixels;
        var width = baseline.Width;
        var height = baseline.Height;

        var changedPixels = 0;
        var maxDelta = 0;
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                var offset = rowOffset + x * 4;

                var deltaB = Math.Abs(a[offset] - b[offset]);
                var deltaG = Math.Abs(a[offset + 1] - b[offset + 1]);
                var deltaR = Math.Abs(a[offset + 2] - b[offset + 2]);
                var pixelDelta = Math.Max(deltaB, Math.Max(deltaG, deltaR));

                if (pixelDelta > maxDelta)
                {
                    maxDelta = pixelDelta;
                }

                if (pixelDelta <= channelTolerance)
                {
                    continue;
                }

                changedPixels++;
                if (x < minX)
                {
                    minX = x;
                }

                if (y < minY)
                {
                    minY = y;
                }

                if (x > maxX)
                {
                    maxX = x;
                }

                if (y > maxY)
                {
                    maxY = y;
                }
            }
        }

        return new RegionDiff(
            changedPixels >= minChangedPixels,
            changedPixels,
            maxDelta,
            changedPixels == 0 ? 0 : minX,
            changedPixels == 0 ? 0 : minY,
            maxX,
            maxY);
    }

    public static bool AreEqual(
        RegionCaptureResult baseline,
        RegionCaptureResult current,
        int channelTolerance = 0)
    {
        return Compare(baseline, current, channelTolerance, minChangedPixels: 1).ChangedPixels == 0;
    }
}
