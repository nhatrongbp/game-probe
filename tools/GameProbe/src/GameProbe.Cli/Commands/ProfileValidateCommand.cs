using System.Text.Json;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Profiles;

namespace GameProbe.Cli.Commands;

internal static class ProfileValidateCommand
{
    public static int Execute(string[] args, IActionLogger logger)
    {
        string? file = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h":
                case "--help":
                    HelpText.PrintProfileValidate();
                    return ExitCodes.Success;

                case "-f":
                case "--file":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("Thiếu giá trị cho --file.");
                        return ExitCodes.Usage;
                    }

                    file = args[++i];
                    break;

                default:
                    if (!args[i].StartsWith('-') && file is null)
                    {
                        file = args[i];
                        break;
                    }

                    Console.Error.WriteLine($"Option không hợp lệ: {args[i]}");
                    HelpText.PrintProfileValidate();
                    return ExitCodes.Usage;
            }
        }

        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("Thiếu đường dẫn profile. Dùng --file <path>.");
            HelpText.PrintProfileValidate();
            return ExitCodes.Usage;
        }

        var fullPath = Path.GetFullPath(file);
        var store = new ProfileStore();

        try
        {
            var profile = store.Load(fullPath);
            var validation = ProfileValidator.Validate(profile);

            logger.Info(
                "profile.validate",
                "Profile hợp lệ",
                new Dictionary<string, object?>
                {
                    ["file"] = fullPath,
                    ["name"] = profile.Name,
                    ["clickPoints"] = profile.ClickPoints.Count,
                    ["pixelProbes"] = profile.PixelProbes.Count
                });

            Console.WriteLine();
            Console.WriteLine($"Profile hợp lệ: {profile.Name}");
            Console.WriteLine($"  File:         {fullPath}");
            Console.WriteLine($"  Schema:       {profile.SchemaVersion}");
            Console.WriteLine($"  Click points: {profile.ClickPoints.Count}");
            Console.WriteLine($"  Pixel probes: {profile.PixelProbes.Count}");

            if (validation.Warnings.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Cảnh báo:");
                foreach (var warning in validation.Warnings)
                {
                    Console.WriteLine($"  - {warning}");
                }
            }

            return ExitCodes.Success;
        }
        catch (ProfileValidationException ex)
        {
            logger.Error("profile.invalid", "Profile không hợp lệ", new Dictionary<string, object?>
            {
                ["file"] = fullPath,
                ["errors"] = ex.Errors.Count
            });

            Console.Error.WriteLine($"Profile không hợp lệ: {fullPath}");
            foreach (var error in ex.Errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }

            return ExitCodes.Usage;
        }
        catch (FileNotFoundException)
        {
            logger.Error("profile.not_found", "Không tìm thấy profile", new Dictionary<string, object?>
            {
                ["file"] = fullPath
            });
            Console.Error.WriteLine($"Không tìm thấy file: {fullPath}");
            return ExitCodes.NotFound;
        }
        catch (UnauthorizedAccessException)
        {
            logger.Error("profile.access_denied", "Không có quyền đọc profile", new Dictionary<string, object?>
            {
                ["file"] = fullPath
            });
            Console.Error.WriteLine($"Không có quyền đọc file: {fullPath}");
            return ExitCodes.AccessDenied;
        }
        catch (JsonException ex)
        {
            logger.Error("profile.json_invalid", "JSON không hợp lệ", new Dictionary<string, object?>
            {
                ["file"] = fullPath,
                ["detail"] = ex.Message
            });
            Console.Error.WriteLine($"JSON không hợp lệ trong {fullPath}: {ex.Message}");
            return ExitCodes.Usage;
        }
        catch (IOException ex)
        {
            logger.Error("profile.io_error", ex.Message, new Dictionary<string, object?>
            {
                ["file"] = fullPath
            });
            Console.Error.WriteLine($"Lỗi đọc file {fullPath}: {ex.Message}");
            return ExitCodes.Failure;
        }
    }
}
