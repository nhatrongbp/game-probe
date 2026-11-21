namespace GameProbe.Core.Models;

public sealed class MemoryOptions
{
    public int MinLength { get; set; } = 4;

    public List<string> Encodings { get; set; } = new() { "utf16le", "utf8" };

    public List<string> RegionTypes { get; set; } = new() { "private" };

    public int MaxResults { get; set; } = 20000;

    public string Filter { get; set; } = "";
}
