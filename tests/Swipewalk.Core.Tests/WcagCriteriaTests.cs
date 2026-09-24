using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class WcagCriteriaTests
{
    [Fact]
    public void All_HasUniqueNumbers()
    {
        var numbers = WcagCriteria.All.Select(c => c.Number).ToList();

        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    [Fact]
    public void ToString_IncludesNumberNameAndLevel()
    {
        Assert.Equal("4.1.2 Name, Role, Value (A)", WcagCriteria.NameRoleValue.ToString());
    }
}
