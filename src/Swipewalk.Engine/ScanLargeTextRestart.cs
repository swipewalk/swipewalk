using Swipewalk.Collectors;
using Swipewalk.Core.Model;

namespace Swipewalk.Engine;

/// <summary>
/// Builds scan mode's ask-before-restart hook (<see cref="Swipewalk.Collectors.ScanLargeTextRestartAsk"/>) from
/// the resolved <see cref="LargeTextRestartPolicy"/> and an optional <see cref="LargeTextRestartAsker"/> (the
/// CLI console prompt or the desktop app's dialog). Kept apart
/// from <see cref="ScanService"/> so it can be unit-tested without a device: the policy/choice mapping is pure,
/// even though the hook it returns is async. A scan is one screen, so the asker's "always check"/"never check"
/// answers just mean "check"/"don't check" here -- there's no later screen for a standing choice to apply to.
/// </summary>
public static class ScanLargeTextRestart
{
    /// <param name="policy">Resolved once per scan -- see <see cref="LargeTextRestartPolicy"/> and
    /// <see cref="LargeTextRestartPolicies.Default"/>.</param>
    /// <param name="ask">The CLI/desktop asker; null (e.g. no console or dialog available) is treated as if the
    /// person had answered "don't check", mirroring <c>Recorder.ResolveChoiceAsync</c>'s own fallback.</param>
    /// <param name="screenName">Shown in the prompt, same as <see cref="ScanOptions.ScreenName"/>.</param>
    /// <param name="platform">Picks the Android/iOS wording in <c>LargeTextAskWording.WhatHappened</c>.</param>
    /// <returns>Null when nothing needs to ask (<see cref="LargeTextRestartPolicy.Always"/>): the caller passes
    /// null straight through to the collector, which then restarts automatically, the same as before this
    /// feature existed. Otherwise a hook that resolves "proceed" (returns null) or the skip reason to record
    /// (<see cref="LargeTextCapture.DeclinedByPerson"/> / <see cref="LargeTextCapture.NotCheckedThisScan"/>).</returns>
    public static ScanLargeTextRestartAsk? Build(
        LargeTextRestartPolicy policy, LargeTextRestartAsker? ask, string screenName, Platform platform) =>
        policy switch
        {
            LargeTextRestartPolicy.Always => null,
            LargeTextRestartPolicy.Never => (_, _) => Task.FromResult<string?>(LargeTextCapture.NotCheckedThisScan),
            _ => (reason, cancellationToken) => ResolveAsync(ask, screenName, reason, platform, cancellationToken),
        };

    private static async Task<string?> ResolveAsync(
        LargeTextRestartAsker? ask, string screenName, string reason, Platform platform, CancellationToken cancellationToken)
    {
        if (ask is null)
            return LargeTextCapture.DeclinedByPerson;
        var choice = await ask(screenName, reason, learnedFromEarlierScreen: false, platform, cancellationToken);
        return choice is LargeTextRestartChoice.RestartAndCheck or LargeTextRestartChoice.AlwaysRestart
            ? null
            : LargeTextCapture.DeclinedByPerson;
    }
}
