using Swipewalk.Collectors;

namespace Swipewalk.Core.Tests;

/// <summary>
/// <see cref="LargeTextReset.Decide"/>: what to do with the app under test, once the system text size has
/// been restored, after a large-text escalation (terminate + relaunch at the larger size because the text
/// didn't grow live -- see <see cref="LargeTextReset"/>'s own remarks). Shared by every large-text path (iOS
/// Simulator, physical iPhone, Android), in both scan and record mode.
/// </summary>
public class LargeTextResetTests
{
    [Theory]
    [InlineData(false)] // scan
    [InlineData(true)] // record
    public void NotRelaunched_AlwaysNone(bool recordMode)
    {
        // Text grew live: the app was never relaunched at the larger size, so there's nothing to clean up,
        // regardless of mode.
        Assert.Equal(LargeTextResetAction.None, LargeTextReset.Decide(relaunchedAtLargerSize: false, recordMode));
    }

    [Fact]
    public void Relaunched_ScanMode_Terminates()
    {
        // Scan has no user to hand the app back to: stop it rather than leave it running at the stale
        // enlarged size (the original bug: a later plain scan would silently inherit that process).
        Assert.Equal(LargeTextResetAction.Terminate, LargeTextReset.Decide(relaunchedAtLargerSize: true, recordMode: false));
    }

    [Fact]
    public void Relaunched_RecordMode_Relaunches()
    {
        // Record mode needs the app back in front, correctly sized, for the user to keep going.
        Assert.Equal(LargeTextResetAction.Relaunch, LargeTextReset.Decide(relaunchedAtLargerSize: true, recordMode: true));
    }
}
