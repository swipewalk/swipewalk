using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.ScreenReader;

namespace Swipewalk.Engine;

/// <summary>
/// What <c>record --continue</c> (or the desktop app's Continue action on an ended-early run in History) gives
/// a new <see cref="Recorder"/> so it resumes the same run instead of starting a fresh one: the screens already
/// kept (in report order), their folder numbers and identity fingerprints (for same-screen replacement to keep
/// working -- see <see cref="ScreenIdentity.IsSameScreen(ScreenFingerprint,ScreenSnapshot)"/>), the earlier
/// sessions' start/end times, and the large-text restart reason this run had already learned, if any. Build one
/// with <see cref="ScanService.ResolveContinuation"/>.
/// </summary>
/// <param name="StateWasSaved">
/// Whether record-state.json (<see cref="RecordState"/>) actually existed and loaded for this run, as opposed
/// to <see cref="ScanService.ResolveContinuation"/> falling back to an empty one (a run saved before that file
/// existed, or one whose file is missing/damaged). False means <see cref="LearnedRestartReason"/> being null
/// doesn't tell you anything by itself -- it could mean "nothing learned yet" (a legitimate, common case: the
/// text grew live, or large text wasn't checked) or "we don't know, nothing was saved" -- so callers use this,
/// not <c>LearnedRestartReason is null</c>, to decide whether to say "starting fresh" out loud. False also means
/// <see cref="PriorIdentities"/> is padded with <see cref="ScreenFingerprint.Unknown"/> for every one of this
/// run's earlier screens.
/// </param>
public sealed record RecordContinuation(
    IReadOnlyList<ScreenResult> PriorResults,
    IReadOnlyList<RecordedScreenState> PriorScreens,
    IReadOnlyList<RecordingSession> PriorSessions,
    string? LearnedRestartReason,
    int NextScreenNumber,
    bool StateWasSaved)
{
    /// <summary>Fingerprints parallel to <see cref="PriorResults"/> (same order, same count), padding any
    /// screen whose fingerprint wasn't saved with <see cref="ScreenFingerprint.Unknown"/> -- see
    /// <see cref="RecordState"/>'s remarks on a run saved before it existed.</summary>
    public IReadOnlyList<ScreenFingerprint> PriorIdentities => [.. Enumerable.Range(0, PriorResults.Count)
        .Select(i => i < PriorScreens.Count ? PriorScreens[i].Fingerprint : ScreenFingerprint.Unknown)];

    /// <summary>Folder numbers parallel to <see cref="PriorResults"/>, defaulting to 1-based position when not
    /// saved (see <see cref="PriorIdentities"/>'s remarks) -- only used to locate a replaced screen's capture
    /// files, and every real screen's own folder number is always saved once RecordState exists, so this
    /// fallback is only ever exercised for a pre-RecordState run's screens.</summary>
    public IReadOnlyList<int> PriorScreenNumbers => [.. Enumerable.Range(0, PriorResults.Count)
        .Select(i => i < PriorScreens.Count ? PriorScreens[i].Number : i + 1)];
}
