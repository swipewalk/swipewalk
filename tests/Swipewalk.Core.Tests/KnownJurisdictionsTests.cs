using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Standards;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class KnownJurisdictionsTests
{
    private static IEnumerable<JurisdictionStatus> AllStatuses =>
        KnownJurisdictions.ReferencedStandardDiffers
            .Concat(KnownJurisdictions.NotApplicableToApps)
            .Concat(KnownJurisdictions.NotYetMapped)
            .Concat(KnownJurisdictions.NoRequirementFound);

    [Fact]
    public void Ids_AreUniqueAcrossDefaultAndJurisdictionStandards()
    {
        var ids = KnownStandards.All.Concat(KnownJurisdictions.Jurisdictions).Select(s => s.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void EveryJurisdictionStandard_HasASourceAndACheckedDate()
    {
        Assert.NotEmpty(KnownJurisdictions.Jurisdictions);
        foreach (var s in KnownJurisdictions.Jurisdictions)
        {
            Assert.StartsWith("https://", s.Source);
            Assert.False(string.IsNullOrWhiteSpace(s.CheckedOn));
            Assert.False(new RuleSource(s.Name, s.Basis, s.CheckedOn, s.Source)
                .IsStale(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), RuleSources.MaxAge));
        }
    }

    [Fact]
    public void KnownStandards_Find_AlsoResolvesJurisdictionIds()
    {
        Assert.NotNull(KnownStandards.Find("us-il"));
        Assert.NotNull(KnownStandards.Find("germany-bitv"));
        Assert.Null(KnownStandards.Find("not-a-real-id"));
    }

    [Fact]
    public void JurisdictionStandards_AreNotPartOfTheDefaultSet()
    {
        // The default set must stay small (never every US state) so a finding's chips never list 40 states.
        Assert.DoesNotContain(KnownStandards.All, s => s.Id.StartsWith("us-", StringComparison.Ordinal));
        Assert.All(KnownJurisdictions.Jurisdictions, j => Assert.DoesNotContain(KnownStandards.All, s => s.Id == j.Id));
    }

    [Fact]
    public void OldWcagOneStandards_AreNotTranslatedIntoAMappedWcagTwoStandard()
    {
        // Rhode Island and Vermont reference WCAG 1.0 -- they must never appear as a mapped Standard with a
        // WCAG 2.x version/level (that would silently translate a different standard into ours).
        Assert.DoesNotContain(KnownJurisdictions.Jurisdictions, s => s.Jurisdiction.Contains("Rhode Island"));
        Assert.DoesNotContain(KnownJurisdictions.Jurisdictions, s => s.Jurisdiction.Contains("Vermont"));
        Assert.Contains(KnownJurisdictions.ReferencedStandardDiffers, e => e.Jurisdiction == "Rhode Island");
        Assert.Contains(KnownJurisdictions.ReferencedStandardDiffers, e => e.Jurisdiction == "Vermont");
    }

    [Fact]
    public void EveryJurisdictionStatus_HasAReasonAndACheckedDate()
    {
        Assert.NotEmpty(AllStatuses);
        foreach (var e in AllStatuses)
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Jurisdiction));
            Assert.False(string.IsNullOrWhiteSpace(e.Reason));
            Assert.False(string.IsNullOrWhiteSpace(e.CheckedOn));
        }
    }

    [Fact]
    public void NoRequirementFoundEntries_HaveNoLegalTier()
    {
        // A "no requirement found" entry describes an absence, not a confirmed instrument -- it shouldn't
        // carry a legal tier as if something binding had been found.
        Assert.All(KnownJurisdictions.NoRequirementFound, e => Assert.Null(e.LegalTier));
    }

    [Fact]
    public void NoTextField_ContainsAComplianceVerdictWord()
    {
        var jurisdictionFields = KnownJurisdictions.Jurisdictions
            .SelectMany(s => new[] { s.Name, s.AppliesTo, s.BeyondWcag ?? "" });
        var statusFields = AllStatuses.Select(e => e.Reason);
        var storeFields = StoreGuidance.All.SelectMany(n => new[] { n.Wording, n.Detail });

        foreach (var field in jurisdictionFields.Concat(statusFields).Concat(storeFields))
            foreach (var word in VerdictWords.All)
                Assert.False(field.Contains(word, StringComparison.OrdinalIgnoreCase),
                    $"Found banned word \"{word}\" in \"{field}\".");
    }

    [Fact]
    public void StoreGuidance_NeverClaimsPassingReview()
    {
        foreach (var note in StoreGuidance.All)
            Assert.DoesNotContain("will pass review", note.Wording, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StoreGuidance_IsFoundOnlyForMatchingCriteriaAndPlatform()
    {
        var contrastFinding = new Finding
        {
            RuleId = "text-contrast", Kind = FindingKind.WcagIssue, Message = "", NodePath = "0", Role = "text",
            Criteria = [WcagCriteria.ContrastMinimum],
        };
        Assert.Contains(StoreGuidance.For(contrastFinding, Platform.iOS), n => n.Store == "Apple");
        // Apple's guidance never appears on an Android finding, and vice versa.
        Assert.DoesNotContain(StoreGuidance.For(contrastFinding, Platform.Android), n => n.Store == "Apple");

        var platformAdvisory = new Finding
        {
            RuleId = "target-size", Kind = FindingKind.PlatformAdvisory, Message = "", NodePath = "0", Role = "button",
            PlatformGuideline = "guideline",
        };
        Assert.Empty(StoreGuidance.For(platformAdvisory, Platform.iOS));
    }

    [Fact]
    public void StoreGuidance_CameFromAtf_OnlyWhenTheFindingActuallyDid()
    {
        var atfFinding = new Finding
        {
            RuleId = "atf", Kind = FindingKind.NeedsReview, Message = "", NodePath = "0", Role = "text",
            Criteria = [WcagCriteria.ContrastMinimum], Source = StoreGuidance.AtfEngineName,
        };
        Assert.True(StoreGuidance.CameFromAtf(atfFinding));

        var ownRuleFinding = atfFinding with { Source = Finding.DefaultSource };
        Assert.False(StoreGuidance.CameFromAtf(ownRuleFinding));
    }

    [Fact]
    public void FocusStandard_WorksForAnOptInJurisdictionStandard()
    {
        var finding = new Finding
        {
            RuleId = "identifier-name", Kind = FindingKind.WcagIssue, Message = "", NodePath = "0", Role = "button",
            Criteria = [WcagCriteria.NameRoleValue],
            RelevantStandards = KnownStandards.RelevantTo(new Finding
            {
                RuleId = "identifier-name", Kind = FindingKind.WcagIssue, Message = "", NodePath = "0", Role = "button",
                Criteria = [WcagCriteria.NameRoleValue],
            }),
        };
        var report = new ScanReport
        {
            ToolVersion = "test",
            Screens = [new ScreenResult { Platform = Platform.Android, ScreenName = "Home", Findings = [finding] }],
            FocusStandard = "us-il",
        };

        Assert.True(report.InFocus(finding));
        Assert.Equal(1, report.Count(FindingKind.WcagIssue));
        Assert.Contains(report.Standards, s => s.Id == "us-il");
    }
}
