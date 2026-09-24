namespace Swipewalk.Collectors;

/// <summary>What to do with the app under test once the large-text escalation has been evaluated and the
/// system text size restored. See <see cref="LargeTextReset.Decide"/>.</summary>
public enum LargeTextResetAction
{
    /// <summary>The text grew live; the app was never relaunched at the larger size, so there's nothing to
    /// undo.</summary>
    None,

    /// <summary>Scan mode: stop the app rather than leave it running at the stale enlarged size. (On a
    /// platform with no separate "stop" primitive -- a physical iPhone -- callers realize this by relaunching
    /// once more with nothing changed; the app ends up correctly sized either way.)</summary>
    Terminate,

    /// <summary>Record mode: relaunch the app so it's back in front, correctly sized, for the user to keep
    /// going; the caller should also surface <see cref="LargeTextCapture.RestartedToRestoreNotice"/>.</summary>
    Relaunch,
}

/// <summary>
/// The pure decision behind the "clean up after a terminate + relaunch escalation" step that every large-text
/// path (iOS Simulator, physical iPhone, and Android, in both scan and record mode) needs once the system text
/// size has been restored. Only an *escalated* capture (text didn't grow live, so the app was terminated and
/// relaunched while the size was still enlarged) leaves a stale, still-enlarged process behind -- restoring
/// the system setting afterwards changes it back, but frameworks that only read the text size at launch (e.g.
/// .NET MAUI before 10.0.100 on iOS; Android apps declaring <c>android:configChanges="fontScale"</c>) keep
/// rendering at the size they launched with until relaunched again (reproduced 2026-09-22: a study's apps all
/// measured "no growth" because each was still the relaunched-at-large-size process an earlier check left
/// running). A capture where the text grew live never relaunched the app, so there's nothing to clean up.
/// </summary>
public static class LargeTextReset
{
    public static LargeTextResetAction Decide(bool relaunchedAtLargerSize, bool recordMode) =>
        !relaunchedAtLargerSize ? LargeTextResetAction.None
        : recordMode ? LargeTextResetAction.Relaunch
        : LargeTextResetAction.Terminate;
}
