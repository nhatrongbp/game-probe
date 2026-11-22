using System.Globalization;
using GameProbe.Core.Models;

namespace GameProbe.Cli.Commands;

internal static class FormatHelpers
{
    public static string DescribeCoordinate(CoordinateSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        return spec.Mode switch
        {
            CoordinateMode.Client => $"client {spec.Value.ToString("0.##", CultureInfo.InvariantCulture)}",
            CoordinateMode.Percent =>
                $"percent {spec.Value.ToString("0.###", CultureInfo.InvariantCulture)}{DescribeOffset(spec.Offset)}",
            CoordinateMode.Anchor =>
                $"anchor {spec.Anchor?.ToString().ToLowerInvariant()}{DescribeOffset(spec.Offset)}",
            _ => "?"
        };
    }

    private static string DescribeOffset(double offset)
    {
        if (offset == 0)
        {
            return "";
        }

        var text = offset.ToString("0.##", CultureInfo.InvariantCulture);
        return offset > 0 ? $" +{text}" : $" {text}";
    }
}
