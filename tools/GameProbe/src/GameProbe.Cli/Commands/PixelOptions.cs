using System.Globalization;
using GameProbe.Core.Capture;
using GameProbe.Core.Models;
using GameProbe.Core.Profiles;
using GameProbe.Core.Windows;

namespace GameProbe.Cli.Commands;

internal sealed class PixelOptions
{
    public TargetSelection Target { get; } = new();

    public string? ProbeId { get; private set; }

    public int? X { get; private set; }

    public int? Y { get; private set; }

    public int? Tolerance { get; private set; }

    public int? ExpectedClass { get; private set; }

    public string? ExpectedRgb { get; private set; }

    public bool Json { get; private set; }

    public bool HelpRequested { get; private set; }

    public bool TryConsume(string[] args, ref int index)
    {
        if (Target.TryConsume(args, ref index))
        {
            return true;
        }

        switch (args[index])
        {
            case "--probe":
                ProbeId = RequireValue(args, ref index, "--probe");
                return true;

            case "--x":
                X = ParseInt(RequireValue(args, ref index, "--x"), "--x");
                return true;

            case "--y":
                Y = ParseInt(RequireValue(args, ref index, "--y"), "--y");
                return true;

            case "--tolerance":
                var tolerance = ParseInt(RequireValue(args, ref index, "--tolerance"), "--tolerance");
                if (tolerance is < 0 or > 128)
                {
                    throw new CliUsageException("--tolerance phải nằm trong khoảng 0..128.");
                }

                Tolerance = tolerance;
                return true;

            case "--expect-class":
                var expectedClass = ParseInt(
                    RequireValue(args, ref index, "--expect-class"),
                    "--expect-class");
                if (expectedClass is < 0 or > 3)
                {
                    throw new CliUsageException("--expect-class phải nằm trong khoảng 0..3.");
                }

                ExpectedClass = expectedClass;
                return true;

            case "--expect-rgb":
                var expectedRgb = RequireValue(args, ref index, "--expect-rgb");
                try
                {
                    _ = PixelColor.FromHex(expectedRgb);
                }
                catch (FormatException ex)
                {
                    throw new CliUsageException(ex.Message);
                }

                ExpectedRgb = expectedRgb;
                return true;

            case "--json":
                Json = true;
                return true;

            case "-h":
            case "--help":
                HelpRequested = true;
                return true;

            default:
                return false;
        }
    }

    public void Validate()
    {
        Target.Validate();

        if (ProbeId is not null)
        {
            if (X is not null || Y is not null)
            {
                throw new CliUsageException(
                    "Không dùng --x/--y cùng lúc với --probe; tọa độ đã nằm trong probe.");
            }

            if (Target.ProfilePath is null)
            {
                throw new CliUsageException("--probe yêu cầu --profile để đọc định nghĩa probe.");
            }
        }
        else if (X is null || Y is null)
        {
            throw new CliUsageException("Cần --x và --y, hoặc --profile cùng --probe.");
        }
    }

    public Profile? LoadProfile() => Target.LoadProfileIfSpecified();

    public PixelReadPlan BuildPlan(Profile? profile)
    {
        if (ProbeId is not null)
        {
            if (profile is null)
            {
                throw new CliUsageException("--probe yêu cầu --profile.");
            }

            var probe = profile.PixelProbes.FirstOrDefault(
                item => item.Id.Equals(ProbeId, StringComparison.OrdinalIgnoreCase));

            if (probe is null)
            {
                throw new CliUsageException(
                    $"Profile '{profile.Name}' không có pixel probe '{ProbeId}'.");
            }

            var effectiveProbe = new PixelProbeSpec
            {
                Id = probe.Id,
                Name = probe.Name,
                X = probe.X,
                Y = probe.Y,
                Tolerance = Tolerance ?? probe.Tolerance,
                ExpectedClass = ExpectedClass ?? probe.ExpectedClass,
                ExpectedRgb = ExpectedRgb ?? probe.ExpectedRgb,
                WatchIntervalMs = probe.WatchIntervalMs
            };

            return new PixelReadPlan(effectiveProbe, null);
        }

        var request = new PixelProbeRequest
        {
            X = X ?? throw new CliUsageException("Thiếu --x."),
            Y = Y ?? throw new CliUsageException("Thiếu --y."),
            Tolerance = Tolerance ?? 8,
            ExpectedClass = ExpectedClass,
            ExpectedRgb = ExpectedRgb
        };

        return new PixelReadPlan(null, request);
    }

    private static int ParseInt(string text, string option)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new CliUsageException($"Giá trị cho {option} không phải số nguyên: {text}");
        }

        return value;
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new CliUsageException($"Thiếu giá trị cho {option}.");
        }

        return args[++index];
    }
}

internal sealed record PixelReadPlan(PixelProbeSpec? Spec, PixelProbeRequest? Request)
{
    public int DefaultWatchIntervalMs => Spec?.WatchIntervalMs ?? 200;

    public PixelReadResult Read(PixelProbe probe, GameWindow window)
    {
        return Spec is not null
            ? probe.Read(window, Spec)
            : probe.Read(window, Request ?? throw new InvalidOperationException("Pixel request is missing."));
    }

    public string DescribeX() => Spec is not null
        ? FormatHelpers.DescribeCoordinate(Spec.X)
        : $"client {Request!.X}";

    public string DescribeY() => Spec is not null
        ? FormatHelpers.DescribeCoordinate(Spec.Y)
        : $"client {Request!.Y}";

    public string DescribeTarget() => Spec is not null
        ? $"probe '{Spec.Id}'"
        : $"point ({Request!.X},{Request.Y})";
}
