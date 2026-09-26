using Swipewalk.Collectors;
using Swipewalk.Collectors.Preflight;
using Swipewalk.Core.Limitations;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Standards;
using Swipewalk.Core.Wcag;
using Swipewalk.Engine;

const string Usage = """
    swipewalk - accessibility scanner for Android and iOS apps (Windows planned)

    Usage:
      swipewalk run [--config swipewalk.json] [--history <dir>] [--no-history]
                                                                    Install, check, scan and save, as configured
      swipewalk scan --platform android [--large-text-restart ask|always|never] [options]
      swipewalk scan --platform ios --bundle-id <id> [--large-text-restart ask|always|never] [options]
      swipewalk record --platform android|ios [--bundle-id <id>] [--expect "Login,Home"] [--large-text false]
                       [--auto] [--large-text-restart ask|always|never] [options]
      swipewalk record --continue <run>                           Resume a recording that ended early
      swipewalk limitations [--platform <p>] [--framework <f>]   Print known limitations as Markdown
      swipewalk doctor --platform android|ios [--device <id>] [--package <p> | --bundle-id <id>] [--team <id>]
                       [--screen-reader]                              Check the device and app are ready to scan
                                                                       (Android --screen-reader also checks for and
                                                                       repairs TalkBack settings left over from an
                                                                       interrupted capture)
      swipewalk devices                                             List connected devices, emulators and simulators
      swipewalk standards                                           Print the standards table as Markdown
      swipewalk checks                                              Print every automated check as Markdown
      swipewalk history [--history <dir>]                           List saved runs
      swipewalk compare <earlier> <later>                           What changed between two runs (run folders
                                                                       or results.json files)
      swipewalk guide <run> [--screen <name>] [--criterion <number>] [--history <dir>]
                                                                       Ask the guided-check questions for a
                                                                       saved run, one screen and criterion at a
                                                                       time; needs an interactive terminal
      swipewalk --version

    Scan options:
      --platform <android|ios>  Platform of the running app (Windows is not implemented yet)
      --bundle-id <id>       iOS: bundle identifier of the app to scan
      --device <id>          Android: adb serial. iOS: simulator or device UDID (default: the only connected Android
                             device / the booted simulator). See: swipewalk devices
      --keep-status-bar      Android: keep the status bar in screenshots (it is blanked by default to keep
                             notifications and other personal information out of reports)
      --profile <uuid|name>  iOS physical devices: installed provisioning profile to sign the harness with (by
                             default a fitting installed development profile is used, else Xcode automatic signing)
      --harness-bundle-prefix <p>  iOS: harness bundle id prefix, to fit a company wildcard profile (com.company.*)
      --install <file>       Install an already-built app first: Android .apk; iOS Simulator .app (or .zip); iOS
                             device .ipa signed for the device. The app is never rebuilt or re-signed
      --package <name>       Android: app package; the pre-flight check confirms it is installed and in front
      --skip-checks          Skip the pre-flight checks that run before scan and record
      --team <id>            iOS physical devices: Apple developer team that signs the scanning harness (found
                             automatically when there is one; a given team is remembered)
      --harness <path>       iOS: path to SwipewalkHarness.xcodeproj (default: found from the repo)
      --android-harness <path>  Android: path to harness/android, for Google's Accessibility Test Framework
                             (default: found from the repo; a missing or unbuildable harness only skips
                             those checks, never the scan -- see the report/results.json for why)
      --screen <name>        Name for the scanned screen in the report (default: "Screen 1")
      --out <dir>            Output directory (default: ./swipewalk-report); runs are also saved to the history
      --no-history           Don't save this run to the history (scan, record and run)
      --history <dir>        History folder to save to (scan, record and run; default: the user's Swipewalk/runs
                             folder). Given explicitly, runs re-scanned with --from are saved too
      --from <dir>           Re-scan a saved capture directory instead of a live device
      --framework <maui>     App framework for fix examples (Android, and iOS on the Simulator, detect MAUI
                             automatically, and on an iPhone when installed with --install; overrides detection)
      --result-bundle        iOS: use the physical-device result-bundle capture path even on a Simulator.
                             Advanced/testing use only; scans work correctly without it
      --screen-reader        Android only for now: also drive TalkBack over the screen's focusable elements
                             and read back exactly what it says, compared against the predicted transcript
                             in the report. Off by default -- costs roughly 1-2 seconds per element (a
                             30-element screen ~1 minute); entirely local, on-device (see
                             docs/limitations.md). Turns TalkBack on for the capture and temporarily makes
                             a small helper app Swipewalk installs TalkBack's default text-to-speech
                             engine, so it receives the exact spoken text -- TalkBack speaks nothing aloud
                             for the length of the capture; don't use this on a phone someone is relying
                             on TalkBack with right now. The accessibility settings this changes (which
                             service is enabled, touch exploration, the default text-to-speech engine) are
                             restored three ways (this method's own restore, a marker read back at the
                             next run, and a timer armed on the device itself that restores those settings
                             on its own if Swipewalk stops or is disconnected from the phone). Once a
                             capture's restore is known to have succeeded, the helper app itself is
                             uninstalled too, taking its device permission with it; only left when a
                             restore failed or is pending, for the safety timer and doctor's own repair.
                             Tested with the device's
                             system language set to English, Spanish, Hindi, Arabic and Japanese;
                             TalkBack's own role and hint words follow that language, the app's own
                             wording does not. Needs TalkBack (Android Accessibility Suite) installed on
                             the device. On a physical phone, asks for confirmation first
                             (--screen-reader-confirm skips the prompt for a non-interactive run); use a
                             test device
      --screen-reader-confirm  Skip --screen-reader's physical-device confirmation prompt (required
                             instead of the prompt for a non-interactive run on a physical phone)

      --large-text           scan: also capture at a large system text size (Android 200%; iOS AX3, about 235%,
                             set through the Settings app on a physical iPhone). On a physical device this
                             changes the phone's text size and restores it; use a test device. record does
                             this by default
      --appearance <both>    scan: also capture the screen in the device's other dark/light appearance (Android
                             `cmd uimode night`; iOS Simulator `simctl ui appearance`) and run every check on
                             it too, so a contrast failure that only shows up in one theme isn't missed just
                             because the device happened to be in the other one. Restores the device's original
                             appearance afterward, including on an error or Ctrl-C. Off by default. Not
                             supported yet on a physical iPhone (the report says why it was skipped); Simulator
                             and emulator/Android device both work
      --orientation <both>   scan: also rotate the device to its other orientation (portrait <-> landscape;
                             Android `settings put system accelerometer_rotation/user_rotation`; iOS Simulator
                             via the harness) and check whether the screen's content actually followed --
                             for review against WCAG 1.3.4 Orientation, since a single orientation can be
                             essential to a screen. When it does rotate, every check runs on that capture too.
                             Restores the device's orientation and rotation-lock state afterward (exactly, on
                             Android; on the iOS Simulator, back to whichever of portrait/landscape the first
                             capture showed, since there's no way to read the original back), including on an
                             error or Ctrl-C. Off by default. Not supported yet on a physical iPhone (the
                             report says why it was skipped); Simulator and emulator/Android device both work
      --standard <id>        Focus the report on one standard: ada-title-ii, section-508, en-301-549, en-301-549-v4, uk-public-sector
      --expect <names>       record: comma-separated screens you meant to cover; missing ones are listed
      --auto                 record: also scan automatically when the screen changes (default: off -- only
                             "Enter"/"Scan this screen now" captures a screen, so a screen is only captured
                             when you ask for it)
      --continue <run>       record: resume a recording that ended early (see: swipewalk history) -- run
                             folder or id, including one interrupted because Swipewalk itself was closed or
                             crashed (the next `history` or `--continue` sees that the recording process is
                             gone and lists the run as ended early). Appends new screens to that same run; rescanning
                             a screen already in it replaces the earlier capture, so it isn't counted twice.
                             --platform and the package/bundle id come from the run itself unless you override
                             them; --out is ignored (it always writes back into the run's own folder). Not
                             offered for a run that finished normally (Finish was chosen), or one still being
                             recorded (here, or in another window) -- record a new one, or wait, instead.
      --large-text-restart <ask|always|never>
                             Once a screen shows that checking the larger size needs the app restarted (the
                             text didn't change while the app kept running, or, on some Android apps, the
                             screen changed on its own): ask before restarting (default when the console can
                             prompt), always restart without asking, or never (the report says why the screen
                             wasn't checked). scan asks once, for the one screen it's checking; if a different
                             screen comes back after restarting, scan's own output points at `record` instead
                             (record lets you navigate back). record asks the same question about later screens too,
                             each check costing two extra navigations (back once the size is enlarged, then to
                             wherever you scan next once it's restored); "l" during a recording changes this
                             live. Non-interactive runs (redirected input, or `swipewalk run` unless
                             swipewalk.json sets "largeTextRestart": "ask") default to never for record (so an
                             unattended recording never blocks) and always for scan (so CI keeps checking large
                             text the way it always has).

    scan checks the screen currently shown (bringing the app forward, or starting it, first if needed).
    record watches while you use the app; nothing is captured until you scan a screen (Enter, or --auto to
    scan every new screen automatically). q finishes and writes the report. Exits with 2 if the recording
    ended early (an error or a cancellation), and the report says so; screens after that point were not
    scanned -- `record --continue <run>` resumes it later, appending screens to the same run rather than
    starting a new one; the report then says it was recorded across more than one session, with each one's
    times. If Swipewalk itself is closed or crashes mid-recording, there's no exit code to report that by --
    but `swipewalk history` and `record --continue` notice the process is gone the next time you run them,
    and list it as ended early, so it can be continued. run does what swipewalk.json describes and
    exits with 3 when failOn is "wcag-issues" and issues were found. Output: results.json and report.html.
    Automated checks find only some issues; manual testing is still required.
    """;

var version = ScanService.ToolVersion;
var service = new ScanService(new ConsoleLog());

if (args is ["--version"])
{
    Console.WriteLine(version);
    return 0;
}

if (args is ["limitations", ..])
{
    var filter = ParseOptions(args[1..]) ?? [];
    var selected = KnownLimitations.All.Where(l =>
        (!filter.TryGetValue("platform", out var p) || l.Platforms.Count == 0 || l.Platforms.Contains(Enum.Parse<Platform>(p, ignoreCase: true)))
        && (!filter.TryGetValue("framework", out var f) || l.Frameworks.Count == 0 || l.Frameworks.Contains(Enum.Parse<AppFramework>(f, ignoreCase: true))));
    Console.Write(LimitationsMarkdown.Render(selected));
    return 0;
}

if (args is ["standards"])
{
    Console.Write(StandardsMarkdown.Render(KnownStandards.All, DefaultRules.MappedCriteria));
    return 0;
}

if (args is ["checks"])
{
    Console.Write(ChecksMarkdown.Render(DefaultRules.Coverage, RuleCatalog.OwnRules, EngineIssueRule.Catalog, AtfIssueRule.Catalog));
    return 0;
}

if (args is ["devices"])
{
    var android = await Devices.AndroidAsync();
    var ios = (await Devices.IosWithStatusAsync()).Where(d => d.Device.IsPhysical || d.Problem is null).ToList();
    if (android.Count == 0 && ios.Count == 0)
        Console.WriteLine("No Android devices/emulators, booted iOS simulators or iOS devices found.");
    foreach (var d in android)
        Console.WriteLine($"{d.Id,-40} {d}");
    foreach (var (d, problem) in ios)
        Console.WriteLine($"{d.Id,-40} {d}{(problem is null ? "" : $"  ({problem})")}");
    return 0;
}

if (args is ["history", ..])
{
    var history = new RunHistory(ParseOptions(args[1..])?.GetValueOrDefault("history"));
    var runs = history.List();
    if (runs.Count == 0)
        Console.WriteLine($"No saved runs in {history.Root}.");
    foreach (var r in runs)
    {
        // RecordingInProgress here only means "confirmed still running" (see RunHistory.List): a recording
        // killed outright already reads as CanContinue below instead, the same as any other ended-early one.
        var status = r.RecordingInProgress ? "  (recording in progress)"
            : r.CanContinue ? $"  (ended early -- resume with: swipewalk record --continue {r.Id})"
            : "";
        Console.WriteLine($"{r.StartedAt:yyyy-MM-dd HH:mm}  {r.App,-35} {r.Platform,-8} {r.Counts.WcagIssues,3} WCAG  {r.Counts.NeedsReview,3} review  {r.Counts.Screens,2} screen(s)  {r.ReportPath}{status}");
    }
    return 0;
}

if (args is ["compare", var earlierPath, var laterPath])
{
    static ScanReport? LoadReport(string path)
    {
        var file = Directory.Exists(path) ? Path.Combine(path, "results.json") : path;
        return File.Exists(file) ? JsonReport.Deserialize(File.ReadAllText(file)) : null;
    }
    if (LoadReport(earlierPath) is not { } earlier || LoadReport(laterPath) is not { } later)
    {
        Console.Error.WriteLine("Give two run folders or results.json files (see: swipewalk history).");
        return 1;
    }
    var diff = ReportComparison.Compare(earlier, later);
    Console.WriteLine($"{diff.New.Count} new, {diff.NoLongerFound.Count} no longer found, {diff.StillFound.Count} still found.");
    void Section(string title, IEnumerable<string> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0)
            return;
        Console.WriteLine($"\n{title}:");
        foreach (var line in list)
            Console.WriteLine($"  {line}");
    }
    Section("New", diff.New.Select(ReportComparison.Describe));
    Section("No longer found", diff.NoLongerFound.Select(ReportComparison.Describe));
    Section("Not checked again (screen not rescanned, or the check didn't run)", diff.NotCheckedAgain.Select(ReportComparison.Describe));
    Section("Screens not scanned again (their findings were not compared)", diff.ScreensNotScannedAgain);
    Console.WriteLine("\n\"No longer found\" means the automated checks didn't report it this time; check by hand whether the issue is gone.");
    return 0;
}

if (args is ["guide", ..])
    return await Swipewalk.Cli.GuidedCheckCli.RunAsync(args[1..]);

if (args is ["run", ..])
{
    var runOptions = ParseOptions(args[1..]) ?? [];
    var configPath = runOptions.GetValueOrDefault("config") ?? "swipewalk.json";
    if (!File.Exists(configPath))
    {
        Console.Error.WriteLine($"{configPath} not found. See README for an example swipewalk.json.");
        return 1;
    }
    try
    {
        var config = RunConfig.Load(configPath);
        using var cancel = CancelOnSignals();
        var control = new RecorderControl();
        var initialPolicy = config.LargeTextRestart is null
            ? LargeTextRestartPolicies.Default(interactive: false, recordMode: config.Mode == "record")
            : LargeTextRestartPolicies.Parse(config.LargeTextRestart);
        // Record's own live-changeable console reader (Enter/q/l, and the C/D/A/N answer to its own prompt);
        // scan has no later screens for any of that, just the one ask below, so it reuses the plain
        // AskScanLargeTextRestartAsync function scan itself uses, not ConsoleRecordingInput.
        using var input = config.Mode == "record" ? new ConsoleRecordingInput(control, initialPolicy) : null;
        LargeTextRestartAsker largeTextRestartAsk = input is not null ? input.AskAsync : AskScanLargeTextRestartAsync;
        // Same --history/--no-history convention as scan/record (see the "run" saveToHistory branch in
        // Runner.RunAsync): saved to the local history by default, a given --history folder instead, or not
        // saved at all with --no-history.
        var saveToHistory = !runOptions.ContainsKey("no-history");
        var outcomes = await new Runner(service, new RunHistory(runOptions.GetValueOrDefault("history")), new ConsoleLog()).RunAsync(
            config, control, largeTextRestartAsk, input is null ? null : input.RevisitAsync, cancellationToken: cancel.Token, saveToHistory: saveToHistory);
        return Runner.ExitCode(config, outcomes);
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

var command = args.Length > 0 ? args[0] : "";
if (command is not ("scan" or "record" or "doctor"))
{
    // Asking for help is not an error: `swipewalk`, `--help`, `-h` and `help` all succeed, so shells,
    // scripts and CI don't read the usage text as a failure. Anything else is a typo, which exits 1.
    var askedForHelp = args.Length == 0 || command is "--help" or "-h" or "help";
    Console.WriteLine(Usage);
    return askedForHelp ? 0 : 1;
}

var options = ParseOptions(args[1..]);

// record --continue <run>: resolved up front, before the usual --platform/--bundle-id validation below, since
// the run itself already knows its platform and app id -- filled into `options` here
// so `record --continue <run>` alone is enough, unless the person overrides one explicitly (e.g. a different
// --device for this session).
RunRecord? continuingRun = null;
RecordContinuation? continuation = null;
ScanReport? continuingReport = null;
if (command == "record" && options?.GetValueOrDefault("continue") is { } continueTarget)
{
    var continueHistory = new RunHistory(options.GetValueOrDefault("history"));
    try
    {
        (continuingRun, continuingReport, continuation) = ScanService.ResolveContinuation(continueHistory, continueTarget);
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
    options.TryAdd("platform", continuingRun.Platform == "iOS" ? "ios" : "android");
    if (!options.ContainsKey("package") && !options.ContainsKey("bundle-id") && continuingRun.AppKey is { } appKey)
        options[continuingRun.Platform == "iOS" ? "bundle-id" : "package"] = appKey;
}

var platformName = options?.GetValueOrDefault("platform");
if (options is null || platformName is not ("android" or "ios")
    || (command != "doctor" && platformName == "ios" && !options.ContainsKey("bundle-id") && !options.ContainsKey("from") && !options.ContainsKey("install")))
{
    Console.Error.WriteLine($"Use {command} --platform android, or --platform ios with --bundle-id or --install.\n");
    if (command != "doctor")
        Console.WriteLine(Usage);
    return 1;
}
if (!ValidStandard(options.GetValueOrDefault("standard")))
    return 1;
if (options.GetValueOrDefault("appearance") is { } appearanceValue)
{
    if (appearanceValue != "both")
    {
        Console.Error.WriteLine($"--appearance must be \"both\", not \"{appearanceValue}\".");
        return 1;
    }
    if (command == "record")
    {
        // Not wired into Recorder yet (see ScanOptions.AppearanceBoth); reject rather than silently do
        // nothing, so nobody thinks a recording checked both appearances when it didn't.
        Console.Error.WriteLine("--appearance is scan only for now; record does not support it yet.");
        return 1;
    }
}
if (options.GetValueOrDefault("orientation") is { } orientationValue)
{
    if (orientationValue != "both")
    {
        Console.Error.WriteLine($"--orientation must be \"both\", not \"{orientationValue}\".");
        return 1;
    }
    if (command == "record")
    {
        // Not wired into Recorder yet (see ScanOptions.OrientationBoth); reject rather than silently do
        // nothing, so nobody thinks a recording checked both orientations when it didn't.
        Console.Error.WriteLine("--orientation is scan only for now; record does not support it yet.");
        return 1;
    }
}

var scanOptions = ToScanOptions(platformName, options, command);
if (command == "doctor")
{
    var checks = await service.CheckAsync(scanOptions);
    foreach (var check in checks)
        Console.WriteLine(check);
    Console.WriteLine(Preflight.CanProceed(checks) ? "Ready to scan." : "Not ready: fix the items marked FAIL.");
    return Preflight.CanProceed(checks) ? 0 : 1;
}

if (scanOptions.ScreenReaderCapture
    && !await ConfirmScreenReaderOnPhysicalDeviceAsync(scanOptions.Device, options.ContainsKey("screen-reader-confirm")))
{
    return 1;
}

// The run's own StartedAt (RunRecord/History/Dashboard ordering) must stay the original recording's start, not
// when this continued session began.
var started = continuingRun?.StartedAt ?? DateTimeOffset.Now;
try
{
    if (scanOptions.FromCapture is null)
    {
        scanOptions = await service.InstallAsync(scanOptions);
        if (!scanOptions.SkipChecks)
        {
            var checks = await service.CheckAsync(scanOptions);
            foreach (var check in checks.Where(c => c.Status != CheckStatus.Pass))
                Console.WriteLine(check);
            if (!Preflight.CanProceed(checks))
            {
                Console.Error.WriteLine($"Pre-flight checks failed; fix the items above (details: swipewalk doctor --platform {platformName}).");
                return 1;
            }
        }
    }

    RunResult result;
    var historyDir = options.GetValueOrDefault("history");
    var saveToHistory = continuingRun is not null || (!options.ContainsKey("no-history") && (scanOptions.FromCapture is null || historyDir is not null));
    if (command == "record")
    {
        using var cancel = CancelOnSignals();
        var control = new RecorderControl();
        using var input = new ConsoleRecordingInput(control, scanOptions.LargeTextRestartPolicy);
        Console.WriteLine("  Enter   scan the current screen now (e.g. after opening a menu)\n" +
                           "  q       finish and write the report (Ctrl+C stops early instead; the report says so)\n" +
                           $"  l       when checking larger text needs the app restarted (currently: {input.CurrentPolicyLabel})");
        var history = new RunHistory(historyDir);
        if (continuingRun is not null)
        {
            // Continuing writes back into the same run's own folder and appends to what it already has -- never
            // a new folder, and never another run (separate runs are never merged, overwritten or replaced).
            scanOptions = scanOptions with { OutputDirectory = continuingRun.Folder };
            if (options.ContainsKey("out"))
                Console.WriteLine("  (--out is ignored when continuing a run: it always writes back into that run's own folder.)");
            Console.WriteLine($"Continuing {continuingRun.Id}: session {continuation!.PriorSessions.Count + 1}, " +
                               $"{continuation.PriorResults.Count} screen(s) already captured.");
            // continuation.LearnedRestartReason being null on its own doesn't mean anything was lost -- it's
            // also what a run with nothing to learn yet looks like (text grew live, or large text wasn't
            // checked). Only StateWasSaved false means record-state.json itself wasn't there to load from.
            if (!continuation.StateWasSaved && continuation.PriorResults.Count > 0)
                Console.WriteLine("  (This run's earlier session(s) saved no resume state -- an older run, or a damaged file: " +
                                   "starting fresh on the large-text-restart behaviour, and their screens won't be recognized if you rescan them.)");
        }
        // Record directly into a history folder (unless the user gave --out) so the incremental save below
        // needs no copy: a recording interrupted by an error, a cancel, a killed app or the process itself
        // being stopped still shows up in History/Dashboard with whatever it captured (see
        // RunHistory.OnScreenSaved). --no-history keeps the old local-folder-only behaviour.
        else if (saveToHistory && !options.ContainsKey("out"))
            scanOptions = scanOptions with { OutputDirectory = history.NewRunFolder(scanOptions.AppId ?? "unknown-app") };
        // Written before the first screen, not just after it (that's OnScreenSaved below): a process killed
        // before it captures anything still leaves this run in History marked "in progress" -- see
        // RunHistory.StartRecordingAsync.
        if (saveToHistory)
            await history.StartRecordingAsync(scanOptions, started, scanOptions.OutputDirectory, continuingReport);
        var onScreen = saveToHistory ? history.OnScreenSaved(scanOptions, command, started, scanOptions.OutputDirectory, new ConsoleLog()) : null;
        result = await service.RecordAsync(scanOptions, control, cancel.Token, onScreen, input.AskAsync, input.RevisitAsync, continuation);
    }
    else
    {
        result = await service.ScanAsync(scanOptions, largeTextRestartAsk: AskScanLargeTextRestartAsync);
    }

    Console.WriteLine($"{ReportWriter.Summary(result.Report)}\n  {result.HtmlPath}\n  {result.JsonPath}");
    if (result.Report.EndedEarlyReason is { } endedEarly)
        Console.WriteLine($"Note: {endedEarly}");
    if (saveToHistory)
        await new RunHistory(historyDir).SaveAsync(result, scanOptions, command, started);
    return result.Report.EndedEarlyReason is null ? 0 : 2;
}
catch (Exception ex) when (ex is InvalidOperationException or IOException or FormatException or System.ComponentModel.Win32Exception)
{
    Console.Error.WriteLine($"{(command == "record" ? "Recording" : "Scan")} failed: {ex.Message}");
    return 2;
}

static ScanOptions ToScanOptions(string platform, Dictionary<string, string> o, string command) => new()
{
    Platform = platform == "ios" ? TargetPlatform.Ios : TargetPlatform.Android,
    Device = o.GetValueOrDefault("device"),
    Package = o.GetValueOrDefault("package"),
    BundleId = o.GetValueOrDefault("bundle-id"),
    InstallFile = o.GetValueOrDefault("install"),
    Framework = o.GetValueOrDefault("framework") is { } f ? Enum.Parse<AppFramework>(f, ignoreCase: true) : null,
    Standard = o.GetValueOrDefault("standard"),
    ScreenName = o.GetValueOrDefault("screen") ?? "Screen 1",
    LargeText = command == "record" ? o.GetValueOrDefault("large-text") != "false" : o.GetValueOrDefault("large-text") is { } lt && lt != "false",
    AppearanceBoth = o.GetValueOrDefault("appearance") == "both",
    OrientationBoth = o.GetValueOrDefault("orientation") == "both",
    KeepStatusBar = o.GetValueOrDefault("keep-status-bar") is { } k && k != "false",
    SkipChecks = o.ContainsKey("skip-checks"),
    FromCapture = o.GetValueOrDefault("from"),
    ExpectedScreens = [.. (o.GetValueOrDefault("expect") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
    AutoScanOnScreenChange = o.ContainsKey("auto"),
    LargeTextRestartPolicy = o.GetValueOrDefault("large-text-restart") is { } lr
        ? LargeTextRestartPolicies.Parse(lr)
        : LargeTextRestartPolicies.Default(interactive: !Console.IsInputRedirected, recordMode: command == "record"),
    OutputDirectory = o.GetValueOrDefault("out") ?? "swipewalk-report",
    Team = o.GetValueOrDefault("team"),
    Profile = o.GetValueOrDefault("profile"),
    HarnessBundlePrefix = o.GetValueOrDefault("harness-bundle-prefix"),
    HarnessProject = o.GetValueOrDefault("harness"),
    AndroidHarnessDir = o.GetValueOrDefault("android-harness"),
    ForceResultBundle = o.ContainsKey("result-bundle"),
    ScreenReaderCapture = o.ContainsKey("screen-reader"),
};

/// <summary>
/// scan's ask-before-restart prompt: unlike record's
/// ConsoleRecordingInput, a scan is one screen and isn't reading Enter/q concurrently while the person
/// navigates, so this blocks on the console directly instead of running its own background reader, and offers
/// only "check"/"don't check" -- there's no later screen for "always"/"never" to apply to. Redirected input
/// answers "don't check" straight away rather than waiting forever, but scan's own non-interactive default is
/// "always" (see ToScanOptions), so this is normally only reached with a console to ask on.
/// </summary>
static Task<LargeTextRestartChoice> AskScanLargeTextRestartAsync(
    string screenName, string reason, bool learnedFromEarlierScreen, Platform platform, CancellationToken cancellationToken)
{
    if (Console.IsInputRedirected)
        return Task.FromResult(LargeTextRestartChoice.SkipForThisScreen);
    Console.WriteLine($"  {LargeTextAskWording.Message(screenName, reason, learnedFromEarlierScreen, platform, recordMode: false)}");
    Console.WriteLine("  [C] check anyway   [D] don't check this screen");
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.C)
            return Task.FromResult(LargeTextRestartChoice.RestartAndCheck);
        if (key.Key == ConsoleKey.D)
            return Task.FromResult(LargeTextRestartChoice.SkipForThisScreen);
    }
}

/// <summary>
/// --screen-reader turns TalkBack on and changes which accessibility service is enabled, touch exploration and
/// the default text-to-speech engine for the run (restored afterward -- see ScreenReaderSettingsRestore -- and
/// the small helper app it installs is removed too once that restore is confirmed; see KnownLimitations
/// "android-screen-reader-capture" for when it can be left behind), and TalkBack
/// speaks nothing aloud while it runs; on an emulator that's low-stakes, but on a physical phone it's worth a
/// person's explicit say-so first, once per run. Interactive: ask y/N. Non-interactive (CI, redirected input): the
/// person can't answer, so an explicit --screen-reader-confirm flag stands in for them; without it, the run stops
/// with a plain explanation rather than silently touching device settings. Emulators, and a device this can't
/// identify yet (e.g. more than one connected with none chosen -- pre-flight reports that clearly on its own),
/// are not gated at all.
/// </summary>
static async Task<bool> ConfirmScreenReaderOnPhysicalDeviceAsync(string? device, bool confirmedByFlag)
{
    var candidates = await Devices.AndroidAsync();
    var chosen = device is not null
        ? candidates.FirstOrDefault(d => d.Id == device)
        : candidates.Count == 1 ? candidates[0] : null;
    if (chosen is not { IsPhysical: true } || confirmedByFlag)
        return true;
    if (Console.IsInputRedirected)
    {
        Console.Error.WriteLine(
            "--screen-reader changes accessibility settings on this physical phone (which service is enabled, " +
            "touch exploration, and the default text-to-speech engine) to capture what TalkBack says -- TalkBack " +
            "speaks nothing aloud while it runs. The settings are restored afterward, even if the run is " +
            "interrupted; the small helper app it installs is removed once that restore is confirmed (it stays " +
            "only if a restore fails or is pending -- see docs/limitations.md). Non-interactive runs need " +
            "--screen-reader-confirm to do this on a physical device; add it, or drop --screen-reader and test " +
            "on an emulator instead.");
        return false;
    }
    Console.WriteLine(
        $"--screen-reader will turn TalkBack on and change {chosen.Name}'s accessibility settings (which service " +
        "is enabled, touch exploration, and the default text-to-speech engine) to capture exactly what TalkBack " +
        "says -- TalkBack will speak nothing aloud while this runs, so don't use it on a phone someone is relying " +
        "on TalkBack with right now. Those settings are restored afterward, even if the run is interrupted; the " +
        "small helper app it installs is removed once that restore is confirmed (it stays only if a restore " +
        "fails or is pending -- see docs/limitations.md). Use a test device. Continue? [y/N]");
    var key = Console.ReadKey(intercept: true);
    Console.WriteLine();
    return key.Key == ConsoleKey.Y;
}

/// <summary>Ctrl+C / kill finish the report instead of exiting; works with or without a terminal.</summary>
static SignalCancellation CancelOnSignals() => new();

static bool ValidStandard(string? id)
{
    if (id is null || KnownStandards.Find(id) is not null)
        return true;
    Console.Error.WriteLine($"Unknown --standard '{id}'. Use one of: {string.Join(", ", KnownStandards.All.Select(s => s.Id))}.");
    return false;
}

/// <summary>Parses "--name value" pairs; a flag followed by another flag (or nothing) gets the value "true".</summary>
static Dictionary<string, string>? ParseOptions(string[] args)
{
    var options = new Dictionary<string, string>();
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal))
            return null;
        var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
        options[args[i][2..]] = hasValue ? args[++i] : "true";
    }
    return options;
}

/// <summary>
/// The console's one reader of keyboard input while recording: normally Enter requests a scan, q stops, and
/// l cycles what to do once the larger size needs a restart (see LargeTextRestartPolicy). While a per-screen
/// question is pending (<see cref="AskAsync"/>, C/D/A/N) or a Finish-time offer to revisit screens is pending
/// (<see cref="RevisitAsync"/>, Y/N), the same reader switches to answering that instead, so they never race
/// for the same keystrokes. Does nothing when input is redirected (CI, piped input): both askers then answer
/// straight away (don't check; don't revisit) rather than waiting forever.
/// </summary>
sealed class ConsoleRecordingInput : IDisposable
{
    private readonly RecorderControl _control;
    private readonly CancellationTokenSource _stop = new();
    private LargeTextRestartPolicy _policy;
    private TaskCompletionSource<LargeTextRestartChoice>? _pending;
    private TaskCompletionSource<bool>? _pendingRevisit;

    public ConsoleRecordingInput(RecorderControl control, LargeTextRestartPolicy initialPolicy)
    {
        _control = control;
        _policy = initialPolicy;
        if (!Console.IsInputRedirected)
            _ = Task.Run(RunAsync);
    }

    public string CurrentPolicyLabel => Label(_policy);

    private static string Label(LargeTextRestartPolicy policy) => policy switch
    {
        LargeTextRestartPolicy.Always => "always check",
        LargeTextRestartPolicy.Never => "never check",
        _ => "ask",
    };

    private async Task RunAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (_pendingRevisit is { } pendingRevisit)
                {
                    _pendingRevisit = null;
                    pendingRevisit.TrySetResult(key.KeyChar is 'y' or 'Y');
                }
                else if (_pending is { } pending)
                {
                    // Only C/D/A/N answer the question: Enter and q are meaningful everywhere else in a
                    // recording, and must never be silently read as "don't check this screen".
                    var choice = key.Key switch
                    {
                        ConsoleKey.C => LargeTextRestartChoice.RestartAndCheck,
                        ConsoleKey.D => LargeTextRestartChoice.SkipForThisScreen,
                        ConsoleKey.A => LargeTextRestartChoice.AlwaysRestart,
                        ConsoleKey.N => LargeTextRestartChoice.AlwaysSkip,
                        _ => (LargeTextRestartChoice?)null,
                    };
                    if (choice is not { } answered)
                        continue;
                    _pending = null;
                    if (answered == LargeTextRestartChoice.AlwaysRestart)
                        _policy = LargeTextRestartPolicy.Always;
                    else if (answered == LargeTextRestartChoice.AlwaysSkip)
                        _policy = LargeTextRestartPolicy.Never;
                    pending.TrySetResult(answered);
                }
                else if (key.Key == ConsoleKey.Enter)
                    _control.RequestScanNow();
                else if (key.KeyChar is 'q' or 'Q')
                    _control.Stop();
                else if (key.KeyChar is 'l' or 'L')
                    CyclePolicy();
            }
            await Task.Delay(100);
        }
    }

    private void CyclePolicy()
    {
        _policy = _policy switch
        {
            LargeTextRestartPolicy.Ask => LargeTextRestartPolicy.Always,
            LargeTextRestartPolicy.Always => LargeTextRestartPolicy.Never,
            _ => LargeTextRestartPolicy.Ask,
        };
        _control.SetLargeTextRestartPolicy(_policy);
        Console.WriteLine($"  When checking larger text needs the app restarted: {CurrentPolicyLabel}");
    }

    /// <summary>Matches the <c>LargeTextRestartAsker</c> delegate; pass <c>input.AskAsync</c> directly.</summary>
    public Task<LargeTextRestartChoice> AskAsync(string screenName, string reason, bool learnedFromEarlierScreen, Platform platform, CancellationToken cancellationToken)
    {
        if (Console.IsInputRedirected)
            return Task.FromResult(LargeTextRestartChoice.SkipForThisScreen);
        Console.WriteLine($"  {LargeTextAskWording.Message(screenName, reason, learnedFromEarlierScreen, platform, recordMode: true)}");
        Console.WriteLine("  [C] check anyway   [D] don't check this screen   [A] always check   [N] never check");
        var tcs = new TaskCompletionSource<LargeTextRestartChoice>();
        _pending = tcs;
        // Ctrl+C (or any other cancellation) while a question is pending must not hang forever, but it's a
        // cancellation, not the person answering "don't check": propagate it as such so Recorder's own
        // cancellation handling (an "ended early" recording) takes over, rather than recording a choice
        // nobody made.
        cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        return tcs.Task;
    }

    /// <summary>Matches the <c>RevisitSkippedScreensAsker</c> delegate; pass <c>input.RevisitAsync</c> directly.</summary>
    public Task<bool> RevisitAsync(IReadOnlyList<string> screenNames, CancellationToken cancellationToken)
    {
        if (Console.IsInputRedirected)
            return Task.FromResult(false);
        Console.WriteLine($"  {screenNames.Count} screen(s) weren't checked at the larger text size: {string.Join(", ", screenNames)}.");
        Console.WriteLine("  Keep recording to check them now? [Y] yes   [N] no, finish now");
        var tcs = new TaskCompletionSource<bool>();
        _pendingRevisit = tcs;
        // See AskAsync: a cancellation here is not the person answering "no, finish now".
        cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        return tcs.Task;
    }

    public void Dispose() => _stop.Cancel();
}

/// <summary>Writes progress immediately (Progress&lt;T&gt; would post to the thread pool and reorder lines).</summary>
sealed class ConsoleLog : IProgress<string>
{
    public void Report(string value) => Console.WriteLine(value);
}

/// <summary>A cancellation source triggered by SIGINT/SIGTERM, which are then not fatal.</summary>
sealed class SignalCancellation : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    private readonly IDisposable _interrupt;
    private readonly IDisposable _terminate;

    public SignalCancellation()
    {
        _interrupt = System.Runtime.InteropServices.PosixSignalRegistration.Create(
            System.Runtime.InteropServices.PosixSignal.SIGINT, ctx => { ctx.Cancel = true; _source.Cancel(); });
        _terminate = System.Runtime.InteropServices.PosixSignalRegistration.Create(
            System.Runtime.InteropServices.PosixSignal.SIGTERM, ctx => { ctx.Cancel = true; _source.Cancel(); });
    }

    public CancellationToken Token => _source.Token;

    public void Dispose()
    {
        _interrupt.Dispose();
        _terminate.Dispose();
        _source.Dispose();
    }
}
