using Swipewalk.Collectors.Android;
using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;

namespace Swipewalk.Core.Tests;

public class AndroidCollectorTests
{
    [Fact]
    public void ParseDensity_WithOverride_PrefersOverrideDensity()
    {
        const string output = "Physical density: 420\nOverride density: 320\n";

        Assert.Equal(320, AndroidCollector.ParseDensity(output));
    }

    [Fact]
    public void ParseDensity_WithoutOverride_UsesPhysicalDensity()
    {
        const string output = "Physical density: 420\n";

        Assert.Equal(420, AndroidCollector.ParseDensity(output));
    }

    [Fact]
    public void ParseDensity_UnexpectedOutput_Throws()
    {
        Assert.Throws<FormatException>(() => AndroidCollector.ParseDensity("nonsense"));
    }

    [Theory]
    [InlineData("mWakefulness=Awake", "isKeyguardShowing=false", true)]
    [InlineData("mWakefulness=Dozing", "isKeyguardShowing=true", false)]
    [InlineData("mWakefulness=Awake", "mShowingDream=false mDreamingLockscreen=true isKeyguardShowing=true", false)]
    public void IsAwakeAndUnlocked_DetectsSleepAndLockScreen(string power, string window, bool expected)
    {
        Assert.Equal(expected, AndroidCollector.IsAwakeAndUnlocked(power, window));
    }

    [Theory]
    [InlineData("        InsetsSource type=ITYPE_STATUS_BAR frame=[0,0][1080,136] visible=true", 136)]
    [InlineData("        InsetsSource id=f0be0000 type=statusBars frame=[0,0][1080,142] visible=true flags=", 142)]
    public void StatusBarFrame_ParsesAndroid13And16(string dump, int bottom)
    {
        Assert.Equal((0, bottom), AndroidCollector.StatusBarFrame(dump));
    }

    [Fact]
    public void ForegroundPackage_ReadsFocusedWindow()
    {
        const string dump = "  mCurrentFocus=Window{46f66e5 u0 org.swipewalk.buggyapp/crc64d21699e466916214.MainActivity}";

        Assert.Equal("org.swipewalk.buggyapp", AndroidCollector.ForegroundPackage(dump));
    }

    private static readonly string BuggyAppCapture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "BuggyApp.Android");

    [Fact]
    public void Load_WithoutPackage_SetsAppIdFromTheCapture()
    {
        // No --package given: AndroidCollector.Load must still tell run history which app this is (see
        // UiAutomatorParser.GuessPackage), so a scan run this way groups with one that did pass --package.
        var snapshot = AndroidCollector.Load(BuggyAppCapture, "Pay a parking ticket");

        Assert.Equal("org.swipewalk.buggyapp", snapshot.AppId);
    }

    [Fact]
    public void Load_WithPackage_SetsAppIdToTheGivenPackage()
    {
        var snapshot = AndroidCollector.Load(BuggyAppCapture, "Pay a parking ticket", "org.swipewalk.buggyapp");

        Assert.Equal("org.swipewalk.buggyapp", snapshot.AppId);
    }

    [Fact]
    public void Load_PackageFile_WinsOverGuessingFromTheDump()
    {
        // package.txt (written by CaptureAsync from dumpsys window -- ground truth for whichever app was
        // actually in front) must be preferred over UiAutomatorParser.GuessPackage's node-count guess, which
        // a system overlay dumped alongside the app could otherwise win by having more clickable elements.
        var dir = Path.Combine(Path.GetTempPath(), $"cf-android-load-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, AndroidCollector.DensityFile), "160");
            File.WriteAllText(Path.Combine(dir, AndroidCollector.PackageFile), "com.example.app");
            File.WriteAllText(Path.Combine(dir, AndroidCollector.FullDumpFile), """
                <hierarchy rotation="0">
                  <node index="0" text="" resource-id="" class="android.widget.FrameLayout" package="com.overlay.app" clickable="true" bounds="[0,0][100,100]" />
                  <node index="1" text="" resource-id="" class="android.widget.FrameLayout" package="com.overlay.app" clickable="true" bounds="[0,100][100,200]" />
                  <node index="2" text="" resource-id="" class="android.widget.FrameLayout" package="com.example.app" clickable="false" bounds="[0,200][100,300]" />
                </hierarchy>
                """);

            var snapshot = AndroidCollector.Load(dir, "Screen 1");

            Assert.Equal("com.example.app", snapshot.AppId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_WithoutPackageFile_FallsBackToGuessingFromTheDump()
    {
        // A capture made before PackageFile existed (or a hand-built fixture, like BuggyApp.Android above)
        // has no such sidecar; Load must still work, falling back to the dump-based guess.
        var dir = Path.Combine(Path.GetTempPath(), $"cf-android-load-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, AndroidCollector.DensityFile), "160");
            File.WriteAllText(Path.Combine(dir, AndroidCollector.FullDumpFile), """
                <hierarchy rotation="0">
                  <node index="0" text="Hi" resource-id="" class="android.widget.TextView" package="com.example.app" clickable="false" bounds="[0,0][100,100]" />
                </hierarchy>
                """);

            var snapshot = AndroidCollector.Load(dir, "Screen 1");

            Assert.Equal("com.example.app", snapshot.AppId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string SetUpScreenReaderCaptureDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cf-android-load-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, AndroidCollector.DensityFile), "160");
        File.WriteAllText(Path.Combine(dir, AndroidCollector.FullDumpFile), """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.widget.FrameLayout" package="com.example.app" content-desc="" bounds="[0,0][100,300]">
                <node index="0" text="Pay" resource-id="" class="android.widget.Button" package="com.example.app" content-desc="Submit" bounds="[0,0][100,100]" />
              </node>
            </hierarchy>
            """);
        return dir;
    }

    [Fact]
    public void Load_NoScreenReaderSidecar_ScreenReaderCaptureStaysNull()
    {
        // --screen-reader wasn't passed for this capture: neither sidecar file exists, so
        // ScreenSnapshot.ScreenReaderCapture must stay null (see its own remarks on why that's different
        // from a capture that ran and found nothing).
        var dir = SetUpScreenReaderCaptureDir();
        try
        {
            Assert.Null(AndroidCollector.Load(dir, "Screen 1", "com.example.app").ScreenReaderCapture);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_ScreenReaderResultFile_MatchesItemsToNodePaths()
    {
        var dir = SetUpScreenReaderCaptureDir();
        try
        {
            // Matches AndroidCollector.CaptureAsync's own JsonSerializer.Serialize(AndroidHarness.ScreenReaderHarnessResult)
            // -- the already-parsed C# record, PascalCase, not the raw device JSON (see AndroidHarness.ParseScreenReaderResult
            // for that; the harness's raw JSON never touches disk on the C# side).
            File.WriteAllText(Path.Combine(dir, AndroidCollector.ScreenReaderResultFile), """
                {"ToolVersion":"17.0.1","Complete":true,"NotCompleteReason":null,"SkipReason":null,"Items":[
                    {"Order":1,"SpokenText":"Submit. Button","Key":"android.widget.Button|[0,0][100,100]||Pay|Submit"}
                ]}
                """);

            var capture = AndroidCollector.Load(dir, "Screen 1", "com.example.app").ScreenReaderCapture;

            Assert.NotNull(capture);
            Assert.True(capture.Complete);
            // Scope is a constant fact about the TalkBack route (see ScreenReaderCaptureScope's own
            // remarks), always set here regardless of what the harness JSON says -- it never carries this
            // through its own contract.
            Assert.Equal(ScreenReaderCaptureScope.FocusableElementsOnly, capture.Scope);
            var item = Assert.Single(capture.Items);
            Assert.Equal("Submit. Button", item.SpokenText);
            Assert.Equal("0/0", item.MatchedNodePath);
            Assert.Equal(MatchConfidence.Exact, item.MatchConfidence);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_CompleteTalkBackCapture_LetsScreenReaderLabelInNameRuleFire()
    {
        // Regression for the real bug this fix addresses: TalkBackCollector.kt used to pass complete = false
        // on EVERY return path, so ScreenReaderLabelInNameRule's own gate (Items.Count > 0, Complete: true)
        // could never be satisfied by a capture built through the real collector path -- only by a hand-built
        // ScreenReaderCapture in a unit test (see ScreenReaderLabelInNameRuleTests.cs, which never exercised
        // AndroidCollector.Load and so never caught this). This test goes through that real path end to end:
        // a button with no tree-level content-desc (so LabelInNameRule, the tree-only check, never runs on
        // it and can't have already reported this), visible text "Pay", and a real TalkBack utterance that
        // doesn't contain it ("Submit. Button") -- the exact shape of samples/BuggyApp's planted bug B6,
        // though B6 itself also has a tree-level Label and so is skipped here by design (see this rule's own
        // remarks on not duplicating LabelInNameRule) -- this fixture omits that Label specifically so the
        // real-capture check is the only thing that can catch the mismatch.
        var dir = Path.Combine(Path.GetTempPath(), $"cf-android-load-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, AndroidCollector.DensityFile), "160");
            File.WriteAllText(Path.Combine(dir, AndroidCollector.FullDumpFile), """
                <hierarchy rotation="0">
                  <node index="0" text="" resource-id="" class="android.widget.FrameLayout" package="com.example.app" content-desc="" bounds="[0,0][100,300]">
                    <node index="0" text="Pay" resource-id="" class="android.widget.Button" package="com.example.app" content-desc="" clickable="true" focusable="true" bounds="[0,0][100,100]" />
                  </node>
                </hierarchy>
                """);
            File.WriteAllText(Path.Combine(dir, AndroidCollector.ScreenReaderResultFile), """
                {"ToolVersion":"17.0.1","Complete":true,"NotCompleteReason":null,"Items":[
                    {"Order":1,"SpokenText":"Submit. Button","Key":"android.widget.Button|[0,0][100,100]||Pay|"}
                ]}
                """);

            var snapshot = AndroidCollector.Load(dir, "Screen 1", "com.example.app");
            Assert.Equal(ScreenReaderCaptureScope.FocusableElementsOnly, snapshot.ScreenReaderCapture!.Scope);

            var findings = new ScreenReaderLabelInNameRule().Evaluate(snapshot).ToList();

            var finding = Assert.Single(findings);
            Assert.Equal("screen-reader-label-in-name", finding.RuleId);
            Assert.Contains("Pay", finding.Message);
            Assert.Contains("Submit. Button", finding.Message);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_ScreenReaderResultFile_UnmatchedKey_KeptWithNoConfidenceMatch()
    {
        var dir = SetUpScreenReaderCaptureDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, AndroidCollector.ScreenReaderResultFile), """
                {"Complete":true,"Items":[{"Order":1,"SpokenText":"Something else","Key":"no.such.Class|[9,9][9,9]||none|"}]}
                """);

            var capture = AndroidCollector.Load(dir, "Screen 1", "com.example.app").ScreenReaderCapture!;

            var item = Assert.Single(capture.Items);
            Assert.Null(item.MatchedNodePath);
            Assert.Equal(MatchConfidence.None, item.MatchConfidence);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_ScreenReaderSkipReasonFile_BuildsIncompleteCaptureWithTheReason()
    {
        var dir = SetUpScreenReaderCaptureDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, AndroidCollector.ScreenReaderSkipReasonFile),
                "TalkBack (Android Accessibility Suite) is not installed on this device");

            var capture = AndroidCollector.Load(dir, "Screen 1", "com.example.app").ScreenReaderCapture;

            Assert.NotNull(capture);
            Assert.False(capture.Complete);
            Assert.Equal(ScreenReaderCaptureScope.FocusableElementsOnly, capture.Scope);
            Assert.Equal("TalkBack (Android Accessibility Suite) is not installed on this device", capture.NotCompleteReason);
            Assert.Empty(capture.Items);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
