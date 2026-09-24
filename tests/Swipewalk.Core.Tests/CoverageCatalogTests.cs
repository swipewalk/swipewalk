using Swipewalk.Core.Coverage;
using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class CoverageCatalogTests
{
    private static Finding Finding(string ruleId, FindingKind kind, WcagCriterion criterion) => new()
    {
        RuleId = ruleId,
        Kind = kind,
        Message = "test finding",
        NodePath = "0",
        Role = "button",
        Criteria = [criterion],
    };

    private static Finding AdvisoryFinding(string ruleId) => new()
    {
        RuleId = ruleId,
        Kind = FindingKind.PlatformAdvisory,
        Message = "test advisory",
        NodePath = "0",
        Role = "button",
        PlatformGuideline = "some platform guideline",
    };

    // --- Catalog completeness ---

    [Fact]
    public void EveryLevelAAndAaCriterion_AppearsExactlyOnce()
    {
        var numbers = CoverageCatalog.All.Select(c => c.Criterion.Number).ToList();

        Assert.Equal(55, numbers.Count); // 31 Level A + 24 Level AA on WCAG 2.2; 4.1.1 Parsing excluded (obsolete)
        Assert.Equal(numbers.Distinct().Count(), numbers.Count);
        Assert.All(CoverageCatalog.All, c => Assert.True(c.Criterion.Level is WcagLevel.A or WcagLevel.AA));
        Assert.DoesNotContain(CoverageCatalog.All, c => c.Criterion.Number == "4.1.1");
    }

    [Fact]
    public void EveryCriterionInWcagCriteria_IsInTheCoverageCatalog()
    {
        // The catalog must be a superset of the existing rule-facing WCAG criteria catalog, not a fork of it.
        foreach (var criterion in WcagCriteria.All)
            Assert.Contains(CoverageCatalog.All, c => c.Criterion == criterion);
    }

    [Fact]
    public void PartlyAutomatedEntries_NameAtLeastOneExistingRuleId()
    {
        var existingRuleIds = DefaultRules.All.Select(r => r.Id).ToHashSet();

        foreach (var entry in CoverageCatalog.All.Where(c => c.BaseStatus == CoverageBaseStatus.PartlyAutomated))
        {
            Assert.NotEmpty(entry.RuleIds);
            Assert.All(entry.RuleIds, id => Assert.Contains(id, existingRuleIds));
        }
    }

    [Fact]
    public void EveryRulesMappedCriterion_IsInTheCatalogWithANonManualStatus()
    {
        foreach (var ruleCoverage in DefaultRules.Coverage)
        {
            foreach (var criterion in ruleCoverage.Criteria)
            {
                var entry = Assert.Single(CoverageCatalog.All, c => c.Criterion == criterion);
                Assert.NotEqual(CoverageBaseStatus.Manual, entry.BaseStatus);
                Assert.Contains(ruleCoverage.RuleId, entry.RuleIds);
            }
        }
    }

    [Fact]
    public void ManualAndPartlyAutomatedEntries_HaveAHowToCheck()
    {
        foreach (var entry in CoverageCatalog.All.Where(c =>
                     c.BaseStatus is CoverageBaseStatus.Manual or CoverageBaseStatus.PartlyAutomated))
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Note));
        }
    }

    [Fact]
    public void UsuallyNotApplicableEntries_AreExactlyTheFourSetOfSoftwareProgramsCriteria()
    {
        var entries = CoverageCatalog.All.Where(c => c.BaseStatus == CoverageBaseStatus.UsuallyNotApplicable).ToList();

        Assert.Equal(["2.4.1", "2.4.5", "3.2.3", "3.2.6"], entries.Select(e => e.Criterion.Number).Order());
        Assert.All(entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Note));
            Assert.Contains("usually", e.Note, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("set of software programs", e.Note, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(e.SourceUrls, url => url.Contains("wcag2ict", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void ConsistentIdentification_IsManualNotUsuallyNotApplicable()
    {
        // 3.2.4 is the one "set of software programs" criterion the review moved to Manual, since
        // EN 301 549 v4.1.1 (11.3.2.4) applies it within a single app even though WCAG2ICT alone would not.
        var entry = Assert.Single(CoverageCatalog.All, c => c.Criterion.Number == "3.2.4");

        Assert.Equal(CoverageBaseStatus.Manual, entry.BaseStatus);
        Assert.Contains("EN 301 549", entry.Note);
    }

    [Fact]
    public void NoCatalogText_ContainsAComplianceVerdict()
    {
        foreach (var entry in CoverageCatalog.All)
        {
            var text = $"{entry.Criterion.Name} {entry.Note}";
            foreach (var word in VerdictWords.All)
                Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void EveryEntry_HasAtLeastOneSourceUrl()
    {
        Assert.All(CoverageCatalog.All, c =>
        {
            Assert.NotEmpty(c.SourceUrls);
            Assert.All(c.SourceUrls, url => Assert.StartsWith("https://", url));
        });
    }

    [Fact]
    public void DisplayLabels_ContainNoComplianceVerdict()
    {
        var texts = new[]
        {
            CoverageDisplay.PartlyAutomated(0, 0),
            CoverageDisplay.PartlyAutomated(2, 3),
            CoverageDisplay.ManualCheckNeeded,
            CoverageDisplay.UsuallyNotApplicableText,
            CoverageDisplay.NotTestedInThisRun("text-resize", "skipped on every screen"),
        };

        foreach (var text in texts)
        foreach (var word in VerdictWords.All)
            Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PartlyAutomatedDisplay_ZeroFindings_SaysZeroAutomatedFindingsNotNoIssues()
    {
        var text = CoverageDisplay.PartlyAutomated(0, 0);

        Assert.Contains("0 automated findings", text);
        Assert.DoesNotContain("no issues", text, StringComparison.OrdinalIgnoreCase);
    }

    // --- Per-run status ---

    private static CriterionCoverage ResizeTextCoverage =>
        Assert.Single(CoverageCatalog.All, c => c.Criterion.Number == "1.4.4");

    private static ScreenActivity Screen(
        IReadOnlySet<string>? ran = null, IReadOnlyDictionary<string, string>? skipped = null, IReadOnlyList<Finding>? findings = null) =>
        new(ran ?? new HashSet<string>(), skipped ?? new Dictionary<string, string>(), findings ?? []);

    [Fact]
    public void LargeTextCheckSkippedOnAllScreens_ResizeTextIsNotTestedInThisRun()
    {
        var activity = new RunActivity([
            Screen(skipped: new Dictionary<string, string> { ["text-resize"] = "no LargeText capture on this screen" }),
            Screen(skipped: new Dictionary<string, string> { ["text-resize"] = "no LargeText capture on this screen" }),
        ]);

        var result = ResizeTextCoverage.ComputeRunStatus(activity);

        Assert.Equal(CoverageRunStatus.NotTestedInThisRun, result.Status);
        Assert.Equal(0, result.WcagIssueCount);
        Assert.Equal(0, result.NeedsReviewCount);
        Assert.Contains("text-resize", result.RuleIdsThatDidNotRun);
        Assert.Contains("engine", result.RuleIdsThatDidNotRun); // never ran either (e.g. Android, no engine audit)
        Assert.Contains("no LargeText capture on this screen", result.SkipReasons);
    }

    [Fact]
    public void LargeTextCheckRanOnSomeScreens_ResizeTextIsPartlyAutomatedWithFindingsAndScreenCount()
    {
        var finding = Finding("text-resize", FindingKind.WcagIssue, WcagCriteria.ResizeText);
        var activity = new RunActivity([
            Screen(ran: new HashSet<string> { "text-resize" }, findings: [finding]),
            Screen(skipped: new Dictionary<string, string> { ["text-resize"] = "no LargeText capture on this screen" }),
            Screen(skipped: new Dictionary<string, string> { ["text-resize"] = "no LargeText capture on this screen" }),
        ]);

        var result = ResizeTextCoverage.ComputeRunStatus(activity);

        Assert.Equal(CoverageRunStatus.PartlyAutomated, result.Status);
        Assert.Equal(1, result.WcagIssueCount);
        Assert.Equal(0, result.NeedsReviewCount);
        Assert.Equal(1, result.ScreensRan);
        Assert.Equal(3, result.ScreensTotal);
    }

    [Fact]
    public void TargetSizeAdvisoryAndEngineHitRegionFinding_CountAsZeroForTheCriteriaTheyDoNotCite()
    {
        // A PlatformAdvisory finding is never counted, and both these findings cite no WCAG criterion.
        var advisory = AdvisoryFinding("target-size");
        var engineAdvisory = AdvisoryFinding("engine:hitRegion");
        var activity = new RunActivity([
            Screen(ran: new HashSet<string> { "target-size", "engine:hitRegion" }, findings: [advisory, engineAdvisory]),
        ]);

        foreach (var number in new[] { "2.5.8", "1.4.3", "1.4.4", "4.1.2" })
        {
            var coverage = Assert.Single(CoverageCatalog.All, c => c.Criterion.Number == number);
            var result = coverage.ComputeRunStatus(activity);

            Assert.Equal(0, result.WcagIssueCount);
            Assert.Equal(0, result.NeedsReviewCount);
        }
    }

    [Fact]
    public void MissingNameFindingCitingOnlyNameRoleValue_IsNotCountedUnderNonTextContent()
    {
        var finding = Finding("missing-name", FindingKind.WcagIssue, WcagCriteria.NameRoleValue);
        var activity = new RunActivity([Screen(ran: new HashSet<string> { "missing-name" }, findings: [finding])]);

        var nonTextContent = Assert.Single(CoverageCatalog.All, c => c.Criterion.Number == "1.1.1");
        var nameRoleValue = Assert.Single(CoverageCatalog.All, c => c.Criterion.Number == "4.1.2");

        Assert.Equal(0, nonTextContent.ComputeRunStatus(activity).WcagIssueCount);
        Assert.Equal(1, nameRoleValue.ComputeRunStatus(activity).WcagIssueCount);
    }

    [Fact]
    public void EngineRuleId_MatchesOnlyOnScreensWhereAnEngineAuditActuallyRan()
    {
        // "engine" (the coverage rule id) must only be treated as having run where a screen recorded an
        // "engine:<type>" id (e.g. iOS, Apple audit); a screen with no such id (e.g. Android) means it didn't.
        var contrastCoverage = Assert.Single(CoverageCatalog.All, c => c.Criterion.Number == "1.4.3");
        var androidScreen = Screen(ran: new HashSet<string> { "text-contrast" }); // no "engine:*" id
        var iosScreen = Screen(ran: new HashSet<string> { "text-contrast", "engine:contrast" });

        var androidOnly = contrastCoverage.ComputeRunStatus(new RunActivity([androidScreen]));
        var withIos = contrastCoverage.ComputeRunStatus(new RunActivity([androidScreen, iosScreen]));

        Assert.Contains("engine", androidOnly.RuleIdsThatDidNotRun);
        Assert.DoesNotContain("engine", withIos.RuleIdsThatDidNotRun);
        // ScreensRan counts screens where *any* mapped rule ran (text-contrast ran on both screens here).
        Assert.Equal(2, withIos.ScreensRan);
    }

    [Fact]
    public void AllMappedRulesRanWithZeroFindings_StillReportsPartlyAutomatedNotAVerdict()
    {
        var coverage = Assert.Single(CoverageCatalog.All, c => c.Criterion.Number == "2.5.8"); // target-size only
        var activity = new RunActivity([Screen(ran: new HashSet<string> { "target-size" })]);

        var result = coverage.ComputeRunStatus(activity);

        Assert.Equal(CoverageRunStatus.PartlyAutomated, result.Status);
        Assert.Equal(0, result.WcagIssueCount);
        Assert.Equal(0, result.NeedsReviewCount);
    }

    [Fact]
    public void ManualCriterion_AlwaysReportsManualCheckNeededRegardlessOfRunActivity()
    {
        var coverage = Assert.Single(CoverageCatalog.All, c => c.Criterion.Number == "1.2.1"); // Audio-only and Video-only (Prerecorded), no rule
        var activity = new RunActivity([
            Screen(ran: new HashSet<string> { "some-unrelated-rule" },
                findings: [Finding("some-unrelated-rule", FindingKind.WcagIssue, WcagCriteria.AudioOnlyAndVideoOnlyPrerecorded)]),
        ]);

        var result = coverage.ComputeRunStatus(activity);

        Assert.Equal(CoverageRunStatus.ManualCheckNeeded, result.Status);
        Assert.Equal(0, result.WcagIssueCount);
    }

    [Fact]
    public void UsuallyNotApplicableCriterion_AlwaysReportsUsuallyNotApplicable()
    {
        var coverage = Assert.Single(CoverageCatalog.All, c => c.Criterion.Number == "2.4.1"); // Bypass Blocks

        var result = coverage.ComputeRunStatus(RunActivity.Empty);

        Assert.Equal(CoverageRunStatus.UsuallyNotApplicable, result.Status);
        Assert.Equal(0, result.WcagIssueCount);
    }

    [Fact]
    public void RuleExplicitlySkippedEvenThoughItAlsoAppearsToHaveRun_IsTreatedAsNotRun()
    {
        // Defensive case: a rule id present in both "ran" and "skipped" on the same screen (a wiring bug)
        // must not be counted as having run there.
        var activity = new RunActivity([
            Screen(ran: new HashSet<string> { "text-resize" },
                skipped: new Dictionary<string, string> { ["text-resize"] = "conflicting signal" }),
        ]);

        var result = ResizeTextCoverage.ComputeRunStatus(activity);

        // "engine" (the other rule mapped to 1.4.4) also did not run, so nothing ran for this criterion.
        Assert.Equal(CoverageRunStatus.NotTestedInThisRun, result.Status);
        Assert.Equal(0, result.WcagIssueCount);
    }
}
