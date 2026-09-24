using System.Text.Json;

namespace Swipewalk.Collectors.Android;

/// <summary>
/// Remembers a device's original accessibility settings, on the HOST machine, while a --screen-reader
/// capture has them changed (~/.config/swipewalk/restore/&lt;device&gt;-screenreader.json) -- the host-side
/// counterpart of the device-side marker harness/android's <c>TalkBackCollector.kt</c> writes (its own
/// <c>RestoreMarkerFile</c>) and the on-device safety timer (harness/android/ttsengine's
/// <c>SafetyTimerReceiver</c>): three independent copies of "what to put back", so losing any one of them
/// (this host process being killed before it writes here, the device-side marker being lost, the phone
/// being unreachable from this host) still leaves at least one path to restore the device. Mirrors
/// <see cref="Swipewalk.Collectors.TextSizeRestore"/>'s shape for the iOS/Android large-text safeguard.
///
/// Written by <see cref="AndroidHarness.RunScreenReaderCaptureAsync"/> BEFORE it changes anything on the
/// device (not after a successful round trip -- writing it only once the change is confirmed would miss
/// exactly the case this exists for: the process dying partway through). Checked by
/// <see cref="Swipewalk.Collectors.Preflight.Preflight.AndroidAsync"/> on every run with
/// <c>--screen-reader</c>, which restores directly with plain <c>adb shell settings</c> commands -- no
/// harness/instrumentation needed for this path, so it still works even if the device-side marker or the
/// harness app itself was lost.
/// </summary>
public static class ScreenReaderSettingsRestore
{
    /// <summary>The accessibility settings this can remember and restore. <see cref="Null"/> stands in for
    /// "the setting was unset", the same sentinel <c>TalkBackCollector.kt</c>'s own marker uses, since an
    /// empty string can't round-trip through <c>settings put</c>/<c>settings delete</c> unambiguously.</summary>
    public sealed record Snapshot(string EnabledServices, string AccessibilityEnabled, string TouchExploration, string TtsDefaultSynth)
    {
        public const string Null = "\u0000null\u0000";
    }

    public static string DefaultDirectory => Path.Combine(Path.GetDirectoryName(UserSettings.DefaultPath)!, "restore");

    /// <summary>Records <paramref name="original"/> for <paramref name="device"/>, keeping an earlier
    /// record if one exists (mirrors <see cref="Swipewalk.Collectors.TextSizeRestore.Remember"/>: if a
    /// record is already there, the settings are already changed and that earlier record holds the real
    /// original, not this call's -- which would be capturing an already-changed state).</summary>
    public static void Remember(string device, Snapshot original, string? directory = null)
    {
        var path = PathFor(device, directory);
        if (File.Exists(path))
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(original));
    }

    public static void Forget(string device, string? directory = null)
    {
        try { File.Delete(PathFor(device, directory)); } catch (IOException) { /* best effort */ }
    }

    /// <summary>The settings to put back on <paramref name="device"/>, or null when nothing is pending.</summary>
    public static Snapshot? Pending(string device, string? directory = null)
    {
        var path = PathFor(device, directory);
        if (!File.Exists(path))
            return null;
        try { return JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(path)); }
        catch (JsonException) { return null; } // a corrupted/partial file is the same as nothing pending
    }

    private static string PathFor(string device, string? directory) =>
        Path.Combine(directory ?? DefaultDirectory,
            string.Concat(device.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' ? c : '_')) + "-screenreader.json");
}
