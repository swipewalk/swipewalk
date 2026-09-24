using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Swipewalk.Collectors.Ios;

/// <summary>An installed .mobileprovision, reduced to what matters for signing the harness.</summary>
public sealed record ProvisioningProfile(
    string Uuid, string Name, string TeamId, string AppIdPattern, bool IsDevelopment, DateTime Expires,
    IReadOnlySet<string> Devices, bool AllDevices, string Path, bool IsXcodeManaged = false)
{
    /// <summary>True when the profile's app id ("*", "com.x.*" or exact) covers <paramref name="bundleId"/>.</summary>
    public bool Covers(string bundleId) =>
        AppIdPattern == "*"
        || (AppIdPattern.EndsWith(".*", StringComparison.Ordinal) && bundleId.StartsWith(AppIdPattern[..^1], StringComparison.Ordinal))
        || AppIdPattern == bundleId;

    public override string ToString() => $"'{Name}' ({TeamId}.{AppIdPattern}, expires {Expires:yyyy-MM-dd})";
}

/// <summary>
/// Reads provisioning profiles installed on this Mac, whether Xcode downloaded them or the user installed a
/// certificate and profile by hand (common in companies and on CI, with no Apple ID signed in to Xcode).
/// </summary>
public static class ProvisioningProfiles
{
    public static IEnumerable<string> Folders()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return System.IO.Path.Combine(home, "Library/MobileDevice/Provisioning Profiles");
        yield return System.IO.Path.Combine(home, "Library/Developer/Xcode/UserData/Provisioning Profiles");
    }

    public static IReadOnlyList<ProvisioningProfile> FindInstalled() =>
        [.. Folders().Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.mobileprovision"))
            .Select(TryRead)
            .OfType<ProvisioningProfile>()
            .DistinctBy(p => p.Uuid)];

    public static ProvisioningProfile? TryRead(string path)
    {
        try
        {
            return Parse(File.ReadAllBytes(path), path);
        }
        catch (Exception ex) when (ex is IOException or FormatException or System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>The profile is a CMS-signed plist; the XML payload is stored unencrypted inside it.</summary>
    internal static ProvisioningProfile? Parse(byte[] data, string path)
    {
        var text = Encoding.UTF8.GetString(data);
        var start = text.IndexOf("<?xml", StringComparison.Ordinal);
        var end = text.IndexOf("</plist>", StringComparison.Ordinal);
        if (start < 0 || end < start)
            return null;

        var dict = ReadDict(XDocument.Parse(text[start..(end + "</plist>".Length)]).Root!.Element("dict")!);
        var team = (dict.GetValueOrDefault("TeamIdentifier") as List<object?>)?.OfType<string>().FirstOrDefault() ?? "";
        var entitlements = dict.GetValueOrDefault("Entitlements") as Dictionary<string, object?> ?? [];
        var appId = entitlements.GetValueOrDefault("application-identifier") as string ?? "";
        return new ProvisioningProfile(
            dict.GetValueOrDefault("UUID") as string ?? "",
            dict.GetValueOrDefault("Name") as string ?? "",
            team,
            appId.StartsWith(team + ".", StringComparison.Ordinal) ? appId[(team.Length + 1)..] : appId,
            entitlements.GetValueOrDefault("get-task-allow") is true,
            dict.GetValueOrDefault("ExpirationDate") as DateTime? ?? DateTime.MinValue,
            ((dict.GetValueOrDefault("ProvisionedDevices") as List<object?>)?.OfType<string>() ?? []).ToHashSet(),
            dict.GetValueOrDefault("ProvisionsAllDevices") is true,
            path,
            dict.GetValueOrDefault("IsXcodeManaged") is true);
    }

    private static Dictionary<string, object?> ReadDict(XElement dict)
    {
        var result = new Dictionary<string, object?>();
        var elements = dict.Elements().ToList();
        for (var i = 0; i + 1 < elements.Count; i += 2)
            result[elements[i].Value] = ReadValue(elements[i + 1]);
        return result;
    }

    private static object? ReadValue(XElement e) => e.Name.LocalName switch
    {
        "string" => e.Value,
        "true" => true,
        "false" => false,
        "integer" => long.Parse(e.Value, CultureInfo.InvariantCulture),
        "date" => DateTime.Parse(e.Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
        "array" => e.Elements().Select(ReadValue).ToList(),
        "dict" => ReadDict(e),
        _ => null,
    };
}
