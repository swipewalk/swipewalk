using Swipewalk.Core.Reports;

namespace Swipewalk.Collectors.Android;

/// <summary>
/// Reads and sets Android's dark/light theme via <c>adb shell cmd uimode night</c>, for the appearance
/// rescan (<c>scan --appearance both</c>; see <see cref="Collectors.AppearanceRestore"/>). Each device state
/// change is a single, fast adb round trip -- unlike the large-text check, this needs no Settings navigation
/// or app restart of its own.
/// </summary>
public static class AndroidAppearance
{
    /// <summary>The device's current mode ("dark" or "light"), read from `cmd uimode night`'s
    /// "Night mode: yes/no/custom" output. A device left in "custom" (follows a schedule) reads back
    /// whichever mode it currently resolves to, the same as the command itself reports.</summary>
    public static async Task<string> ReadAsync(string? serial) =>
        Parse(await new Adb(serial).RunAsync("shell", "cmd", "uimode", "night"));

    public static Task SetAsync(string? serial, string appearance) =>
        new Adb(serial).RunAsync("shell", "cmd", "uimode", "night", appearance == AppearanceLabels.Dark ? "yes" : "no");

    internal static string Parse(string output) =>
        output.Contains("Night mode: yes", StringComparison.OrdinalIgnoreCase) ? AppearanceLabels.Dark : AppearanceLabels.Light;
}
