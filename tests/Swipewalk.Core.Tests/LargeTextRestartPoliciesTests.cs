using Swipewalk.Engine;

namespace Swipewalk.Core.Tests;

/// <summary>
/// <see cref="LargeTextRestartPolicies.Default"/>: the policy scan and record fall back to when nothing set one
/// explicitly (no --large-text-restart / swipewalk.json's largeTextRestart): interactive always means Ask;
/// non-interactive means Never for record (an unattended recording
/// must never block) but Always for scan (a one-shot scan has no later screen to keep blocking on, so it keeps
/// the automatic restart CI already relied on before this policy applied to scan at all).
/// </summary>
public class LargeTextRestartPoliciesTests
{
    [Theory]
    [InlineData(true)] // record
    [InlineData(false)] // scan
    public void Interactive_AlwaysAsks_RegardlessOfMode(bool recordMode)
    {
        Assert.Equal(LargeTextRestartPolicy.Ask, LargeTextRestartPolicies.Default(interactive: true, recordMode));
    }

    [Fact]
    public void NotInteractive_Record_DefaultsToNever()
    {
        Assert.Equal(LargeTextRestartPolicy.Never, LargeTextRestartPolicies.Default(interactive: false, recordMode: true));
    }

    [Fact]
    public void NotInteractive_Scan_DefaultsToAlways()
    {
        // Not "Never": a non-interactive scan (CI, a redirected console, an unattended `swipewalk run`) must
        // keep the automatic restart it always did before scan had a policy at all.
        Assert.Equal(LargeTextRestartPolicy.Always, LargeTextRestartPolicies.Default(interactive: false, recordMode: false));
    }
}
