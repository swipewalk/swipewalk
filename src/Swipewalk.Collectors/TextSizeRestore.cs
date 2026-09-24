namespace Swipewalk.Collectors;

/// <summary>
/// Remembers a device's original text size while a large-text check has changed it
/// (~/.config/swipewalk/restore/&lt;device&gt;.txt). A scan that is killed before it restores the size (a crash,
/// a closed terminal) leaves the file behind, and the next pre-flight check puts the size back.
/// </summary>
public static class TextSizeRestore
{
    public static string DefaultDirectory => Path.Combine(Path.GetDirectoryName(UserSettings.DefaultPath)!, "restore");

    /// <summary>Records <paramref name="original"/> for <paramref name="device"/>, keeping an earlier record if one exists.</summary>
    public static void Remember(string device, string original, string? directory = null)
    {
        var path = PathFor(device, directory);
        if (File.Exists(path))
            return; // the size is already enlarged; the earlier record holds the real original
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, original);
    }

    public static void Forget(string device, string? directory = null) => File.Delete(PathFor(device, directory));

    /// <summary>The text size to put back on <paramref name="device"/>, or null when nothing is pending.</summary>
    public static string? Pending(string device, string? directory = null)
    {
        var path = PathFor(device, directory);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    private static string PathFor(string device, string? directory) =>
        Path.Combine(directory ?? DefaultDirectory,
            string.Concat(device.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' ? c : '_')) + ".txt");
}
