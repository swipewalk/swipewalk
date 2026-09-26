using System.Text.RegularExpressions;
using Swipewalk.Core.Standards;

namespace Swipewalk.Core.Tests;

/// <summary>Data-integrity checks for the beyond-WCAG clause catalog itself (not the per-run computation --
/// see <see cref="BeyondWcagCoverageTests"/>).</summary>
public class BeyondWcagClausesTests
{
    private static readonly Regex CheckedOnPattern = new(@"^\d{4}-\d{2}$");

    [Fact]
    public void EveryClause_HasAKnownStandardId()
    {
        Assert.All(KnownBeyondWcagClauses.All, c => Assert.Contains(KnownStandards.All, s => s.Id == c.StandardId));
    }

    [Fact]
    public void EveryClause_HasAPrimarySourceAndACheckedOnDate()
    {
        Assert.All(KnownBeyondWcagClauses.All, c =>
        {
            Assert.StartsWith("https://", c.Source);
            Assert.Matches(CheckedOnPattern, c.CheckedOn);
        });
    }

    [Fact]
    public void EveryClause_HasANonEmptyTitleSummaryAndExplanation()
    {
        Assert.All(KnownBeyondWcagClauses.All, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Title));
            Assert.False(string.IsNullOrWhiteSpace(c.Summary));
            Assert.False(string.IsNullOrWhiteSpace(c.Explanation));
        });
    }

    [Fact]
    public void ConditionalClauses_HaveAConditionDescription_OthersDoNot()
    {
        foreach (var c in KnownBeyondWcagClauses.All)
        {
            if (c.Applicability == BeyondWcagApplicability.Conditional)
                Assert.False(string.IsNullOrWhiteSpace(c.ConditionDescription), $"{c.StandardId} {c.ClauseNumber} is conditional but has no condition description.");
            else
                Assert.Null(c.ConditionDescription);
        }
    }

    [Fact]
    public void PlatformOrOrganizationalClauses_AreAlwaysNotTestable()
    {
        Assert.All(KnownBeyondWcagClauses.All.Where(c => c.Applicability == BeyondWcagApplicability.PlatformOrOrganizational),
            c => Assert.Equal(BeyondWcagCheckMethod.NotTestable, c.CheckMethod));
    }

    [Fact]
    public void GuidedSteps_NeverExceedThree()
    {
        Assert.All(KnownBeyondWcagClauses.All, c => Assert.True(c.GuidedSteps.Count <= 3,
            $"{c.StandardId} {c.ClauseNumber} has {c.GuidedSteps.Count} guided steps; keep it to 1-3."));
    }

    [Fact]
    public void EveryAlwaysGuidedClause_HasOneToThreeSteps()
    {
        // An Always + Guided clause always needs a check, so unlike a Conditional clause's informational
        // steps (which can be empty when the source gives nothing concrete beyond a cross-reference, e.g.
        // "en-301-549" 11.5.1), it must give a person something concrete to do.
        Assert.All(
            KnownBeyondWcagClauses.All.Where(c => c.Applicability == BeyondWcagApplicability.Always && c.CheckMethod == BeyondWcagCheckMethod.Guided),
            c => Assert.InRange(c.GuidedSteps.Count, 1, 3));
    }

    [Fact]
    public void SkippedNote_ContainsNoComplianceVerdictWord()
    {
        foreach (var word in VerdictWords.All)
            Assert.DoesNotContain(word, KnownBeyondWcagClauses.SkippedNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OnlyAlwaysPartlyAutomatedClauses_HaveAnEvidenceSignal()
    {
        foreach (var c in KnownBeyondWcagClauses.All)
        {
            var expectEvidence = c.Applicability == BeyondWcagApplicability.Always && c.CheckMethod == BeyondWcagCheckMethod.PartlyAutomated;
            if (expectEvidence)
                Assert.NotEqual(BeyondWcagEvidenceSignal.None, c.Evidence);
            else
                Assert.Equal(BeyondWcagEvidenceSignal.None, c.Evidence);
        }
    }

    [Fact]
    public void ClauseNumbers_AreUniqueWithinEachStandard()
    {
        foreach (var group in KnownBeyondWcagClauses.All.GroupBy(c => c.StandardId))
        {
            var numbers = group.Select(c => c.ClauseNumber).ToList();
            Assert.Equal(numbers.Distinct().Count(), numbers.Count);
        }
    }

    [Fact]
    public void EveryStandardWithBeyondWcagClauses_HasAOneSentenceSummaryToo()
    {
        // Standards.BeyondWcag (the short summary shown in the standards table) should exist for any standard
        // that also has a detailed clause catalog here, so a reader sees the summary before the detail.
        foreach (var standardId in KnownBeyondWcagClauses.All.Select(c => c.StandardId).Distinct())
        {
            var standard = KnownStandards.Find(standardId);
            Assert.NotNull(standard);
            Assert.False(string.IsNullOrWhiteSpace(standard!.BeyondWcag));
        }
    }

    [Fact]
    public void ConditionalPartlyAutomatedClauses_ExplanationStartsWithTheCouldBeCheckedPrefix()
    {
        // A Conditional + PartlyAutomated clause always resolves to "not tested" today (no per-clause feature
        // detector exists), so its "PartlyAutomated" check method only describes a possible future check --
        // the explanation must say so, never imply the check runs now.
        Assert.All(
            KnownBeyondWcagClauses.All.Where(c => c.Applicability == BeyondWcagApplicability.Conditional && c.CheckMethod == BeyondWcagCheckMethod.PartlyAutomated),
            c => Assert.StartsWith(KnownBeyondWcagClauses.CouldBeCheckedPrefix, c.Explanation));
    }

    [Fact]
    public void NoClauseField_EverContainsAComplianceVerdictWord()
    {
        foreach (var c in KnownBeyondWcagClauses.All)
        {
            IReadOnlyList<string> fields = [c.Title, c.Summary, c.ConditionDescription ?? "", c.Explanation, .. c.GuidedSteps];
            foreach (var field in fields)
                foreach (var word in VerdictWords.All)
                    Assert.False(field.Contains(word, StringComparison.OrdinalIgnoreCase),
                        $"{c.StandardId} {c.ClauseNumber}: found banned word \"{word}\" in \"{field}\".");
        }
    }
}
