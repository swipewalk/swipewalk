using System.Text;
using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>Renders docs/checks.md: every automated check Swipewalk runs, Swipewalk's own rules
/// (<see cref="DefaultRules"/>, described by <see cref="RuleCatalog"/>) and the two wrapped platform engines
/// (Apple's accessibility audit via <see cref="EngineIssueRule.Catalog"/>, Google's Accessibility Test
/// Framework via <see cref="AtfIssueRule.Catalog"/>).</summary>
public static class ChecksMarkdown
{
    public static string Render(
        IReadOnlyList<RuleCoverage> coverage,
        IReadOnlyList<RuleCatalog.Entry> ownRules,
        IReadOnlyList<EngineIssueRule.IssueMapping> engineChecks,
        IReadOnlyList<AtfIssueRule.IssueMapping> atfChecks)
    {
        var md = new StringBuilder();
        md.AppendLine("# Automated checks");
        md.AppendLine();
        md.AppendLine("<!-- Generated from src/Swipewalk.Core/Rules/DefaultRules.cs, RuleCatalog.cs,");
        md.AppendLine("     EngineIssueRule.cs and AtfIssueRule.cs.");
        md.AppendLine("     Regenerate: dotnet run --project src/Swipewalk.Cli -- checks > docs/checks.md -->");
        md.AppendLine();
        md.AppendLine("This is every automated check Swipewalk runs: Swipewalk's own rules, plus the checks it " +
                      "reads from the platform accessibility engines it wraps (Apple's accessibility audit on " +
                      "iOS, Google's Accessibility Test Framework on Android). Each check tests only part of " +
                      "the WCAG criteria it's listed against -- see [docs/limitations.md](limitations.md). " +
                      "A screen with no findings has not been shown to meet WCAG or any law; most WCAG 2.2 " +
                      "success criteria have no automated check at all.");
        md.AppendLine();
        md.AppendLine("- **WCAG issue** -- a possible failure of the cited criteria.");
        md.AppendLine("- **Needs review** -- automated checks can't decide; a person reviews against the cited criteria.");
        md.AppendLine("- **Platform advisory** -- doesn't meet a platform guideline; not a WCAG failure.");

        md.AppendLine();
        md.AppendLine("## Swipewalk's own rules");
        md.AppendLine();
        md.AppendLine("| Rule | What it checks | Kind | WCAG criteria | Platforms |");
        md.AppendLine("|---|---|---|---|---|");
        var byId = coverage.ToDictionary(c => c.RuleId);
        foreach (var rule in ownRules)
        {
            var c = byId[rule.RuleId];
            md.AppendLine($"| `{c.RuleId}` | {c.Checks} | {rule.Kinds} | {Criteria(c.Criteria)} | {rule.Platforms} |");
        }

        md.AppendLine();
        md.AppendLine("## Apple's accessibility audit (iOS, wrapped by the `engine` rule)");
        md.AppendLine();
        md.AppendLine("Runs Apple's own `performAccessibilityAudit` through the XCUITest harness; Apple does not " +
                      "document its thresholds against WCAG, so audit issues are reported for review, except " +
                      "`hitRegion`, which is a platform advisory against Apple's 44×44 pt guideline.");
        md.AppendLine();
        AppendEngineTable(md, engineChecks, "Audit issue type", "iOS");

        md.AppendLine();
        md.AppendLine("## Google's Accessibility Test Framework (Android, wrapped by the `atf` rule)");
        md.AppendLine();
        md.AppendLine("Runs ATF 4.1.1's 14 checks (one of which, UnexposedTextCheck, never reports anything, " +
                      "and one, TextSizeCheck, has reported on some devices but not others; see below) " +
                      "through the Android instrumentation harness (harness/android); needs the harness to have " +
                      "installed and run (see \"Google's Accessibility Test Framework needs the instrumentation " +
                      "harness to install and run\" in [docs/limitations.md](limitations.md)). Most checks are " +
                      "reported for review, the same reasoning as the Apple audit above.");
        md.AppendLine();
        AppendEngineTable(md, atfChecks, "ATF check", "Android (needs the instrumentation harness)");

        return md.ToString();
    }

    private static void AppendEngineTable(
        StringBuilder md, IReadOnlyList<EngineIssueRule.IssueMapping> checks, string firstColumn, string platforms)
    {
        md.AppendLine($"| {firstColumn} | What it checks | Kind | WCAG criteria or platform guideline | Platforms |");
        md.AppendLine("|---|---|---|---|---|");
        foreach (var check in checks)
            md.AppendLine($"| `{check.Type}` | {check.What} | {Kind(check.Kind)} | {CriteriaOrGuideline(check.Criteria, check.PlatformGuideline)} | {platforms} |");
    }

    private static void AppendEngineTable(
        StringBuilder md, IReadOnlyList<AtfIssueRule.IssueMapping> checks, string firstColumn, string platforms)
    {
        md.AppendLine($"| {firstColumn} | What it checks | Kind | WCAG criteria or platform guideline | Platforms |");
        md.AppendLine("|---|---|---|---|---|");
        foreach (var check in checks)
            md.AppendLine($"| `{check.CheckName}` | {check.What} | {Kind(check.Kind)} | {CriteriaOrGuideline(check.Criteria, check.PlatformGuideline)} | {platforms} |");
    }

    private static string Kind(FindingKind kind) => kind switch
    {
        FindingKind.WcagIssue => "WCAG issue",
        FindingKind.NeedsReview => "Needs review",
        _ => "Platform advisory",
    };

    private static string Criteria(IReadOnlyList<WcagCriterion> criteria) =>
        criteria.Count == 0 ? "No WCAG criterion mapped" : string.Join("; ", criteria);

    private static string CriteriaOrGuideline(IReadOnlyList<WcagCriterion> criteria, string? guideline)
    {
        if (criteria.Count > 0)
            return string.Join("; ", criteria);
        return guideline is null ? "No WCAG criterion mapped" : guideline;
    }
}
