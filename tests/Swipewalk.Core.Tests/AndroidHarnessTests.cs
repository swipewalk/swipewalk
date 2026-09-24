using Swipewalk.Collectors.Android;

namespace Swipewalk.Core.Tests;

public class AndroidHarnessTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("cf-android-harness-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static void CreateHarness(string harnessDir) =>
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(harnessDir).FullName, "gradlew"), "#!/bin/sh\n");

    [Fact]
    public void Checkout_FindsHarnessInPlace()
    {
        CreateHarness(Path.Combine(_root, "repo", "harness", "android"));
        var subdir = Directory.CreateDirectory(Path.Combine(_root, "repo", "src")).FullName;

        var found = AndroidHarness.FindHarnessDir(subdir, Path.Combine(_root, "elsewhere"));

        Assert.Equal(Path.Combine(_root, "repo", "harness", "android"), found);
    }

    [Fact]
    public void BundledCopy_FoundNextToAssemblies()
    {
        var tool = Path.Combine(_root, "tool");
        CreateHarness(Path.Combine(tool, "harness", "android"));

        var found = AndroidHarness.FindHarnessDir(_root, tool);

        Assert.Equal(Path.Combine(tool, "harness", "android"), found);
    }

    [Fact]
    public void NoHarness_ReturnsNull() =>
        Assert.Null(AndroidHarness.FindHarnessDir(_root, _root));

    private static void CreateBundledApk(string dir) =>
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(dir).FullName, AndroidHarness.BundledApkFileName), "apk");

    [Fact]
    public void FindBundledApk_DotnetTool_FoundNextToAssemblies()
    {
        // Matches Swipewalk.Collectors.csproj's Link="harness/android/..." (see the nupkg layout:
        // tools/net10.0/any/harness/android/harness-debug-androidTest.apk).
        var tool = Path.Combine(_root, "tool");
        CreateBundledApk(Path.Combine(tool, "harness", "android"));

        var found = AndroidHarness.FindBundledApk(tool);

        Assert.Equal(Path.Combine(tool, "harness", "android", AndroidHarness.BundledApkFileName), found);
    }

    [Fact]
    public void FindBundledApk_DesktopApp_FoundInTheBundleResources()
    {
        // Matches Swipewalk.Desktop's csproj (BundleResource): Contents/Resources/harness/android/...,
        // with AppContext.BaseDirectory at Contents/MonoBundle -- the same "../Resources" pattern
        // IosCollector.FindHarness uses for harness/ios.
        var contents = Path.Combine(_root, "Swipewalk.app", "Contents");
        CreateBundledApk(Path.Combine(contents, "Resources", "harness", "android"));

        var found = AndroidHarness.FindBundledApk(Path.Combine(contents, "MonoBundle") + Path.DirectorySeparatorChar);

        Assert.Equal(Path.Combine(contents, "Resources", "harness", "android", AndroidHarness.BundledApkFileName), found);
    }

    [Fact]
    public void FindBundledApk_NoneShipped_ReturnsNull() =>
        Assert.Null(AndroidHarness.FindBundledApk(_root));

    [Fact]
    public void CurrentToolVersion_MatchesTheAssemblysOwnVersion() =>
        // Not a fixed literal: whatever Directory.Build.props's <Version> resolves to for this build,
        // since that's exactly what this is meant to track (see EnsureInstalledAsync's doc comment).
        Assert.Equal(typeof(AndroidHarness).Assembly.GetName().Version?.ToString(), AndroidHarness.CurrentToolVersion);

    [Fact]
    public void Parse_ReadsNodesAndConvertsAtfIssueBoundsToDp()
    {
        var json = """
            {
              "nodes": [
                { "key": "a|[0,0][10,10]|||", "isShowingHintText": true, "isImportantForAccessibility": true },
                { "key": "b|[0,0][10,10]|||", "isHeading": true, "paneTitle": "Home" }
              ],
              "atfIssues": [
                { "checkName": "TextContrastCheck", "resultType": "WARNING", "message": "low contrast",
                  "bounds": "[160,320][480,480]", "text": "Pay" }
              ]
            }
            """;

        var result = AndroidHarness.Parse(json, density: 160);

        Assert.True(result.Nodes["a|[0,0][10,10]|||"].IsShowingHintText);
        Assert.True(result.Nodes["a|[0,0][10,10]|||"].IsImportantForAccessibility);
        Assert.True(result.Nodes["b|[0,0][10,10]|||"].IsHeading);
        Assert.Equal("Home", result.Nodes["b|[0,0][10,10]|||"].PaneTitle);

        var issue = Assert.Single(result.AtfIssues);
        Assert.Equal("TextContrastCheck", issue.CheckName);
        Assert.Equal("Pay", issue.Label);
        // density 160 => scale 1.0, so bounds convert 1:1 from px to dp.
        Assert.Equal(160, issue.Bounds.X);
        Assert.Equal(320, issue.Bounds.Y);
        Assert.Equal(320, issue.Bounds.Width);
        Assert.Equal(160, issue.Bounds.Height);
    }

    [Fact]
    public void Parse_ScalesBoundsByDensity()
    {
        var json = """{"atfIssues": [{ "checkName": "TouchTargetSizeCheck", "resultType": "ERROR", "message": "small", "bounds": "[0,0][210,210]" }]}""";

        var result = AndroidHarness.Parse(json, density: 420); // scale = 2.625

        var issue = Assert.Single(result.AtfIssues);
        Assert.Equal(80, issue.Bounds.Width, precision: 3);
        Assert.Equal(80, issue.Bounds.Height, precision: 3);
    }

    [Fact]
    public void Parse_SkipsNonErrorWarningNotRepresented_EmptyListsWhenSectionsMissing()
    {
        var result = AndroidHarness.Parse("{}", density: 160);

        Assert.Empty(result.Nodes);
        Assert.Empty(result.AtfIssues);
        Assert.Null(result.SkipReason);
    }

    [Fact]
    public void SummarizeFailure_ExtractsTheJUnitErrorHeaderAndFollowingLine()
    {
        var output = """
            org.swipewalk.harness.HarnessTest:.
            Error in capture(org.swipewalk.harness.HarnessTest):
            java.lang.IllegalArgumentException: Pass -e package <app package under test>.
            	at org.swipewalk.harness.HarnessTest.capture(HarnessTest.kt:30)

            Time: 0.12
            """;

        var summary = AndroidHarness.SummarizeFailure(output);

        Assert.Contains("Error in capture", summary);
        Assert.Contains("IllegalArgumentException", summary);
    }

    [Fact]
    public void SummarizeFailure_FallsBackToWholeOutput_WhenNoJUnitHeaderFound()
    {
        var summary = AndroidHarness.SummarizeFailure("INSTRUMENTATION_STATUS_CODE: -1\nshell died\n");

        Assert.Contains("shell died", summary);
    }

    [Fact]
    public void ParseScreenReaderResult_ReadsItemsCompleteAndToolVersion()
    {
        var json = """
            {
              "talkBackVersion": "17.0.1.926549743",
              "complete": true,
              "items": [
                { "order": 1, "spokenText": "Ticket number. Edit box", "key": "android.widget.EditText|[55,934][1025,1055]||Ticket number|Ticket number" },
                { "order": 2, "spokenText": "Button", "key": "android.widget.ImageView|[893,323][1025,455]|||" }
              ]
            }
            """;

        var result = AndroidHarness.ParseScreenReaderResult(json);

        Assert.Equal("17.0.1.926549743", result.ToolVersion);
        Assert.True(result.Complete);
        Assert.Null(result.NotCompleteReason);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Ticket number. Edit box", result.Items[0].SpokenText);
        Assert.Equal(1, result.Items[0].Order);
        Assert.Equal("android.widget.ImageView|[893,323][1025,455]|||", result.Items[1].Key);
    }

    [Fact]
    public void ParseScreenReaderResult_IncompleteWithReason_EmptyItems()
    {
        var json = """{"complete": false, "notCompleteReason": "screen has more elements (45) than the capture limit (40)", "items": []}""";

        var result = AndroidHarness.ParseScreenReaderResult(json);

        Assert.False(result.Complete);
        Assert.Equal("screen has more elements (45) than the capture limit (40)", result.NotCompleteReason);
        Assert.Empty(result.Items);
        Assert.Null(result.ToolVersion);
    }

    [Fact]
    public void ParseScreenReaderResult_NoItemsSection_EmptyList()
    {
        var result = AndroidHarness.ParseScreenReaderResult("{}");

        Assert.Empty(result.Items);
        Assert.False(result.Complete);
        Assert.Null(result.SkipReason);
    }
}
