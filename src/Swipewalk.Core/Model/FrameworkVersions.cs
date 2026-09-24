namespace Swipewalk.Core.Model;

/// <summary>
/// Compares a detected <see cref="ScreenSnapshot.FrameworkVersion"/> (e.g. "10.0.60", with any "+commit"
/// suffix already stripped by the collector that read it) against known fixed-in versions cited in rule
/// messages. Never guesses: an unparsable or missing version compares as unknown, not as any particular side
/// of a threshold.
/// </summary>
public static class FrameworkVersions
{
    /// <summary>
    /// The Microsoft.Maui.Controls version that fixed iOS live text-size updates
    /// (dotnet/maui#34445, merged 2026-06-23, .NET 10 SR10; see RuleSources).
    /// </summary>
    public static readonly Version MauiLiveTextSizeFix = new(10, 0, 100);

    /// <summary>
    /// Parses a plain major.minor.patch version string into a comparable <see cref="Version"/>; null when
    /// <paramref name="version"/> is null/blank or isn't in that shape (for example a prerelease tag such as
    /// "-rc.1" that <see cref="Version"/> cannot parse). Also strips a "+commit" suffix, in case a caller
    /// passes the raw AssemblyInformationalVersion instead of the already-stripped form.
    /// </summary>
    public static Version? Parse(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return null;
        var core = version.Split('+', 2)[0].Trim();
        return Version.TryParse(core, out var parsed) ? parsed : null;
    }
}
