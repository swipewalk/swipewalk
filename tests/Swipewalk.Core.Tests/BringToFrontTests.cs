using Swipewalk.Collectors;
using Swipewalk.Collectors.Android;
using Swipewalk.Collectors.Ios;

namespace Swipewalk.Core.Tests;

/// <summary>
/// <see cref="BringToFront.Decide"/>: what Swipewalk should do with the app under test before a capture, from
/// its <see cref="AppForegroundState"/>. The one rule above all: never silently change what's on screen -- an
/// already-foreground app is left alone; a backgrounded app is brought forward; a not-running app is started,
/// but the caller must be told (the caller, not this pure decision, is what carries <see cref="BringToFront.LaunchedNote"/>);
/// an app that isn't installed can't be helped automatically.
/// </summary>
public class BringToFrontTests
{
    [Fact]
    public void InFront_DoesNothing()
    {
        Assert.Equal(BringToFront.Action.None, BringToFront.Decide(AppForegroundState.InFront));
    }

    [Fact]
    public void Background_IsBroughtForward()
    {
        Assert.Equal(BringToFront.Action.Activate, BringToFront.Decide(AppForegroundState.Background));
    }

    [Fact]
    public void NotRunning_IsLaunched()
    {
        Assert.Equal(BringToFront.Action.Launch, BringToFront.Decide(AppForegroundState.NotRunning));
    }

    [Fact]
    public void NotInstalled_Fails()
    {
        Assert.Equal(BringToFront.Action.Fail, BringToFront.Decide(AppForegroundState.NotInstalled));
    }
}

/// <summary>
/// <see cref="AndroidCollector.DecideForegroundState"/>: the platform-specific state read behind
/// <see cref="BringToFront"/>, from three cheap adb reads (the focused window, <c>pm list packages</c>,
/// <c>pidof</c>) given directly as strings rather than a real device -- the same "fake device layer" style as
/// <see cref="AndroidCollectorTests.IsAwakeAndUnlocked_DetectsSleepAndLockScreen"/> and
/// <see cref="AndroidCollectorTests.ForegroundPackage_ReadsFocusedWindow"/>.
/// </summary>
public class AndroidCollectorForegroundStateTests
{
    private const string Package = "org.swipewalk.buggyapp";
    private const string FocusedOnPackage = "  mCurrentFocus=Window{46f66e5 u0 org.swipewalk.buggyapp/crc64d21699e466916214.MainActivity}";
    private const string FocusedOnOtherApp = "  mCurrentFocus=Window{46f66e5 u0 com.android.launcher3/com.android.launcher3.Launcher}";

    [Fact]
    public void FocusedOnTheApp_IsInFront()
    {
        var state = AndroidCollector.DecideForegroundState(FocusedOnPackage, Package, installed: true, pidofOutput: "12345");

        Assert.Equal(AppForegroundState.InFront, state);
    }

    [Fact]
    public void NotFocusedButProcessRunning_IsBackground()
    {
        // pidof returns a pid: the app is still running, just not the focused window.
        var state = AndroidCollector.DecideForegroundState(FocusedOnOtherApp, Package, installed: true, pidofOutput: "12345\n");

        Assert.Equal(AppForegroundState.Background, state);
    }

    [Fact]
    public void NotFocusedAndNoProcess_IsNotRunning()
    {
        // pidof prints nothing when no process matches.
        var state = AndroidCollector.DecideForegroundState(FocusedOnOtherApp, Package, installed: true, pidofOutput: "");

        Assert.Equal(AppForegroundState.NotRunning, state);
    }

    [Fact]
    public void NotInstalled_IsNotInstalledRegardlessOfFocusOrProcess()
    {
        // Checked first: an uninstalled app can't have a real pid or focus, but even a stale/misleading read
        // must not be reported as "not running" (which would tell the caller it can safely be started).
        var state = AndroidCollector.DecideForegroundState(FocusedOnOtherApp, Package, installed: false, pidofOutput: "");

        Assert.Equal(AppForegroundState.NotInstalled, state);
    }
}

/// <summary>
/// <see cref="IosCollector.ReadForegroundFlags"/>: reads the "launched"/"broughtForward" keys the harness's
/// <c>app(_:)</c> writes into tree.json (see ScanTests.swift), the iOS side of the same decision covered by
/// <see cref="AndroidCollectorForegroundStateTests"/> above (the harness itself decides which of activate() or
/// launch() to use, since only XCUITest can read <c>XCUIApplication.state</c>).
/// </summary>
public class IosCollectorForegroundFlagsTests
{
    private static string WriteTreeJson(string json)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cf-ios-foreground-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, IosCollector.TreeFile), json);
        return dir;
    }

    [Fact]
    public void BothFlagsTrue_ReadsLaunchedAndBroughtForward()
    {
        var dir = WriteTreeJson("""{"scale":1,"tree":{},"auditIssues":[],"launched":true,"broughtForward":false}""");
        try
        {
            var (launched, broughtForward) = IosCollector.ReadForegroundFlags(dir);
            Assert.True(launched);
            Assert.False(broughtForward);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void BroughtForwardOnly_NotLaunched()
    {
        var dir = WriteTreeJson("""{"scale":1,"tree":{},"auditIssues":[],"launched":false,"broughtForward":true}""");
        try
        {
            var (launched, broughtForward) = IosCollector.ReadForegroundFlags(dir);
            Assert.False(launched);
            Assert.True(broughtForward);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void MissingKeys_DefaultToFalse()
    {
        // A capture made before these keys existed (or from a code path that doesn't set them).
        var dir = WriteTreeJson("""{"scale":1,"tree":{},"auditIssues":[]}""");
        try
        {
            var (launched, broughtForward) = IosCollector.ReadForegroundFlags(dir);
            Assert.False(launched);
            Assert.False(broughtForward);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
