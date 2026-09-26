using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Coverage;

/// <summary>
/// A guided answer that disagrees with something else Swipewalk knows about the same (screen, criterion): an
/// automated finding, or an earlier, different answer for the same pair. See <c>Reports.ScanReport.Contradictions</c>
/// -- the CLI (<c>swipewalk guide</c>) and desktop Guided checks page both show this immediately when it
/// happens, and the HTML report lists every one of these in a "Flagged" section near the top.
/// </summary>
/// <param name="ConflictingWcagIssueCount">Findings on this screen citing this criterion with
/// <see cref="FindingKind.WcagIssue"/> that conflict with <paramref name="Answer"/>. Zero for an
/// answer-vs-answer disagreement (see <paramref name="Description"/>).</param>
/// <param name="ConflictingNeedsReviewCount">Same, for <see cref="FindingKind.NeedsReview"/>.</param>
public sealed record CoverageContradiction(
    string ScreenId,
    string ScreenName,
    string CriterionNumber,
    GuidedAnswer Answer,
    int ConflictingWcagIssueCount,
    int ConflictingNeedsReviewCount,
    string Description);

/// <summary>
/// Finds guided answers that disagree with something else on the same screen. A contradiction is: the CURRENT
/// (most recent) answer for a (screen, criterion) being a Pass, or a confirmed not-applicable, sitting next to
/// a real automated finding for the same criterion (a Fail/Inconclusive answer never contradicts a finding
/// this way -- it agrees or is neutral); a superseded, older answer is never re-flagged just because it once
/// said Pass -- only the current one speaks for the (screen, criterion) pair. Or: the most recent answer for a
/// (screen, criterion) disagreeing with the previous one about whether the criterion applies here at all
/// (required fix 4: "a tester answer on an N/A-here criterion is shown and flagged, never hidden").
/// </summary>
public static class ContradictionChecker
{
    public static IReadOnlyList<CoverageContradiction> Find(IReadOnlyList<ScreenResult> screens, IReadOnlyList<GuidedAnswer> answers)
    {
        // GroupBy+first, not ToDictionary: two screens can share the same (possibly empty) ScreenId in a
        // hand-built ScanReport that never went through RuleRunner.Run/JsonReport.Deserialize (e.g. a test
        // fixture, or a defensively-read damaged file) -- this must never crash the whole computed property
        // just because of that, the same "never crash on odd data" bar the rest of this codebase holds to.
        var screensById = screens.GroupBy(s => s.ScreenId).ToDictionary(g => g.Key, g => g.First());
        var result = new List<CoverageContradiction>();

        foreach (var group in answers.GroupBy(a => (a.ScreenId, a.CriterionNumber)))
        {
            var ordered = group.OrderBy(a => a.AnsweredAt).ToList();
            var current = ordered[^1];
            if (!screensById.TryGetValue(current.ScreenId, out var screen))
                continue;

            if (current.Result is GuidedAnswerResult.Pass or GuidedAnswerResult.ConfirmedNotApplicable
                && WcagCriteria.All.FirstOrDefault(c => c.Number == current.CriterionNumber) is { } criterion)
            {
                var conflicting = screen.Findings
                    .Where(f => f.Criteria.Contains(criterion) && f.Kind is FindingKind.WcagIssue or FindingKind.NeedsReview)
                    .ToList();
                if (conflicting.Count > 0)
                {
                    var wcagIssues = conflicting.Count(f => f.Kind == FindingKind.WcagIssue);
                    var needsReview = conflicting.Count(f => f.Kind == FindingKind.NeedsReview);
                    var description = current.Result == GuidedAnswerResult.Pass
                        ? GuidedChecksDisplay.Contradiction(current.CriterionNumber, screen.ScreenName,
                            string.Join("; ", current.Evidence.Select(e => e.Description)), wcagIssues, needsReview)
                        : GuidedChecksDisplay.NotApplicableContradiction(current.CriterionNumber, screen.ScreenName,
                            current.NotApplicableReason ?? "", wcagIssues, needsReview);
                    result.Add(new CoverageContradiction(
                        screen.ScreenId, screen.ScreenName, current.CriterionNumber, current, wcagIssues, needsReview, description));
                }
            }

            if (ordered.Count >= 2)
            {
                var previous = ordered[^2];
                var currentIsNotApplicable = current.Result == GuidedAnswerResult.ConfirmedNotApplicable;
                var previousIsNotApplicable = previous.Result == GuidedAnswerResult.ConfirmedNotApplicable;
                if (currentIsNotApplicable != previousIsNotApplicable)
                    result.Add(new CoverageContradiction(
                        current.ScreenId, screen.ScreenName, current.CriterionNumber, current, 0, 0,
                        GuidedChecksDisplay.AnswerDisagreement(current.CriterionNumber, screen.ScreenName, previous, current)));
            }
        }

        return result;
    }
}
