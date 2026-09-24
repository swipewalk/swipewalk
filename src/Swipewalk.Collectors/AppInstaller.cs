using System.Diagnostics;
using System.IO.Compression;
using Swipewalk.Collectors.Android;
using Swipewalk.Collectors.Ios;
using Swipewalk.Core.Model;

namespace Swipewalk.Collectors;

/// <summary>
/// Installs an already-built app before scanning, so users can scan builds from anywhere (CI artifacts,
/// release builds, ad-hoc builds) without source code. The app is installed as-is: never rebuilt or re-signed.
/// </summary>
public static class AppInstaller
{
    /// <summary>
    /// The bundle id and detected framework of an installed iOS app. Framework detection only runs for a
    /// physical device here (a Simulator install is detected later, from the running app, by
    /// <see cref="IosCollector.CaptureAsync"/>/<see cref="IosCollector.DetectFrameworkAsync"/>, which is more
    /// reliable): a physical iPhone's installed bundle can't be read from the Mac, so this is the only chance
    /// to detect it, from the .app/.ipa file itself before it's installed.
    /// </summary>
    public sealed record InstalledIosApp(string? BundleId, AppFramework Framework = AppFramework.Unknown, string? FrameworkVersion = null);

    /// <summary>Opens the app's launcher activity.</summary>
    public static async Task LaunchAndroidAsync(string package, string? serial)
    {
        var adb = new Adb(await new Adb(serial).ResolveSerialAsync());
        var activity = (await adb.RunAsync("shell", "cmd", "package", "resolve-activity", "--brief", package))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).LastOrDefault(l => l.Contains('/'));
        if (activity is null)
            throw new InvalidOperationException($"{package} has no launcher activity; open the screen to scan by hand.");
        await adb.RunAsync("shell", "am", "start", "-W", "-n", activity);
    }

    /// <summary>
    /// .NET for Android (MAUI) Debug builds use fast deployment: the app's assemblies are pushed by the IDE and are not
    /// inside the .apk, so the .apk cannot run on its own. Detect that before installing.
    /// </summary>
    internal static bool IsFastDeploymentApk(IEnumerable<string> entries)
    {
        var list = entries.ToList();
        var dotnetApp = list.Any(e => e.EndsWith("/libmonodroid.so", StringComparison.Ordinal) || e.EndsWith("/libxamarin-app.so", StringComparison.Ordinal));
        // Embedded assemblies appear as assemblies/*.dll, an assembly store (*assemblies*.blob*), or in .NET 10 as lib_*.dll.so.
        var hasAssemblies = list.Any(e => e.Contains(".dll", StringComparison.Ordinal) || e.Contains("assemblies", StringComparison.Ordinal));
        return dotnetApp && !hasAssemblies;
    }

    /// <summary>Installs on the given iOS device, or the booted Simulator.</summary>
    public static async Task<InstalledIosApp> InstallIosAsync(string path, string? device)
    {
        var udid = device ?? await IosCollector.BootedSimulatorAsync()
            ?? throw new InvalidOperationException("No booted iOS Simulator found; boot one or pass --device <udid>.");
        var physical = (await Devices.IosAsync()).Any(d => d.Id == udid && d.IsPhysical);
        return await InstallIosAsync(path, udid, physical);
    }

    /// <summary>Installs an .apk on the Android device and returns its package name.</summary>
    public static async Task<string?> InstallAndroidAsync(string apkPath, string? serial)
    {
        if (apkPath.EndsWith(".aab", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "An .aab (Play bundle) cannot be installed directly. Build an .apk (for example with bundletool build-apks --mode=universal) " +
                "or install through Play internal testing, then scan by package.");
        if (!apkPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Android --install expects an .apk file.");

        using (var apk = ZipFile.OpenRead(apkPath))
        {
            if (IsFastDeploymentApk(apk.Entries.Select(e => e.FullName)))
                throw new InvalidOperationException(
                    "This is a .NET MAUI Debug build that uses fast deployment: its code is not inside the .apk, so it cannot run on " +
                    "its own. Build Release, or Debug with -p:EmbedAssembliesIntoApk=true.");
        }

        var adb = new Adb(await new Adb(serial).ResolveSerialAsync());
        var package = await ApkPackageNameAsync(apkPath);
        var before = PackageSet(await adb.RunAsync("shell", "pm", "list", "packages", "-3"));
        await adb.RunAsync("install", "-r", "-g", Path.GetFullPath(apkPath));
        if (package is not null)
            return package;
        // Without aapt2, a newly added package identifies the app; a reinstall can't be told apart.
        var added = PackageSet(await adb.RunAsync("shell", "pm", "list", "packages", "-3")).Except(before).ToList();
        return added.Count == 1 ? added[0] : null;
    }

    /// <summary>The package name inside an .apk, via the Android SDK's aapt2 (build-tools); null without it.</summary>
    internal static async Task<string?> ApkPackageNameAsync(string apkPath)
    {
        var aapt2 = Adb.SdkRoots()
            .Select(sdk => Path.Combine(sdk, "build-tools"))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateDirectories(dir).OrderByDescending(d => d, StringComparer.Ordinal))
            .Select(dir => Path.Combine(dir, OperatingSystem.IsWindows() ? "aapt2.exe" : "aapt2"))
            .FirstOrDefault(File.Exists);
        if (aapt2 is null)
            return null;
        var (exitCode, output) = await RunAsync(aapt2, "dump", "packagename", Path.GetFullPath(apkPath));
        return exitCode == 0 && output.Trim() is { Length: > 0 } name && !name.Contains(' ') ? name : null;
    }

    /// <summary>
    /// Installs a Simulator .app (or a .zip containing one) or a device .ipa. Device .ipa files must be signed for
    /// the device (development, ad-hoc including this device, or enterprise); App Store builds cannot be sideloaded.
    /// </summary>
    public static async Task<InstalledIosApp> InstallIosAsync(string path, string udid, bool physical)
    {
        path = Path.GetFullPath(path);
        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var extracted = Path.Combine(Path.GetTempPath(), $"swipewalk-app-{Guid.NewGuid():N}");
            ZipFile.ExtractToDirectory(path, extracted);
            path = Directory.EnumerateDirectories(extracted, "*.app", SearchOption.AllDirectories).FirstOrDefault()
                   ?? throw new InvalidOperationException("The .zip does not contain an .app bundle.");
        }

        var isIpa = path.EndsWith(".ipa", StringComparison.OrdinalIgnoreCase);
        if (!physical && isIpa)
            throw new InvalidOperationException(
                "An .ipa is built for devices and cannot run on the Simulator. Use a Simulator build (.app), or scan on a physical device.");
        if (!isIpa && !path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("iOS --install expects a Simulator .app (or .zip of one) or a device .ipa.");

        var (exitCode, output) = physical
            ? await RunAsync("xcrun", "devicectl", "device", "install", "app", "--device", udid, path)
            : await RunAsync("xcrun", "simctl", "install", udid, path);
        if (exitCode != 0)
            throw new InvalidOperationException(physical
                ? "The app could not be installed on the device. A device .ipa must be signed for it: a development or ad-hoc " +
                  $"build whose profile includes this device, or an enterprise build. App Store builds cannot be sideloaded; install " +
                  $"them from the App Store or TestFlight and scan by --bundle-id.\n{output.Trim()}"
                : $"The app could not be installed on the Simulator. Make sure it is a Simulator build.\n{output.Trim()}");

        var bundleId = isIpa ? await IpaBundleIdAsync(path) : await BundleIdAsync(path);
        // A physical iPhone's installed bundle can't be read from the Mac afterwards (see InstalledIosApp), so
        // this is detected here from the file, while it's still available, instead of after installing.
        var (framework, version) = physical ? DetectFrameworkFromInstallFile(path, isIpa) : (AppFramework.Unknown, null);
        return new InstalledIosApp(bundleId, framework, version);
    }

    /// <summary>
    /// Detects .NET MAUI from an installed .app directory, or a device .ipa's Payload/*.app, the same marker
    /// <see cref="IosCollector.DetectFrameworkInDirectory"/> uses on a Simulator (Microsoft.Maui.Controls.dll
    /// flat in the app bundle root). Reads only that one entry out of an .ipa rather than extracting it whole.
    /// </summary>
    internal static (AppFramework Framework, string? Version) DetectFrameworkFromInstallFile(string path, bool isIpa)
    {
        if (!isIpa)
        {
            var detected = IosCollector.DetectFrameworkInDirectory(path);
            return (detected.Framework, detected.Version);
        }

        using var zip = ZipFile.OpenRead(path);
        var entry = zip.Entries.FirstOrDefault(e =>
            e.FullName.StartsWith("Payload/", StringComparison.Ordinal)
            && e.FullName.EndsWith(".app/Microsoft.Maui.Controls.dll", StringComparison.Ordinal)
            && e.FullName.Count(c => c == '/') == 2);
        if (entry is null)
            return (AppFramework.Unknown, null);

        var dll = Path.Combine(Path.GetTempPath(), $"swipewalk-{Guid.NewGuid():N}.dll");
        entry.ExtractToFile(dll);
        try
        {
            return (AppFramework.Maui, IosCollector.ReadInformationalVersion(dll));
        }
        finally
        {
            File.Delete(dll);
        }
    }

    /// <summary>Reads the bundle id from Payload/*.app/Info.plist inside an .ipa.</summary>
    private static async Task<string?> IpaBundleIdAsync(string ipaPath)
    {
        using var zip = ZipFile.OpenRead(ipaPath);
        var entry = zip.Entries.FirstOrDefault(e =>
            e.FullName.StartsWith("Payload/", StringComparison.Ordinal) && e.FullName.EndsWith(".app/Info.plist", StringComparison.Ordinal)
            && e.FullName.Count(c => c == '/') == 2);
        if (entry is null)
            return null;
        var plist = Path.Combine(Path.GetTempPath(), $"swipewalk-{Guid.NewGuid():N}.plist");
        entry.ExtractToFile(plist);
        try
        {
            var (exitCode, output) = await RunAsync("/usr/libexec/PlistBuddy", "-c", "Print :CFBundleIdentifier", plist);
            return exitCode == 0 ? output.Trim() : null;
        }
        finally
        {
            File.Delete(plist);
        }
    }

    private static async Task<string?> BundleIdAsync(string appPath)
    {
        var (exitCode, output) = await RunAsync("/usr/libexec/PlistBuddy", "-c", "Print :CFBundleIdentifier", Path.Combine(appPath, "Info.plist"));
        return exitCode == 0 ? output.Trim() : null;
    }

    private static HashSet<string> PackageSet(string output) =>
        [.. output.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("package:", StringComparison.Ordinal)).Select(l => l[8..])];

    private static async Task<(int ExitCode, string Output)> RunAsync(string file, params string[] args)
    {
        var info = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        return await IosCollector.RunAsync(info);
    }
}
