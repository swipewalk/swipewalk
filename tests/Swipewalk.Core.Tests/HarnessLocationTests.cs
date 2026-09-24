using Swipewalk.Collectors.Ios;

namespace Swipewalk.Core.Tests;

public class HarnessLocationTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("cf-harness-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static void CreateHarness(string dir)
    {
        Directory.CreateDirectory(Path.Combine(dir, "SwipewalkHarness.xcodeproj"));
        File.WriteAllText(Path.Combine(dir, "SwipewalkHarness.xcodeproj", "project.pbxproj"), "project");
        Directory.CreateDirectory(Path.Combine(dir, "HarnessUITests"));
        File.WriteAllText(Path.Combine(dir, "HarnessUITests", "ScanTests.swift"), "tests");
    }

    [Fact]
    public void Checkout_UsesTheHarnessInPlace()
    {
        CreateHarness(Path.Combine(_root, "repo", "harness", "ios"));
        var subdir = Directory.CreateDirectory(Path.Combine(_root, "repo", "src")).FullName;

        var found = IosCollector.FindHarness(subdir, Path.Combine(_root, "elsewhere"), Path.Combine(_root, "copy"));

        Assert.Equal(Path.Combine(_root, "repo", "harness", "ios", "SwipewalkHarness.xcodeproj"), found);
    }

    [Fact]
    public void DotnetTool_BuildsACopyOfTheHarnessNextToTheAssemblies()
    {
        var tool = Path.Combine(_root, "tool");
        CreateHarness(Path.Combine(tool, "harness", "ios"));

        var found = IosCollector.FindHarness(_root, tool, Path.Combine(_root, "copy"));

        Assert.Equal(Path.Combine(_root, "copy", "SwipewalkHarness.xcodeproj"), found);
        Assert.Equal("tests", File.ReadAllText(Path.Combine(_root, "copy", "HarnessUITests", "ScanTests.swift")));
    }

    [Fact]
    public void DesktopApp_BuildsACopyOfTheHarnessInTheBundleResources()
    {
        var contents = Path.Combine(_root, "Swipewalk.app", "Contents");
        CreateHarness(Path.Combine(contents, "Resources", "harness", "ios"));

        var found = IosCollector.FindHarness("/", Path.Combine(contents, "MonoBundle") + "/", Path.Combine(_root, "copy"));

        Assert.Equal(Path.Combine(_root, "copy", "SwipewalkHarness.xcodeproj"), found);
    }

    [Fact]
    public void NoHarness_ReturnsNull()
    {
        Assert.Null(IosCollector.FindHarness(_root, _root, Path.Combine(_root, "copy")));
    }

    [Fact]
    public void Sync_LeavesUnchangedFilesAlone()
    {
        var source = Path.Combine(_root, "source");
        var target = Path.Combine(_root, "target");
        CreateHarness(source);
        IosCollector.SyncDirectory(source, target);
        var unchanged = Path.Combine(target, "SwipewalkHarness.xcodeproj", "project.pbxproj");
        var written = File.GetLastWriteTimeUtc(unchanged);
        File.WriteAllText(Path.Combine(source, "HarnessUITests", "ScanTests.swift"), "tests v2");

        IosCollector.SyncDirectory(source, target);

        Assert.Equal(written, File.GetLastWriteTimeUtc(unchanged));
        Assert.Equal("tests v2", File.ReadAllText(Path.Combine(target, "HarnessUITests", "ScanTests.swift")));
    }
}
