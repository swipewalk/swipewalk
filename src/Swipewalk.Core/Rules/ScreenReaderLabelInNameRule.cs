using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// WCAG 2.5.3 Label in Name, checked against REAL screen-reader evidence (<see cref="ScreenSnapshot.ScreenReaderCapture"/>)
/// instead of the accessibility tree alone. <see cref="LabelInNameRule"/> already compares a node's own
/// <see cref="AccessibilityNode.Label"/> against its own <see cref="AccessibilityNode.VisibleText"/>; this
/// rule instead checks whether the name TalkBack actually spoke for that element contains its visible text --
/// the node's own (<see cref="AccessibilityNode.VisibleText"/>), or, when that is null, its only descendant's
/// visible text (see <see cref="VisibleText"/>). That second path is what lets this rule see a control
/// <see cref="LabelInNameRule"/> structurally cannot: one whose own <see cref="AccessibilityNode.VisibleText"/>
/// is null because the text is only on a child -- still true for a several-named-descendants shape
/// <c>Swipewalk.Collectors.Android.UiAutomatorParser.TryMergeDescendantName</c> doesn't merge (more than one
/// contentDescription candidate, more than one visible-text candidate, or a descendant carrying both
/// its own content-desc and visible text). The ONE such shape confirmed against real TalkBack evidence
/// (samples/NativeAndroid's Compose bug N5: <c>Modifier.semantics { contentDescription = "Submit" }</c> set
/// directly on the Button itself, not on an icon, alongside a separate <c>Text("Pay")</c> child) is merged by
/// that parser method as of 2026-09-26, so <see cref="LabelInNameRule"/> can now evaluate N5's own button
/// directly from the tree; this rule still adds independent, capture-based coverage on top of that merge (see
/// the remarks on "Doesn't duplicate" below) -- see docs/case-study.md and <c>android-compose-merged-name</c>
/// in docs/limitations.md for what real captures of that button did and didn't find.
///
/// Every finding is <see cref="FindingKind.NeedsReview"/>, never <see cref="FindingKind.WcagIssue"/>: 2.5.3
/// is about the accessible name a speech-input user (Android Voice Access, iOS Voice Control) relies on, and
/// what TalkBack actually said is evidence of that name -- not proof of it, since TalkBack is not itself
/// speech-input software, and the capture's own match between a screen-reader stop and a tree node
/// (bounds/order-based, see <see cref="MatchConfidence"/>) can be wrong.
///
/// Restricted to <see cref="ScreenReaderSource.TalkBack"/> for now (see the guard in <see cref="Evaluate"/>):
/// <see cref="ScreenReaderCaptureComparer.NamesMatch"/>'s whole-word, cross-language forgiveness -- which this
/// rule needs, since its own "predicted" side (a control's visible text) is never empty -- is only established
/// for TalkBack; for any other source it falls back to exact equality only, which would over-report on a real
/// name that legitimately contains extra words. iOS's Accessibility Inspector route now produces a
/// non-TalkBack <see cref="ScreenSnapshot.ScreenReaderCapture"/> (<c>ScreenReaderSource.AccessibilityInspector</c>),
/// so this guard is live, not just a safeguard for a future source.
///
/// Doesn't duplicate <see cref="LabelInNameRule"/>: when a node's OWN visible text is what's being checked
/// (not a descendant's) and it already has a <see cref="AccessibilityNode.Label"/> that fails to contain it,
/// <see cref="LabelInNameRule"/> already reports that exact 2.5.3 concern from the tree alone, so this rule
/// skips it there -- see <see cref="Evaluate"/>. It still checks independently when the tree alone says
/// nothing (no <see cref="AccessibilityNode.Label"/> at all, so <see cref="LabelInNameRule"/> never runs on
/// this node either way), including when the visible text is read from a descendant.
/// </summary>
public sealed class ScreenReaderLabelInNameRule : IRule
{
    public string Id => "screen-reader-label-in-name";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        // Bail entirely on an incomplete capture or one with nothing in it: a capture that hit a real
        // problem is too unreliable a basis for a per-element WCAG citation, even for the elements it did
        // reach, since where and why it stopped isn't accounted for here. For this rule's only real source
        // today (TalkBack, guarded below), "incomplete" means the per-capture element cap was reached or
        // TalkBack said nothing for one or more focused elements -- not "device disconnected, walk timed
        // out, order wrapped", which are the Accessibility Inspector route's own failure reasons (see
        // ScreenReaderCapture.Complete's remarks); this rule is TalkBack-only, so those never apply here.
        if (snapshot.ScreenReaderCapture is not { Items.Count: > 0, Complete: true } capture)
            yield break;

        // See this type's remarks: the matching this rule needs is only established for TalkBack.
        if (capture.Source != ScreenReaderSource.TalkBack)
            yield break;

        var itemsByPath = capture.Items
            .Where(i => i.MatchedNodePath is not null)
            .GroupBy(i => i.MatchedNodePath!)
            .ToDictionary(g => g.Key, g => g.First()); // MatchedNodePath is effectively unique per capture; First() is defensive.
        var tool = ScreenReaderCaptureComparer.ToolLabel(capture.Source);

        foreach (var (node, path) in snapshot.Root.DescendantsAndSelfWithPath())
        {
            if (!node.IsAccessible || !node.IsInteractive || !RuleFinding.HasArea(node) || node.Role == "textfield")
                continue;

            var ownVisibleText = string.IsNullOrWhiteSpace(node.VisibleText) ? null : node.VisibleText;
            var visible = ownVisibleText ?? SingleDescendantVisibleText(node);
            if (visible is null)
                continue; // no visible text on the node itself, and none, or more than one, on its descendants

            // Same "not really a text label" exceptions as LabelInNameRule: a bare toggle state or a single
            // symbol isn't visible text 2.5.3 is about.
            var normalized = LabelInNameRule.Normalize(visible);
            if (normalized.Length <= 1 || (node.Role is "switch" or "checkbox" && normalized is "on" or "off"))
                continue;

            // Avoid duplicating LabelInNameRule -- see this type's remarks.
            if (ownVisibleText is not null && node.Label is { } treeLabel
                && !LabelInNameRule.Normalize(treeLabel).Contains(normalized, StringComparison.Ordinal))
                continue;

            if (!itemsByPath.TryGetValue(path, out var item))
                continue; // this element wasn't walked by the capture at all

            // A weak match (order alone, on a busy screen) is too uncertain a basis for a 2.5.3 citation on
            // this specific element -- see ScreenReaderCaptureComparer's own reasoning for the same skip.
            if (item.MatchConfidence == MatchConfidence.Weak)
                continue;

            var (spokenName, _) = ScreenReaderCaptureComparer.ActualNameAndRole(item, capture.Source);
            if (ScreenReaderCaptureComparer.NamesMatch(visible, spokenName, capture.Source, capture.Language))
                continue;

            yield return new Finding
            {
                RuleId = Id,
                Kind = FindingKind.NeedsReview,
                Message = $"Visible text \"{visible}\" was not found in the name {tool} actually spoke for this " +
                          $"element ({Describe(item, spokenName)}). WCAG 2.5.3 Label in Name is about the " +
                          "accessible name a speech-input user relies on, and this is evidence of that name, " +
                          "not proof of it -- check by hand with speech input (Android Voice Access, iOS Voice Control).",
                Criteria = [WcagCriteria.LabelInName],
                NodePath = path,
                Role = node.Role,
                Label = node.Label ?? visible,
                Bounds = node.Bounds,
                Details = new Dictionary<string, string>
                {
                    ["visibleText"] = visible,
                    ["spokenName"] = spokenName ?? "",
                    ["tool"] = tool,
                },
            };
        }
    }

    /// <summary>
    /// The visible text of this node's only descendant that has any, when that's unambiguous -- deliberately
    /// mirroring the safety checks <c>Swipewalk.Collectors.Android.UiAutomatorParser.TryMergeDescendantName</c>
    /// uses for a related but different problem (merging a NAME onto a clickable node): (1) if anything else
    /// in the subtree is independently accessible and focusable or interactive, a screen reader may stop on
    /// it separately, so its text isn't necessarily read as part of THIS node -- too ambiguous to guess, so
    /// this returns null rather than borrow it (for example a card with a nested, separately-focusable
    /// "Delete" button: TalkBack focuses that button on its own and doesn't fold its text into the card);
    /// (2) a hidden or zero-size descendant's leftover text is not something a person actually sees, so it
    /// isn't treated as real visible text either. Among what's left, this needs exactly one DISTINCT
    /// non-blank text -- more than one (for example an icon's content description on one child alongside
    /// separate visible text on another, both under one control -- see docs/limitations.md's
    /// <c>android-compose-merged-name</c>, a different field but the same "don't guess with two candidates"
    /// reasoning) is left unresolved rather than picked at random.
    /// </summary>
    private static string? SingleDescendantVisibleText(AccessibilityNode node)
    {
        var descendants = node.Children.SelectMany(c => c.DescendantsAndSelf()).ToList();
        if (descendants.Any(d => d.IsAccessible && (d.IsInteractive || d.IsFocusable)))
            return null;

        var texts = descendants
            .Where(d => d.IsAccessible && RuleFinding.HasArea(d) && !string.IsNullOrWhiteSpace(d.VisibleText))
            .Select(d => d.VisibleText)
            .Distinct()
            .ToList();
        return texts.Count == 1 ? texts[0] : null;
    }

    /// <summary>Plain-words description of what this item reported, for the finding message. Prefers the
    /// verbatim spoken text (what a person actually heard, role words and all) over the parsed name, since
    /// the message is explaining what the tool said, not restating <paramref name="parsedName"/> a second
    /// time. Joins any utterances the item merges (see <see cref="ScreenReaderCaptureComparer.UtteranceJoinSeparator"/>)
    /// with a comma for readability -- the raw " || " join is an implementation detail, not something a
    /// person needs to see.</summary>
    private static string Describe(ScreenReaderCaptureItem item, string? parsedName)
    {
        if (!string.IsNullOrEmpty(item.SpokenText))
        {
            var readable = item.SpokenText.Replace(ScreenReaderCaptureComparer.UtteranceJoinSeparator, ", ");
            return $"it said \"{readable}\"";
        }
        var structured = item.Label ?? item.Value;
        if (!string.IsNullOrEmpty(structured))
            return $"it reported the name \"{structured}\"";
        return parsedName is null ? "it reported no name at all" : $"it reported \"{parsedName}\"";
    }
}
