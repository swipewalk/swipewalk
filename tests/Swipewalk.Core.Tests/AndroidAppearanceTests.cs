using Swipewalk.Collectors.Android;
using Swipewalk.Core.Reports;

namespace Swipewalk.Core.Tests;

public class AndroidAppearanceTests
{
    [Theory]
    [InlineData("Night mode: yes", AppearanceLabels.Dark)]
    [InlineData("Night mode: no", AppearanceLabels.Light)]
    [InlineData("Night mode: custom", AppearanceLabels.Light)]
    public void Parse_ReadsCmdUimodeNightOutput(string output, string expected) =>
        Assert.Equal(expected, AndroidAppearance.Parse(output));
}
