using Swipewalk.Engine;

namespace Swipewalk.Core.Tests;

public class RunConfigTests
{
    private static RunConfig Load(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cf-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        try
        {
            return RunConfig.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SampleConfig_LoadsWithCommentsAndTrailingCommas()
    {
        var sample = Path.Combine(AppContext.BaseDirectory, "Fixtures", "swipewalk.json");
        var config = RunConfig.Load(sample);

        Assert.Equal(2, config.Targets.Count);
        Assert.Equal("ada-title-ii", config.Standard);
        var ios = config.ToOptions(config.Targets[1], "out");
        Assert.Equal(TargetPlatform.Ios, ios.Platform);
        Assert.Null(ios.Device); // "booted" means the booted Simulator
        Assert.Equal("org.swipewalk.buggyapp", ios.BundleId);
    }

    [Fact]
    public void InstallPath_IsRelativeToTheConfigFile()
    {
        var config = Load("""{ "app": { "android": { "install": "builds/app.apk" } }, "targets": [ { "platform": "android" } ] }""");

        Assert.True(Path.IsPathRooted(config.App.Android!.Install));
        Assert.EndsWith(Path.Combine("builds", "app.apk"), config.App.Android.Install);
    }

    /// <summary>
    /// swipewalk.json's largeTextRestart, left unset, must not silently change behaviour for a "run" using
    /// mode "scan" from before ScanService started respecting the resolved policy: a "run" is unattended by
    /// default, so record keeps defaulting to Never
    /// (never blocks), but scan defaults to Always (keeps the automatic restart it always did).
    /// </summary>
    [Theory]
    [InlineData(null, "record", LargeTextRestartPolicy.Never)]
    [InlineData(null, "scan", LargeTextRestartPolicy.Always)]
    [InlineData("ask", "scan", LargeTextRestartPolicy.Ask)]
    [InlineData("never", "record", LargeTextRestartPolicy.Never)]
    public void LargeTextRestart_DefaultsDependOnMode_ExplicitValueAlwaysWins(string? largeTextRestart, string mode, LargeTextRestartPolicy expected)
    {
        var restartField = largeTextRestart is null ? "" : $", \"largeTextRestart\": \"{largeTextRestart}\"";
        var json = "{ \"app\": { \"android\": {} }, \"targets\": [ { \"platform\": \"android\" } ], \"mode\": \"" + mode + "\"" + restartField + " }";
        var config = Load(json);

        var options = config.ToOptions(config.Targets[0], "out");

        Assert.Equal(expected, options.LargeTextRestartPolicy);
    }

    [Theory]
    [InlineData("""{ "targets": [] }""", "at least one")]
    [InlineData("""{ "app": { "ios": {} }, "targets": [ { "platform": "ios" } ] }""", "app.ios")]
    [InlineData("""{ "app": { "android": {} }, "targets": [ { "platform": "android" } ], "mode": "auto" }""", "mode")]
    [InlineData("""{ "app": { "android": {} }, "targets": [ { "platform": "android" } ], "standard": "ada" }""", "Unknown standard")]
    [InlineData("""{ "app": { "android": {} }, "targets": [ { "platform": "android" } ], "typo": 1 }""", "typo")]
    public void InvalidConfig_ExplainsTheProblem(string json, string expected)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Load(json));
        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void ExitCode_FollowsFailOn()
    {
        var target = new TargetConfig { Platform = "android" };
        RunRecord Run(int issues) => new()
        {
            Id = "1", App = "a", Platform = "Android", Mode = "run", StartedAt = default, FinishedAt = default,
            Counts = new RunCounts(issues, 0, 0, 1), ToolVersion = "1", RulesetVersion = "1",
        };
        var failing = new RunConfig { FailOn = "wcag-issues", Targets = [target] };

        Assert.Equal(3, Runner.ExitCode(failing, [new TargetOutcome(target, Run(2), null)]));
        Assert.Equal(0, Runner.ExitCode(failing, [new TargetOutcome(target, Run(0), null)]));
        Assert.Equal(0, Runner.ExitCode(failing with { FailOn = "never" }, [new TargetOutcome(target, Run(2), null)]));
        Assert.Equal(2, Runner.ExitCode(failing, [new TargetOutcome(target, null, "device missing")]));
    }

    [Fact]
    public async Task History_SavesAndListsRuns()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cf-history-{Guid.NewGuid():N}");
        var history = new RunHistory(root);
        var folder = history.NewRunFolder("org.example.app");
        File.WriteAllText(Path.Combine(folder, "report.html"), "<html></html>");
        var report = new Core.Reports.ScanReport { ToolVersion = "1.0.0", Screens = [] };
        var options = new ScanOptions { Platform = TargetPlatform.Android, Package = "org.example.app", OutputDirectory = folder };

        await history.SaveAsync(new RunResult(report, Path.Combine(folder, "report.html"), Path.Combine(folder, "results.json")), options, "scan", DateTimeOffset.Now);

        var run = Assert.Single(history.List());
        Assert.Equal("org.example.app", run.App);
        Assert.Equal(folder, run.Folder);
        Directory.Delete(root, recursive: true);
    }
}
