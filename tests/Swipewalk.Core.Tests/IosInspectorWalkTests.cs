using Swipewalk.Collectors.Ios;
using Swipewalk.Core.Model;

namespace Swipewalk.Core.Tests;

/// <summary>
/// <see cref="IosInspectorWalk.RunAsync"/> actually runs harness/mac-inspector-walk/InspectorWalk.swift (via
/// <c>xcrun swift</c>) -- a real process invocation, deliberately not mocked, so this test suite depends on
/// this machine's live Accessibility permission and Accessibility Inspector state, which this suite does not
/// control and must not assume: a fresh macOS CI runner (see .github/workflows/ci.yml) has neither, so the
/// walk fails cleanly every time; a dev Mac can have the permission granted and the Inspector already
/// pointed at a target (for example right after a real device test), in which case the walk can genuinely
/// succeed. Both were confirmed by hand on a real Mac before this test was written. Either way, the one
/// thing this test insists on is that the call never crashes or hangs and always comes back as a normal,
/// well-formed <see cref="InspectorWalkResult"/>.
/// </summary>
public class IosInspectorWalkTests
{
    [Fact]
    public async Task RunAsync_NeverCrashesOrHangs_AndReturnsAWellFormedResultEitherWay()
    {
        var result = await IosInspectorWalk.RunAsync(timeout: TimeSpan.FromSeconds(20));

        if (result.Ok)
        {
            // A real walk on this machine (Accessibility granted, Inspector already set up) -- nothing more
            // specific to assert about its content here; IosInspectorCaptureTests covers matching/normalizing
            // what a real walk like this produces.
            Assert.True(result.Complete || result.NotCompleteReason is not null); // an incomplete walk must say why
        }
        else
        {
            // The expected outcome with no permission and/or no Inspector running: a clean failure, never a
            // crash -- confirmed by hand: "Xcode's Accessibility Inspector is not running..." or "Could not
            // find the Accessibility Inspector's inspection panel...", depending on whether it was open.
            Assert.False(string.IsNullOrWhiteSpace(result.Error));
            Assert.Empty(result.Items);
            Assert.False(result.Complete);
        }
    }

    [Fact]
    public async Task RunAsync_MissingScript_FailsWithAClearReasonInstead()
    {
        var result = await IosInspectorWalk.RunAsync(scriptPath: "/does/not/exist/InspectorWalk.swift");

        Assert.False(result.Ok);
        Assert.Contains("InspectorWalk.swift", result.Error);
    }

    [Fact]
    public void FindScript_Checkout_UsesTheScriptInPlace()
    {
        using var temp = new TempDir();
        var scriptDir = Directory.CreateDirectory(Path.Combine(temp.Root, "repo", "harness", "mac-inspector-walk")).FullName;
        File.WriteAllText(Path.Combine(scriptDir, "InspectorWalk.swift"), "// script");
        var subdir = Directory.CreateDirectory(Path.Combine(temp.Root, "repo", "src")).FullName;

        var found = IosInspectorWalk.FindScript(subdir, baseDirectory: Path.Combine(temp.Root, "elsewhere"));

        Assert.Equal(Path.Combine(scriptDir, "InspectorWalk.swift"), found);
    }

    [Fact]
    public void FindScript_InstalledCopy_FindsItNextToTheAssemblies()
    {
        using var temp = new TempDir();
        var tool = Path.Combine(temp.Root, "tool");
        var scriptDir = Directory.CreateDirectory(Path.Combine(tool, "harness", "mac-inspector-walk")).FullName;
        File.WriteAllText(Path.Combine(scriptDir, "InspectorWalk.swift"), "// script");

        var found = IosInspectorWalk.FindScript(temp.Root, baseDirectory: tool);

        Assert.Equal(Path.Combine(scriptDir, "InspectorWalk.swift"), found);
    }

    [Fact]
    public void FindScript_NotFoundAnywhere_ReturnsNull()
    {
        using var temp = new TempDir();

        Assert.Null(IosInspectorWalk.FindScript(temp.Root, temp.Root));
    }

    private sealed class TempDir : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("cf-inspector-walk-").FullName;
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
