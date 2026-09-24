using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;

namespace Swipewalk.Core.Tests;

public class JsonReportTests
{
    private static ScanReport Report(AppFramework framework, string? frameworkVersion) => new()
    {
        ToolVersion = "test",
        Screens =
        [
            new ScreenResult
            {
                Platform = Platform.iOS,
                ScreenName = "Home",
                Framework = framework,
                FrameworkVersion = frameworkVersion,
                Findings = [],
            },
        ],
    };

    [Fact]
    public void FrameworkVersion_RoundTripsThroughJson()
    {
        var json = JsonReport.Serialize(Report(AppFramework.Maui, "10.0.60"));
        var roundTripped = JsonReport.Deserialize(json)!;

        Assert.Equal("10.0.60", roundTripped.Screens[0].FrameworkVersion);
        Assert.Equal(AppFramework.Maui, roundTripped.Screens[0].Framework);
    }

    [Fact]
    public void FrameworkVersion_Null_IsOmittedFromJson()
    {
        var json = JsonReport.Serialize(Report(AppFramework.Unknown, null));

        Assert.DoesNotContain("frameworkVersion", json);
    }

    [Fact]
    public void FrameworkVersion_Null_RoundTripsAsNull()
    {
        var roundTripped = JsonReport.Deserialize(JsonReport.Serialize(Report(AppFramework.Unknown, null)))!;

        Assert.Null(roundTripped.Screens[0].FrameworkVersion);
    }

    [Fact]
    public void AppId_RoundTripsThroughJson()
    {
        var json = JsonReport.Serialize(Report(AppFramework.Unknown, null) with { AppId = "org.example.app" });
        var roundTripped = JsonReport.Deserialize(json)!;

        Assert.Equal("org.example.app", roundTripped.AppId);
    }

    [Fact]
    public void AppId_Null_IsOmittedFromJson()
    {
        var json = JsonReport.Serialize(Report(AppFramework.Unknown, null));

        Assert.DoesNotContain("appId", json);
    }

    [Fact]
    public void AppId_Null_RoundTripsAsNull()
    {
        var roundTripped = JsonReport.Deserialize(JsonReport.Serialize(Report(AppFramework.Unknown, null)))!;

        Assert.Null(roundTripped.AppId);
    }
}
