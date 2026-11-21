using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameProbe.Core.Models;

namespace GameProbe.Core.Profiles;

public sealed class ProfileStore
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    public Profile Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Profile file was not found.", fullPath);
        }

        using var stream = File.OpenRead(fullPath);
        var profile = JsonSerializer.Deserialize<Profile>(stream, SerializerOptions)
            ?? throw new JsonException($"Profile file '{fullPath}' contains an empty JSON value.");

        ProfileValidator.ThrowIfInvalid(profile);
        return profile;
    }

    public void Save(Profile profile, string path)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        ProfileValidator.ThrowIfInvalid(profile);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(profile, SerializerOptions);
        var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            File.WriteAllText(tempPath, json, Utf8NoBom);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
