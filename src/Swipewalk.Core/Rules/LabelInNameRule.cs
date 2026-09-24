using System.Text;
using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// WCAG 2.5.3: when a control shows text and also has an explicit accessible name, the name must
/// contain the visible text so speech-input users can say what they see.
/// </summary>
public sealed class LabelInNameRule : IRule
{
    public string Id => "label-in-name";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        foreach (var (node, path) in snapshot.Root.DescendantsAndSelfWithPath())
        {
            if (!node.IsAccessible || !node.IsInteractive || !RuleFinding.HasArea(node) || node.Role == "textfield"
                || node.Label is not { } label || node.VisibleText is not { } visible)
                continue;

            // State text on toggles and single symbols ("X", "×") are not text labels for 2.5.3.
            var normalized = Normalize(visible);
            if (normalized.Length <= 1
                || (node.Role is "switch" or "checkbox" && normalized is "on" or "off"))
                continue;

            if (!Normalize(label).Contains(normalized, StringComparison.Ordinal))
                yield return RuleFinding.Create(Id, FindingKind.WcagIssue,
                    $"Visible text \"{visible}\" is not part of the accessible name \"{label}\". " +
                    $"A speech-input user saying \"{visible}\" may not be able to activate it.",
                    node, path, [WcagCriteria.LabelInName]) with
                {
                    Details = new Dictionary<string, string> { ["visibleText"] = visible, ["accessibleName"] = label },
                };
        }
    }

    internal static string Normalize(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
