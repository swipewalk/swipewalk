using System.Diagnostics;
using System.Text.Json;
using Swipewalk.Collectors.Android;
using Swipewalk.Core.Model;

namespace Swipewalk.Collectors;

/// <summary>
/// An app installed on a device, for the New scan page's app picker (and later the CLI).
/// </summary>
/// <param name="Id">Android package or iOS bundle id.</param>
/// <param name="Label">Human-readable app name, when it was cheap to get. Null on Android, where getting a
/// label needs one extra command per app (too slow for a list); present on iOS.</param>
public sealed record AppListing(string Id, string? Label)
{
    /// <summary>
    /// What to read for this app as one accessible name (Pages/AppPickerPage's list row): the id alone, or the
    /// name followed by the id when one is known, so two apps sharing a name (or a generic MAUI sample name)
    /// stay distinguishable.
    /// </summary>
    public string AccessibleName => Label is null ? Id : $"{Label}, {Id}";
}

/// <summary>
/// Apps installed on a device, for the New scan page's app picker
/// (<see cref="Swipewalk.Desktop"/>/Pages/AppPickerPage) and later CLI autocomplete -- kept in the collectors
/// layer, next to the other device/app queries (<see cref="Devices"/>, <see cref="AppInstaller"/>), rather than
/// in the desktop app, so it isn't tied to MAUI. Defaults to apps a person would actually want to scan
/// (third-party / user apps); pass <c>includeSystem</c> to add the platform's own apps too.
/// </summary>
public static class AppsList
{
    /// <summary>Lists apps on the given device, dispatching to the right platform query.</summary>
    public static Task<IReadOnlyList<AppListing>> ForDeviceAsync(DeviceInfo device, bool includeSystem = false) =>
        device.Platform == Platform.Android
            ? AndroidAsync(device.Id, includeSystem)
            : device.IsPhysical
                ? IosDeviceAsync(device.Id, includeSystem)
                : IosSimulatorAsync(device.Id, includeSystem);

    /// <summary>
    /// Installed packages via <c>pm list packages -3</c> (third-party only) or, with <paramref name="includeSystem"/>,
    /// the full <c>pm list packages</c>. No app labels: Android has no single call that returns them, and a call
    /// per package would make the list slow to show, so the id is shown alone (see <see cref="AppListing.Label"/>).
    /// </summary>
    public static async Task<IReadOnlyList<AppListing>> AndroidAsync(string? serial, bool includeSystem = false)
    {
        var adb = new Adb(await new Adb(serial).ResolveSerialAsync());
        var output = includeSystem
            ? await adb.RunAsync("shell", "pm", "list", "packages")
            : await adb.RunAsync("shell", "pm", "list", "packages", "-3");
        return [.. ParsePmPackages(output).OrderBy(id => id, StringComparer.Ordinal).Select(id => new AppListing(id, null))];
    }

    /// <summary>Parses <c>pm list packages</c> output ("package:id" per line), for both the full and -3 (third-party-only) listing.</summary>
    internal static IReadOnlyList<string> ParsePmPackages(string output) =>
        [.. output.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("package:", StringComparison.Ordinal))
            .Select(l => l[8..])
            .Where(l => l.Length > 0)];

    /// <summary>
    /// Apps on a booted Simulator via <c>xcrun simctl listapps</c>, with display names. Only "User" apps
    /// (installed, not Apple's own) unless <paramref name="includeSystem"/>.
    /// </summary>
    public static async Task<IReadOnlyList<AppListing>> IosSimulatorAsync(string udid, bool includeSystem = false)
    {
        var listInfo = new ProcessStartInfo("xcrun") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "simctl", "listapps", udid })
            listInfo.ArgumentList.Add(arg);
        using var list = Process.Start(listInfo) ?? throw new InvalidOperationException("Could not start xcrun.");
        var plistTask = list.StandardOutput.ReadToEndAsync();
        var listErrorTask = list.StandardError.ReadToEndAsync();
        await list.WaitForExitAsync();
        var (plist, listError) = (await plistTask, await listErrorTask);
        if (list.ExitCode != 0)
            throw new InvalidOperationException($"simctl listapps failed: {listError.Trim()}");
        return await ParseSimctlListApps(plist, includeSystem);
    }

    /// <summary>
    /// Parses <c>simctl listapps</c> output: an old-style ("OpenStep") plist, not JSON or XML, which <c>plutil</c>
    /// (ships with Xcode, so no extra dependency) converts to JSON: an object keyed by bundle id, each with
    /// <c>ApplicationType</c> ("User" or "System") and a display name.
    /// </summary>
    internal static async Task<IReadOnlyList<AppListing>> ParseSimctlListApps(string rawPlist, bool includeSystem)
    {
        var convertInfo = new ProcessStartInfo("plutil")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[] { "-convert", "json", "-o", "-", "-" })
            convertInfo.ArgumentList.Add(arg);
        using var convert = Process.Start(convertInfo) ?? throw new InvalidOperationException("Could not start plutil.");
        await convert.StandardInput.WriteAsync(rawPlist);
        convert.StandardInput.Close();
        var jsonTask = convert.StandardOutput.ReadToEndAsync();
        var convertErrorTask = convert.StandardError.ReadToEndAsync();
        await convert.WaitForExitAsync();
        var (json, convertError) = (await jsonTask, await convertErrorTask);
        if (convert.ExitCode != 0)
            throw new InvalidOperationException($"Could not read the Simulator's app list: {convertError.Trim()}");

        using var doc = JsonDocument.Parse(json);
        var apps = new List<AppListing>();
        foreach (var app in doc.RootElement.EnumerateObject())
        {
            var info = app.Value;
            var isUser = Str(info, "ApplicationType") == "User";
            if (!includeSystem && !isUser)
                continue;
            var id = Str(info, "CFBundleIdentifier") ?? app.Name;
            var label = Str(info, "CFBundleDisplayName") ?? Str(info, "CFBundleName");
            apps.Add(new AppListing(id, label));
        }
        return [.. apps.OrderBy(a => a.Label ?? a.Id, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Apps on a physical iPhone via <c>devicectl device info apps</c>. Without <paramref name="includeSystem"/>,
    /// Apple's own apps are already excluded by devicectl itself (no <c>--include-all-apps</c>); with it,
    /// everything is listed. Both cases give reliable filtering, unlike a heuristic on the app data.
    /// </summary>
    public static async Task<IReadOnlyList<AppListing>> IosDeviceAsync(string udid, bool includeSystem = false)
    {
        var json = Path.Combine(Path.GetTempPath(), $"swipewalk-apps-{Guid.NewGuid():N}.json");
        try
        {
            var args = new List<string> { "devicectl", "device", "info", "apps", "--device", udid, "--json-output", json };
            if (includeSystem)
                args.Add("--include-all-apps");
            var info = new ProcessStartInfo("xcrun") { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in args)
                info.ArgumentList.Add(arg);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start xcrun.");
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0 || !File.Exists(json))
                throw new InvalidOperationException($"devicectl device info apps failed: {(await errorTask).Trim()}");
            return ParseDevicectlApps(await File.ReadAllTextAsync(json));
        }
        finally
        {
            File.Delete(json);
        }
    }

    /// <summary>Parses <c>devicectl device info apps --json-output</c> (result.apps[].bundleIdentifier/name).</summary>
    internal static IReadOnlyList<AppListing> ParseDevicectlApps(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("result", out var result) || !result.TryGetProperty("apps", out var apps))
            return [];
        var list = new List<AppListing>();
        foreach (var app in apps.EnumerateArray())
        {
            var id = Str(app, "bundleIdentifier");
            if (id is null)
                continue;
            list.Add(new AppListing(id, Str(app, "name")));
        }
        return [.. list.OrderBy(a => a.Label ?? a.Id, StringComparer.OrdinalIgnoreCase)];
    }

    private static string? Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
