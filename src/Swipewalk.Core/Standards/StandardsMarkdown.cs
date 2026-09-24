using System.Text;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Standards;

/// <summary>Renders docs/standards.md: the standards table and which checked criteria each includes.</summary>
public static class StandardsMarkdown
{
    public static string Render(IEnumerable<Standard> standards, IEnumerable<WcagCriterion> criteria)
    {
        var list = standards.ToList();
        var md = new StringBuilder();
        md.AppendLine("# Standards and laws");
        md.AppendLine();
        md.AppendLine("<!-- Generated from src/Swipewalk.Core/Standards/KnownStandards.cs.");
        md.AppendLine("     Regenerate: dotnet run --project src/Swipewalk.Cli -- standards > docs/standards.md -->");
        md.AppendLine();
        md.AppendLine(KnownStandards.Disclaimer);
        md.AppendLine();
        md.AppendLine("| Standard | Jurisdiction | Based on | Applies to | Checked |");
        md.AppendLine("|---|---|---|---|---|");
        foreach (var s in list)
            md.AppendLine($"| [{s.Name}]({s.Source}) (`{s.Id}`) | {s.Jurisdiction} | {s.Basis} | {s.AppliesTo} | {s.CheckedOn} |");

        md.AppendLine();
        md.AppendLine($"## Rule sources (ruleset {RuleSources.RulesetVersion})");
        md.AppendLine();
        md.AppendLine("Reports record these versions. Later changes to WCAG, laws or platform guidelines are not reflected until " +
                      "Swipewalk is updated; reports warn when a mapping was last reviewed more than a year earlier.");
        md.AppendLine();
        md.AppendLine("| Source | Version | Mapping reviewed |");
        md.AppendLine("|---|---|---|");
        foreach (var r in RuleSources.All)
            md.AppendLine($"| [{r.Name}]({r.Source}) | {r.Version} | {r.CheckedOn} |");

        md.AppendLine();
        md.AppendLine("## Exceptions and requirements beyond WCAG (not checked)");
        md.AppendLine();
        foreach (var s in list.Where(s => s.BeyondWcag is not null))
            md.AppendLine($"- **{s.Name}:** {s.BeyondWcag}");

        md.AppendLine();
        md.AppendLine("## Criteria Swipewalk maps findings to");
        md.AppendLine();
        md.AppendLine($"| Criterion | Since | {string.Join(" | ", list.Select(s => s.Id))} |");
        md.AppendLine($"|---|---|{string.Concat(list.Select(_ => "---|"))}");
        foreach (var c in criteria)
            md.AppendLine($"| {c} | WCAG {c.Since.Display()} | {string.Join(" | ", list.Select(s => s.Includes(c) ? "yes" : "—"))} |");
        return md.ToString();
    }
}
