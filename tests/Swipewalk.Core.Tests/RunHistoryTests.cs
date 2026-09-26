using System.Diagnostics;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Engine;

namespace Swipewalk.Core.Tests;

/// <summary>
/// Covers the "app id unknown" fallback in <see cref="RunHistory.SaveAsync"/>: it must never label a run with
/// a screen name (the bug that produced dashboard cards titled "Screen 1 · Android"), and must tell "no app id,
/// live scan" apart from "no app id, replayed from a saved capture".
/// </summary>
public class RunHistoryTests
{
    private static async Task<RunRecord> Save(
        RunHistory history, ScanOptions options, string appName = "unused", string? reportAppId = null, DateTimeOffset? startedAt = null)
    {
        var folder = history.NewRunFolder(appName);
        File.WriteAllText(Path.Combine(folder, "report.html"), "<html></html>");
        var report = new ScanReport
        {
            ToolVersion = "1.0.0",
            Screens = [new ScreenResult { Platform = Platform.Android, ScreenName = "Screen 1", Findings = [] }],
            // Simulates what ScanService/Recorder set from the capture (see ScreenSnapshot.AppId) when the
            // caller passed no --package/--bundle-id; AndroidCollectorTests and UiAutomatorParserTests cover
            // that derivation itself.
            AppId = reportAppId,
        };
        options = options with { OutputDirectory = folder };
        return await history.SaveAsync(
            new RunResult(report, Path.Combine(folder, "report.html"), Path.Combine(folder, "results.json")),
            options, "scan", startedAt ?? DateTimeOffset.Now);
    }

    private static RunHistory NewHistory(out string root)
    {
        root = Path.Combine(Path.GetTempPath(), $"cf-history-{Guid.NewGuid():N}");
        return new RunHistory(root);
    }

    [Fact]
    public async Task NoAppId_RecordsUnknownApp_NotTheScreenName()
    {
        var history = NewHistory(out var root);
        try
        {
            var options = new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = "" };
            var run = await Save(history, options);

            Assert.Equal(RunRecord.UnknownApp, run.App);
            Assert.NotEqual("Screen 1", run.App);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NoAppId_FromASavedCapture_RecordsTheSavedCaptureVariant()
    {
        var history = NewHistory(out var root);
        try
        {
            var options = new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = "", FromCapture = "/tmp/some-capture" };
            var run = await Save(history, options);

            Assert.Equal(RunRecord.UnknownAppFromCapture, run.App);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WithAppId_IsUnchanged_EvenFromASavedCapture()
    {
        var history = NewHistory(out var root);
        try
        {
            var options = new ScanOptions
            {
                Platform = TargetPlatform.Android, OutputDirectory = "", Package = "org.example.app", FromCapture = "/tmp/some-capture",
            };
            var run = await Save(history, options);

            Assert.Equal("org.example.app", run.App);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExistingRunJson_WithAnArbitraryAppValue_StillLoads()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cf-history-{Guid.NewGuid():N}");
        var folder = Path.Combine(root, "20260101-000000-old");
        Directory.CreateDirectory(folder);
        try
        {
            // Simulates a run.json written before this fix, where App was set from a screen name.
            File.WriteAllText(Path.Combine(folder, "run.json"), """
                {
                  "id": "20260101-000000-old",
                  "app": "Screen 1",
                  "platform": "Android",
                  "mode": "scan",
                  "startedAt": "2026-01-01T00:00:00+00:00",
                  "finishedAt": "2026-01-01T00:01:00+00:00",
                  "counts": { "wcagIssues": 0, "needsReview": 0, "platformAdvisories": 0, "screens": 1 },
                  "toolVersion": "1.0.0",
                  "rulesetVersion": "1.0.0"
                }
                """);

            var run = Assert.Single(new RunHistory(root).List());

            Assert.Equal("Screen 1", run.App);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WithAppId_SetsAppKey()
    {
        var history = NewHistory(out var root);
        try
        {
            var options = new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = "", Package = "org.swipewalk.buggyapp" };
            var run = await Save(history, options);

            Assert.Equal("org.swipewalk.buggyapp", run.AppKey);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NoAppId_ButCaptureIdentifiedTheApp_SetsAppKeyFromTheCapture()
    {
        // No --package given, but the scan itself told us the app from the capture (see
        // AndroidCollectorTests.Load_WithoutPackage_SetsAppIdFromTheCapture / UiAutomatorParser.GuessPackage) --
        // that must still become AppKey, not just leave the run unidentified.
        var history = NewHistory(out var root);
        try
        {
            var options = new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = "" };
            var run = await Save(history, options, reportAppId: "org.swipewalk.buggyapp");

            Assert.Equal("org.swipewalk.buggyapp", run.AppKey);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NoAppId_AtAll_LeavesAppKeyNull()
    {
        var history = NewHistory(out var root);
        try
        {
            var options = new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = "" };
            var run = await Save(history, options);

            Assert.Null(run.AppKey);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Previous_MatchesARunWithNoTypedPackage_ToAnEarlierRunOfTheSameAppThatHadOne()
    {
        // The scenario in the bug report, fixed: a scan without --package must still group with an earlier
        // scan of the same app that did pass --package, using the package the capture itself identified
        // (reportAppId here stands in for what AndroidCollector.Load would have set from the uiautomator dump).
        var history = NewHistory(out var root);
        try
        {
            var earlier = await Save(
                history, new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = "", Package = "org.swipewalk.buggyapp" },
                appName: "earlier", startedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var later = await Save(
                history, new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = "" }, appName: "later",
                reportAppId: "org.swipewalk.buggyapp", startedAt: new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));

            Assert.Equal(earlier.Id, history.Previous(later)?.Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Previous_NeverMatchesTwoUnidentifiedRuns_EvenThoughBothShowTheSameUnknownAppLabel()
    {
        var history = NewHistory(out var root);
        try
        {
            var earlier = await Save(
                history, new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = "" }, appName: "earlier",
                startedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var later = await Save(
                history, new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = "" }, appName: "later",
                startedAt: new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));

            Assert.Equal(RunRecord.UnknownApp, earlier.App);
            Assert.Equal(earlier.App, later.App); // same display label...
            Assert.Null(history.Previous(later)); // ...but never treated as the same app
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExistingRunJson_WithoutAppKey_StillLoads_AndStaysUnidentifiedAndUngrouped()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cf-history-{Guid.NewGuid():N}");
        var folderA = Path.Combine(root, "20260101-000000-old-a");
        var folderB = Path.Combine(root, "20260102-000000-old-b");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);
        try
        {
            // Simulates two run.json files written before AppKey existed.
            foreach (var (folder, id) in new[] { (folderA, "20260101-000000-old-a"), (folderB, "20260102-000000-old-b") })
                File.WriteAllText(Path.Combine(folder, "run.json"), $$"""
                    {
                      "id": "{{id}}",
                      "app": "Unknown app",
                      "platform": "Android",
                      "mode": "scan",
                      "startedAt": "2026-01-0{{(id.Contains("-a") ? 1 : 2)}}T00:00:00+00:00",
                      "finishedAt": "2026-01-0{{(id.Contains("-a") ? 1 : 2)}}T00:01:00+00:00",
                      "counts": { "wcagIssues": 0, "needsReview": 0, "platformAdvisories": 0, "screens": 1 },
                      "toolVersion": "1.0.0",
                      "rulesetVersion": "1.0.0"
                    }
                    """);

            var history = new RunHistory(root);
            var runs = history.List();

            Assert.Equal(2, runs.Count);
            Assert.All(runs, r => Assert.Null(r.AppKey));
            var later = runs.Single(r => r.Id.EndsWith("-b", StringComparison.Ordinal));
            Assert.Null(history.Previous(later));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // Covers RunRecord.InferAppKeyFromLegacyApp: a run.json saved before AppKey existed has App set but
    // no appKey field on disk. Loading it should recover AppKey only when App clearly looks like a package/bundle
    // id, so an old run of a known app groups with newer runs of it instead of showing a second "unidentified"
    // card for the same app (the bug this fixes: an old card titled "org.swipewalk.buggyapp" that then said "No
    // app was identified for this run").

    [Fact]
    public async Task LegacyRecord_WithAppLookingLikeAPackageId_GroupsWithANewRunOfTheSameApp()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cf-history-{Guid.NewGuid():N}");
        try
        {
            WriteLegacyRunJson(
                root, "20260101-000000-old", app: "org.swipewalk.buggyapp",
                startedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

            var history = new RunHistory(root);
            var older = Assert.Single(history.List());
            Assert.Equal("org.swipewalk.buggyapp", older.AppKey);

            var newer = await Save(
                history, new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = "", Package = "org.swipewalk.buggyapp" },
                appName: "newer", startedAt: new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));

            Assert.Equal(older.Id, history.Previous(newer)?.Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LegacyRecord_WithAScreenNameAsApp_StaysItsOwnGroup()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cf-history-{Guid.NewGuid():N}");
        try
        {
            WriteLegacyRunJson(root, "20260101-000000-old", app: "Screen 1", startedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

            var run = Assert.Single(new RunHistory(root).List());

            Assert.Null(run.AppKey);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LegacyRecord_WithUnknownAppLabel_StaysItsOwnGroup()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cf-history-{Guid.NewGuid():N}");
        try
        {
            WriteLegacyRunJson(root, "20260101-000000-old", app: RunRecord.UnknownApp, startedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

            var run = Assert.Single(new RunHistory(root).List());

            Assert.Null(run.AppKey);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RecordWithAppKeyOnDisk_IgnoresApp_EvenWhenAppDoesNotLookLikeThatId()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cf-history-{Guid.NewGuid():N}");
        try
        {
            // App is a display label that doesn't itself look like the id; AppKey (from disk) must win outright,
            // never be second-guessed or replaced by inferring from App.
            WriteLegacyRunJson(
                root, "20260101-000000-old", app: "Unknown app", appKey: "org.swipewalk.buggyapp",
                startedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

            var run = Assert.Single(new RunHistory(root).List());

            Assert.Equal("org.swipewalk.buggyapp", run.AppKey);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TwoLegacyRecords_OfTheSamePackage_GroupTogether()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cf-history-{Guid.NewGuid():N}");
        try
        {
            WriteLegacyRunJson(
                root, "20260101-000000-old-a", app: "org.swipewalk.buggyapp",
                startedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            WriteLegacyRunJson(
                root, "20260102-000000-old-b", app: "org.swipewalk.buggyapp",
                startedAt: new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));

            var history = new RunHistory(root);
            var runs = history.List();
            var later = runs.Single(r => r.Id.EndsWith("-b", StringComparison.Ordinal));

            Assert.Equal(2, runs.Count);
            Assert.All(runs, r => Assert.Equal("org.swipewalk.buggyapp", r.AppKey));
            Assert.Equal("20260101-000000-old-a", history.Previous(later)?.Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Writes a run.json in the shape saved before <see cref="RunRecord.AppKey"/> existed (no "appKey"
    /// field), unless <paramref name="appKey"/> is given.</summary>
    private static void WriteLegacyRunJson(string root, string id, string app, DateTimeOffset startedAt, string? appKey = null)
    {
        var folder = Path.Combine(root, id);
        Directory.CreateDirectory(folder);
        var appKeyField = appKey is null ? "" : $"""
            , "appKey": "{appKey}"
            """;
        File.WriteAllText(Path.Combine(folder, "run.json"), $$"""
            {
              "id": "{{id}}",
              "app": "{{app}}"{{appKeyField}},
              "platform": "Android",
              "mode": "scan",
              "startedAt": "{{startedAt:O}}",
              "finishedAt": "{{startedAt:O}}",
              "counts": { "wcagIssues": 0, "needsReview": 0, "platformAdvisories": 0, "screens": 1 },
              "toolVersion": "1.0.0",
              "rulesetVersion": "1.0.0"
            }
            """);
    }

    [Fact]
    public void GroupKey_TwoUnidentifiedRecords_AreNeverEqual()
    {
        var a = MinimalRecord(id: "run-a", appKey: null);
        var b = MinimalRecord(id: "run-b", appKey: null);

        Assert.NotEqual(a.GroupKey, b.GroupKey);
    }

    [Fact]
    public void GroupKey_SameAppKey_AreEqual()
    {
        var a = MinimalRecord(id: "run-a", appKey: "org.example.app");
        var b = MinimalRecord(id: "run-b", appKey: "org.example.app");

        Assert.Equal(a.GroupKey, b.GroupKey);
    }

    [Fact]
    public async Task SaveAsync_RecordsEndedEarlyReasonFromTheReport()
    {
        var history = NewHistory(out var root);
        try
        {
            var folder = history.NewRunFolder("org.example.app");
            File.WriteAllText(Path.Combine(folder, "report.html"), "<html></html>");
            var report = new ScanReport
            {
                ToolVersion = "1.0.0",
                Screens = [new ScreenResult { Platform = Platform.Android, ScreenName = "Home", Findings = [] }],
                EndedEarlyReason = "An error interrupted the recording: device disconnected. Screens after that were not scanned.",
            };
            var options = new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = folder, Package = "org.example.app" };

            var run = await history.SaveAsync(
                new RunResult(report, Path.Combine(folder, "report.html"), Path.Combine(folder, "results.json")), options, "record", DateTimeOffset.Now);

            Assert.Equal(report.EndedEarlyReason, run.EndedEarlyReason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OnScreenSaved_WritesTheRunToHistory_BeforeTheRecordingFinishes()
    {
        // Simulates what Recorder already does after every screen (write report.html/results.json into
        // outDir); OnScreenSaved's job is to notice that and get the run into history/run.json from it,
        // without needing the in-memory ScanReport.
        var history = NewHistory(out var root);
        try
        {
            var outDir = history.NewRunFolder("org.example.app");
            var options = new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = outDir, Package = "org.example.app" };
            var started = DateTimeOffset.Now;
            var onScreen = history.OnScreenSaved(options, "record", started, outDir);

            async Task WriteReportWithScreens(int count)
            {
                var report = new ScanReport
                {
                    ToolVersion = "1.0.0",
                    Screens = [.. Enumerable.Range(1, count).Select(i => new ScreenResult { Platform = Platform.Android, ScreenName = $"Screen {i}", Findings = [] })],
                };
                await ReportWriter.WriteAsync(report, outDir);
            }

            await WriteReportWithScreens(1);
            onScreen(new ScreenResult { Platform = Platform.Android, ScreenName = "Screen 1", Findings = [] });

            var afterFirst = Assert.Single(history.List());
            Assert.Equal(1, afterFirst.Counts.Screens);

            await WriteReportWithScreens(2);
            onScreen(new ScreenResult { Platform = Platform.Android, ScreenName = "Screen 2", Findings = [] });

            var afterSecond = Assert.Single(history.List()); // same run, updated in place -- not a second entry
            Assert.Equal(2, afterSecond.Counts.Screens);
            Assert.Equal(afterFirst.Id, afterSecond.Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static RunRecord MinimalRecord(string id, string? appKey) => new()
    {
        Id = id,
        App = appKey ?? RunRecord.UnknownApp,
        AppKey = appKey,
        Platform = "Android",
        Mode = "scan",
        StartedAt = DateTimeOffset.UtcNow,
        FinishedAt = DateTimeOffset.UtcNow,
        Counts = new RunCounts(0, 0, 0, 1),
        ToolVersion = "1.0.0",
        RulesetVersion = "1.0.0",
    };

    // Covers the case where Swipewalk itself was killed outright (SIGKILL, a force-quit, a crash, power loss):
    // a process that never gets to write a final save leaves run.json saying RecordingInProgress forever.
    // RunHistory.List() must tell that apart from a recording
    // that's genuinely still going, using the owning process's pid + start time (RunRecord.RecordingProcessId/
    // RecordingProcessStartedAt) -- never by rewriting the file (kept a pure read, like the AppKey inference
    // above).

    /// <summary>Writes a run.json with the fields RunHistory.SaveAsync(..., recordingInProgress: true) would
    /// have written, but with an explicit (possibly fake) owning process -- WriteRun in ScanServiceTests covers
    /// the same shape for a normal (non-in-progress) run; this is specific to the in-progress fields.</summary>
    private static void WriteInProgressRunJson(string root, string id, DateTimeOffset startedAt, int? processId, DateTimeOffset? processStartedAt)
    {
        var folder = Path.Combine(root, id);
        Directory.CreateDirectory(folder);
        var pidField = processId is null ? "" : $", \"recordingProcessId\": {processId}";
        var startField = processStartedAt is null ? "" : $", \"recordingProcessStartedAt\": \"{processStartedAt:O}\"";
        File.WriteAllText(Path.Combine(folder, "run.json"), $$"""
            {
              "id": "{{id}}",
              "app": "org.swipewalk.buggyapp",
              "appKey": "org.swipewalk.buggyapp",
              "platform": "Android",
              "mode": "record",
              "startedAt": "{{startedAt:O}}",
              "finishedAt": "{{startedAt:O}}",
              "counts": { "wcagIssues": 0, "needsReview": 0, "platformAdvisories": 0, "screens": 1 },
              "toolVersion": "1.0.0",
              "rulesetVersion": "1.0.0",
              "recordingInProgress": true{{pidField}}{{startField}}
            }
            """);
    }

    [Fact]
    public void InProgress_OwningProcessGone_ReadsAsEndedEarly_AndCanContinue()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cf-history-{Guid.NewGuid():N}");
        try
        {
            // A pid far past any real process id on any supported OS -- guaranteed not to exist -- stands in
            // for "the process that was recording this is gone".
            WriteInProgressRunJson(root, "20260101-000000-old", DateTimeOffset.UtcNow.AddMinutes(-5), processId: 2_000_000_000, processStartedAt: DateTimeOffset.UtcNow.AddMinutes(-5));

            var run = Assert.Single(new RunHistory(root).List());

            Assert.False(run.RecordingInProgress);
            Assert.Equal(RunRecord.AbandonedRecordingReason, run.EndedEarlyReason);
            Assert.True(run.CanContinue);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InProgress_NoOwningProcessRecorded_ReadsAsEndedEarly()
    {
        // Defensive case: RecordingInProgress true but no pid saved alongside it (shouldn't happen from
        // RunHistory.SaveAsync itself, which always sets both together) -- "can't confirm it's alive" is
        // treated the same as "it's gone", not left stuck "in progress" forever.
        var root = Path.Combine(Path.GetTempPath(), $"cf-history-{Guid.NewGuid():N}");
        try
        {
            WriteInProgressRunJson(root, "20260101-000000-old", DateTimeOffset.UtcNow.AddMinutes(-5), processId: null, processStartedAt: null);

            var run = Assert.Single(new RunHistory(root).List());

            Assert.False(run.RecordingInProgress);
            Assert.True(run.CanContinue);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InProgress_OwningProcessStillRunning_StaysInProgress_AndCannotBeContinued()
    {
        var history = NewHistory(out var root);
        try
        {
            // The test process itself: guaranteed alive for the life of this test, with a real, matching
            // start time -- exactly what RunHistory.SaveAsync(..., recordingInProgress: true) would have saved
            // for a recording actually under way right now.
            var folder = history.NewRunFolder("org.swipewalk.buggyapp");
            var options = new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = folder, Package = "org.swipewalk.buggyapp" };
            var report = new ScanReport { ToolVersion = "1.0.0", Screens = [] };
            var run = await history.SaveAsync(
                new RunResult(report, Path.Combine(folder, "report.html"), Path.Combine(folder, "results.json")),
                options, "record", DateTimeOffset.Now, recordingInProgress: true);

            Assert.Equal(Environment.ProcessId, run.RecordingProcessId);

            var reloaded = Assert.Single(history.List());

            Assert.True(reloaded.RecordingInProgress);
            Assert.Null(reloaded.EndedEarlyReason);
            Assert.False(reloaded.CanContinue);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NormalFinish_IsUnaffected_NotInProgress_NotContinuable()
    {
        // The final save (recordingInProgress left at its default, false) of a recording that reached Finish:
        // must read exactly as it always did.
        var history = NewHistory(out var root);
        try
        {
            var run = await Save(history, new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = "", Package = "org.example.app" });

            Assert.False(run.RecordingInProgress);
            Assert.Null(run.EndedEarlyReason);
            Assert.False(run.CanContinue);
            Assert.Null(run.RecordingProcessId);
            Assert.Null(run.RecordingProcessStartedAt);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExistingRunJson_WithoutTheInProgressFields_StillLoads_AsNotInProgress()
    {
        // A run.json saved before this feature existed has none of recordingInProgress/RecordingProcessId/
        // RecordingProcessStartedAt on disk -- must load exactly as a normal, non-continuable run, never as
        // "in progress" or "ended early" out of nowhere.
        var root = Path.Combine(Path.GetTempPath(), $"cf-history-{Guid.NewGuid():N}");
        var folder = Path.Combine(root, "20260101-000000-old");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "run.json"), """
                {
                  "id": "20260101-000000-old",
                  "app": "org.swipewalk.buggyapp",
                  "appKey": "org.swipewalk.buggyapp",
                  "platform": "Android",
                  "mode": "record",
                  "startedAt": "2026-01-01T00:00:00+00:00",
                  "finishedAt": "2026-01-01T00:01:00+00:00",
                  "counts": { "wcagIssues": 0, "needsReview": 0, "platformAdvisories": 0, "screens": 1 },
                  "toolVersion": "1.0.0",
                  "rulesetVersion": "1.0.0"
                }
                """);

            var run = Assert.Single(new RunHistory(root).List());

            Assert.False(run.RecordingInProgress);
            Assert.Null(run.EndedEarlyReason);
            Assert.False(run.CanContinue);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartRecordingAsync_WritesAnInProgressRun_BeforeAnyScreen()
    {
        var history = NewHistory(out var root);
        try
        {
            var outDir = history.NewRunFolder("org.example.app");
            var options = new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = outDir, Package = "org.example.app" };
            var started = DateTimeOffset.Now;

            var run = await history.StartRecordingAsync(options, started, outDir);

            Assert.True(run.RecordingInProgress);
            Assert.Equal(0, run.Counts.Screens);
            Assert.Equal(Environment.ProcessId, run.RecordingProcessId);
            var listed = Assert.Single(history.List());
            Assert.True(listed.RecordingInProgress);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartRecordingAsync_ForAContinuation_KeepsThePriorScreenCount()
    {
        var history = NewHistory(out var root);
        try
        {
            var outDir = history.NewRunFolder("org.example.app");
            var options = new ScanOptions { Platform = TargetPlatform.Android, OutputDirectory = outDir, Package = "org.example.app" };
            var priorReport = new ScanReport
            {
                ToolVersion = "1.0.0",
                Screens = [new ScreenResult { Platform = Platform.Android, ScreenName = "Home", Findings = [] }],
            };

            var run = await history.StartRecordingAsync(options, DateTimeOffset.Now, outDir, priorReport);

            Assert.Equal(1, run.Counts.Screens);
            Assert.True(run.RecordingInProgress);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IsOwningProcessRunning_TheCurrentProcess_IsTrue()
    {
        using var current = Process.GetCurrentProcess();

        Assert.True(RunHistory.IsOwningProcessRunning(current.Id, new DateTimeOffset(current.StartTime)));
    }

    [Fact]
    public void IsOwningProcessRunning_NoSuchProcess_IsFalse()
    {
        Assert.False(RunHistory.IsOwningProcessRunning(2_000_000_000, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsOwningProcessRunning_MissingFields_IsFalse()
    {
        Assert.False(RunHistory.IsOwningProcessRunning(null, DateTimeOffset.UtcNow));
        Assert.False(RunHistory.IsOwningProcessRunning(1234, null));
    }

    // Covers RunHistory.Build, the pure computation SaveAsync itself does before writing run.json -- used by
    // `swipewalk run --no-history` (see Runner.RunAsync) so it can still report Counts/EndedEarlyReason for its
    // own exit code without saving the run into the history or copying its output there.

    [Fact]
    public void Build_DoesNotTouchTheFileSystem()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cf-history-{Guid.NewGuid():N}"); // deliberately never created
        var folder = Path.Combine(Path.GetTempPath(), $"cf-nohistory-{Guid.NewGuid():N}"); // deliberately never created
        var report = new ScanReport
        {
            ToolVersion = "1.0.0",
            Screens = [new ScreenResult { Platform = Platform.Android, ScreenName = "Home", Findings = [] }],
        };
        var options = new ScanOptions { Platform = TargetPlatform.Android, Package = "org.example.app", OutputDirectory = folder };

        var run = RunHistory.Build(
            new RunResult(report, Path.Combine(folder, "report.html"), Path.Combine(folder, "results.json")),
            options, "run", DateTimeOffset.Now, folder);

        Assert.Equal("org.example.app", run.App);
        Assert.Equal(folder, run.Folder);
        Assert.False(Directory.Exists(folder), "Build must not create the output folder, or anything in it.");
        Assert.False(Directory.Exists(root), "Build must never touch the history root.");
    }

    [Fact]
    public async Task Build_ComputesTheSameCountsAndAppAsSaveAsync()
    {
        var history = NewHistory(out var root);
        try
        {
            var folder = history.NewRunFolder("org.example.app");
            var report = new ScanReport
            {
                ToolVersion = "1.0.0",
                Screens = [new ScreenResult { Platform = Platform.Android, ScreenName = "Home", Findings = [] }],
            };
            var options = new ScanOptions { Platform = TargetPlatform.Android, Package = "org.example.app", OutputDirectory = folder };
            var startedAt = DateTimeOffset.Now;
            var result = new RunResult(report, Path.Combine(folder, "report.html"), Path.Combine(folder, "results.json"));

            var saved = await history.SaveAsync(result, options, "run", startedAt);
            var built = RunHistory.Build(result, options, "run", startedAt, folder);

            Assert.Equal(saved.App, built.App);
            Assert.Equal(saved.AppKey, built.AppKey);
            Assert.Equal(saved.Platform, built.Platform);
            Assert.Equal(saved.Counts, built.Counts);
            Assert.Equal(saved.EndedEarlyReason, built.EndedEarlyReason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Build_RecordsEndedEarlyReasonFromTheReport()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"cf-nohistory-{Guid.NewGuid():N}");
        var report = new ScanReport
        {
            ToolVersion = "1.0.0",
            Screens = [new ScreenResult { Platform = Platform.Android, ScreenName = "Home", Findings = [] }],
            EndedEarlyReason = "An error interrupted the recording: device disconnected. Screens after that were not scanned.",
        };
        var options = new ScanOptions { Platform = TargetPlatform.Android, Package = "org.example.app", OutputDirectory = folder };

        var run = RunHistory.Build(
            new RunResult(report, Path.Combine(folder, "report.html"), Path.Combine(folder, "results.json")),
            options, "run", DateTimeOffset.Now, folder);

        Assert.Equal(report.EndedEarlyReason, run.EndedEarlyReason);
    }
}
