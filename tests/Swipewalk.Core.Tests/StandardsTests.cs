using Swipewalk.Core.Model;
using Swipewalk.Core.Standards;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class StandardsTests
{
    private static Finding Finding(FindingKind kind, params WcagCriterion[] criteria) => new()
    {
        RuleId = "test",
        Kind = kind,
        Message = "",
        NodePath = "0",
        Role = "button",
        Criteria = criteria,
        PlatformGuideline = kind == FindingKind.PlatformAdvisory ? "guideline" : null,
    };

    [Fact]
    public void WcagTwoPointZeroCriterion_IsRelevantToAllStandards()
    {
        var relevant = KnownStandards.RelevantTo(Finding(FindingKind.WcagIssue, WcagCriteria.NameRoleValue));

        Assert.Equal(KnownStandards.All.Select(s => s.Id), relevant);
    }

    [Fact]
    public void WcagTwoPointOneCriterion_IsNotRelevantToSection508()
    {
        var relevant = KnownStandards.RelevantTo(Finding(FindingKind.WcagIssue, WcagCriteria.LabelInName));

        Assert.Equal(["ada-title-ii", "en-301-549", "en-301-549-v4", "uk-public-sector"], relevant);
    }

    [Fact]
    public void WcagTwoPointTwoCriterion_IsOnlyRelevantToWcagTwoPointTwoStandards()
    {
        var relevant = KnownStandards.RelevantTo(Finding(FindingKind.WcagIssue, WcagCriteria.TargetSizeMinimum));

        Assert.Equal(["en-301-549-v4", "uk-public-sector"], relevant);
    }

    [Fact]
    public void PlatformAdvisory_IsRelevantToNone()
    {
        Assert.Empty(KnownStandards.RelevantTo(Finding(FindingKind.PlatformAdvisory)));
    }

    [Fact]
    public void Ids_AreUnique_AndEveryStandardHasASource()
    {
        Assert.Equal(KnownStandards.All.Count, KnownStandards.All.Select(s => s.Id).Distinct().Count());
        Assert.All(KnownStandards.All, s => Assert.StartsWith("https://", s.Source));
    }

    [Fact]
    public void CriteriaExcludedForNonWebSoftware_AreNotRelevantTo508()
    {
        var bypassBlocks = new WcagCriterion("2.4.1", "Bypass Blocks", WcagLevel.A, WcagVersion.V2_0);
        var relevant = KnownStandards.RelevantTo(Finding(FindingKind.WcagIssue, bypassBlocks));

        Assert.DoesNotContain("section-508", relevant);
        Assert.DoesNotContain("en-301-549", relevant);
        Assert.Contains("ada-title-ii", relevant);
    }

    [Fact]
    public void DocsPage_IsUpToDate()
    {
        string? docs = null;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null && docs is null; dir = dir.Parent)
            docs = File.Exists(Path.Combine(dir.FullName, "docs/standards.md")) ? Path.Combine(dir.FullName, "docs/standards.md") : null;

        Assert.True(docs is not null && File.ReadAllText(docs) == StandardsMarkdown.Render(KnownStandards.All, DefaultRules.MappedCriteria),
            "docs/standards.md is out of date. Run: dotnet run --project src/Swipewalk.Cli -- standards > docs/standards.md");
    }

    [Fact]
    public void RuleSource_IsStaleOnlyAfterMaxAge()
    {
        var source = new RuleSource("X", "1", "2026-09", "https://example.org");

        Assert.False(source.IsStale(new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero), RuleSources.MaxAge));
        Assert.True(source.IsStale(new DateTimeOffset(2027, 10, 1, 0, 0, 0, TimeSpan.Zero), RuleSources.MaxAge));
    }

    [Fact]
    public void RuleSources_IncludeEveryStandard()
    {
        Assert.All(KnownStandards.All, s => Assert.Contains(RuleSources.All, r => r.Source == s.Source));
    }
}
