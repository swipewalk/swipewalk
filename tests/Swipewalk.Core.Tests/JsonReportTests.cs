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

    [Fact]
    public void SchemaVersion_Is0_4()
    {
        // 0.4 adds CapturedEvidenceSummary/CapturedEvidenceSource to ScreenCriterionReport (real screen-reader
        // evidence in per-screen coverage) -- this test is updated deliberately, not a surprise CI failure,
        // whenever the schema version changes.
        Assert.Equal("0.4", ScanReport.CurrentSchemaVersion);
    }

    [Fact]
    public void ScreenId_RoundTripsThroughJson()
    {
        // A real id, as RuleRunner.Run would assign one -- not the empty default (see
        // OldResultsJsonWithNoScreenId_GetsTheSameIdOnEveryLoad for that case).
        var originalId = Guid.NewGuid().ToString();
        var report = Report(AppFramework.Unknown, null) with { Screens = [Report(AppFramework.Unknown, null).Screens[0] with { ScreenId = originalId }] };

        var roundTripped = JsonReport.Deserialize(JsonReport.Serialize(report))!;

        Assert.Equal(originalId, roundTripped.Screens[0].ScreenId);
    }

    [Fact]
    public void OldResultsJsonWithNoScreenId_StillLoads_AndGetsAnId()
    {
        // A results.json saved before ScreenId existed has no "screenId" property at all.
        var json = """
            {
              "toolVersion": "test",
              "screens": [ { "platform": "iOS", "screenName": "Home", "findings": [] } ]
            }
            """;

        var report = JsonReport.Deserialize(json);

        Assert.NotNull(report);
        Assert.False(string.IsNullOrEmpty(report.Screens[0].ScreenId));
    }

    [Fact]
    public void OldResultsJsonWithNoScreenId_GetsTheSameIdOnEveryLoad()
    {
        // The real bug this guards against: a random per-load id would let a guided-check answer saved
        // against one load's id become permanently unreachable the next time the same old file is loaded (a
        // separate `swipewalk guide` process, or the report reopened later) -- the id must be deterministic
        // (by screen position) instead.
        var json = """
            {
              "toolVersion": "test",
              "screens": [
                { "platform": "iOS", "screenName": "Home", "findings": [] },
                { "platform": "iOS", "screenName": "Settings", "findings": [] }
              ]
            }
            """;

        var first = JsonReport.Deserialize(json)!;
        var second = JsonReport.Deserialize(json)!;

        Assert.Equal(first.Screens[0].ScreenId, second.Screens[0].ScreenId);
        Assert.Equal(first.Screens[1].ScreenId, second.Screens[1].ScreenId);
        Assert.NotEqual(first.Screens[0].ScreenId, first.Screens[1].ScreenId);
    }

    [Fact]
    public void ProposedNotApplicable_RoundTripsThroughJson()
    {
        var report = Report(AppFramework.Unknown, null) with
        {
            Screens = [Report(AppFramework.Unknown, null).Screens[0] with
            {
                ProposedNotApplicable = [new Swipewalk.Core.Coverage.ProposedNotApplicable("1.4.13", "no focusable node")],
            }],
        };

        var roundTripped = JsonReport.Deserialize(JsonReport.Serialize(report))!;

        var proposal = Assert.Single(roundTripped.Screens[0].ProposedNotApplicable);
        Assert.Equal("1.4.13", proposal.CriterionNumber);
        Assert.Equal("no focusable node", proposal.Reason);
    }
}
