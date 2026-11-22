using System.Globalization;
using GameProbe.Core.Models;

namespace GameProbe.Cli.Commands;

/// <summary>Shared parser for region capture/stability options.</summary>
internal sealed class RegionOptions
{
    public string? RegionId { get; private set; }

    public int? X { get; private set; }

    public int? Y { get; private set; }

    public int? Width { get; private set; }

    public int? Height { get; private set; }

    public string? OutPath { get; private set; }

    public int Count { get; private set; } = 10;

    public int IntervalMs { get; private set; } = 200;

    public bool Json { get; private set; }

    public bool HelpRequested { get; private set; }

    public bool TryConsume(string[] args, ref int index)
    {
        switch (args[index])
        {
            case "-h":
            case "--help":
                HelpRequested = true;
                return true;

            case "--region":
                RegionId = RequireValue(args, ref index, "--region");
                return true;

            case "--x":
                X = ParseInt(RequireValue(args, ref index, "--x"), "--x");
                return true;

            case "--y":
                Y = ParseInt(RequireValue(args, ref index, "--y"), "--y");
                return true;

            case "--width":
                Width = ParseInt(RequireValue(args, ref index, "--width"), "--width");
                return true;

            case "--height":
                Height = ParseInt(RequireValue(args, ref index, "--height"), "--height");
                return true;

            case "--out":
            case "-o":
                OutPath = RequireValue(args, ref index, "--out");
                return true;

            case "--count":
                Count = ParseInt(RequireValue(args, ref index, "--count"), "--count");
                return true;

            case "--interval":
                IntervalMs = ParseInt(RequireValue(args, ref index, "--interval"), "--interval");
                return true;

            case "--json":
                Json = true;
                return true;

            default:
                return false;
        }
    }

    public RegionSpec BuildRegion(Profile? profile)
    {
        if (!string.IsNullOrWhiteSpace(RegionId))
        {
            if (profile is null)
            {
                throw new CliUsageException("--region yêu cầu --profile.");
            }

            return profile.Regions.FirstOrDefault(
                       item => item.Id.Equals(RegionId, StringComparison.OrdinalIgnoreCase))
                   ?? throw new CliUsageException(
                       $"Profile '{profile.Name}' không có region '{RegionId}'.");
        }

        if (X is null || Y is null || Width is null || Height is null)
        {
            throw new CliUsageException(
                "Cần --region <id> hoặc đủ --x --y --width --height.");
        }

        return new RegionSpec
        {
            Id = "adhoc",
            Name = "Ad-hoc region",
            X = CoordinateSpec.Client(X.Value),
            Y = CoordinateSpec.Client(Y.Value),
            Width = Width.Value,
            Height = Height.Value
        };
    }

    public void Validate()
    {
        if (Count is < 1 or > 1000)
        {
            throw new CliUsageException("--count phải nằm trong khoảng 1..1000.");
        }

        if (IntervalMs is < 0 or > 600000)
        {
            throw new CliUsageException("--interval phải nằm trong khoảng 0..600000.");
        }

        if (!string.IsNullOrWhiteSpace(RegionId)
            && (X is not null || Y is not null || Width is not null || Height is not null))
        {
            throw new CliUsageException("Không dùng --region cùng lúc với --x/--y/--width/--height.");
        }
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new CliUsageException($"Thiếu giá trị cho {option}.");
        }

        var value = args[++index];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CliUsageException($"Giá trị cho {option} không được để trống.");
        }

        return value;
    }

    private static int ParseInt(string value, string option)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new CliUsageException($"Giá trị không hợp lệ cho {option}: {value}");
        }

        return parsed;
    }
}
