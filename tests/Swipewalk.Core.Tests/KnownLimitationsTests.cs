using Swipewalk.Core.Limitations;
using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;

namespace Swipewalk.Core.Tests;

public class KnownLimitationsTests
{
    [Fact]
    public void Ids_AreUnique()
    {
        var ids = KnownLimitations.All.Select(l => l.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void ReferencedRules_Exist()
    {
        var ruleIds = DefaultRules.All.Select(r => r.Id).ToHashSet();
        Assert.All(KnownLimitations.All.SelectMany(l => l.Rules), id => Assert.Contains(id, ruleIds));
    }

    [Fact]
    public void For_FiltersByPlatformAndFramework()
    {
        var androidMaui = KnownLimitations.For(Platform.Android, AppFramework.Maui).Select(l => l.Id).ToList();

        Assert.Contains("partial-wcag-coverage", androidMaui);
        Assert.Contains("maui-android-tap-gesture", androidMaui);
        Assert.DoesNotContain("ios-simulator-only", androidMaui);
        Assert.DoesNotContain("other-framework-fix-examples", androidMaui);
    }

    [Fact]
    public void DocsPage_IsUpToDate()
    {
        var docs = FindRepoFile("docs/limitations.md");
        var expected = LimitationsMarkdown.Render(KnownLimitations.All);

        Assert.True(docs is not null && File.ReadAllText(docs) == expected,
            "docs/limitations.md is out of date. Run: dotnet run --project src/Swipewalk.Cli -- limitations > docs/limitations.md");
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
