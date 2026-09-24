using System.Text.Json;
using Swipewalk.Core.Reports;
using Swipewalk.Core.ScreenReader;

namespace Swipewalk.Engine;

/// <summary>
/// One screen Recorder has kept in the report so far: its folder number (for
/// <c>screens/{Number:00}</c> -- kept even after other entries are removed by a same-screen replacement, so a
/// later screen never reuses a number whose folder might still be referenced elsewhere) and its identity
/// fingerprint, for same-screen replacement to keep working after <c>record --continue</c> starts a new
/// process (see <see cref="ScreenIdentity.IsSameScreen(ScreenFingerprint,Swipewalk.Core.Model.ScreenSnapshot)"/>).
/// </summary>
public sealed record RecordedScreenState(int Number, ScreenFingerprint Fingerprint);

/// <summary>
/// Engine-internal bookkeeping for one run's recording, saved as record-state.json alongside
/// report.html/results.json so <c>record --continue</c> (or the desktop app's Continue action) can resume it in
/// a new process. Never part of the public report or results.json: everything the report itself needs to say
/// (session start/end times) is on <see cref="ScanReport.Sessions"/> instead, which already round-trips through
/// results.json. A run saved before this file existed (or one whose record-state.json is missing or damaged)
/// has none: Recorder then starts fresh on everything here and says so.
/// </summary>
public sealed record RecordState
{
    /// <summary>Next number to hand out for a screen's folder (<c>screens/{n:00}</c>) and its "Screen n:" log
    /// lines -- kept separate from the count of screens actually kept in the report, because a same-screen
    /// replacement removes an earlier entry (see Recorder.ReplaceIfSameScreenAsync) but must never free up its
    /// folder number for a later, unrelated screen to reuse.</summary>
    public int NextScreenNumber { get; init; } = 1;

    /// <summary>See Recorder's own <c>_learnedRestartReason</c> field.</summary>
    public string? LearnedRestartReason { get; init; }

    /// <summary>Screens currently kept in the report, in the same order as results.json's <c>screens</c> and
    /// the same count -- see <see cref="RecordedScreenState"/>.</summary>
    public IReadOnlyList<RecordedScreenState> Screens { get; init; } = [];

    private static readonly JsonSerializerOptions Json = new(JsonReport.Options) { WriteIndented = true };

    public static string PathFor(string outDir) => Path.Combine(outDir, "record-state.json");

    /// <summary>Null when there's no saved state (a fresh run, a run from before this file existed, or a
    /// damaged file) -- callers treat that the same as "start fresh".</summary>
    public static RecordState? Load(string outDir)
    {
        try
        {
            var path = PathFor(outDir);
            if (!File.Exists(path))
                return null;
            return JsonSerializer.Deserialize<RecordState>(File.ReadAllText(path), Json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public Task SaveAsync(string outDir) =>
        File.WriteAllTextAsync(PathFor(outDir), JsonSerializer.Serialize(this, Json));
}
