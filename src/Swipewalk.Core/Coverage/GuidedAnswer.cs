using System.Text.Json.Serialization;

namespace Swipewalk.Core.Coverage;

/// <summary>
/// The result a tester recorded for one guided check. Never "Passed" on its own in report text --
/// <see cref="GuidedChecksDisplay"/> always spells a pass out with its evidence quoted. "Inconclusive" covers
/// "I tried the steps but can't tell": it is never folded into Pass, and is shown as its own bucket.
/// </summary>
public enum GuidedAnswerResult
{
    Pass,
    Fail,
    Inconclusive,
    ConfirmedNotApplicable,
}

/// <summary>
/// One piece of evidence behind a guided answer. A <see cref="GuidedAnswer"/> with
/// <see cref="GuidedAnswer.Result"/> == <see cref="GuidedAnswerResult.Pass"/> must have at least one of these
/// with a non-empty <see cref="Description"/> -- enforced by the CLI/desktop UI before it lets a tester save a
/// Pass, and by <see cref="GuidedAnswerStore.SaveAsync"/> as a second line of defense.
/// </summary>
/// <param name="Description">What the tester observed, in their own words. Required, non-empty, for a Pass.</param>
/// <param name="ScreenshotPath">Optional: relative to the run folder, e.g. "screens/03/guided-1.3.1.png".</param>
/// <param name="CaptureItemOrder">Optional: <c>Model.ScreenReaderCaptureItem.Order</c> on this screen's
/// <c>Model.ScreenReaderCapture</c>, when a real TalkBack/VoiceOver capture ran alongside the guided check.</param>
public sealed record GuidedEvidence(string Description, string? ScreenshotPath = null, int? CaptureItemOrder = null);

/// <summary>
/// One tester's recorded answer for one (screen, criterion). Stored in <see cref="GuidedAnswerStore"/>
/// (guided-answers.json, in the run folder) -- this is data a person typed in, so unlike everything
/// <see cref="ScreenCoverageBuilder"/> computes, it can't be recomputed from the tree and must be persisted.
/// </summary>
/// <param name="ScreenId">The stable per-run screen id (see <c>Model.ScreenResult.ScreenId</c>), never a list
/// index or screen name (names repeat across a run, or even after a restart).</param>
/// <param name="CriterionNumber">e.g. "2.4.3" -- looked up from <c>Wcag.WcagCriteria.All</c>, never reconstructed.</param>
/// <param name="Note">Optional free-text note, in addition to any required evidence/reason below.</param>
/// <param name="NotApplicableReason">Required, non-empty, when <paramref name="Result"/> is
/// <see cref="GuidedAnswerResult.ConfirmedNotApplicable"/> (pre-filled from a matching
/// <see cref="ProposedNotApplicable.Reason"/> but editable); null otherwise.</param>
/// <param name="FastPassConfirmed">True only when the tester answered suspiciously fast and explicitly
/// confirmed anyway (see the "very fast passes" gap check); false for a normal-paced answer. Never records
/// elapsed time itself, just this one flag.</param>
/// <param name="Tester">Free text the tester types (initials, "me", a name if they choose -- never required to
/// be a real name). Saved only in this run's own local folder, never shared automatically.</param>
/// <param name="Device">From the run's own device info when known, else what the tester typed.</param>
/// <param name="AssistiveTechnology">"TalkBack" | "VoiceOver" | "Hardware keyboard" | "None" -- free text, not
/// an enum, so a future assistive technology doesn't need a schema change.</param>
/// <param name="Evidence">Required, non-empty, when <paramref name="Result"/> is
/// <see cref="GuidedAnswerResult.Pass"/>.</param>
public sealed record GuidedAnswer(
    string ScreenId,
    string CriterionNumber,
    GuidedAnswerResult Result,
    string? Note,
    string? NotApplicableReason,
    bool FastPassConfirmed,
    string Tester,
    DateTimeOffset AnsweredAt,
    string? Device,
    string AssistiveTechnology,
    string? AssistiveTechnologyVersion,
    IReadOnlyList<GuidedEvidence> Evidence)
{
    /// <summary>True when this answer satisfies the model's own required-evidence/required-reason rules,
    /// checked by <see cref="GuidedAnswerStore.SaveAsync"/> as a second line of defense behind the UI (which
    /// should never let a tester get this far in the first place). Computed, not stored.</summary>
    [JsonIgnore]
    public bool IsValid => Result switch
    {
        GuidedAnswerResult.Pass => Evidence.Any(e => !string.IsNullOrWhiteSpace(e.Description)),
        GuidedAnswerResult.ConfirmedNotApplicable => !string.IsNullOrWhiteSpace(NotApplicableReason),
        _ => true,
    };
}
