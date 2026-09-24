using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// Turns differences between the predicted screen-reader transcript and real evidence
/// (<see cref="ScreenSnapshot.ScreenReaderCapture"/>) into findings. Reports nothing when no capture was
/// made for a screen -- the common case, since screen-reader capture is opt-in (<c>--screen-reader</c>;
/// see <c>Swipewalk.Collectors.Android.AndroidHarness.RunScreenReaderCaptureAsync</c>, Android only for
/// now -- iOS stays predicted-only until its own capture route ships).
///
/// Every difference is reported as <see cref="FindingKind.NeedsReview"/>, never <see cref="FindingKind.WcagIssue"/>:
/// a difference from Swipewalk's own prediction is real evidence something needs a look, but not, by itself,
/// a confirmed WCAG failure -- the prediction could be the one that's wrong (see
/// <see cref="ScreenReaderCaptureComparer"/>'s remarks on its role-word vocabulary being a deliberately
/// conservative, under- rather than over-reporting approximation). A
/// <see cref="ScreenReaderDifferenceKind.Unmatched"/> difference (a captured item that couldn't be lined up
/// to any tree node at all) is not turned into a finding: it says something about the capture's coverage,
/// not about the app.
///
/// WCAG mapping, per difference kind:
/// <list type="bullet">
/// <item><see cref="ScreenReaderDifferenceKind.RoleMismatch"/> -- the reader didn't confirm a role Swipewalk
/// predicted -- 4.1.2 Name, Role, Value.</item>
/// <item><see cref="ScreenReaderDifferenceKind.OrderMismatch"/> -- never reported for
/// <see cref="ScreenReaderSource.TalkBack"/>: that capture drives TalkBack's focus to each element itself,
/// in the tree's own order (harness/android's <c>TalkBackCollector.kt</c>), so its "order" is Swipewalk's
/// walk order, not TalkBack's own swipe order -- an OrderMismatch there would say nothing about the app.
/// A source that captures the reader's real navigation order (for example a person-driven VoiceOver
/// session) would report 1.3.2 Meaningful Sequence and 2.4.3 Focus Order here instead.</item>
/// <item><see cref="ScreenReaderDifferenceKind.Missing"/> and <see cref="ScreenReaderDifferenceKind.TextMismatch"/>
/// -- mapped from the underlying tree node, via <see cref="NodeCriteria"/>: 4.1.2 Name, Role, Value for an
/// interactive or focusable element (a user interface component's name/role is what's in question); 1.1.1
/// Non-text Content is added only for an image-role element (a name problem on plain static text is neither
/// -- 1.1.1 is about non-text content specifically, and static text is not a user interface component under
/// 4.1.2 -- so it is left unmapped, like <see cref="ScreenReaderDifferenceKind.Extra"/> below).</item>
/// <item><see cref="ScreenReaderDifferenceKind.Extra"/> -- the reader reported something the predicted
/// transcript has no stop for -- no WCAG criterion is solid enough to cite (could be a genuinely extra
/// announcement, or the predictor missing a stop); left unmapped like <see cref="AtfIssueRule"/>'s unmapped
/// checks rather than guessed.</item>
/// </list>
/// </summary>
public sealed class ScreenReaderCaptureRule : IRule
{
    public string Id => "screen-reader-capture";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        if (snapshot.ScreenReaderCapture is not { Items.Count: > 0 } capture)
            yield break;

        var predicted = ScreenReaderPredictor.Predict(snapshot);
        var nodesByPath = snapshot.Root.DescendantsAndSelfWithPath().ToDictionary(t => t.Path, t => t.Node);
        var tool = ScreenReaderCaptureComparer.ToolLabel(capture.Source);

        foreach (var diff in ScreenReaderCaptureComparer.Compare(predicted, capture))
        {
            // Diagnostic only -- see this type's remarks.
            if (diff.Kind == ScreenReaderDifferenceKind.Unmatched)
                continue;
            // See this type's remarks on OrderMismatch: TalkBack's capture order is Swipewalk's own walk
            // order, not a real navigation order, so an order difference here is meaningless.
            if (diff.Kind == ScreenReaderDifferenceKind.OrderMismatch && capture.Source == ScreenReaderSource.TalkBack)
                continue;

            var path = diff.Predicted?.NodePath ?? diff.Actual?.MatchedNodePath ?? "";
            var node = nodesByPath.GetValueOrDefault(path);
            var criteria = Criteria(diff.Kind, node);

            yield return new Finding
            {
                RuleId = $"{Id}:{Suffix(diff.Kind)}",
                Kind = FindingKind.NeedsReview,
                Message = $"{diff.Description} Check this by hand: it is based on output captured from {tool}, " +
                          "and either the app or Swipewalk's prediction may be wrong.",
                Criteria = criteria,
                NodePath = path,
                Role = node?.Role ?? "element",
                Label = node?.Label ?? node?.VisibleText ?? diff.Predicted?.Text ?? diff.Actual?.Label ?? diff.Actual?.SpokenText,
                Bounds = node?.Bounds ?? default,
            };
        }
    }

    private static string Suffix(ScreenReaderDifferenceKind kind) => kind switch
    {
        ScreenReaderDifferenceKind.Missing => "missing",
        ScreenReaderDifferenceKind.Extra => "extra",
        ScreenReaderDifferenceKind.TextMismatch => "name",
        ScreenReaderDifferenceKind.OrderMismatch => "order",
        ScreenReaderDifferenceKind.RoleMismatch => "role",
        _ => kind.ToString(),
    };

    private static IReadOnlyList<WcagCriterion> Criteria(ScreenReaderDifferenceKind kind, AccessibilityNode? node) => kind switch
    {
        ScreenReaderDifferenceKind.RoleMismatch => [WcagCriteria.NameRoleValue],
        ScreenReaderDifferenceKind.OrderMismatch => [WcagCriteria.MeaningfulSequence, WcagCriteria.FocusOrder],
        ScreenReaderDifferenceKind.Missing or ScreenReaderDifferenceKind.TextMismatch => NodeCriteria(node),
        _ => [], // Extra: no solid WCAG mapping, see this type's remarks.
    };

    /// <summary>See this type's remarks on <see cref="ScreenReaderDifferenceKind.Missing"/> and
    /// <see cref="ScreenReaderDifferenceKind.TextMismatch"/> for the reasoning.</summary>
    private static IReadOnlyList<WcagCriterion> NodeCriteria(AccessibilityNode? node)
    {
        var criteria = new List<WcagCriterion>();
        if (node is { } n)
        {
            if (n.IsInteractive || n.IsFocusable)
                criteria.Add(WcagCriteria.NameRoleValue);
            if (n.Role == "image")
                criteria.Add(WcagCriteria.NonTextContent);
        }
        else
        {
            // The tree node couldn't be found at all (shouldn't normally happen: NodePath comes from the
            // same tree ScreenReaderPredictor.Predict walked) -- fall back to the narrower of the two
            // criteria rather than guessing either way.
            criteria.Add(WcagCriteria.NameRoleValue);
        }
        return criteria;
    }
}
