using System.Text;

namespace Swipewalk.Core.Limitations;

/// <summary>Renders the limitations catalog as Markdown (docs/limitations.md), grouped by scope.</summary>
public static class LimitationsMarkdown
{
    public static string Render(IEnumerable<Limitation> limitations)
    {
        var md = new StringBuilder();
        md.AppendLine("# Known limitations");
        md.AppendLine();
        md.AppendLine("<!-- Generated from src/Swipewalk.Core/Limitations/KnownLimitations.cs.");
        md.AppendLine("     Regenerate: dotnet run --project src/Swipewalk.Cli -- limitations > docs/limitations.md -->");
        md.AppendLine();
        md.AppendLine("Swipewalk finds some accessibility issues automatically. This page lists what it cannot check or may get wrong, " +
                      "and what to test by hand instead. Each scan report shows the entries that apply to that scan.");
        md.AppendLine();
        md.AppendLine("- **Limitation**: something Swipewalk cannot check or may get wrong.");
        md.AppendLine("- **Framework note**: platform or framework behavior that affects accessibility.");

        foreach (var group in limitations.GroupBy(l => l.ScopeLabel))
        {
            md.AppendLine();
            md.AppendLine($"## {group.Key}");
            foreach (var l in group)
            {
                md.AppendLine();
                md.AppendLine($"### {l.Title}");
                md.AppendLine();
                var rules = l.Rules.Count == 0 ? "" : $" · rules: {string.Join(", ", l.Rules.Select(r => $"`{r}`"))}";
                md.AppendLine($"`{l.Id}` · {(l.Kind == LimitationKind.FrameworkNote ? "Framework note" : "Limitation")} · {l.Area}{rules}");
                md.AppendLine();
                md.AppendLine($"- **What:** {l.Description}");
                md.AppendLine($"- **Impact:** {l.Impact}");
                md.AppendLine($"- **Check manually:** {l.ManualCheck}");
                if (l.Planned is not null)
                    md.AppendLine($"- **Planned:** {l.Planned}");
            }
        }
        return md.ToString();
    }
}
