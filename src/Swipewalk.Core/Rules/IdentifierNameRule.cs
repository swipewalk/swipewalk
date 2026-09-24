using System.Text.RegularExpressions;
using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// Accessible names that look like developer identifiers ("btnSubmit", "img_email_receipt"), often
/// copied from an AutomationId or resource name, are read aloud verbatim by screen readers.
/// </summary>
public sealed partial class IdentifierNameRule : IRule
{
    public string Id => "identifier-name";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        foreach (var (node, path) in snapshot.Root.DescendantsAndSelfWithPath())
        {
            if (!node.IsAccessible || !RuleFinding.HasArea(node) || node.Label is not { } label
                || !LooksLikeIdentifier(label, node.AutomationId))
                continue;

            // The pattern is a guess, and a name that reads like code can still describe the purpose
            // ("img_email_receipt"), so every finding needs review. A "label" in WCAG is presented to all
            // users (2.4.6); a name only exposed to assistive technology is covered by 1.1.1 when the
            // control's content is non-text. Otherwise no criterion applies: 4.1.2 only requires a name.
            var shownOnScreen = node.VisibleText is { } visible
                && LabelInNameRule.Normalize(visible) == LabelInNameRule.Normalize(label);
            var (criteria, consequence) = shownOnScreen
                ? ((IReadOnlyList<WcagCriterion>)[WcagCriteria.HeadingsAndLabels],
                    "Check that the visible label describes the element's purpose.")
                : RuleFinding.IsImageBased(node)
                    ? ([WcagCriteria.NonTextContent],
                        "Check that the text alternative of this image-based control describes its purpose.")
                    : ([],
                        "Check whether the name describes the element. No WCAG criterion is mapped: this is a quality concern rather than a failure.");
            yield return RuleFinding.Create(Id, FindingKind.NeedsReview,
                $"Accessible name \"{label}\" looks like a developer identifier, and screen readers read it verbatim. {consequence}",
                node, path, criteria);
        }
    }

    internal static bool LooksLikeIdentifier(string name, string? automationId)
    {
        name = name.Trim();
        if (name.Contains(' '))
            return false;
        if (SnakeCase().IsMatch(name) || PrefixedCamelCase().IsMatch(name))
            return true;
        // Equal to the AutomationId only counts when it also reads like code ("SaveButton", not "Submit").
        return automationId is not null && string.Equals(name, automationId, StringComparison.OrdinalIgnoreCase)
            && CamelHump().IsMatch(name);
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*(_[A-Za-z0-9]+)+$")]
    private static partial Regex SnakeCase();

    [GeneratedRegex(@"[a-z][A-Z]")]
    private static partial Regex CamelHump();

    [GeneratedRegex(@"^(btn|img|lbl|txt|ic|icon|iv|tv|et|button|label|image)[A-Z0-9_][A-Za-z0-9_]*$")]
    private static partial Regex PrefixedCamelCase();
}
