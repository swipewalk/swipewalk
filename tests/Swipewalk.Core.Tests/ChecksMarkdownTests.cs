using Swipewalk.Core.Rules;

namespace Swipewalk.Core.Tests;

public class ChecksMarkdownTests
{
    [Fact]
    public void EveryOwnRuleInDefaultRules_HasACatalogEntry()
    {
        // DefaultRules.All has 19 rules: the 17 RuleCatalog.OwnRules describes, plus "engine" and "atf",
        // which get their own detail tables (EngineIssueRule.Catalog, AtfIssueRule.Catalog) instead of a
        // RuleCatalog row -- see ChecksMarkdown.
        var ruleIds = DefaultRules.All.Select(r => r.Id).ToHashSet();
        var ownIds = RuleCatalog.OwnRules.Select(r => r.RuleId).ToHashSet();

        Assert.All(RuleCatalog.OwnRules, r => Assert.Contains(r.RuleId, ruleIds));
        Assert.Equal(ruleIds.Except(["engine", "atf"]).OrderBy(id => id), ownIds.OrderBy(id => id));
    }

    [Fact]
    public void EveryCatalogEntry_HasARuleCoverageEntry()
    {
        var coverageIds = DefaultRules.Coverage.Select(c => c.RuleId).ToHashSet();
        Assert.All(RuleCatalog.OwnRules, r => Assert.Contains(r.RuleId, coverageIds));
    }

    [Fact]
    public void EngineCatalog_CoversTheTypesEngineIssueRuleMaps()
    {
        // These type strings are duplicated from EngineIssueRule.Map's switch cases deliberately (Map is
        // private): if a case is added or renamed there without updating EngineIssueRule.Catalog, this
        // test -- not just a stale doc -- catches it.
        string[] knownTypes = ["contrast", "sufficientElementDescription", "dynamicType", "textClipped", "trait", "hitRegion"];
        Assert.Equal(knownTypes, EngineIssueRule.Catalog.Select(c => c.Type));
    }

    [Fact]
    public void AtfCatalog_CoversAllFourteenAtfChecks()
    {
        Assert.Equal(14, AtfIssueRule.Catalog.Count);
        Assert.Equal(AtfIssueRule.Catalog.Select(c => c.CheckName).Distinct().Count(), AtfIssueRule.Catalog.Count);
    }

    [Fact]
    public void DocsPage_IsUpToDate()
    {
        var docs = FindRepoFile("docs/checks.md");
        var expected = ChecksMarkdown.Render(DefaultRules.Coverage, RuleCatalog.OwnRules, EngineIssueRule.Catalog, AtfIssueRule.Catalog);

        Assert.True(docs is not null && File.ReadAllText(docs) == expected,
            "docs/checks.md is out of date. Run: dotnet run --project src/Swipewalk.Cli -- checks > docs/checks.md");
    }

    private static string? FindRepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
