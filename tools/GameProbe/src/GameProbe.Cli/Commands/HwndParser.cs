using System.Globalization;

namespace GameProbe.Cli.Commands;

internal static class HwndParser
{
    public static bool TryParse(string? text, out IntPtr handle)
    {
        handle = IntPtr.Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        long parsed;

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!long.TryParse(
                    value[2..],
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out parsed))
            {
                return false;
            }
        }
        else if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
        {
            return false;
        }

        if (parsed <= 0)
        {
            return false;
        }

        handle = new IntPtr(parsed);
        return true;
    }
}
