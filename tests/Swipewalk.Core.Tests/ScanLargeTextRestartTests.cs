using Swipewalk.Collectors;
using Swipewalk.Core.Model;
using Swipewalk.Engine;

namespace Swipewalk.Core.Tests;

/// <summary>
/// <see cref="ScanLargeTextRestart.Build"/>: maps the resolved <see cref="LargeTextRestartPolicy"/> and the
/// person's answer (via a fake <see cref="LargeTextRestartAsker"/>) onto the reason ScanService reports when
/// the person (or the policy) declines to restart: declining
/// records <see cref="LargeTextCapture.DeclinedByPerson"/>, a standing "never check" policy records
/// <see cref="LargeTextCapture.NotCheckedThisScan"/>, and "always"/"check anyway" proceed (a null hook, or a
/// hook that resolves to null) without asking further.
/// </summary>
public class ScanLargeTextRestartTests
{
    private static LargeTextRestartAsker Answering(LargeTextRestartChoice choice) =>
        (screenName, reason, learnedFromEarlierScreen, platform, cancellationToken) => Task.FromResult(choice);

    [Fact]
    public void Always_ProceedsWithoutAsking()
    {
        // Null is the sentinel the collectors already understand as "restart automatically", unchanged from
        // before this feature existed -- so Always must not build a hook that would call an asker at all.
        var hook = ScanLargeTextRestart.Build(LargeTextRestartPolicy.Always, ask: null, "Login", Platform.Android);

        Assert.Null(hook);
    }

    [Fact]
    public async Task Never_SkipsWithoutAsking_UsingTheScanWording()
    {
        var asked = false;
        LargeTextRestartAsker ask = (_, _, _, _, _) => { asked = true; return Task.FromResult(LargeTextRestartChoice.RestartAndCheck); };

        var hook = ScanLargeTextRestart.Build(LargeTextRestartPolicy.Never, ask, "Login", Platform.Android);
        var declinedReason = await hook!(LargeTextCapture.DidNotGrowLive, CancellationToken.None);

        Assert.False(asked, "Never must not call the asker at all -- nobody is asked about this particular screen.");
        Assert.Equal(LargeTextCapture.NotCheckedThisScan, declinedReason);
    }

    [Fact]
    public async Task Ask_PersonDeclines_RecordsDeclinedByPerson()
    {
        var hook = ScanLargeTextRestart.Build(
            LargeTextRestartPolicy.Ask, Answering(LargeTextRestartChoice.SkipForThisScreen), "Login", Platform.Android);

        var declinedReason = await hook!(LargeTextCapture.DidNotGrowLive, CancellationToken.None);

        Assert.Equal(LargeTextCapture.DeclinedByPerson, declinedReason);
    }

    [Fact]
    public async Task Ask_PersonChecksAnyway_ProceedsWithARestart()
    {
        var hook = ScanLargeTextRestart.Build(
            LargeTextRestartPolicy.Ask, Answering(LargeTextRestartChoice.RestartAndCheck), "Login", Platform.Android);

        var declinedReason = await hook!(LargeTextCapture.DidNotGrowLive, CancellationToken.None);

        Assert.Null(declinedReason);
    }

    /// <summary>A scan is one screen, so "always"/"never" from the prompt just mean "check"/"don't check"
    /// here -- there's no later screen for a standing choice to apply to.</summary>
    [Theory]
    [InlineData(LargeTextRestartChoice.AlwaysRestart, null)]
    [InlineData(LargeTextRestartChoice.AlwaysSkip, LargeTextCapture.DeclinedByPerson)]
    public async Task Ask_AlwaysOrNeverAnswer_BehavesLikeTheOneScreenChoice(LargeTextRestartChoice choice, string? expectedReason)
    {
        var hook = ScanLargeTextRestart.Build(LargeTextRestartPolicy.Ask, Answering(choice), "Login", Platform.Android);

        var declinedReason = await hook!(LargeTextCapture.DidNotGrowLive, CancellationToken.None);

        Assert.Equal(expectedReason, declinedReason);
    }

    [Fact]
    public async Task Ask_NoAskerWiredUp_DeclinesRatherThanWaitingForever()
    {
        // Mirrors Recorder.ResolveChoiceAsync's own fallback: a non-interactive run with policy "ask" but no
        // console/dialog available must resolve, not hang.
        var hook = ScanLargeTextRestart.Build(LargeTextRestartPolicy.Ask, ask: null, "Login", Platform.Android);

        var declinedReason = await hook!(LargeTextCapture.DidNotGrowLive, CancellationToken.None);

        Assert.Equal(LargeTextCapture.DeclinedByPerson, declinedReason);
    }
}
