using Swipewalk.Core.Model;

namespace Swipewalk.Core.Tests;

public class FrameworkVersionsTests
{
    [Fact]
    public void Parse_PlainVersion_Parses()
    {
        Assert.Equal(new Version(10, 0, 60), FrameworkVersions.Parse("10.0.60"));
    }

    [Fact]
    public void Parse_WithCommitSuffix_StripsItFirst()
    {
        Assert.Equal(new Version(10, 0, 60), FrameworkVersions.Parse("10.0.60+bf6156897c887d33dcd40592db3bcb6471916e03"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    [InlineData("10.0.100-rc.1")]
    public void Parse_UnparsableOrMissing_ReturnsNull(string? version)
    {
        Assert.Null(FrameworkVersions.Parse(version));
    }

    [Fact]
    public void MauiLiveTextSizeFix_Is10_0_100()
    {
        Assert.Equal(new Version(10, 0, 100), FrameworkVersions.MauiLiveTextSizeFix);
    }

    [Fact]
    public void Parse_VersionBeforeFix_ComparesLess()
    {
        Assert.True(FrameworkVersions.Parse("10.0.99") < FrameworkVersions.MauiLiveTextSizeFix);
    }

    [Fact]
    public void Parse_VersionAtFix_ComparesEqual()
    {
        Assert.True(FrameworkVersions.Parse("10.0.100") >= FrameworkVersions.MauiLiveTextSizeFix);
    }

    [Fact]
    public void Parse_VersionAfterFix_ComparesGreater()
    {
        Assert.True(FrameworkVersions.Parse("10.0.101") >= FrameworkVersions.MauiLiveTextSizeFix);
    }
}
