using Swipewalk.Collectors.Preflight;
using Swipewalk.Core.Model;

namespace Swipewalk.Engine;

/// <summary>Outcome of one target in a run.</summary>
public sealed record TargetOutcome(TargetConfig Target, RunRecord? Run, string? Error);

/// <summary>
/// `swipewalk run`: for each target, install the app if a build is given, check readiness, scan or record,
/// and save the run to the history. One target failing doesn't stop the others.
/// </summary>
public sealed class Runner(ScanService service, RunHistory history, IProgress<string> log)
{
    public async Task<IReadOnlyList<TargetOutcome>> RunAsync(
        RunConfig config, RecorderControl? control = null, LargeTextRestartAsker? largeTextRestartAsk = null,
        RevisitSkippedScreensAsker? revisitSkippedScreensAsk = null, CancellationToken cancellationToken = default,
        bool saveToHistory = true)
    {
        var outcomes = new List<TargetOutcome>();
        foreach (var target in config.Targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            log.Report($"== {target.Platform}{(target.Device is null ? "" : $" ({target.Device})")}");
            var started = DateTimeOffset.Now;
            try
            {
                var options = await service.InstallAsync(config.ToOptions(target, outDir: ""));
                if (options.AppId is null)
                {
                    // Never scan whatever happens to be on screen: stop rather than guess.
                    outcomes.Add(new TargetOutcome(target, null,
                        $"could not tell which app to scan; set app.{target.Platform}.{(target.Platform == "ios" ? "bundleId" : "package")}"));
                    log.Report($"Failed: {outcomes[^1].Error}");
                    continue;
                }

                // screenReaderCapture changes a physical phone's accessibility settings; a swipewalk.json run
                // is never interactive, so nobody can answer the CLI's --screen-reader-confirm prompt --
                // screenReaderConfirm stands in for that answer instead (see ScreenReaderCaptureGate). Never
                // gates an emulator, or an iOS target -- screenReaderCapture is Android only for now, so an
                // iOS target with it set must never be blocked over a physical Android phone that happens to
                // be connected too.
                if (options.Platform == TargetPlatform.Android && options.ScreenReaderCapture && !config.ScreenReaderConfirm
                    && await ScreenReaderCaptureGate.PhysicalDeviceNeedingConfirmationAsync(options.Device) is { } physical)
                {
                    outcomes.Add(new TargetOutcome(target, null,
                        $"screenReaderCapture would change {physical.Name}'s accessibility settings; set " +
                        "\"screenReaderConfirm\": true in swipewalk.json to confirm this on a physical phone, or test on an emulator instead"));
                    log.Report($"Failed: {outcomes[^1].Error}");
                    continue;
                }

                options = options with
                {
                    // --no-history writes to a local folder instead of the history, like scan/record's own
                    // --no-history; unlike them, each target gets a timestamped, app-named subfolder under
                    // "out" (or "swipewalk-report") so several targets don't overwrite each other's output --
                    // see LocalOutputDirectory.
                    OutputDirectory = saveToHistory && config.Out is null
                        ? history.NewRunFolder(options.AppId)
                        : LocalOutputDirectory(config.Out, options.AppId),
                };
                var checks = await service.CheckAsync(options);
                foreach (var check in checks.Where(c => c.Status != CheckStatus.Pass))
                    log.Report(check.ToString());
                if (!Preflight.CanProceed(checks))
                {
                    outcomes.Add(new TargetOutcome(target, null, "pre-flight checks failed"));
                    continue;
                }

                var mode = config.Mode == "record" ? "record" : "run";
                if (saveToHistory && config.Mode == "record")
                    // Written before the first screen, not just after it: a process killed before it captures
                    // anything still leaves this run in History marked "in progress" -- see
                    // RunHistory.StartRecordingAsync.
                    // `swipewalk run` never continues an earlier run (no --continue equivalent), so this is
                    // always a fresh recording.
                    await history.StartRecordingAsync(options, started, options.OutputDirectory);
                var result = config.Mode == "record"
                    // Save after every screen, not just at the end: a recording this loop's own catch below
                    // can't reach (a killed device, the whole process stopped) still ends up in
                    // History/Dashboard with whatever it captured -- see RunHistory.OnScreenSaved.
                    // --no-history skips this the same way scan/record's own --no-history does: Recorder still
                    // writes report.html/results.json to options.OutputDirectory after every screen regardless.
                    ? await service.RecordAsync(options, control ?? new RecorderControl(), cancellationToken,
                        onScreen: saveToHistory ? history.OnScreenSaved(options, mode, started, options.OutputDirectory, log) : null,
                        largeTextRestartAsk: largeTextRestartAsk, revisitSkippedScreensAsk: revisitSkippedScreensAsk)
                    : await service.ScanAsync(options, cancellationToken, largeTextRestartAsk: largeTextRestartAsk);
                // --no-history: build the same record (for the exit code and the ended-early reason below)
                // without writing it into the history, or copying the output there -- see RunHistory.Build.
                var run = saveToHistory
                    ? await history.SaveAsync(result, options, mode, started)
                    : RunHistory.Build(result, options, mode, started, options.OutputDirectory);
                log.Report(ReportWriter.Summary(result.Report));
                log.Report($"  {result.HtmlPath}");
                // A recording that ended early (cancelled, or an error mid-capture) is still written above with
                // whatever it captured (and saved to history unless --no-history), but the target still counts
                // as not fully scanned for CI purposes -- see ExitCode -- while keeping Run set so the partial
                // report is still reachable, unlike a pre-flight failure above.
                outcomes.Add(new TargetOutcome(target, run, run.EndedEarlyReason));
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
            {
                log.Report($"Failed: {ex.Message}");
                outcomes.Add(new TargetOutcome(target, null, ex.Message));
            }
        }
        return outcomes;
    }

    /// <summary>CI exit code: 0 ok, 2 a target could not be scanned, 3 WCAG issues found and failOn is "wcag-issues".</summary>
    public static int ExitCode(RunConfig config, IReadOnlyList<TargetOutcome> outcomes) =>
        outcomes.Any(o => o.Error is not null) ? 2
        : config.FailOn == "wcag-issues" && outcomes.Any(o => o.Run?.Counts.WcagIssues > 0) ? 3
        : 0;

    /// <summary>
    /// Where a target's output goes when it isn't going into the run history: <paramref name="configOut"/>
    /// (swipewalk.json's "out", already resolved to an absolute path by <see cref="RunConfig.Validate"/>) if
    /// given, else a "swipewalk-report" folder relative to the current directory -- the same default folder
    /// name <c>scan</c>/<c>record</c> use (see Program.cs's <c>ToScanOptions</c>), though they write straight
    /// into it since there's only ever one target. A timestamped, app-named subfolder under it keeps multiple
    /// targets (or repeated runs) from overwriting each other's output here, the same as the history's own
    /// <see cref="RunHistory.NewRunFolder"/>.
    /// </summary>
    public static string LocalOutputDirectory(string? configOut, string appId) =>
        Path.Combine(configOut ?? "swipewalk-report", $"{DateTime.Now:yyyyMMdd-HHmmss}-{appId}");
}
