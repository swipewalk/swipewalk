using System.Diagnostics;
using System.Text.Json;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Wcag;
using Swipewalk.Engine;

namespace Swipewalk.Core.Tests;

/// <summary>
/// End-to-end tests of <c>swipewalk guide</c>: run the built CLI as a subprocess against a small fixture run
/// folder, with scripted answers fed over redirected stdin, the same way a person would type them. Program.cs
/// is top-level statements (not a library the test project can reference directly -- see
/// <see cref="UserGuideTests"/>/<see cref="CliHelpTests"/>'s own remarks), so this drives the built
/// swipewalk.dll instead, like a person would from a terminal.
/// </summary>
public class GuidedCheckCliTests
{
    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Swipewalk.slnx")))
                return dir.FullName;
        throw new InvalidOperationException("Could not find Swipewalk.slnx above AppContext.BaseDirectory.");
    }

    private static string CliDll()
    {
        var path = Path.Combine(RepoRoot(), "src", "Swipewalk.Cli", "bin", "Debug", "net10.0", "swipewalk.dll");
        if (!File.Exists(path))
            throw new InvalidOperationException($"swipewalk.dll not found at {path}; build the solution first.");
        return path;
    }

    /// <summary>Writes a minimal one-screen fixture run (run.json + results.json) under a fresh temp history
    /// root, and returns (historyRoot, runId). <paramref name="findings"/> lets a test plant an automated
    /// finding to check the contradiction path.</summary>
    private static (string HistoryRoot, string RunId) WriteFixtureRun(IReadOnlyList<Finding>? findings = null)
    {
        var historyRoot = Directory.CreateTempSubdirectory("swipewalk-guide-test-").FullName;
        var runFolder = Path.Combine(historyRoot, "run1");
        Directory.CreateDirectory(runFolder);

        var report = new ScanReport
        {
            ToolVersion = "test",
            Screens =
            [
                new ScreenResult
                {
                    Platform = Platform.Android,
                    ScreenName = "Home",
                    Findings = findings ?? [],
                },
            ],
        };
        File.WriteAllText(Path.Combine(runFolder, "results.json"), JsonReport.Serialize(report));

        var record = new RunRecord
        {
            Id = "run1",
            App = "org.example.app",
            AppKey = "org.example.app",
            Platform = "Android",
            Mode = "scan",
            StartedAt = DateTimeOffset.Now,
            FinishedAt = DateTimeOffset.Now,
            Counts = new RunCounts(findings?.Count ?? 0, 0, 0, 1),
            ToolVersion = "test",
            RulesetVersion = "test",
        };
        File.WriteAllText(Path.Combine(runFolder, "run.json"),
            JsonSerializer.Serialize(record, new JsonSerializerOptions(JsonReport.Options) { WriteIndented = true }));

        return (historyRoot, "run1");
    }

    private static (int ExitCode, string StdOut) RunGuide(string historyRoot, string runId, string input, params string[] extraArgs)
    {
        var psi = new ProcessStartInfo("dotnet", $"\"{CliDll()}\" guide {runId} --history \"{historyRoot}\" {string.Join(' ', extraArgs)}")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // Only this test suite sets this: it must redirect stdin to feed scripted answers, which is otherwise
        // indistinguishable from a real non-interactive run piping input by accident (see GuidedCheckCli.RunAsync).
        psi.Environment["SWIPEWALK_GUIDE_TEST_ALLOW_REDIRECTED_INPUT"] = "1";
        using var process = Process.Start(psi)!;
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(30_000);
        return (process.ExitCode, stdout + stderr);
    }

    private static GuidedAnswerStore LoadAnswers(string historyRoot, string runId) =>
        GuidedAnswerStore.Load(Path.Combine(historyRoot, runId)) ?? GuidedAnswerStore.Empty;

    [Fact]
    public void EmptyEvidencePass_IsRePromptedUntilNonEmpty_ThenSaved()
    {
        var (historyRoot, runId) = WriteFixtureRun();
        // AT choice, then: pass, empty evidence (Enter), then real evidence, confirm the fast-pass prompt
        // (an automated script always answers "fast"), no note, default tester.
        var input = "N\np\n\nstructure looks fine\ny\n\n\n";

        var (exitCode, output) = RunGuide(historyRoot, runId, input, "--criterion", "1.3.1");

        Assert.Equal(0, exitCode);
        // The prompt appears twice: once for the empty attempt, once for the accepted one -- proof it re-asked.
        Assert.Equal(2, CountOccurrences(output, "Evidence (required for pass"));
        var answers = LoadAnswers(historyRoot, runId);
        var answer = Assert.Single(answers.Answers);
        Assert.Equal("1.3.1", answer.CriterionNumber);
        Assert.Equal(Core.Coverage.GuidedAnswerResult.Pass, answer.Result);
        var evidence = Assert.Single(answer.Evidence);
        Assert.Equal("structure looks fine", evidence.Description);
        Assert.Equal("me", answer.Tester);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    [Fact]
    public void PassContradictingAWcagFinding_PrintsContradictionAndSavesBoth()
    {
        var finding = new Finding
        {
            RuleId = "missing-name",
            Kind = FindingKind.WcagIssue,
            Message = "test finding",
            NodePath = "0",
            Role = "image",
            Criteria = [WcagCriteria.NonTextContent],
        };
        var (historyRoot, runId) = WriteFixtureRun([finding]);
        var input = "N\np\nimages look fine\ny\n\ntester1\n";

        var (exitCode, output) = RunGuide(historyRoot, runId, input, "--criterion", "1.1.1");

        Assert.Equal(0, exitCode);
        Assert.Contains("The tester recorded a pass for 1.1.1", output);
        Assert.Contains("Check both.", output);
        var answers = LoadAnswers(historyRoot, runId);
        var answer = Assert.Single(answers.Answers);
        Assert.Equal("tester1", answer.Tester);
    }

    [Fact]
    public void SkipForNow_RecordsNothing()
    {
        var (historyRoot, runId) = WriteFixtureRun();
        var input = "N\ns\n";

        var (exitCode, _) = RunGuide(historyRoot, runId, input, "--criterion", "1.3.1");

        Assert.Equal(0, exitCode);
        var answers = LoadAnswers(historyRoot, runId);
        Assert.Empty(answers.Answers);
    }

    [Fact]
    public void ConfirmNotApplicable_RequiresAReason()
    {
        var (historyRoot, runId) = WriteFixtureRun();
        // Choose "confirm not applicable", press Enter on an empty reason (re-prompted), then give a reason.
        var input = "N\nc\n\nno headings or groups on this screen\n\n\n";

        var (exitCode, _) = RunGuide(historyRoot, runId, input, "--criterion", "1.3.1");

        Assert.Equal(0, exitCode);
        var answer = Assert.Single(LoadAnswers(historyRoot, runId).Answers);
        Assert.Equal(Core.Coverage.GuidedAnswerResult.ConfirmedNotApplicable, answer.Result);
        Assert.Equal("no headings or groups on this screen", answer.NotApplicableReason);
    }
}
