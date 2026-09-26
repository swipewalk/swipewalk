using Swipewalk.Core.Coverage;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Standards;

namespace Swipewalk.Core.Tests;

public class BeyondWcagCoverageTests
{
    private static ScreenResult Screen(string? largeTextSetting = null, string? otherAppearance = null) => new()
    {
        Platform = Platform.Android,
        ScreenName = "Home",
        Findings = [],
        LargeTextSetting = largeTextSetting,
        OtherAppearance = otherAppearance,
    };

    private static BeyondWcagClause Clause(
        BeyondWcagApplicability applicability = BeyondWcagApplicability.Always,
        BeyondWcagCheckMethod method = BeyondWcagCheckMethod.PartlyAutomated,
        BeyondWcagEvidenceSignal evidence = BeyondWcagEvidenceSignal.None,
        string? condition = null) =>
        new("test-standard", "1.1", "Test clause", "A test clause.", applicability, condition, method,
            "How to check it.", [], evidence, "https://example.org", "2026-09");

    // --- ComputeRunStatus ---

    [Fact]
    public void PlatformOrOrganizational_IsAlwaysNotTestable_RegardlessOfScreens()
    {
        var clause = Clause(BeyondWcagApplicability.PlatformOrOrganizational, BeyondWcagCheckMethod.NotTestable);

        Assert.Equal(BeyondWcagRunStatus.NotTestableBySwipewalk, clause.ComputeRunStatus([]));
        Assert.Equal(BeyondWcagRunStatus.NotTestableBySwipewalk, clause.ComputeRunStatus([Screen(largeTextSetting: "200%")]));
    }

    [Fact]
    public void Conditional_IsAlwaysNotTested_NoDetectorExistsYet()
    {
        var clause = Clause(BeyondWcagApplicability.Conditional, BeyondWcagCheckMethod.PartlyAutomated,
            BeyondWcagEvidenceSignal.None, condition: "the app plays video");

        Assert.Equal(BeyondWcagRunStatus.NotTested, clause.ComputeRunStatus([]));
        Assert.Equal(BeyondWcagRunStatus.NotTested, clause.ComputeRunStatus([Screen(largeTextSetting: "200%")]));
    }

    [Fact]
    public void AlwaysGuided_IsAlwaysGuided_RegardlessOfScreens()
    {
        var clause = Clause(BeyondWcagApplicability.Always, BeyondWcagCheckMethod.Guided);

        Assert.Equal(BeyondWcagRunStatus.Guided, clause.ComputeRunStatus([]));
        Assert.Equal(BeyondWcagRunStatus.Guided, clause.ComputeRunStatus([Screen()]));
    }

    [Fact]
    public void AlwaysNotTestable_IsAlwaysNotTestable()
    {
        var clause = Clause(BeyondWcagApplicability.Always, BeyondWcagCheckMethod.NotTestable);

        Assert.Equal(BeyondWcagRunStatus.NotTestableBySwipewalk, clause.ComputeRunStatus([Screen()]));
    }

    [Fact]
    public void AlwaysPartlyAutomated_WithTreeRolesAndNames_RunsWheneverAScreenWasScanned()
    {
        var clause = Clause(BeyondWcagApplicability.Always, BeyondWcagCheckMethod.PartlyAutomated, BeyondWcagEvidenceSignal.TreeRolesAndNames);

        Assert.Equal(BeyondWcagRunStatus.NotTested, clause.ComputeRunStatus([]));
        Assert.Equal(BeyondWcagRunStatus.PartlyAutomated, clause.ComputeRunStatus([Screen()]));
    }

    [Fact]
    public void AlwaysPartlyAutomated_WithUserPreferenceRescan_NeedsLargeTextOrAppearanceEvidence()
    {
        var clause = Clause(BeyondWcagApplicability.Always, BeyondWcagCheckMethod.PartlyAutomated, BeyondWcagEvidenceSignal.UserPreferenceRescan);

        Assert.Equal(BeyondWcagRunStatus.NotTested, clause.ComputeRunStatus([Screen()]));
        Assert.Equal(BeyondWcagRunStatus.PartlyAutomated, clause.ComputeRunStatus([Screen(largeTextSetting: "200%")]));
        Assert.Equal(BeyondWcagRunStatus.PartlyAutomated, clause.ComputeRunStatus([Screen(otherAppearance: "dark")]));
    }

    // --- Display wording: never a compliance verdict, matches the CoverageDisplay vocabulary ---

    [Fact]
    public void Display_PartlyAutomated_NeverSaysAutomatedAlone()
    {
        var clause = Clause(BeyondWcagApplicability.Always, BeyondWcagCheckMethod.PartlyAutomated, BeyondWcagEvidenceSignal.TreeRolesAndNames);

        var label = BeyondWcagDisplay.For(clause, BeyondWcagRunStatus.PartlyAutomated, [Screen()]);

        Assert.StartsWith("Partly checked by automation:", label);
        Assert.DoesNotContain("Automated:", label);
    }

    [Fact]
    public void Display_PartlyAutomated_ForUserPreferenceRescan_NamesTheRescanThatActuallyRan()
    {
        var clause = Clause(BeyondWcagApplicability.Always, BeyondWcagCheckMethod.PartlyAutomated, BeyondWcagEvidenceSignal.UserPreferenceRescan);

        var largeTextOnly = BeyondWcagDisplay.For(clause, BeyondWcagRunStatus.PartlyAutomated, [Screen(largeTextSetting: "200%")]);
        var appearanceOnly = BeyondWcagDisplay.For(clause, BeyondWcagRunStatus.PartlyAutomated, [Screen(otherAppearance: "dark")]);
        var both = BeyondWcagDisplay.For(clause, BeyondWcagRunStatus.PartlyAutomated, [Screen(largeTextSetting: "200%", otherAppearance: "dark")]);

        Assert.Contains("the large-text rescan ran", largeTextOnly);
        Assert.DoesNotContain("appearance rescan ran", largeTextOnly);
        Assert.Contains("the dark/light appearance rescan ran", appearanceOnly);
        Assert.DoesNotContain("large-text rescan ran", appearanceOnly);
        Assert.Contains("both ran", both);
    }

    [Fact]
    public void Display_Guided_IncludesNumberedSteps()
    {
        var clause = new BeyondWcagClause("test-standard", "1.1", "Test", "Summary", BeyondWcagApplicability.Always,
            null, BeyondWcagCheckMethod.Guided, "Context.", ["Do the first thing.", "Do the second thing."],
            BeyondWcagEvidenceSignal.None, "https://example.org", "2026-09");

        var label = BeyondWcagDisplay.For(clause, BeyondWcagRunStatus.Guided, []);

        Assert.Contains("1. Do the first thing.", label);
        Assert.Contains("2. Do the second thing.", label);
    }

    [Fact]
    public void Display_NotTested_MentionsTheConditionAndIncludesGuidedSteps()
    {
        var clause = new BeyondWcagClause("test-standard", "1.1", "Test clause", "A test clause.",
            BeyondWcagApplicability.Conditional, "the app plays video with synchronized audio",
            BeyondWcagCheckMethod.PartlyAutomated, "How to check it.", ["Do the first thing."],
            BeyondWcagEvidenceSignal.None, "https://example.org", "2026-09");

        var label = BeyondWcagDisplay.For(clause, BeyondWcagRunStatus.NotTested, []);

        Assert.StartsWith("Not tested:", label);
        Assert.Contains("the app plays video with synchronized audio", label);
        Assert.Contains("1. Do the first thing.", label);
    }

    [Fact]
    public void Display_NotApplicableHere_IsWiredEvenThoughNoShippedClauseReachesItYet()
    {
        // No clause in KnownBeyondWcagClauses.All resolves to NotApplicableHere today (see
        // BeyondWcagCoverageExtensions.ComputeRunStatus): no per-clause feature detector exists yet. This
        // proves the status and its wording are wired correctly, ready for when one is added.
        var clause = Clause(BeyondWcagApplicability.Conditional, BeyondWcagCheckMethod.PartlyAutomated,
            BeyondWcagEvidenceSignal.None, condition: "the app plays video");

        var label = BeyondWcagDisplay.For(clause, BeyondWcagRunStatus.NotApplicableHere, []);

        Assert.StartsWith("Not applicable here:", label);
    }

    [Fact]
    public void NoShippedClause_EverComputesToNotApplicableHere()
    {
        var screens = new[] { Screen(largeTextSetting: "200%"), Screen(otherAppearance: "dark") };
        Assert.All(KnownBeyondWcagClauses.All, c => Assert.NotEqual(BeyondWcagRunStatus.NotApplicableHere, c.ComputeRunStatus(screens)));
    }

    // --- BeyondWcagCoverageReport ---

    [Fact]
    public void Build_ReturnsOnlyClausesForTheRequestedStandard_InCatalogOrder()
    {
        var entries = BeyondWcagCoverageReport.Build("section-508", [Screen()]);

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.Equal("section-508", e.StandardId));
        Assert.Equal(
            KnownBeyondWcagClauses.All.Where(c => c.StandardId == "section-508").Select(c => c.ClauseNumber),
            entries.Select(e => e.ClauseNumber));
    }

    [Fact]
    public void BuildAll_OmitsStandardsWithNoBeyondWcagClauses()
    {
        var coverage = BeyondWcagCoverageReport.BuildAll(KnownStandards.All, [Screen()]);

        Assert.DoesNotContain(coverage, sc => sc.StandardId == "ada-title-ii");
        Assert.DoesNotContain(coverage, sc => sc.StandardId == "uk-public-sector");
        Assert.Contains(coverage, sc => sc.StandardId == "section-508");
        Assert.Contains(coverage, sc => sc.StandardId == "en-301-549");
        Assert.Contains(coverage, sc => sc.StandardId == "en-301-549-v4");
    }

    [Fact]
    public void NoEntry_EverHasAnEmptyLabel()
    {
        var coverage = BeyondWcagCoverageReport.BuildAll(KnownStandards.All, [Screen()]);

        Assert.All(coverage, sc => Assert.All(sc.Clauses, e => Assert.False(string.IsNullOrWhiteSpace(e.Label))));
    }

    [Fact]
    public void NoEntry_EverContainsAComplianceVerdictWord()
    {
        // Two runs so both the "evidence ran" and "evidence didn't run" wordings for Always/PartlyAutomated
        // clauses get exercised, alongside every Guided/NotTestable/Conditional label.
        var withEvidence = BeyondWcagCoverageReport.BuildAll(KnownStandards.All, [Screen(largeTextSetting: "200%", otherAppearance: "dark")]);
        var withoutEvidence = BeyondWcagCoverageReport.BuildAll(KnownStandards.All, [Screen()]);

        foreach (var coverage in new[] { withEvidence, withoutEvidence })
            foreach (var sc in coverage)
                foreach (var e in sc.Clauses)
                    foreach (var word in VerdictWords.All)
                        Assert.DoesNotContain(word, e.Label, StringComparison.OrdinalIgnoreCase);
    }

    // --- ScanReport wiring ---

    [Fact]
    public void ScanReport_BeyondWcag_HasNoFocusStandard_ListsEveryStandardWithClauses()
    {
        var report = new ScanReport { ToolVersion = "test", Screens = [Screen()] };

        Assert.Equal(3, report.BeyondWcag.Count); // section-508, en-301-549, en-301-549-v4
    }

    [Fact]
    public void ScanReport_BeyondWcag_WithFocusStandard_ListsOnlyThatStandard()
    {
        var report = new ScanReport { ToolVersion = "test", Screens = [Screen()], FocusStandard = "section-508" };

        var only = Assert.Single(report.BeyondWcag);
        Assert.Equal("section-508", only.StandardId);
    }

    [Fact]
    public void ScanReport_BeyondWcag_WithFocusStandardThatHasNoClauses_IsEmpty()
    {
        var report = new ScanReport { ToolVersion = "test", Screens = [Screen()], FocusStandard = "ada-title-ii" };

        Assert.Empty(report.BeyondWcag);
    }

    [Fact]
    public void ScanReport_BeyondWcag_RoundTripsThroughJson()
    {
        var report = new ScanReport { ToolVersion = "test", Screens = [Screen()] };

        var json = JsonReport.Serialize(report);
        Assert.Contains("\"beyondWcag\"", json);

        var roundTripped = JsonReport.Deserialize(json)!;
        Assert.Equal(report.BeyondWcag.Count, roundTripped.BeyondWcag.Count);
    }
}
