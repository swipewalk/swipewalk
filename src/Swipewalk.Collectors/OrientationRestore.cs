namespace Swipewalk.Collectors;

/// <summary>
/// Remembers a device's original screen-rotation state while the orientation rescan (<c>scan --orientation
/// both</c>) has changed it (~/.config/swipewalk/restore/orientation-&lt;device&gt;.txt) -- the same shape as
/// <see cref="AppearanceRestore"/> for dark/light appearance and <see cref="TextSizeRestore"/> for text size:
/// a scan killed before it restores orientation leaves the file behind, and the next pre-flight check (or
/// <c>swipewalk doctor</c>) puts it back. The stored value is opaque to this class -- each platform's own
/// preflight/doctor code parses it back: Android writes <c>"&lt;accelerometerRotation&gt;,&lt;userRotation&gt;"</c>
/// (e.g. "1,0"); the iOS Simulator writes the orientation to restore ("portrait" or "landscapeLeft").
/// </summary>
public static class OrientationRestore
{
    public static string DefaultDirectory => TextSizeRestore.DefaultDirectory;

    /// <summary>Records <paramref name="original"/> for <paramref name="device"/>, keeping an earlier record
    /// if one exists (the device is already rotated; that record holds the real original).</summary>
    public static void Remember(string device, string original, string? directory = null)
    {
        var path = PathFor(device, directory);
        if (File.Exists(path))
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, original);
    }

    public static void Forget(string device, string? directory = null) => File.Delete(PathFor(device, directory));

    /// <summary>The state to put back on <paramref name="device"/>, or null when nothing is pending.</summary>
    public static string? Pending(string device, string? directory = null)
    {
        var path = PathFor(device, directory);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    private static string PathFor(string device, string? directory) =>
        Path.Combine(directory ?? DefaultDirectory,
            "orientation-" + string.Concat(device.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' ? c : '_')) + ".txt");
}
