using System.Text.Json;
using Swipewalk.Core.Coverage;
using Swipewalk.Core.Reports;

namespace Swipewalk.Engine;

/// <summary>
/// Every guided-check answer recorded for one run, saved as guided-answers.json alongside
/// report.html/results.json/record-state.json -- same run folder, same "written incrementally, loaded on
/// resume" lifecycle as <see cref="RecordState"/>. Human answers can't be recomputed from the tree the way
/// everything else in <c>Coverage.ScreenCoverageBuilder</c> can, so unlike the run's computed coverage, this
/// has to be its own stored, mutable, resumable file. Never part of results.json itself: a report loaded from
/// disk pulls this in separately (see <c>ScanReport.GuidedAnswers</c>).
/// </summary>
public sealed record GuidedAnswerStore(IReadOnlyList<GuidedAnswer> Answers)
{
    public static readonly GuidedAnswerStore Empty = new([]);

    public static string PathFor(string outDir) => Path.Combine(outDir, "guided-answers.json");

    private static readonly JsonSerializerOptions Json = new(JsonReport.Options) { WriteIndented = true };

    /// <summary>Null when there's no saved file (no guided checks recorded yet for this run, or a damaged
    /// file) -- callers treat that the same as "no answers yet", matching <see cref="RecordState.Load"/>.</summary>
    public static GuidedAnswerStore? Load(string outDir)
    {
        try
        {
            var path = PathFor(outDir);
            if (!File.Exists(path))
                return null;
            return JsonSerializer.Deserialize<GuidedAnswerStore>(File.ReadAllText(path), Json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns a copy with <paramref name="answer"/> appended, for the caller to persist with
    /// <see cref="SaveAsync"/> right away ("save as you go": each answer is written immediately, no "submit
    /// all" step that risks losing work). Older answers for the same (screen, criterion) are kept, never
    /// overwritten in place -- <c>Coverage.ScreenCoverageBuilder</c> reads the most recent one and keeps the
    /// rest as <c>ScreenCriterionReport.OlderAnswers</c>, so a later disagreement is flagged, not hidden.
    /// </summary>
    public GuidedAnswerStore With(GuidedAnswer answer) => this with { Answers = [.. Answers, answer] };

    /// <summary>
    /// Saves this store to <paramref name="outDir"/>. Second line of defense behind the CLI/desktop UI (which
    /// must never let a tester get this far): refuses to write a Pass with no evidence description or a
    /// confirmed-not-applicable answer with no reason, rather than silently accepting one written some other
    /// way (e.g. a hand-edited file, or a future caller that skips the UI's own checks).
    /// </summary>
    public Task SaveAsync(string outDir)
    {
        var invalid = Answers.FirstOrDefault(a => !a.IsValid);
        if (invalid is not null)
            throw new InvalidOperationException(
                $"Refusing to save an invalid guided answer for {invalid.CriterionNumber} on screen " +
                $"{invalid.ScreenId} ({invalid.Result}): a Pass needs evidence, a confirmed N/A needs a reason.");

        return File.WriteAllTextAsync(PathFor(outDir), JsonSerializer.Serialize(this, Json));
    }
}
