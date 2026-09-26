using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;

namespace Swipewalk.Engine;

/// <summary>Numbers for one run, for history lists and the dashboard.</summary>
public sealed record RunCounts(int WcagIssues, int NeedsReview, int PlatformAdvisories, int Screens);

/// <summary>Metadata of a saved run (run.json in the run's folder).</summary>
public sealed record RunRecord
{
    /// <summary>
    /// <see cref="App"/> when the scan didn't know which app it was running against (no <c>--package</c> or
    /// <c>--bundle-id</c>, and not a saved capture). Never fall back to a screen name here: a screen isn't an
    /// app, and the Dashboard/History/Compare pages show <see cref="App"/> as if it were one.
    /// </summary>
    public const string UnknownApp = "Unknown app";

    /// <summary>
    /// <see cref="App"/> for a scan replayed from a saved capture (<c>scan --from</c>) with no app id recorded
    /// alongside it.
    /// </summary>
    public const string UnknownAppFromCapture = "Unknown app (from a saved capture)";

    /// <summary>
    /// <see cref="EndedEarlyReason"/> when <see cref="RunHistory.List"/> infers, from the owning process being
    /// gone, that a recording marked <see cref="RecordingInProgress"/> was actually killed outright (SIGKILL, a
    /// force-quit, a crash, power loss) rather than stopped through Finish, a cancellation or an error Recorder
    /// itself caught. Worded like the other
    /// <see cref="EndedEarlyReason"/> values built in Recorder.Report() (a capitalized sentence ending with the
    /// same "screens after that point were not scanned" note, so a person reading History sees the same thing
    /// whichever way a recording ended): never "unexpectedly", since closing Swipewalk on purpose (a Cmd-Q, a
    /// deliberate kill) is one of the ways this happens too.
    /// </summary>
    public const string AbandonedRecordingReason =
        "Swipewalk stopped running before the recording was finished (for example, it was closed, force-quit or crashed). " +
        "Screens after that point were not scanned; scan or test them manually.";

    public required string Id { get; init; }
    public required string App { get; init; }

    /// <summary>
    /// Stable identity used to group this run with other runs of the same app -- for <see cref="RunHistory.Previous"/>
    /// and the Dashboard's "latest run per app" -- independent of <see cref="App"/>, which is only ever a display
    /// label (e.g. <see cref="UnknownApp"/> when no app id is known) and must never be used for grouping: two runs
    /// can share that label without being the same app. Set from the app id passed in when the scan started
    /// (<c>--package</c>/<c>--bundle-id</c>), or else detected from the capture itself (see
    /// <c>Swipewalk.Core.Model.ScreenSnapshot.AppId</c> and <c>Swipewalk.Core.Reports.ScanReport.AppId</c>) -- for
    /// example Android's uiautomator dump records the app's package on every node. Null when neither is known.
    /// For a run.json saved before this field existed, <see cref="RunHistory.List"/> fills it in at load time
    /// (never rewriting the file) when <see cref="App"/> clearly looks like an app id -- see
    /// <see cref="InferAppKeyFromLegacyApp"/> -- so an old run of a known app still groups with newer runs of
    /// it instead of standing alone forever. A null <see cref="AppKey"/> is never treated as matching another
    /// run's null <see cref="AppKey"/>: see <see cref="GroupKey"/>, which every grouping/lookup in this type
    /// and its consumers must use instead of comparing <see cref="AppKey"/> directly.
    /// </summary>
    public string? AppKey { get; init; }

    /// <summary>
    /// Matches a package/bundle id such as "org.swipewalk.buggyapp": a leading letter, then letters/digits/
    /// underscores, with at least one dot-separated segment after it, and no spaces. Used only to recover
    /// <see cref="AppKey"/> for a run.json saved before that field existed (<see cref="InferAppKeyFromLegacyApp"/>).
    /// </summary>
    private static readonly Regex AppIdPattern = new(@"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)+$", RegexOptions.Compiled);

    /// <summary>
    /// Recovers <see cref="AppKey"/> for a run whose run.json was saved before that field existed (every such
    /// record has <see cref="App"/> set instead, to whatever was known at the time: sometimes the real app id,
    /// sometimes a screen name like "Screen 1" (the older bug this fixed), sometimes <see cref="UnknownApp"/> or
    /// <see cref="UnknownAppFromCapture"/>). A label is trusted as an id only when it matches <see cref="AppIdPattern"/>;
    /// anything else stays unidentified (null) and keeps its own <see cref="GroupKey"/>, because a screen name
    /// or "Unknown app" gives no real evidence that two such runs are the same app, while a package/bundle
    /// pattern does. Never called when <see cref="AppKey"/> is already set -- a real <see cref="AppKey"/> always
    /// wins over this guess.
    /// </summary>
    internal static string? InferAppKeyFromLegacyApp(string app) => AppIdPattern.IsMatch(app) ? app : null;

    /// <summary>
    /// Groups this run with others of the same app: <see cref="AppKey"/> when known, otherwise a key unique to
    /// this run (its <see cref="Id"/>), so two runs with no identified app -- which could be different apps --
    /// are never grouped or compared with each other just because they share the same <see cref="App"/> display
    /// label. Use this (never <see cref="AppKey"/> directly) for "same app" grouping and lookups.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string GroupKey => AppKey ?? $"{UnidentifiedGroupPrefix}{Id}";

    private const string UnidentifiedGroupPrefix = "unidentified:";

    /// <summary>
    /// Shown wherever a comparison with a previous run would otherwise appear, for a run with no identified app
    /// (<see cref="AppKey"/> is null). Unlike a normal first run (which a later run of the same app will be
    /// compared with), this run never will be, since it can't be matched with anything -- so the wording must
    /// not imply a trend is starting.
    /// </summary>
    public const string NotIdentifiedComparisonNote = "No app was identified for this run, so it isn't compared with other runs.";

    public required string Platform { get; init; }
    public DeviceInfo? Device { get; init; }
    public string? Standard { get; init; }

    /// <summary>Short name of the focus standard for display, e.g. "ADA Title II".</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string StandardLabel => Standard is null ? "All standards" : Core.Standards.KnownStandards.Find(Standard)?.ShortName ?? Standard;

    /// <summary>"scan", "record" or "run".</summary>
    public required string Mode { get; init; }

    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset FinishedAt { get; init; }
    public required RunCounts Counts { get; init; }

    /// <summary>
    /// Set (record mode only) when the recording ended before "Finish" was chosen -- see
    /// <see cref="ScanReport.EndedEarlyReason"/>, which this mirrors so History and the Dashboard can
    /// say so without opening results.json. Null for a recording that ran to completion, and for a scan.
    /// </summary>
    public string? EndedEarlyReason { get; init; }

    /// <summary>
    /// Whether <c>record --continue</c> (or the desktop app's Continue action in History) can resume this run:
    /// only a recording that ended before "Finish" (see <see cref="EndedEarlyReason"/>). A run that finished
    /// normally is not offered Continue, since there's nothing ended-early to resume. Never true while
    /// <see cref="RecordingInProgress"/> is true: a run currently being recorded (here or in another window)
    /// has nothing ended-early to resume either -- see <see cref="RunHistory.List"/>, which only leaves
    /// <see cref="RecordingInProgress"/> true when the owning process is confirmed still running.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CanContinue => Mode == "record" && !RecordingInProgress && EndedEarlyReason is not null;

    /// <summary>
    /// True while this run.json reflects a recording that had not yet reached a terminal state (Finish, a
    /// cancellation or an error Recorder itself caught) when it was written: set by the "recording started"
    /// save written before the first screen (see <see cref="RunHistory.SaveAsync"/>'s <c>recordingInProgress</c>
    /// parameter) and by every per-screen incremental save (<see cref="RunHistory.OnScreenSaved"/>); cleared by
    /// the final save once the recording actually stops. A process killed outright (SIGKILL, a force-quit, a
    /// crash, power loss) never gets to write that final save, so the last thing on disk still says
    /// <c>true</c> forever -- see <see cref="RunHistory.List"/>, which checks <see cref="RecordingProcessId"/>/
    /// <see cref="RecordingProcessStartedAt"/> and, only when that process is confirmed gone, returns this run
    /// with <c>RecordingInProgress</c> flipped to false and <see cref="EndedEarlyReason"/> filled in instead --
    /// so a crash of Swipewalk itself still appears in History with what was captured and an ended-early note.
    /// That correction is never written back to run.json (see <see cref="RunHistory.List"/>'s remarks): reading
    /// history stays a read, the same as the existing <see cref="AppKey"/> legacy-inference above. Always false
    /// for <c>Mode != "record"</c> -- a
    /// single-screen scan is atomic (one capture, one save) and isn't resumable, so it has no "in progress"
    /// state worth tracking.
    /// </summary>
    public bool RecordingInProgress { get; init; }

    /// <summary>
    /// Process id of the Swipewalk process that last wrote this run.json while <see cref="RecordingInProgress"/>
    /// was true -- paired with <see cref="RecordingProcessStartedAt"/> (that same process's own OS start time)
    /// so a later read can tell "that process is still running" from "a different, unrelated process now has
    /// the same pid" (pids are reused by the OS over time) -- see <see cref="RunHistory.IsOwningProcessRunning"/>.
    /// Null once the run has a final save (<see cref="RecordingInProgress"/> false) and for any run saved
    /// before this field existed.
    /// </summary>
    public int? RecordingProcessId { get; init; }

    /// <summary>See <see cref="RecordingProcessId"/>.</summary>
    public DateTimeOffset? RecordingProcessStartedAt { get; init; }

    public required string ToolVersion { get; init; }
    public required string RulesetVersion { get; init; }

    /// <summary>Folder of the run (report.html, results.json, captures).</summary>
    public string Folder { get; init; } = "";

    public string ReportPath => Path.Combine(Folder, "report.html");
    public string ResultsPath => Path.Combine(Folder, "results.json");
}

/// <summary>
/// Saved runs, one folder each under the history root (default: the user's local application data folder,
/// Swipewalk/runs). The desktop app's History and Dashboard read from here.
/// </summary>
public sealed class RunHistory(string? root = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonReport.Options) { WriteIndented = true };

    public string Root { get; } = root ?? DefaultRoot;

    /// <summary>
    /// ~/Library/Application Support/Swipewalk/runs on macOS (also from Mac Catalyst, where the .NET special
    /// folder resolves elsewhere), the local application data folder on Windows and Linux.
    /// </summary>
    public static string DefaultRoot => Path.Combine(
        OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Swipewalk", "runs");

    /// <summary>A new, empty folder for a run about to start.</summary>
    public string NewRunFolder(string app)
    {
        var safeApp = string.Concat(app.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_'));
        var folder = Path.Combine(Root, $"{DateTime.Now:yyyyMMdd-HHmmss}-{safeApp}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>
    /// Records a run. Copies its output into the history when it was written elsewhere.
    /// <paramref name="recordingInProgress"/> is true only for a write made while a recording (mode "record")
    /// hasn't yet reached a terminal state: the "recording started" write made before the first screen (see
    /// <see cref="StartRecordingAsync"/>) and every per-screen incremental write (<see cref="OnScreenSaved"/>).
    /// It stamps the run with this process's identity (<see cref="RunRecord.RecordingProcessId"/>/
    /// <see cref="RunRecord.RecordingProcessStartedAt"/>) so a later read can tell whether that process is
    /// still running -- see <see cref="List"/>. The default
    /// (false) is every other caller: a scan's one-shot save, and the final save once a recording actually
    /// stops (Finish, a cancellation, or an error Recorder itself caught) -- which is also what clears a
    /// previously-true value back to false and drops the process identity, since it no longer matters once the
    /// run has a real, final <see cref="RunRecord.EndedEarlyReason"/> (or none, on a clean Finish).
    /// </summary>
    public async Task<RunRecord> SaveAsync(
        RunResult result, ScanOptions options, string mode, DateTimeOffset startedAt, bool recordingInProgress = false)
    {
        var source = Path.GetDirectoryName(result.HtmlPath)!;
        var folder = source.StartsWith(Path.GetFullPath(Root), StringComparison.Ordinal)
            ? source
            : CopyInto(source, NewRunFolder(options.AppId ?? "unknown-app"));

        var record = Build(result, options, mode, startedAt, folder, recordingInProgress);
        await File.WriteAllTextAsync(Path.Combine(folder, "run.json"), JsonSerializer.Serialize(record, Json));
        return record;
    }

    /// <summary>
    /// Builds a <see cref="RunRecord"/> for <paramref name="result"/> without writing anything to disk or
    /// copying its output anywhere -- the pure computation <see cref="SaveAsync"/> itself does before writing
    /// run.json. Used by <c>swipewalk run --no-history</c> (see <c>Runner.RunAsync</c>), which still needs a
    /// record's <see cref="RunRecord.Counts"/> and <see cref="RunRecord.EndedEarlyReason"/> for its own exit
    /// code, but must not save the run into the history or copy its output there.
    /// </summary>
    public static RunRecord Build(
        RunResult result, ScanOptions options, string mode, DateTimeOffset startedAt, string folder, bool recordingInProgress = false)
    {
        var report = result.Report;
        using var currentProcess = recordingInProgress ? Process.GetCurrentProcess() : null;
        return new RunRecord
        {
            Id = Path.GetFileName(folder),
            // Never fall back to a screen name (e.g. "Screen 1") here: it isn't an app, and this value is
            // shown as one in the Dashboard, History and `swipewalk compare`. Distinguish "no app id, and not
            // a saved capture" from "replaying a saved capture with no app id" so the label stays honest.
            App = options.AppId ?? (options.FromCapture is null ? RunRecord.UnknownApp : RunRecord.UnknownAppFromCapture),
            // The app id passed in wins; otherwise whatever the scan itself could tell from the capture (see
            // ScanReport.AppId). Unlike App above, this is never a display fallback string -- it stays null
            // when neither is known, so the run is its own group (see RunRecord.GroupKey) instead of being
            // lumped in with every other run that has no identified app.
            AppKey = options.AppId ?? report.AppId,
            Platform = options.Platform == TargetPlatform.Ios ? "iOS" : "Android",
            Device = report.Screens.FirstOrDefault()?.Device,
            Standard = options.Standard,
            Mode = mode,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.Now,
            Counts = new RunCounts(
                report.Count(FindingKind.WcagIssue), report.Count(FindingKind.NeedsReview),
                report.Count(FindingKind.PlatformAdvisory), report.Screens.Count),
            ToolVersion = report.ToolVersion,
            RulesetVersion = report.RulesetVersion,
            Folder = folder,
            // While still recording, there is no final ended-early reason yet -- see SaveAsync's remarks.
            // Once the recording actually stops, report.EndedEarlyReason is whatever Recorder itself
            // determined (null on a clean Finish).
            EndedEarlyReason = recordingInProgress ? null : report.EndedEarlyReason,
            RecordingInProgress = recordingInProgress,
            RecordingProcessId = currentProcess?.Id,
            RecordingProcessStartedAt = currentProcess is null ? null : new DateTimeOffset(currentProcess.StartTime),
        };
    }

    /// <summary>
    /// Writes run.json for a recording that is about to start (fresh, or via <c>record --continue</c>/the
    /// desktop app's Continue), before the first screen is captured -- so a process killed before it captures
    /// anything still leaves a run in History marked <see cref="RunRecord.RecordingInProgress"/>, instead of no
    /// run at all. <paramref name="priorReport"/> is the run's own report so far when continuing (so Counts
    /// reflect the screens already kept, not zero, until the first new incremental save updates them); null for
    /// a fresh recording.
    /// </summary>
    public Task<RunRecord> StartRecordingAsync(ScanOptions options, DateTimeOffset startedAt, string outDir, ScanReport? priorReport = null) =>
        SaveAsync(
            new RunResult(priorReport ?? new ScanReport { ToolVersion = ScanService.ToolVersion, Screens = [] },
                Path.Combine(outDir, "report.html"), Path.Combine(outDir, "results.json")),
            options, "record", startedAt, recordingInProgress: true);

    /// <summary>
    /// Builds an <c>onScreen</c> callback for <see cref="ScanService.RecordAsync"/> that saves the run to
    /// history after every screen, not just at the end: a recording interrupted by an error, a cancellation,
    /// a killed app, or the whole process being stopped, still shows up in History/Dashboard with whatever it
    /// captured up to that point (see Swipewalk.Engine.Recorder, which writes report.html/results.json to
    /// <paramref name="outDir"/> after every screen already). Reads the report back from the results.json
    /// Recorder just wrote rather than needing the in-memory <see cref="ScanReport"/>, so this works from any
    /// caller (the CLI, the desktop app, `swipewalk run`) without changing Recorder's own API. Best effort: a
    /// failed incremental save is reported but never stops the recording -- the final save (once the caller's
    /// own recording call returns) still runs, since Recorder never throws for a mid-recording problem (see
    /// Recorder.RunAsync's EndedEarlyReason handling). Every call here is mid-recording by definition (the
    /// caller's own final <see cref="SaveAsync"/> call, after the recording actually stops, is separate), so
    /// this always saves with <c>recordingInProgress: true</c> -- see <see cref="StartRecordingAsync"/>.
    /// </summary>
    public Action<ScreenResult> OnScreenSaved(ScanOptions options, string mode, DateTimeOffset startedAt, string outDir, IProgress<string>? log = null)
    {
        var htmlPath = Path.Combine(outDir, "report.html");
        var jsonPath = Path.Combine(outDir, "results.json");
        return _ =>
        {
            try
            {
                if (JsonReport.Deserialize(File.ReadAllText(jsonPath)) is { } report)
                    SaveAsync(new RunResult(report, htmlPath, jsonPath), options, mode, startedAt, recordingInProgress: true).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
            {
                log?.Report($"  (could not update the saved run yet: {ex.Message})");
            }
        };
    }

    /// <summary>All saved runs, newest first.</summary>
    public IReadOnlyList<RunRecord> List()
    {
        if (!Directory.Exists(Root))
            return [];
        var runs = new List<RunRecord>();
        foreach (var file in Directory.EnumerateFiles(Root, "run.json", SearchOption.AllDirectories))
        {
            try
            {
                if (JsonSerializer.Deserialize<RunRecord>(File.ReadAllText(file), Json) is { } run)
                {
                    run = run with { Folder = Path.GetDirectoryName(file)! };
                    // Legacy run.json (saved before AppKey existed) has no AppKey on disk; infer it here, at
                    // load time, rather than rewriting the file -- see RunRecord.InferAppKeyFromLegacyApp.
                    if (run.AppKey is null && RunRecord.InferAppKeyFromLegacyApp(run.App) is { } inferred)
                        run = run with { AppKey = inferred };
                    // A run.json still marked RecordingInProgress means the process that wrote it never got to
                    // make a final save -- normally because it's still recording right now, but a process
                    // killed outright (SIGKILL, a force-quit, a crash, power loss) leaves the same thing on
                    // disk forever. Tell them apart the only way possible after the fact: whether the process
                    // that owned this run is still alive. Computed here, at load time, the same as the AppKey
                    // inference just above -- never written back to run.json, so a read never has the side
                    // effect of mutating a file another process might be writing to right now (see
                    // RunHistory.IsOwningProcessRunning).
                    if (run.RecordingInProgress && !IsOwningProcessRunning(run.RecordingProcessId, run.RecordingProcessStartedAt))
                        run = run with { RecordingInProgress = false, EndedEarlyReason = RunRecord.AbandonedRecordingReason };
                    runs.Add(run);
                }
            }
            catch (JsonException)
            {
                // A damaged run.json should not hide the other runs.
            }
        }
        return [.. runs.OrderByDescending(r => r.StartedAt)];
    }

    /// <summary>
    /// The run of the same app and platform before <paramref name="run"/>, if any. Uses <see cref="RunRecord.GroupKey"/>,
    /// not <see cref="RunRecord.App"/>: a run with no identified app (<see cref="RunRecord.AppKey"/> null) never
    /// matches another run this way, including another unidentified one, since they could be different apps.
    /// </summary>
    /// <summary>
    /// Finds a saved run by its <see cref="RunRecord.Id"/> (the folder name, e.g. from <c>swipewalk history</c>)
    /// or by its folder path (given directly, or via a run's own <see cref="RunRecord.Folder"/>); null if
    /// neither matches. Used by <c>record --continue &lt;run&gt;</c> (see <see cref="ScanService.ResolveContinuation"/>)
    /// and the desktop app's Continue action.
    /// </summary>
    public RunRecord? Resolve(string idOrFolder) =>
        List().FirstOrDefault(r => r.Id == idOrFolder)
        ?? (Directory.Exists(idOrFolder)
            ? List().FirstOrDefault(r => Path.GetFullPath(r.Folder) == Path.GetFullPath(idOrFolder))
            : null);

    public RunRecord? Previous(RunRecord run) =>
        run.AppKey is null ? null : List().FirstOrDefault(r => r.GroupKey == run.GroupKey && r.Platform == run.Platform && r.StartedAt < run.StartedAt);

    /// <summary>Compares two saved runs; null when either run's results.json is missing or unreadable.</summary>
    public static ReportComparison? Compare(RunRecord earlier, RunRecord later) =>
        Load(earlier) is { } before && Load(later) is { } after ? ReportComparison.Compare(before, after) : null;

    /// <summary>
    /// Loads a run's results.json, with any recorded guided-check answers merged in from guided-answers.json
    /// (see <see cref="GuidedAnswerStore"/>) -- a separate sidecar file, never embedded in results.json itself,
    /// so <see cref="ScanReport.GuidedAnswers"/> (and everything computed from it: <see cref="ScanReport.ScreenCoverage"/>,
    /// <see cref="ScanReport.Contradictions"/>, <see cref="ScanReport.ScreensWithNoGuidedAnswers"/>) would
    /// otherwise silently read as empty for every run loaded this way, even one with answers on disk.
    /// </summary>
    public static ScanReport? Load(RunRecord run)
    {
        try
        {
            var report = JsonReport.Deserialize(File.ReadAllText(run.ResultsPath));
            if (report is null)
                return null;
            var guidedAnswers = GuidedAnswerStore.Load(run.Folder)?.Answers;
            return guidedAnswers is null ? report : report with { GuidedAnswers = guidedAnswers };
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public void Delete(RunRecord run)
    {
        if (Path.GetFullPath(run.Folder).StartsWith(Path.GetFullPath(Root), StringComparison.Ordinal))
            Directory.Delete(run.Folder, recursive: true);
    }

    private static string CopyInto(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
        return target;
    }

    /// <summary>
    /// Whether the process that saved a run with <see cref="RunRecord.RecordingInProgress"/> true is still the
    /// one running with that pid -- used by <see cref="List"/> to tell a currently-live recording from one
    /// whose process is gone. Checking the pid alone isn't enough: the OS reuses process ids, so a dead
    /// recording's pid could since have been picked up by a completely unrelated process; comparing that
    /// process's own start time against <paramref name="startedAt"/> (saved alongside the pid -- see
    /// <see cref="RunRecord.RecordingProcessStartedAt"/>) rules that out. Compared with a couple of seconds'
    /// tolerance, not exact equality: OS process-start timestamps can round differently than .NET's own
    /// <see cref="DateTimeOffset"/> round-trip through JSON. False (not confirmed running) for a missing pid,
    /// a pid with no such process, or one whose start time doesn't match -- see <see cref="List"/>'s remarks on
    /// why "can't confirm it's alive" is treated the same as "it's gone" here. This only ever sees processes on
    /// this computer: a history folder synced or shared between machines (not something Swipewalk does itself)
    /// could show a still-running recording on another machine as "ended early" here.
    /// </summary>
    public static bool IsOwningProcessRunning(int? pid, DateTimeOffset? startedAt)
    {
        if (pid is not { } id || startedAt is not { } started)
            return false;
        try
        {
            using var process = Process.GetProcessById(id);
            return Math.Abs((new DateTimeOffset(process.StartTime) - started).TotalSeconds) < 2;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            // ArgumentException: no process with that id (it's gone, or the id was reused and then that
            // process also exited). InvalidOperationException: the process exited between GetProcessById and
            // reading StartTime. NotSupportedException/Win32Exception: StartTime couldn't be read (e.g. a
            // sandboxed or restricted process) -- treated the same as "gone", since there's no way to confirm
            // it's the same process either way.
            return false;
        }
    }
}
