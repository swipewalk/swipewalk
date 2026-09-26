using System.Diagnostics;

namespace Swipewalk.Collectors.Android;

/// <summary>
/// Finds and runs Google's bundletool, the only supported way to install an Android App Bundle (.aab): it
/// builds a set of .apks matching one connected device (<c>build-apks --connected-device</c>), then installs
/// them (<c>install-apks</c>). Swipewalk never downloads bundletool itself -- see <see cref="Locate"/>'s
/// remarks -- so a missing tool fails with a clear, actionable message, the same convention as every other
/// pre-flight check (<see cref="Swipewalk.Collectors.Preflight.Preflight"/>).
/// </summary>
internal static class Bundletool
{
    /// <summary>Where bundletool was found, and whether it's a .jar (started as <c>java -jar &lt;path&gt;</c>)
    /// or a self-running executable such as Homebrew's wrapper (started directly).</summary>
    internal readonly record struct Location(string Path, bool IsJar);

    internal const string NotFoundMessage =
        "bundletool is needed to install an Android App Bundle (.aab) -- it isn't downloaded automatically. " +
        "Install it (`brew install bundletool`, or download the .jar from " +
        "https://github.com/google/bundletool/releases) and either put it on PATH or pass " +
        "--bundletool <path to bundletool or bundletool.jar>. If you build this app with the .NET Android " +
        "SDK workload, its own bundletool.jar is usually found automatically.";

    internal const string JavaNotFoundMessage =
        "bundletool needs a Java runtime to run its .jar, and none was found on PATH or JAVA_HOME. Install a " +
        "JRE (for example: brew install openjdk, or apt install default-jre-headless) or point JAVA_HOME at one.";

    /// <summary>
    /// The standard Android debug keystore's path: ~/.android/debug.keystore, the same one Android Studio and
    /// Gradle create and share. bundletool itself does NOT create this file -- confirmed from its own
    /// <c>--ks</c> help text ("If not set, the default debug keystore will be used if it exists. If not found
    /// the APKs will not be signed.") and by experiment (removing the file and running <c>build-apks
    /// --connected-device</c> without --ks produced an unsigned .apks that then failed to install with
    /// INSTALL_PARSE_FAILED_NO_CERTIFICATES) -- so <see cref="EnsureDebugKeystoreAsync"/> creates it with
    /// keytool when missing, matching Android's own convention (alias androiddebugkey, password "android"),
    /// rather than relying on bundletool to do it.
    /// </summary>
    public static string DebugKeystorePath { get; } =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android", "debug.keystore");

    internal const string DebugKeystoreCreateFailedMessage =
        "No --keystore was given, and the standard Android debug keystore (~/.android/debug.keystore) doesn't " +
        "exist yet; creating it with keytool failed. Create it yourself (for example: keytool -genkeypair " +
        "-keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android -keypass android " +
        "-keyalg RSA -keysize 2048 -validity 10950 -dname \"CN=Android Debug,O=Android,C=US\") -- the same " +
        "keystore Android Studio and Gradle create -- or pass --keystore.";

    /// <summary>
    /// Finds bundletool: a configured path first (--bundletool, or swipewalk.json's <c>bundletoolPath</c>),
    /// then <c>bundletool</c> on PATH (for example after <c>brew install bundletool</c>), then the
    /// bundletool.jar bundled inside the installed .NET Android SDK workload -- already on the machine of
    /// anyone who builds the .NET MAUI/Android app being scanned, under
    /// <c>&lt;dotnet install&gt;/packs/Microsoft.Android.Sdk.*/&lt;version&gt;/tools/bundletool.jar</c>.
    /// Returns null rather than throwing, so callers can show <see cref="NotFoundMessage"/> themselves.
    /// </summary>
    public static Location? Locate(string? configuredPath)
    {
        if (configuredPath is not null)
        {
            if (!File.Exists(configuredPath))
                throw new InvalidOperationException($"--bundletool {configuredPath} does not exist.");
            return new Location(configuredPath, configuredPath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase));
        }

        var onPath = FindOnPath(OperatingSystem.IsWindows() ? ["bundletool.bat", "bundletool.cmd", "bundletool.exe"] : ["bundletool"]);
        if (onPath is not null)
            return new Location(onPath, IsJar: false);

        var bundled = DotnetPacksRoots()
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateDirectories(root, "Microsoft.Android.Sdk.*"))
            .SelectMany(sdkDir => Directory.Exists(sdkDir) ? Directory.EnumerateDirectories(sdkDir) : [])
            .OrderByDescending(versionDir => Version.TryParse(System.IO.Path.GetFileName(versionDir), out var v) ? v : new Version(0, 0))
            .Select(versionDir => System.IO.Path.Combine(versionDir, "tools", "bundletool.jar"))
            .FirstOrDefault(File.Exists);
        return bundled is null ? null : new Location(bundled, IsJar: true);
    }

    /// <summary>Java on PATH, else JAVA_HOME/bin; null if neither has it (see <see cref="JavaNotFoundMessage"/>).</summary>
    public static string? FindJava()
    {
        var onPath = FindOnPath(OperatingSystem.IsWindows() ? ["java.exe"] : ["java"]);
        if (onPath is not null)
            return onPath;
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        var candidate = javaHome is null ? null : System.IO.Path.Combine(javaHome, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java");
        return candidate is not null && File.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// Creates <see cref="DebugKeystorePath"/> with keytool if it doesn't already exist -- see that property's
    /// remarks for why bundletool itself won't. Does nothing (and returns true) if the file already exists,
    /// whoever created it (Android Studio, Gradle, an earlier Swipewalk run, or by hand). Returns false if
    /// keytool can't be found or fails; callers show <see cref="DebugKeystoreCreateFailedMessage"/> then.
    /// </summary>
    public static async Task<bool> EnsureDebugKeystoreAsync(string? java)
    {
        if (File.Exists(DebugKeystorePath))
            return true;
        var keytool = FindKeytool(java);
        if (keytool is null)
            return false;

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DebugKeystorePath)!);
        var info = new ProcessStartInfo(keytool) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[]
                 {
                     "-genkeypair", "-keystore", DebugKeystorePath, "-alias", "androiddebugkey", "-storepass", "android",
                     "-keypass", "android", "-keyalg", "RSA", "-keysize", "2048", "-validity", "10950",
                     "-dname", "CN=Android Debug,O=Android,C=US",
                 })
            info.ArgumentList.Add(arg);
        try
        {
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start keytool.");
            await Task.WhenAll(process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync(), process.WaitForExitAsync());
            return process.ExitCode == 0 && File.Exists(DebugKeystorePath);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>keytool lives next to java in the same JDK/JRE's bin folder; also tried on PATH in case a JDK
    /// puts keytool there without java (uncommon, but cheap to check).</summary>
    private static string? FindKeytool(string? java)
    {
        var keytoolName = OperatingSystem.IsWindows() ? "keytool.exe" : "keytool";
        if (java is not null)
        {
            var nextToJava = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(java) ?? "", keytoolName);
            if (File.Exists(nextToJava))
                return nextToJava;
        }
        return FindOnPath([keytoolName]);
    }

    /// <summary>Runs one bundletool subcommand, returning its exit code and combined stdout+stderr.</summary>
    public static async Task<(int ExitCode, string Output)> RunAsync(Location tool, string? java, IReadOnlyList<string> args)
    {
        var info = new ProcessStartInfo(tool.IsJar ? java ?? throw new InvalidOperationException(JavaNotFoundMessage) : tool.Path)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (tool.IsJar)
        {
            info.ArgumentList.Add("-jar");
            info.ArgumentList.Add(tool.Path);
        }
        foreach (var arg in args)
            info.ArgumentList.Add(arg);

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start bundletool.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr, process.WaitForExitAsync());
        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    private static string? FindOnPath(IReadOnlyList<string> names)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (var name in names)
            {
                var candidate = System.IO.Path.Combine(dir, name);
                if (File.Exists(candidate))
                    return candidate;
            }
        return null;
    }

    /// <summary>
    /// Candidate dotnet install roots to search for the Android SDK workload's packs folder: the one running
    /// Swipewalk itself first (derived from where its own runtime is loaded from, since that's the same
    /// install that would have built the .aab being scanned), then DOTNET_ROOT and the well-known per-OS
    /// default locations.
    /// </summary>
    private static IEnumerable<string> DotnetPacksRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string?[] candidates =
        [
            DotnetRootFromRuntime(),
            Environment.GetEnvironmentVariable("DOTNET_ROOT"),
            OperatingSystem.IsWindows() ? @"C:\Program Files\dotnet" : null,
            OperatingSystem.IsMacOS() ? "/usr/local/share/dotnet" : null,
            !OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() ? "/usr/share/dotnet" : null,
            !OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() ? "/usr/lib/dotnet" : null,
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet"),
        ];
        foreach (var root in candidates)
            if (root is not null && seen.Add(root))
                yield return System.IO.Path.Combine(root, "packs");
    }

    /// <summary>System.Private.CoreLib.dll (this runtime's own assembly) lives at
    /// <c>&lt;dotnet root&gt;/shared/Microsoft.NETCore.App/&lt;version&gt;/System.Private.CoreLib.dll</c>, so
    /// its grandparent's parent is the dotnet install root -- works whether Swipewalk is run via `dotnet run`,
    /// a global tool, or a framework-dependent build, without hardcoding an install path.</summary>
    private static string? DotnetRootFromRuntime()
    {
        var dir = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location);
        for (var i = 0; i < 3 && dir is not null; i++)
            dir = System.IO.Path.GetDirectoryName(dir);
        return dir;
    }
}
