namespace Swipewalk.Collectors;

/// <summary>
/// Remembers a device's original dark/light appearance while the appearance rescan (<c>scan --appearance
/// both</c>) has changed it (~/.config/swipewalk/restore/appearance-&lt;device&gt;.txt) -- the same shape as
/// <see cref="TextSizeRestore"/> for text size: a scan killed before it restores the appearance leaves the
/// file behind, and the next pre-flight check (or `swipewalk doctor`) puts it back.
/// </summary>
public static class AppearanceRestore
{
    public static string DefaultDirectory => TextSizeRestore.DefaultDirectory;

    /// <summary>Records <paramref name="original"/> ("dark" or "light") for <paramref name="device"/>,
    /// keeping an earlier record if one exists (the device is already switched; that record holds the real original).</summary>
    public static void Remember(string device, string original, string? directory = null)
    {
        var path = PathFor(device, directory);
        if (File.Exists(path))
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, original);
    }

    public static void Forget(string device, string? directory = null) => File.Delete(PathFor(device, directory));

    /// <summary>The appearance to put back on <paramref name="device"/>, or null when nothing is pending.</summary>
    public static string? Pending(string device, string? directory = null)
    {
        var path = PathFor(device, directory);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    private static string PathFor(string device, string? directory) =>
        Path.Combine(directory ?? DefaultDirectory,
            "appearance-" + string.Concat(device.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' ? c : '_')) + ".txt");
}
