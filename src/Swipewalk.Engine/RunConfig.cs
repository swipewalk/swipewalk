using System.Text.Json;
using System.Text.Json.Serialization;
using Swipewalk.Core.Model;

namespace Swipewalk.Engine;

/// <summary>
/// swipewalk.json: what to scan and how, so a whole run is one command (and CI-friendly).
/// Secrets never go in this file. Example:
/// <code>
/// {
///   "app": { "android": { "install": "app-release.apk" }, "ios": { "bundleId": "com.example.app" } },
///   "targets": [ { "platform": "android" }, { "platform": "ios", "device": "booted" } ],
///   "mode": "scan", "standard": "ada-title-ii", "largeText": true, "failOn": "wcag-issues"
/// }
/// </code>
/// </summary>
public sealed record RunConfig
{
    public AppConfig App { get; init; } = new();

    /// <summary>Devices to run on; each needs the app for its platform.</summary>
    public IReadOnlyList<TargetConfig> Targets { get; init; } = [];

    /// <summary>"scan" (the screen shown after launch) or "record" (interactive). "auto" is planned.</summary>
    public string Mode { get; init; } = "scan";

    public string? Standard { get; init; }
    public bool LargeText { get; init; } = true;

    /// <summary>scan only for now: also capture and check the device's other dark/light appearance -- see
    /// Swipewalk.Engine.ScanOptions.AppearanceBoth. Off by default.</summary>
    public bool Appearance { get; init; }

    /// <summary>scan only for now: also capture and check the screen rotated to the device's other
    /// orientation -- see Swipewalk.Engine.ScanOptions.OrientationBoth. Off by default.</summary>
    public bool Orientation { get; init; }

    /// <summary>scan only for now: also take a few further captures a few seconds apart, with no input, and
    /// check for content that kept changing on its own -- see Swipewalk.Engine.ScanOptions.AutoUpdateCheck.
    /// Off by default; never touches the device, unlike <see cref="Appearance"/>/<see cref="Orientation"/>.</summary>
    public bool AutoUpdateContent { get; init; }

    /// <summary>Seconds between each extra capture for <see cref="AutoUpdateContent"/> -- see
    /// Swipewalk.Engine.ScanOptions.AutoUpdateIntervalSeconds. Default 3.</summary>
    public double AutoUpdateInterval { get; init; } = 3.0;

    /// <summary>record: scan automatically when the screen changes; off by default, matching the CLI/desktop
    /// default -- see Swipewalk.Engine.ScanOptions.AutoScanOnScreenChange.</summary>
    public bool AutoScanOnScreenChange { get; init; }

    /// <summary>"ask", "always" or "never" -- see Swipewalk.Engine.LargeTextRestartPolicy. Null (default)
    /// means "never" for <c>mode: "record"</c> (an unattended `run` (CI) must not block waiting for an answer
    /// nobody will give) but "always" for <c>mode: "scan"</c> (a one-shot scan has no later screen to keep
    /// blocking on, so it keeps the automatic restart CI already relied on -- see
    /// Swipewalk.Engine.LargeTextRestartPolicies.Default); pass "ask" for an interactive `swipewalk run` at a
    /// terminal, either mode.</summary>
    public string? LargeTextRestart { get; init; }

    /// <summary>Android only for now: also drive TalkBack over every screen and capture what it actually
    /// says -- see Swipewalk.Engine.ScanOptions.ScreenReaderCapture. Off by default.</summary>
    public bool ScreenReaderCapture { get; init; }

    /// <summary>Required, alongside <see cref="ScreenReaderCapture"/>, before it changes a physical Android
    /// phone's accessibility settings -- swipewalk.json runs are non-interactive, so nothing can answer the
    /// CLI's --screen-reader-confirm prompt; this stands in for that answer instead. Ignored for emulators, and
    /// for <see cref="ScreenReaderCapture"/> false.</summary>
    public bool ScreenReaderConfirm { get; init; }
    public string? Framework { get; init; }
    public IReadOnlyList<string> Expect { get; init; } = [];

    /// <summary>Controls auto-navigation must never tap (reserved for the crawler).</summary>
    public IReadOnlyList<string> Avoid { get; init; } = [];

    /// <summary>"wcag-issues": exit code 3 when any WCAG issue (relevant to the standard) is found; "never": always 0.</summary>
    public string FailOn { get; init; } = "never";

    /// <summary>Where reports go; default: the run history.</summary>
    public string? Out { get; init; }

    public IosSigningConfig? IosSigning { get; init; }

    /// <summary>Android only: path to bundletool, for installing app.android.install when it's a .aab -- see
    /// Swipewalk.Engine.ScanOptions.BundletoolPath. Found automatically when not given.</summary>
    public string? BundletoolPath { get; init; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static RunConfig Load(string path)
    {
        try
        {
            var config = JsonSerializer.Deserialize<RunConfig>(File.ReadAllText(path), Options)
                ?? throw new InvalidOperationException($"{path} is empty.");
            return config.Validate(Path.GetDirectoryName(Path.GetFullPath(path))!);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{path}: {ex.Message}");
        }
    }

    /// <summary>Checks the config and resolves install paths relative to the config file.</summary>
    internal RunConfig Validate(string baseDirectory)
    {
        if (Targets.Count == 0)
            throw new InvalidOperationException("swipewalk.json needs at least one entry in \"targets\".");
        if (Mode is not ("scan" or "record"))
            throw new InvalidOperationException($"\"mode\" must be \"scan\" or \"record\" (auto-navigation is planned), not \"{Mode}\".");
        if (FailOn is not ("never" or "wcag-issues"))
            throw new InvalidOperationException($"\"failOn\" must be \"never\" or \"wcag-issues\", not \"{FailOn}\".");
        if (Standard is not null && Core.Standards.KnownStandards.Find(Standard) is null)
            throw new InvalidOperationException($"Unknown standard \"{Standard}\".");
        if (LargeTextRestart is not (null or "ask" or "always" or "never"))
            throw new InvalidOperationException($"\"largeTextRestart\" must be \"ask\", \"always\" or \"never\", not \"{LargeTextRestart}\".");
        if (Appearance && Mode == "record")
            // Not wired into Recorder yet (see ScanOptions.AppearanceBoth); reject rather than silently do
            // nothing, so nobody thinks a recording checked both appearances when it didn't.
            throw new InvalidOperationException("\"appearance\" is scan only for now; record does not support it yet.");
        if (Orientation && Mode == "record")
            // Not wired into Recorder yet (see ScanOptions.OrientationBoth); reject rather than silently do
            // nothing, so nobody thinks a recording checked both orientations when it didn't.
            throw new InvalidOperationException("\"orientation\" is scan only for now; record does not support it yet.");
        if (AutoUpdateContent && Mode == "record")
            // Not wired into Recorder yet (see ScanOptions.AutoUpdateCheck); reject rather than silently do
            // nothing, so nobody thinks a recording checked for auto-updating content when it didn't.
            throw new InvalidOperationException("\"autoUpdateContent\" is scan only for now; record does not support it yet.");
        if (AutoUpdateInterval <= 0)
            throw new InvalidOperationException($"\"autoUpdateInterval\" must be greater than 0, not {AutoUpdateInterval}.");
        foreach (var target in Targets)
        {
            if (target.Platform is not ("android" or "ios"))
                throw new InvalidOperationException($"Target platform must be \"android\" or \"ios\", not \"{target.Platform}\".");
            var app = target.Platform == "ios" ? App.Ios : App.Android;
            if (app is null || (app.Install is null && app.Package is null && app.BundleId is null && target.Platform == "ios"))
                throw new InvalidOperationException($"\"app.{target.Platform}\" must give the app to scan (bundleId/package or install).");
        }

        if (App.Android?.Keystore is not null && App.Android.KeystoreAlias is null)
            throw new InvalidOperationException("\"app.android.keystore\" needs \"app.android.keystoreAlias\" too.");

        string? Resolve(string? file) => file is null || Path.IsPathRooted(file) ? file : Path.GetFullPath(Path.Combine(baseDirectory, file));
        return this with
        {
            App = App with
            {
                Android = App.Android is null ? null : App.Android with
                {
                    Install = Resolve(App.Android.Install),
                    Keystore = Resolve(App.Android.Keystore),
                },
                Ios = App.Ios is null ? null : App.Ios with { Install = Resolve(App.Ios.Install) },
            },
            Out = Resolve(Out),
        };
    }

    /// <summary>Scan options for one target; <paramref name="outDir"/> is where its report goes.</summary>
    public ScanOptions ToOptions(TargetConfig target, string outDir)
    {
        var ios = target.Platform == "ios";
        var app = (ios ? App.Ios : App.Android) ?? new AppTarget();
        return new ScanOptions
        {
            Platform = ios ? TargetPlatform.Ios : TargetPlatform.Android,
            Device = target.Device is null or "booted" or "any" ? null : target.Device,
            Package = app.Package,
            BundleId = app.BundleId,
            InstallFile = app.Install,
            BundletoolPath = BundletoolPath,
            AndroidKeystore = app.Keystore,
            AndroidKeystoreAlias = app.KeystoreAlias,
            Framework = Framework is null ? null : Enum.Parse<AppFramework>(Framework, ignoreCase: true),
            Standard = Standard,
            LargeText = LargeText,
            AppearanceBoth = Appearance,
            OrientationBoth = Orientation,
            AutoUpdateCheck = AutoUpdateContent,
            AutoUpdateIntervalSeconds = AutoUpdateInterval,
            AutoScanOnScreenChange = AutoScanOnScreenChange,
            LargeTextRestartPolicy = LargeTextRestart is null
                ? LargeTextRestartPolicies.Default(interactive: false, recordMode: Mode == "record")
                : LargeTextRestartPolicies.Parse(LargeTextRestart),
            ScreenReaderCapture = ScreenReaderCapture,
            ExpectedScreens = Expect,
            OutputDirectory = outDir,
            Team = IosSigning?.Team,
            Profile = IosSigning?.Profile,
            HarnessBundlePrefix = IosSigning?.HarnessBundlePrefix,
        };
    }
}

public sealed record AppConfig
{
    public AppTarget? Android { get; init; }
    public AppTarget? Ios { get; init; }
}

public sealed record AppTarget
{
    public string? Package { get; init; }
    public string? BundleId { get; init; }

    /// <summary>Already-built app to install first; relative to swipewalk.json.</summary>
    public string? Install { get; init; }

    /// <summary>Android only: keystore to sign a .aab's install with (see
    /// Swipewalk.Collectors.AppInstaller.AndroidBundleSigning); relative to swipewalk.json. Needs
    /// <see cref="KeystoreAlias"/> too, and the SWIPEWALK_KEYSTORE_PASSWORD environment variable at run time --
    /// a keystore password never goes in this file.</summary>
    public string? Keystore { get; init; }

    public string? KeystoreAlias { get; init; }
}

public sealed record TargetConfig
{
    public required string Platform { get; init; }

    /// <summary>adb serial or iOS UDID; omit (or "any"/"booted") for the only device / the booted Simulator.</summary>
    public string? Device { get; init; }
}

public sealed record IosSigningConfig
{
    public string? Team { get; init; }
    public string? Profile { get; init; }
    public string? HarnessBundlePrefix { get; init; }
}
