using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;

namespace Swipewalk.Collectors.Ios;

/// <summary>
/// Turns an <see cref="InspectorWalkResult"/> (the Accessibility Inspector's own report, in its own order)
/// into a <see cref="ScreenReaderCapture"/> matched to <paramref name="snapshot"/>'s tree nodes -- the same
/// shape <see cref="ScreenReaderCaptureComparer"/> and <see cref="Rules.ScreenReaderCaptureRule"/> already
/// consume for Android's TalkBack capture. Pure and side-effect free: everything device/process-related
/// (running the walk itself) is <see cref="IosInspectorWalk.RunAsync"/>'s job, so this can be tested with a
/// synthetic tree and a hand-built <see cref="InspectorWalkResult"/>, no Mac or Inspector needed.
/// </summary>
public static class IosInspectorCapture
{
    /// <summary>A capture that made no walk attempt at all, or whose walk failed outright -- see
    /// <see cref="IosCollector.RunInspectorCaptureAsync"/>.</summary>
    public static ScreenReaderCapture Skipped(string reason) =>
        new(ScreenReaderSource.AccessibilityInspector, ToolVersion: "unknown", DateTimeOffset.UtcNow, [], Complete: false, NotCompleteReason: reason);

    /// <summary>
    /// The Accessibility Inspector's own placeholder text for "nothing set" in its panel, confirmed on a
    /// real physical-iPhone capture (2026-09-25, samples/BuggyApp): the literal text "None" for an
    /// unlabeled button's Label field, and for its Value/Hint/Identifier fields when those were also unset,
    /// and "Empty string" for an empty text field's Value. Not confirmed for ClassName (every real capture
    /// so far always reported a real class), but normalized the same way as a precaution. Neither placeholder
    /// is escaped or otherwise told apart from a genuine value that happens to be that exact text -- left
    /// unnormalized, "None" made a genuinely unlabeled control look like it was named "None", which
    /// <see cref="ScreenReaderCaptureComparer"/> then compared against the predicted (correctly null) name
    /// and reported as a false name-mismatch finding. Matched case-sensitively (<see cref="StringComparer.Ordinal"/>):
    /// this still accepts the real trade-off that a genuine label or value that happens to be exactly "None"
    /// or "Empty string" (for example a settings row whose value is "None") would also be stripped -- the
    /// Inspector gives no way to tell its own placeholder apart from that exact text, and treating an unset
    /// field as unset by default is more useful than the reverse.
    /// </summary>
    private static readonly HashSet<string> InspectorEmptyPlaceholders = new(StringComparer.Ordinal) { "None", "Empty string" };

    private static string? NormalizeInspectorText(string? text) =>
        string.IsNullOrEmpty(text) || InspectorEmptyPlaceholders.Contains(text) ? null : text;

    /// <summary>Normalizes every text field <see cref="InspectorWalkItem"/> carries (see
    /// <see cref="InspectorEmptyPlaceholders"/>) before it is used for matching or turned into a
    /// <see cref="ScreenReaderCaptureItem"/> -- so a placeholder can never masquerade as a real accessibility
    /// identifier match, an accessible name for the comparer to compare, or a class name for
    /// <see cref="RoleFromClassName"/> to (mis)recognize. <see cref="InspectorWalkItem.Traits"/> is left
    /// untouched: it is already a list, empty (not a placeholder string) when the Inspector reports none.</summary>
    private static InspectorWalkItem Normalize(InspectorWalkItem item) => item with
    {
        Label = NormalizeInspectorText(item.Label),
        Value = NormalizeInspectorText(item.Value),
        Hint = NormalizeInspectorText(item.Hint),
        Identifier = NormalizeInspectorText(item.Identifier),
        ClassName = NormalizeInspectorText(item.ClassName),
    };

    /// <summary>
    /// Matches each of <paramref name="raw"/>'s items, in the Inspector's own walk order, to a node in
    /// <paramref name="snapshot"/>'s tree. There is no frame/geometry field in what the Inspector reports
    /// (see <see cref="InspectorWalkItem"/>), so matching goes, per item, in this order:
    /// <list type="number">
    /// <item>Its <see cref="InspectorWalkItem.Identifier"/> against an unused candidate's
    /// <see cref="AccessibilityNode.AutomationId"/> -- <see cref="MatchConfidence.Exact"/> when exactly one
    /// unused candidate has it.</item>
    /// <item>Its <see cref="InspectorWalkItem.Label"/> against an unused candidate's predicted accessible
    /// name (<see cref="ScreenReaderPredictor.AccessibleName"/>) -- <see cref="MatchConfidence.Likely"/> when
    /// exactly one unused candidate matches, or when <see cref="RoleFromClassName"/> narrows several
    /// same-label candidates down to one by role; <see cref="MatchConfidence.Weak"/> when several unused
    /// candidates share both the label and the role and can't be told apart, in which case the one nearest
    /// the walk's current position is picked (this is genuinely uncertain, not a confident pick -- see
    /// <see cref="MatchConfidence.Weak"/>'s own remarks).</item>
    /// <item>Position alone, for an item with no usable label at all (for example an icon-only control the
    /// Inspector reports with an empty Label): the next unused candidate at or after the current position --
    /// <see cref="MatchConfidence.Weak"/>, or unmatched (<see cref="MatchConfidence.None"/>) if none remain.</item>
    /// </list>
    /// The "current position" a Weak/positional match uses is the previous item's matched candidate index
    /// (or 0 before any match), so a run of unlabeled icons after a confidently matched item still lines up
    /// with the tree in order, rather than each independently guessing from the start.
    /// </summary>
    public static ScreenReaderCapture Build(ScreenSnapshot snapshot, InspectorWalkResult raw, DateTimeOffset? capturedAt = null)
    {
        var at = capturedAt ?? DateTimeOffset.UtcNow;
        if (!raw.Ok)
            return Skipped(raw.Error ?? "the Accessibility Inspector walk failed");
        var toolVersion = raw.ToolVersion ?? "unknown";
        if (raw.Items.Count == 0)
            return new ScreenReaderCapture(ScreenReaderSource.AccessibilityInspector, toolVersion, at, [], raw.Complete,
                raw.NotCompleteReason ?? (raw.Complete ? null : "no elements were captured"));

        var predicted = ScreenReaderPredictor.Predict(snapshot);
        var nodesByPath = snapshot.Root.DescendantsAndSelfWithPath().ToDictionary(t => t.Path, t => t.Node);
        var candidates = predicted
            .Select((a, index) =>
            {
                var node = nodesByPath.GetValueOrDefault(a.NodePath);
                return new Candidate(index, a.NodePath, node, node is null ? null : ScreenReaderPredictor.AccessibleName(node), node?.Role);
            })
            .ToList();
        var used = new bool[candidates.Count];
        var cursor = 0;

        var items = new List<ScreenReaderCaptureItem>(raw.Items.Count);
        for (var i = 0; i < raw.Items.Count; i++)
        {
            var item = Normalize(raw.Items[i]);
            var match = FindMatch(candidates, used, cursor, item);
            if (match is { } m)
            {
                used[m.Index] = true;
                cursor = m.Index;
            }
            items.Add(new ScreenReaderCaptureItem(
                Order: i + 1, SpokenText: null, Label: item.Label, Value: item.Value, Traits: item.Traits, Hint: item.Hint,
                Identifier: item.Identifier, ClassName: item.ClassName, Timestamp: null,
                MatchedNodePath: match?.NodePath, MatchConfidence: match?.Confidence ?? MatchConfidence.None));
        }

        var (complete, notCompleteReason) = CheckPlausible(items, predicted.Count, raw.Complete, raw.NotCompleteReason);
        return new ScreenReaderCapture(ScreenReaderSource.AccessibilityInspector, toolVersion, at, items, complete, notCompleteReason);
    }

    /// <summary>
    /// Overrides <paramref name="rawComplete"/> to false when the script reported a complete walk that looks
    /// implausible on its own data. Confirmed on a real physical-iPhone capture (2026-09-25,
    /// samples/NativeiOS) where the person's click had apparently landed on the app's window/background
    /// rather than an element: the walk captured exactly one item, entirely empty (no label, value, hint,
    /// identifier or traits), and reported that as a normal wrap (<c>complete: true</c>) -- most likely
    /// because pressing "Next" with no real element selected has no effect, so the walk saw its own starting
    /// position again on the very next step. Read at face value, "complete" told
    /// <see cref="ScreenReaderCaptureComparer"/> the whole screen had been covered, which turned every one of
    /// the tree's named/focusable stops into a false "was not reported by the Inspector" finding
    /// (<see cref="ScreenReaderDifferenceKind.Missing"/>, gated on <see cref="ScreenReaderCapture.Complete"/>).
    /// Two signals catch this: every captured item being entirely empty (nothing was ever really selected),
    /// or the walk finding at most half of the screen's predicted stops (and at least one fewer) -- both are
    /// plausibility heuristics, not a certain diagnosis: a real screen with only two or three predicted stops
    /// can trigger the second one even when the Inspector genuinely found everything it could, but the only
    /// consequence of a false trigger is that this screen's Missing findings are suppressed for one capture,
    /// not a wrong finding reported. <paramref name="rawComplete"/> is only ever overridden when it was
    /// itself true: a walk the script already reported as incomplete (a timeout, the Inspector closing) keeps
    /// its own, more specific reason rather than losing it to this guess.
    /// </summary>
    private static (bool Complete, string? NotCompleteReason) CheckPlausible(
        IReadOnlyList<ScreenReaderCaptureItem> items, int predictedCount, bool rawComplete, string? rawNotCompleteReason)
    {
        if (!rawComplete)
            return (rawComplete, rawNotCompleteReason);

        var allEmpty = items.All(IsEffectivelyEmpty);
        var farFewerThanExpected = predictedCount > 0 && items.Count < predictedCount && items.Count <= Math.Max(1, predictedCount / 2);
        if (!allEmpty && !farFewerThanExpected)
            return (rawComplete, rawNotCompleteReason);

        var count = $"{items.Count} element{(items.Count == 1 ? "" : "s")}";
        return (false, allEmpty
            ? $"the walk found only {count}, none with a label, value or traits -- the Accessibility Inspector's " +
              "selection was probably not on an element of the app; click one element on the app's screen and scan again"
            : $"the walk found only {count} of about {predictedCount} expected, so it may not have covered the " +
              "whole screen; click the first element on the app's screen and scan again");
    }

    private static bool IsEffectivelyEmpty(ScreenReaderCaptureItem item) =>
        item.Label is null && item.Value is null && item.Hint is null && item.Identifier is null
        && (item.Traits is null || item.Traits.Count == 0);

    private sealed record Candidate(int Index, string NodePath, AccessibilityNode? Node, string? Name, string? Role);

    private static (int Index, string NodePath, MatchConfidence Confidence)? FindMatch(
        IReadOnlyList<Candidate> candidates, bool[] used, int cursor, InspectorWalkItem item)
    {
        if (!string.IsNullOrEmpty(item.Identifier))
        {
            var byId = candidates.Where(c => !used[c.Index] && string.Equals(c.Node?.AutomationId, item.Identifier, StringComparison.Ordinal)).ToList();
            if (byId.Count == 1)
                return (byId[0].Index, byId[0].NodePath, MatchConfidence.Exact);
        }

        if (!string.IsNullOrEmpty(item.Label))
        {
            var byLabel = candidates.Where(c => !used[c.Index] && NamesEqual(c.Name, item.Label)).ToList();
            if (byLabel.Count == 1)
                return (byLabel[0].Index, byLabel[0].NodePath, MatchConfidence.Likely);
            if (byLabel.Count > 1)
            {
                var expectedRole = RoleFromClassName(item.ClassName);
                var byRole = expectedRole is null ? [] : byLabel.Where(c => c.Role == expectedRole).ToList();
                var (pool, confidence) = byRole.Count == 1
                    ? (byRole, MatchConfidence.Likely)
                    : ((IReadOnlyList<Candidate>)byLabel, MatchConfidence.Weak);
                var nearest = pool.OrderBy(c => Math.Abs(c.Index - cursor)).First();
                return (nearest.Index, nearest.NodePath, confidence);
            }
        }

        // No usable label (an icon-only control) and no identifier match: fall back to position alone, never
        // jumping backward over content already matched or passed.
        var positional = candidates.Where(c => !used[c.Index] && c.Index >= cursor).OrderBy(c => c.Index).FirstOrDefault();
        return positional is null ? null : (positional.Index, positional.NodePath, MatchConfidence.Weak);
    }

    private static bool NamesEqual(string? a, string? b) => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A deliberately small, best-effort map from the Accessibility Inspector's "Class" field (the element's
    /// native UIKit/SwiftUI class, for example "UIButton") to Swipewalk's own normalized
    /// <see cref="AccessibilityNode.Role"/> vocabulary, used only to break a tie between several unused
    /// candidates that share the same label (see <see cref="FindMatch"/>). Not exhaustive -- SwiftUI content
    /// is often reported as "Static Text" or another generic class regardless of its real role (see
    /// KnownLimitations "ios-accessibility-element"); a class this doesn't recognize simply can't help
    /// disambiguate, which only means such a tie falls back to <see cref="MatchConfidence.Weak"/>, never a
    /// wrong answer presented as confident.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ClassRoles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["UIButton"] = "button",
        ["UIImageView"] = "image",
        ["UITextField"] = "textfield",
        ["UITextView"] = "textfield",
        ["UISwitch"] = "switch",
        ["UISlider"] = "slider",
    };

    private static string? RoleFromClassName(string? className) => className is null ? null : ClassRoles.GetValueOrDefault(className);
}
