using System.Text.Json;

namespace Swipewalk.Collectors;

/// <summary>Per-user settings remembered between runs (~/.config/swipewalk/settings.json).</summary>
public sealed record UserSettings
{
    /// <summary>Apple developer team used to sign the iOS harness for physical devices.</summary>
    public string? AppleTeamId { get; init; }

    public static string DefaultPath => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
        "swipewalk", "settings.json");

    public static UserSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path)) ?? new() : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
