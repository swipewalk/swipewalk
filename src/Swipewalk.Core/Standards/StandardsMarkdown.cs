using System.Text;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Standards;

/// <summary>Renders docs/standards.md: the standards table and which checked criteria each includes.</summary>
public static class StandardsMarkdown
{
    public static string Render(IEnumerable<Standard> standards, IEnumerable<WcagCriterion> criteria) =>
        Render(standards, criteria, KnownBeyondWcagClauses.All);

    public static string Render(IEnumerable<Standard> standards, IEnumerable<WcagCriterion> criteria, IEnumerable<BeyondWcagClause> beyondWcagClauses)
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

        var beyondWcagList = beyondWcagClauses.ToList();
        if (beyondWcagList.Count > 0)
        {
            md.AppendLine();
            md.AppendLine("## Beyond-WCAG clauses");
            md.AppendLine();
            md.AppendLine("Individual clauses behind the summaries above: what each requires, whether it normally applies to a " +
                          "typical native app, and how Swipewalk could check it (or why it can't). Every clause's per-run status " +
                          "in a report never claims the app meets it -- see the report's own \"Beyond WCAG\" section.");
            md.AppendLine();
            md.AppendLine(KnownBeyondWcagClauses.SkippedNote);
            foreach (var s in list)
            {
                var clauses = beyondWcagList.Where(c => c.StandardId == s.Id).ToList();
                if (clauses.Count == 0)
                    continue;
                md.AppendLine();
                md.AppendLine($"### {s.Name}");
                md.AppendLine();
                md.AppendLine("| Clause | Applies | How Swipewalk could check |");
                md.AppendLine("|---|---|---|");
                foreach (var c in clauses)
                    md.AppendLine($"| {c.ClauseLabel} | {Applies(c)} | {CheckMethodText(c)} |");
            }
        }

        md.AppendLine();
        md.AppendLine("## Criteria Swipewalk maps findings to");
        md.AppendLine();
        md.AppendLine($"| Criterion | Since | {string.Join(" | ", list.Select(s => s.Id))} |");
        md.AppendLine($"|---|---|{string.Concat(list.Select(_ => "---|"))}");
        foreach (var c in criteria)
            md.AppendLine($"| {c} | WCAG {c.Since.Display()} | {string.Join(" | ", list.Select(s => s.Includes(c) ? "yes" : "—"))} |");
        return md.ToString();
    }

    private static string Applies(BeyondWcagClause c) => c.Applicability switch
    {
        BeyondWcagApplicability.Always => "Yes",
        BeyondWcagApplicability.Conditional => $"Conditional -- only when {c.ConditionDescription}",
        BeyondWcagApplicability.PlatformOrOrganizational => "No (platform, hardware, documentation or support services, not the app)",
        _ => throw new ArgumentOutOfRangeException(nameof(c), c.Applicability, "Unhandled BeyondWcagApplicability."),
    };

    private static string CheckMethodText(BeyondWcagClause c) => c.CheckMethod switch
    {
        BeyondWcagCheckMethod.PartlyAutomated => Cell(c.Explanation),
        BeyondWcagCheckMethod.Guided => Cell(c.GuidedSteps.Count > 0
            ? $"{c.Explanation} {string.Join(" ", c.GuidedSteps.Select((s, i) => $"{i + 1}. {s}"))}"
            : c.Explanation),
        BeyondWcagCheckMethod.NotTestable => Cell($"Not testable by Swipewalk: {c.Explanation}"),
        _ => throw new ArgumentOutOfRangeException(nameof(c), c.CheckMethod, "Unhandled BeyondWcagCheckMethod."),
    };

    /// <summary>Escapes a markdown table cell's pipe characters, so a clause's text (none currently contains
    /// one, but a future entry might) can't break the table layout.</summary>
    private static string Cell(string text) => text.Replace("|", "\\|");
}
