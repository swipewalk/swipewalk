using Swipewalk.Collectors.Ios;

namespace Swipewalk.Core.Tests;

public class SigningPlanTests
{
    private const string Team = "A1B2C3D4E5";
    private const string Phone = "00000000-0000000000000000";
    private static readonly DateTime Now = new(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc);

    private static ProvisioningProfile Profile(
        string appId = "*", bool development = true, int daysLeft = 300, string[]? devices = null,
        bool xcodeManaged = false, bool allDevices = false) =>
        new("UUID-1", "Manual dev", Team, appId, development, Now.AddDays(daysLeft),
            (devices ?? [Phone]).ToHashSet(), allDevices, "/p", xcodeManaged);

    private static (SigningPlan? Plan, string? Problem) Plan(
        IReadOnlyList<ProvisioningProfile> installed, bool xcodeAccount = false, string? prefix = null) =>
        SigningPlan.Create(Team, null, prefix, Phone, installed, xcodeAccount, Now);

    [Fact]
    public void FittingManualProfile_IsUsedWithoutAppleId()
    {
        var (plan, _) = Plan([Profile()]);

        Assert.True(plan!.IsManual);
    }

    [Fact]
    public void XcodeManagedProfile_FallsBackToAutomaticSigning()
    {
        Assert.False(Plan([Profile(xcodeManaged: true)], xcodeAccount: true).Plan!.IsManual);
    }

    [Theory]
    [InlineData(false, 300, true, "not a development profile")]
    [InlineData(true, -1, true, "expired")]
    [InlineData(true, 300, false, "does not include this device")]
    public void UnusableProfile_WithoutAppleId_ExplainsWhy(bool development, int daysLeft, bool includesPhone, string expected)
    {
        var (plan, problem) = Plan([Profile(development: development, daysLeft: daysLeft, devices: includesPhone ? null : ["OTHER"])]);

        Assert.Null(plan);
        Assert.Contains(expected, problem);
    }

    [Fact]
    public void CompanyWildcard_NeedsMatchingBundlePrefix()
    {
        var company = Profile(appId: "com.company.*");

        Assert.Contains("does not cover", Plan([company]).Problem);
        Assert.True(Plan([company], prefix: "com.company.swipewalk").Plan!.IsManual);
    }

    [Fact]
    public void EnterpriseProfileForAllDevices_Fits()
    {
        Assert.True(Plan([Profile(devices: [], allDevices: true)]).Plan!.IsManual);
    }

    [Fact]
    public void ProfileCovers_WildcardsAndExactIds()
    {
        Assert.True(Profile(appId: "*").Covers("org.swipewalk.harness.runner"));
        Assert.True(Profile(appId: "org.swipewalk.*").Covers("org.swipewalk.harness.runner"));
        Assert.False(Profile(appId: "com.example.otherapp").Covers("org.swipewalk.harness.runner"));
    }
}
