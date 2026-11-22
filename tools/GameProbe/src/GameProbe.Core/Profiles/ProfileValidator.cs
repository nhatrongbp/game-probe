using System.Text.RegularExpressions;
using GameProbe.Core.Models;

namespace GameProbe.Core.Profiles;

public static class ProfileValidator
{
    private static readonly Regex IdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HexColorPattern = new(
        "^#[0-9A-Fa-f]{6}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<Anchor> XAnchors = new()
    {
        Anchor.Left,
        Anchor.Center,
        Anchor.Right
    };

    private static readonly HashSet<Anchor> YAnchors = new()
    {
        Anchor.Top,
        Anchor.Center,
        Anchor.Bottom
    };

    private static readonly HashSet<string> AllowedEncodings = new(StringComparer.OrdinalIgnoreCase)
    {
        "utf16le",
        "utf8"
    };

    private static readonly HashSet<string> AllowedRegionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "private",
        "image",
        "mapped"
    };

    private static readonly HashSet<string> AllowedLogLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "trace",
        "debug",
        "info",
        "warn",
        "warning",
        "error",
        "none"
    };

    public static ProfileValidationResult Validate(Profile? profile)
    {
        var result = new ProfileValidationResult();

        if (profile is null)
        {
            result.AddError("Profile is null.");
            return result;
        }

        if (profile.SchemaVersion != Profile.CurrentSchemaVersion)
        {
            result.AddError($"schemaVersion must be {Profile.CurrentSchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            result.AddError("name must not be empty.");
        }

        ValidateTarget(profile.Target, result);

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ValidateClickPoints(profile.ClickPoints, seenIds, result);
        ValidatePixelProbes(profile.PixelProbes, seenIds, result);
        ValidateRegions(profile.Regions, seenIds, result);
        ValidatePriceRefresh(profile, result);
        ValidateMemory(profile.Memory, result);
        ValidateHotkeys(profile.Hotkeys, result);
        ValidateLogging(profile.Logging, result);

        return result;
    }

    public static void ThrowIfInvalid(Profile? profile)
    {
        var result = Validate(profile);
        if (!result.IsValid)
        {
            throw new ProfileValidationException(result.Errors);
        }
    }

    private static void ValidateTarget(TargetSpec? target, ProfileValidationResult result)
    {
        if (target is null)
        {
            result.AddError("target must not be null.");
            return;
        }

        if (string.IsNullOrWhiteSpace(target.ClassName)
            && string.IsNullOrWhiteSpace(target.ProcessName)
            && string.IsNullOrWhiteSpace(target.TitleContains))
        {
            result.AddError("target must specify className, processName, or titleContains.");
        }

        if (target.ClassName is { Length: > 256 })
        {
            result.AddError("target.className is too long (max 256).");
        }

        if (target.ProcessName is { Length: > 256 })
        {
            result.AddError("target.processName is too long (max 256).");
        }

        if (target.TitleContains is { Length: > 256 })
        {
            result.AddError("target.titleContains is too long (max 256).");
        }
    }

    private static void ValidateClickPoints(
        List<ClickPoint>? clickPoints,
        HashSet<string> seenIds,
        ProfileValidationResult result)
    {
        if (clickPoints is null)
        {
            result.AddError("clickPoints must not be null.");
            return;
        }

        for (var i = 0; i < clickPoints.Count; i++)
        {
            var point = clickPoints[i];
            var path = $"clickPoints[{i}]";

            if (point is null)
            {
                result.AddError($"{path} must not be null.");
                continue;
            }

            ValidateId(point.Id, path, seenIds, result);

            if (string.IsNullOrWhiteSpace(point.Name))
            {
                result.AddWarning($"{path}.name is empty; the id will be used in the UI.");
            }

            ValidateCoordinate(point.X, isXAxis: true, $"{path}.x", result);
            ValidateCoordinate(point.Y, isXAxis: false, $"{path}.y", result);

            if (point.Count is < 1 or > 100)
            {
                result.AddError($"{path}.count must be between 1 and 100.");
            }

            if (point.HoldMs is < 0 or > 10000)
            {
                result.AddError($"{path}.holdMs must be between 0 and 10000.");
            }

            if (point.IntervalMs is < 20 or > 600000)
            {
                result.AddError($"{path}.intervalMs must be between 20 and 600000.");
            }

            if (point.HoverSettleMs is < 0 or > 10000)
            {
                result.AddError($"{path}.hoverSettleMs must be between 0 and 10000.");
            }

            if (point.SyncMode == ClickSendMode.SendInput)
            {
                result.AddWarning(
                    $"{path}.syncMode=sendInput moves the real cursor and requires foreground; use only for troubleshooting.");
            }
        }
    }

    private static void ValidatePixelProbes(
        List<PixelProbeSpec>? pixelProbes,
        HashSet<string> seenIds,
        ProfileValidationResult result)
    {
        if (pixelProbes is null)
        {
            result.AddError("pixelProbes must not be null.");
            return;
        }

        for (var i = 0; i < pixelProbes.Count; i++)
        {
            var probe = pixelProbes[i];
            var path = $"pixelProbes[{i}]";

            if (probe is null)
            {
                result.AddError($"{path} must not be null.");
                continue;
            }

            ValidateId(probe.Id, path, seenIds, result);

            if (string.IsNullOrWhiteSpace(probe.Name))
            {
                result.AddWarning($"{path}.name is empty; the id will be used in the UI.");
            }

            ValidateCoordinate(probe.X, isXAxis: true, $"{path}.x", result);
            ValidateCoordinate(probe.Y, isXAxis: false, $"{path}.y", result);

            if (probe.ExpectedClass is < 0 or > 3)
            {
                result.AddError($"{path}.expectedClass must be between 0 and 3.");
            }

            if (!string.IsNullOrWhiteSpace(probe.ExpectedRgb)
                && !HexColorPattern.IsMatch(probe.ExpectedRgb))
            {
                result.AddError($"{path}.expectedRgb must use #RRGGBB format.");
            }

            if (probe.Tolerance is < 0 or > 128)
            {
                result.AddError($"{path}.tolerance must be between 0 and 128.");
            }

            if (probe.WatchIntervalMs is < 50 or > 600000)
            {
                result.AddError($"{path}.watchIntervalMs must be between 50 and 600000.");
            }
        }
    }

    private static void ValidateRegions(
        List<RegionSpec>? regions,
        HashSet<string> seenIds,
        ProfileValidationResult result)
    {
        if (regions is null)
        {
            result.AddError("regions must not be null.");
            return;
        }

        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            var path = $"regions[{i}]";

            if (region is null)
            {
                result.AddError($"{path} must not be null.");
                continue;
            }

            ValidateId(region.Id, path, seenIds, result);

            if (string.IsNullOrWhiteSpace(region.Name))
            {
                result.AddWarning($"{path}.name is empty; the id will be used in the UI.");
            }

            ValidateCoordinate(region.X, isXAxis: true, $"{path}.x", result);
            ValidateCoordinate(region.Y, isXAxis: false, $"{path}.y", result);

            if (region.Width is < 1 or > 4096)
            {
                result.AddError($"{path}.width must be between 1 and 4096.");
            }

            if (region.Height is < 1 or > 4096)
            {
                result.AddError($"{path}.height must be between 1 and 4096.");
            }
        }
    }

    private static void ValidatePriceRefresh(Profile profile, ProfileValidationResult result)
    {
        var options = profile.PriceRefresh;
        if (options is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.RelistPointId))
        {
            result.AddError("priceRefresh.relistPointId must not be empty.");
        }
        else if (!ContainsClickPoint(profile, options.RelistPointId))
        {
            result.AddError(
                $"priceRefresh.relistPointId '{options.RelistPointId}' does not exist in clickPoints.");
        }

        if (string.IsNullOrWhiteSpace(options.PricePointId))
        {
            result.AddError("priceRefresh.pricePointId must not be empty.");
        }
        else if (!ContainsClickPoint(profile, options.PricePointId))
        {
            result.AddError(
                $"priceRefresh.pricePointId '{options.PricePointId}' does not exist in clickPoints.");
        }

        if (string.IsNullOrWhiteSpace(options.CancelPointId))
        {
            result.AddError("priceRefresh.cancelPointId must not be empty.");
        }
        else if (!ContainsClickPoint(profile, options.CancelPointId))
        {
            result.AddError(
                $"priceRefresh.cancelPointId '{options.CancelPointId}' does not exist in clickPoints.");
        }

        if (string.IsNullOrWhiteSpace(options.BuyPointId))
        {
            result.AddError("priceRefresh.buyPointId must not be empty.");
        }
        else if (!ContainsClickPoint(profile, options.BuyPointId))
        {
            result.AddError(
                $"priceRefresh.buyPointId '{options.BuyPointId}' does not exist in clickPoints.");
        }

        if (string.IsNullOrWhiteSpace(options.PopupProbeId))
        {
            result.AddError("priceRefresh.popupProbeId must not be empty.");
        }
        else if (!ContainsPixelProbe(profile, options.PopupProbeId))
        {
            result.AddError(
                $"priceRefresh.popupProbeId '{options.PopupProbeId}' does not exist in pixelProbes.");
        }
        else if (!ProbeHasExpectedRgb(profile, options.PopupProbeId))
        {
            result.AddError(
                $"priceRefresh.popupProbeId '{options.PopupProbeId}' must define expectedRgb.");
        }

        if (!string.IsNullOrWhiteSpace(options.RelistGuardProbeId))
        {
            if (!ContainsPixelProbe(profile, options.RelistGuardProbeId))
            {
                result.AddError(
                    $"priceRefresh.relistGuardProbeId '{options.RelistGuardProbeId}' " +
                    "does not exist in pixelProbes.");
            }
            else if (!ProbeHasExpectedRgb(profile, options.RelistGuardProbeId))
            {
                result.AddError(
                    $"priceRefresh.relistGuardProbeId '{options.RelistGuardProbeId}' " +
                    "must define expectedRgb.");
            }
        }

        RegionSpec? priceRegion = null;
        if (string.IsNullOrWhiteSpace(options.PriceRegionId))
        {
            result.AddError("priceRefresh.priceRegionId must not be empty.");
        }
        else
        {
            priceRegion = FindRegion(profile, options.PriceRegionId);
            if (priceRegion is null)
            {
                result.AddError(
                    $"priceRefresh.priceRegionId '{options.PriceRegionId}' does not exist in regions.");
            }
        }

        if (options.StartDelayMs is < 0 or > 60000)
        {
            result.AddError("priceRefresh.startDelayMs must be between 0 and 60000.");
        }

        if (options.SettleMs is < 0 or > 10000)
        {
            result.AddError("priceRefresh.settleMs must be between 0 and 10000.");
        }

        if (options.StableFrameGapMs is < 50 or > 2000)
        {
            result.AddError("priceRefresh.stableFrameGapMs must be between 50 and 2000.");
        }

        if (options.StableTimeoutMs is < 100 or > 30000)
        {
            result.AddError("priceRefresh.stableTimeoutMs must be between 100 and 30000.");
        }

        if (options.PollIntervalMs is < 20 or > 2000)
        {
            result.AddError("priceRefresh.pollIntervalMs must be between 20 and 2000.");
        }

        if (options.PopupTimeoutMs is < 100 or > 60000)
        {
            result.AddError("priceRefresh.popupTimeoutMs must be between 100 and 60000.");
        }

        if (options.RelistGuardTimeoutMs is < 100 or > 60000)
        {
            result.AddError("priceRefresh.relistGuardTimeoutMs must be between 100 and 60000.");
        }

        if (options.CloseTimeoutMs is < 100 or > 60000)
        {
            result.AddError("priceRefresh.closeTimeoutMs must be between 100 and 60000.");
        }

        if (options.AttemptDelayMs is < 0 or > 60000)
        {
            result.AddError("priceRefresh.attemptDelayMs must be between 0 and 60000.");
        }

        if (options.MaxAttempts is < 1 or > 1000)
        {
            result.AddError("priceRefresh.maxAttempts must be between 1 and 1000.");
        }

        if (options.PriceClickDelayMs is < 0 or > 10000)
        {
            result.AddError("priceRefresh.priceClickDelayMs must be between 0 and 10000.");
        }

        if (options.ChannelTolerance is < 0 or > 128)
        {
            result.AddError("priceRefresh.channelTolerance must be between 0 and 128.");
        }

        if (options.MinChangedPixels is < 1 or > 1000000)
        {
            result.AddError("priceRefresh.minChangedPixels must be between 1 and 1000000.");
        }
        else if (priceRegion is { Width: > 0, Height: > 0 }
                 && options.MinChangedPixels > priceRegion.Width * priceRegion.Height)
        {
            result.AddError(
                "priceRefresh.minChangedPixels must not exceed the price region pixel count " +
                $"({priceRegion.Width * priceRegion.Height}).");
        }

        if (options.ExpectedClientWidth is < 0 or > 100000)
        {
            result.AddError("priceRefresh.expectedClientWidth must be between 0 and 100000.");
        }

        if (options.ExpectedClientHeight is < 0 or > 100000)
        {
            result.AddError("priceRefresh.expectedClientHeight must be between 0 and 100000.");
        }

        if ((options.ExpectedClientWidth == 0) != (options.ExpectedClientHeight == 0))
        {
            result.AddError(
                "priceRefresh.expectedClientWidth and expectedClientHeight must both be 0 or both be set.");
        }

        if (options.ExpectedDpi is not 0 and (< 48 or > 768))
        {
            result.AddError("priceRefresh.expectedDpi must be 0 or between 48 and 768.");
        }
    }

    private static bool ContainsClickPoint(Profile profile, string id)
    {
        return profile.ClickPoints?.Any(
                   item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool ContainsPixelProbe(Profile profile, string id)
    {
        return profile.PixelProbes?.Any(
                   item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool ProbeHasExpectedRgb(Profile profile, string id)
    {
        var probe = profile.PixelProbes?.FirstOrDefault(
            item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        return !string.IsNullOrWhiteSpace(probe?.ExpectedRgb);
    }

    private static RegionSpec? FindRegion(Profile profile, string id)
    {
        return profile.Regions?.FirstOrDefault(
            item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateCoordinate(
        CoordinateSpec? spec,
        bool isXAxis,
        string path,
        ProfileValidationResult result)
    {
        if (spec is null)
        {
            result.AddError($"{path} must not be null.");
            return;
        }

        switch (spec.Mode)
        {
            case CoordinateMode.Client:
                if (spec.Value is < 0 or > 100000)
                {
                    result.AddError($"{path}.value must be between 0 and 100000 for client mode.");
                }

                break;

            case CoordinateMode.Percent:
                if (spec.Value is < 0 or > 1)
                {
                    result.AddError($"{path}.value must be between 0 and 1 for percent mode.");
                }

                break;

            case CoordinateMode.Anchor:
                var validAnchors = isXAxis ? XAnchors : YAnchors;
                if (spec.Anchor is null || !validAnchors.Contains(spec.Anchor.Value))
                {
                    var expected = isXAxis ? "left, center, right" : "top, center, bottom";
                    result.AddError($"{path}.anchor must be one of: {expected}.");
                }

                if (spec.Offset is < -10000 or > 10000)
                {
                    result.AddError($"{path}.offset must be between -10000 and 10000.");
                }

                break;

            default:
                result.AddError($"{path}.mode is invalid.");
                break;
        }
    }

    private static void ValidateMemory(MemoryOptions? memory, ProfileValidationResult result)
    {
        if (memory is null)
        {
            result.AddError("memory must not be null.");
            return;
        }

        if (memory.MinLength is < 2 or > 1024)
        {
            result.AddError("memory.minLength must be between 2 and 1024.");
        }
        else if (memory.MinLength < 4)
        {
            result.AddWarning("memory.minLength below 4 produces many false positives.");
        }

        if (memory.Encodings is null || memory.Encodings.Count == 0)
        {
            result.AddError("memory.encodings must contain at least one encoding.");
        }
        else
        {
            foreach (var encoding in memory.Encodings)
            {
                if (string.IsNullOrWhiteSpace(encoding)
                    || !AllowedEncodings.Contains(encoding.Trim()))
                {
                    result.AddError(
                        $"memory.encodings contains unsupported value '{encoding}'. Allowed: utf16le, utf8.");
                }
            }
        }

        if (memory.RegionTypes is null || memory.RegionTypes.Count == 0)
        {
            result.AddError("memory.regionTypes must contain at least one region type.");
        }
        else
        {
            foreach (var regionType in memory.RegionTypes)
            {
                if (string.IsNullOrWhiteSpace(regionType)
                    || !AllowedRegionTypes.Contains(regionType.Trim()))
                {
                    result.AddError(
                        $"memory.regionTypes contains unsupported value '{regionType}'. Allowed: private, image, mapped.");
                }
            }
        }

        if (memory.MaxResults is < 100 or > 1000000)
        {
            result.AddError("memory.maxResults must be between 100 and 1000000.");
        }

        if (memory.Filter is { Length: > 256 })
        {
            result.AddError("memory.filter is too long (max 256).");
        }
    }

    private static void ValidateHotkeys(HotkeyOptions? hotkeys, ProfileValidationResult result)
    {
        if (hotkeys is null)
        {
            result.AddError("hotkeys must not be null.");
            return;
        }

        var bindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["captureCursor"] = hotkeys.CaptureCursor,
            ["testClick"] = hotkeys.TestClick,
            ["readPixel"] = hotkeys.ReadPixel,
            ["memorySnapshot"] = hotkeys.MemorySnapshot,
            ["panic"] = hotkeys.Panic
        };

        var seenHotkeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (action, hotkey) in bindings)
        {
            if (string.IsNullOrWhiteSpace(hotkey))
            {
                result.AddWarning($"hotkeys.{action} is empty; that hotkey is disabled.");
                continue;
            }

            if (seenHotkeys.TryGetValue(hotkey, out var otherAction))
            {
                result.AddError($"hotkeys.{action} duplicates hotkeys.{otherAction} ('{hotkey}').");
            }
            else
            {
                seenHotkeys[hotkey] = action;
            }
        }
    }

    private static void ValidateLogging(LoggingOptions? logging, ProfileValidationResult result)
    {
        if (logging is null)
        {
            result.AddError("logging must not be null.");
            return;
        }

        if (string.IsNullOrWhiteSpace(logging.Level)
            || !AllowedLogLevels.Contains(logging.Level.Trim()))
        {
            result.AddError(
                $"logging.level must be one of: trace, debug, info, warn, error, none (got '{logging.Level}').");
        }
    }

    private static void ValidateId(
        string? id,
        string path,
        HashSet<string> seenIds,
        ProfileValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            result.AddError($"{path}.id must not be empty.");
            return;
        }

        if (!IdPattern.IsMatch(id))
        {
            result.AddError(
                $"{path}.id must match [A-Za-z0-9][A-Za-z0-9._-]{{0,63}} (got '{id}').");
            return;
        }

        if (!seenIds.Add(id))
        {
            result.AddError($"{path}.id '{id}' must be unique across click points and pixel probes.");
        }
    }
}
