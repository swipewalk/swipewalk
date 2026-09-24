using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// Maps issues from platform engines (currently Apple's accessibility audit) to findings. Engine issues
/// are reported as needing review: the engines' thresholds and heuristics are not documented against WCAG.
/// Duplicates of Swipewalk findings are merged by <see cref="RuleRunner"/>.
/// </summary>
public sealed class EngineIssueRule : IRule
{
    public string Id => "engine";

    /// <summary>Issue types reported once per screen, listing the affected elements, instead of once per element.</summary>
    private static readonly HashSet<string> PerScreenTypes = ["dynamicType"];

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        foreach (var group in snapshot.EngineIssues.Where(i => PerScreenTypes.Contains(i.Type)).GroupBy(i => (i.Engine, i.Type)))
        {
            var (kind, criteria, guideline, explanation) = Map(group.Key.Type);
            var names = string.Join(", ", group.Select(i => $"\"{i.Label ?? "unnamed"}\""));
            yield return new Finding
            {
                RuleId = $"engine:{group.Key.Type}",
                Source = group.Key.Engine,
                Kind = kind,
                Message = $"{group.Key.Engine}: {group.First().Description} for {group.Count()} element(s): {names}. {explanation}",
                Criteria = criteria,
                PlatformGuideline = guideline,
                NodePath = "",
                Role = "screen",
            };
        }

        foreach (var issue in snapshot.EngineIssues.Where(i => !PerScreenTypes.Contains(i.Type)))
        {
            var (kind, criteria, guideline, explanation) = Map(issue.Type);
            yield return new Finding
            {
                RuleId = $"engine:{issue.Type}",
                Source = issue.Engine,
                Kind = kind,
                Message = $"{issue.Engine}: {issue.Description}. {explanation}".Trim(),
                Criteria = criteria,
                PlatformGuideline = guideline,
                NodePath = issue.NodePath ?? "",
                Role = "element",
                Label = issue.Label,
                Bounds = issue.Bounds,
            };
        }
    }

    /// <summary>One audit issue type this rule knows about, and what docs/checks.md shows for it (see
    /// <see cref="Catalog"/>). <see cref="What"/> is a plain-words description of what the check looks
    /// for, kept separate from <see cref="Map"/>'s per-finding "check this by hand" explanation text.</summary>
    public sealed record IssueMapping(string Type, string What, FindingKind Kind, IReadOnlyList<WcagCriterion> Criteria, string? PlatformGuideline);

    /// <summary>Every audit issue type <see cref="Map"/> maps by name, in the order docs/checks.md lists them
    /// (an audit type not in this list still gets a finding -- see <see cref="Map"/>'s default case -- just not
    /// its own row on that page).</summary>
    public static IReadOnlyList<IssueMapping> Catalog { get; } =
    [
        Describe("contrast", "Text contrast against its background, checked against Apple's own audit threshold."),
        Describe("sufficientElementDescription", "Whether an element has an accessible description for VoiceOver."),
        Describe("dynamicType", "Whether text supports the Dynamic Type (larger text) setting."),
        Describe("textClipped", "Whether text is cut off at the text size the screen was captured at (not the large-text rescan)."),
        Describe("trait", "Whether an element's accessibility traits (its role) match what it does."),
        Describe("hitRegion", "Whether an element has a hit area Apple's audit considers too small (Apple's guideline is 44×44 pt; the audit's own threshold isn't documented)."),
    ];

    private static IssueMapping Describe(string type, string what)
    {
        var (kind, criteria, guideline, _) = Map(type);
        return new IssueMapping(type, what, kind, criteria, guideline);
    }

    private static (FindingKind Kind, IReadOnlyList<WcagCriterion> Criteria, string? Guideline, string Explanation) Map(string type) => type switch
    {
        "contrast" => (FindingKind.NeedsReview, [WcagCriteria.ContrastMinimum],
            null, "Check the text against the 4.5:1 (normal) or 3:1 (large) minimum."),
        "sufficientElementDescription" => (FindingKind.NeedsReview, [WcagCriteria.NameRoleValue],
            null, "Check that the element has an accessible name that describes its purpose."),
        "dynamicType" => (FindingKind.NeedsReview, [WcagCriteria.ResizeText],
            null, "Check that text can be enlarged to 200% (for example with larger system text) without loss of content."),
        // Clipped at the current text size, which is not a resize failure; the large-text rescan covers 1.4.4.
        "textClipped" => (FindingKind.NeedsReview, [],
            null, "Check that no text is cut off. No WCAG criterion is mapped for clipping at the default text size."),
        "trait" => (FindingKind.NeedsReview, [WcagCriteria.NameRoleValue],
            null, "Check that the element's role (traits) matches what it does."),
        "hitRegion" => (FindingKind.PlatformAdvisory, [],
            "Apple Human Interface Guidelines: hit targets at least 44×44 pt", ""),
        _ => (FindingKind.NeedsReview, [],
            null, "Swipewalk has not mapped this audit type to a WCAG criterion; review it by hand."),
    };
}
