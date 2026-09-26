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

    /// <summary>
    /// Keystore to sign an Android App Bundle's device-specific .apks with, instead of the standard Android
    /// debug key (see <see cref="Bundletool.DebugKeystorePath"/>). The passwords are read from environment
    /// variables rather than taken as constructor arguments, and passed to bundletool via a private temp file
    /// (not its own <c>pass:</c> option) so nothing puts them on the command line, in swipewalk.json, or in any
    /// process's argument list: SWIPEWALK_KEYSTORE_PASSWORD (required), SWIPEWALK_KEY_PASSWORD (optional; see
    /// <see cref="ReadPasswords"/> for what leaving it out means).
    /// </summary>
    public sealed record AndroidBundleSigning(string Keystore, string KeystoreAlias)
    {
        /// <summary>
        /// KeyPassword is null unless SWIPEWALK_KEY_PASSWORD is set: bundletool tries the keystore password for
        /// the key too when --key-pass is left out (its own --help: "If this flag is not set, the keystore
        /// password will be tried"), which is right for the common case of a self-managed keystore where both
        /// passwords are the same, without Swipewalk having to assume that and pass it explicitly.
        /// </summary>
        internal (string StorePassword, string? KeyPassword) ReadPasswords()
        {
            var store = Environment.GetEnvironmentVariable("SWIPEWALK_KEYSTORE_PASSWORD")
                ?? throw new InvalidOperationException(
                    "--keystore needs the SWIPEWALK_KEYSTORE_PASSWORD environment variable set (a keystore " +
                    "password is never taken on the command line or put in swipewalk.json). Set " +
                    "SWIPEWALK_KEY_PASSWORD too if the key's own password is different.");
            return (store, Environment.GetEnvironmentVariable("SWIPEWALK_KEY_PASSWORD"));
        }
    }

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

    /// <summary>
    /// Installs an .apk, or an Android App Bundle (.aab, via bundletool -- see
    /// <see cref="InstallAndroidBundleAsync"/>), on the Android device, and returns its package name.
    /// </summary>
    public static Task<string?> InstallAndroidAsync(
        string apkPath, string? serial, string? bundletoolPath = null, AndroidBundleSigning? signing = null, IProgress<string>? log = null)
    {
        if (apkPath.EndsWith(".aab", StringComparison.OrdinalIgnoreCase))
            return InstallAndroidBundleAsync(apkPath, serial, bundletoolPath, signing, log);
        return InstallAndroidApkAsync(apkPath, serial);
    }

    private static async Task<string?> InstallAndroidApkAsync(string apkPath, string? serial)
    {
        if (!apkPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Android --install expects an .apk or .aab file.");

        EnsureNotFastDeploymentBuild(apkPath, ".apk");

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

    /// <summary>
    /// Installs an Android App Bundle via Google's bundletool: builds a set of .apks matching the connected
    /// device (<c>build-apks --connected-device</c>), then installs them (<c>install-apks</c>). bundletool is
    /// never downloaded by Swipewalk (see <see cref="Bundletool.Locate"/>'s remarks) and, without
    /// <paramref name="signing"/>, signs the .apks with the standard Android debug key
    /// (<see cref="Bundletool.DebugKeystorePath"/>, created with keytool if it doesn't already exist -- see
    /// that property's remarks for why bundletool itself won't) -- a debug-signed build is only for scanning;
    /// it differs from the app-signing key Play would use for the same bundle, so this is never a substitute
    /// for a store build. Installing over an app already installed with a different key fails with a plain
    /// message pointing at --keystore or uninstalling first (see the "signatures do not match" check below).
    /// </summary>
    private static async Task<string?> InstallAndroidBundleAsync(
        string aabPath, string? serial, string? bundletoolPath, AndroidBundleSigning? signing, IProgress<string>? log)
    {
        var tool = Bundletool.Locate(bundletoolPath) ?? throw new InvalidOperationException(Bundletool.NotFoundMessage);
        // Found unconditionally (not just for a .jar): needed to run bundletool.jar, and separately as the
        // likely home of keytool (same JDK/JRE bin folder) when the standard debug keystore has to be created
        // below. A native bundletool executable with no java found at all still works when the debug keystore
        // already exists or --keystore is given; keytool is then found via PATH only (FindKeytool's fallback).
        var java = Bundletool.FindJava();
        if (tool.IsJar && java is null)
            throw new InvalidOperationException(Bundletool.JavaNotFoundMessage);

        EnsureNotFastDeploymentBuild(aabPath, ".aab");

        if (signing is null && !await Bundletool.EnsureDebugKeystoreAsync(java))
            throw new InvalidOperationException(Bundletool.DebugKeystoreCreateFailedMessage);

        var fullAabPath = Path.GetFullPath(aabPath);
        var package = await DumpManifestPackageAsync(tool, java, fullAabPath);

        var resolvedSerial = await new Adb(serial).ResolveSerialAsync();
        var adb = new Adb(resolvedSerial);
        var adbPath = Adb.ExecutablePath();
        // Only needed as a fallback if bundletool's own dump manifest above didn't give the package name --
        // captured before installing, the same before/after diff the plain-.apk path uses without aapt2.
        var before = package is null ? PackageSet(await adb.RunAsync("shell", "pm", "list", "packages", "-3")) : null;
        var apksPath = Path.Combine(Path.GetTempPath(), $"swipewalk-{Guid.NewGuid():N}.apks");
        // Password files rather than --ks-pass=pass:<value>/--key-pass=pass:<value>: bundletool has no way to
        // take a password from an environment variable (only 'pass:<value>' or 'file:<path>' -- checked
        // against its own --help), and 'pass:' would put the plaintext password in this process's argument
        // list, visible to anything else on the machine that can list processes for the moment bundletool
        // runs. Written with 0600-equivalent permissions where supported and deleted in the same finally as
        // apksPath.
        string? storePasswordFile = null;
        string? keyPasswordFile = null;
        try
        {
            List<string> buildArgs =
            [
                "build-apks",
                $"--bundle={fullAabPath}",
                $"--output={apksPath}",
                "--connected-device",
                $"--device-id={resolvedSerial}",
                $"--adb={adbPath}",
            ];
            // The bundletool.jar bundled with the .NET Android SDK workload doesn't embed aapt2 (unlike
            // Google's own bundletool-all.jar release), so build-apks needs it pointed at explicitly; found
            // the same way ApkPackageNameAsync finds it, from the Android SDK's build-tools.
            if (Aapt2Path() is { } aapt2)
                buildArgs.Add($"--aapt2={aapt2}");
            if (signing is not null)
            {
                var (storePassword, keyPassword) = signing.ReadPasswords();
                storePasswordFile = WriteTempPasswordFile(storePassword);
                buildArgs.Add($"--ks={Path.GetFullPath(signing.Keystore)}");
                buildArgs.Add($"--ks-key-alias={signing.KeystoreAlias}");
                buildArgs.Add($"--ks-pass=file:{storePasswordFile}");
                // key-pass is optional: if the key's own password is the same as the keystore's (the common
                // case for a self-managed keystore), leaving it out lets bundletool try the keystore password
                // for the key too, rather than Swipewalk assuming they're the same.
                if (keyPassword is not null)
                {
                    keyPasswordFile = WriteTempPasswordFile(keyPassword);
                    buildArgs.Add($"--key-pass=file:{keyPasswordFile}");
                }
            }
            else
            {
                log?.Report(
                    "No --keystore given: signing with the standard Android debug key (~/.android/debug.keystore, " +
                    "created by Swipewalk with keytool if it doesn't already exist). That differs from your store build's " +
                    "signing key -- for scanning only.");
            }

            var (buildExit, buildOutput) = await Bundletool.RunAsync(tool, java, buildArgs);
            if (buildExit != 0)
                throw new InvalidOperationException($"bundletool build-apks failed:\n{buildOutput.Trim()}");

            var (installExit, installOutput) = await Bundletool.RunAsync(
                tool, java, ["install-apks", $"--apks={apksPath}", $"--adb={adbPath}", $"--device-id={resolvedSerial}"]);
            if (installExit != 0)
                throw new InvalidOperationException(installOutput.Contains("signatures do not match", StringComparison.OrdinalIgnoreCase)
                    ? $"bundletool install-apks failed: the app is already installed, signed with a different key than this install would " +
                      $"use. Uninstall it first (adb uninstall <package>), or pass --keystore with the matching key.\n{installOutput.Trim()}"
                    : $"bundletool install-apks failed:\n{installOutput.Trim()}");
        }
        finally
        {
            File.Delete(apksPath);
            if (storePasswordFile is not null)
                File.Delete(storePasswordFile);
            if (keyPasswordFile is not null)
                File.Delete(keyPasswordFile);
        }

        if (package is not null)
            return package;
        var added = PackageSet(await adb.RunAsync("shell", "pm", "list", "packages", "-3")).Except(before!).ToList();
        return added.Count == 1 ? added[0] : null;
    }

    /// <summary>The package name from a .aab's manifest, via bundletool's own <c>dump manifest</c> (no aapt2
    /// needed -- aapt2 doesn't read bundles).</summary>
    private static async Task<string?> DumpManifestPackageAsync(Bundletool.Location tool, string? java, string aabPath)
    {
        var (exitCode, output) = await Bundletool.RunAsync(tool, java, ["dump", "manifest", $"--bundle={aabPath}", "--xpath=/manifest/@package"]);
        var trimmed = output.Trim().Trim('"');
        return exitCode == 0 && trimmed is { Length: > 0 } && !trimmed.Contains(' ') && !trimmed.Contains('\n') ? trimmed : null;
    }

    private static void EnsureNotFastDeploymentBuild(string path, string extension)
    {
        using var zip = ZipFile.OpenRead(path);
        if (IsFastDeploymentApk(zip.Entries.Select(e => e.FullName)))
            throw new InvalidOperationException(
                $"This is a .NET MAUI Debug build that uses fast deployment: its code is not inside the {extension}, so it cannot run on " +
                "its own. Build Release, or Debug with -p:EmbedAssembliesIntoApk=true.");
    }

    /// <summary>
    /// Writes a password as the sole line of a private temp file, for bundletool's --ks-pass=file:/
    /// --key-pass=file: (see InstallAndroidBundleAsync's remarks on why not --ks-pass=pass:&lt;value&gt;). Best-
    /// effort 0600 permissions on Unix, set at creation time (not after writing) so the password is never
    /// briefly readable at default permissions; Windows has no equivalent single call, so the file relies on
    /// being under the per-user temp folder and deleted right after bundletool exits.
    ///
    /// Deliberate, not a leak (see the CodeQL suppression on the write below): bundletool's own --ks-pass/
    /// --key-pass only accept 'pass:&lt;value&gt;' (exposed in this process's argument list, readable by
    /// anything else on the machine for as long as bundletool runs -- confirmed, not just assumed) or
    /// 'file:&lt;path&gt;' (checked via `bundletool help build-apks`; no environment-variable form exists). This
    /// is the safer of the two options bundletool itself offers, not an alternative to a real secrets store.
    /// </summary>
    private static string WriteTempPasswordFile(string password)
    {
        var path = Path.Combine(Path.GetTempPath(), $"swipewalk-{Guid.NewGuid():N}.pass");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        try
        {
            using (var stream = new FileStream(path, options))
            using (var writer = new StreamWriter(stream))
                // codeql[cs/clear-text-storage-of-sensitive-information] see this method's doc comment: a
                // private, 0600, per-user temp file deleted immediately after use is bundletool's own safer
                // alternative to passing the password as a command-line argument.
                writer.Write(password);
        }
        catch (PlatformNotSupportedException)
        {
            // UnixCreateMode isn't honored on this platform; fall back to a normal create (still deleted
            // right after use either way).
            // codeql[cs/clear-text-storage-of-sensitive-information] same justification as the write above.
            File.WriteAllText(path, password);
        }
        return path;
    }

    /// <summary>The package name inside an .apk, via the Android SDK's aapt2 (build-tools); null without it.</summary>
    internal static async Task<string?> ApkPackageNameAsync(string apkPath)
    {
        var aapt2 = Aapt2Path();
        if (aapt2 is null)
            return null;
        var (exitCode, output) = await RunAsync(aapt2, "dump", "packagename", Path.GetFullPath(apkPath));
        return exitCode == 0 && output.Trim() is { Length: > 0 } name && !name.Contains(' ') ? name : null;
    }

    /// <summary>The newest aapt2 in the Android SDK's build-tools, if the SDK is installed; null otherwise.</summary>
    private static string? Aapt2Path() =>
        Adb.SdkRoots()
            .Select(sdk => Path.Combine(sdk, "build-tools"))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateDirectories(dir).OrderByDescending(d => d, StringComparer.Ordinal))
            .Select(dir => Path.Combine(dir, OperatingSystem.IsWindows() ? "aapt2.exe" : "aapt2"))
            .FirstOrDefault(File.Exists);

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
