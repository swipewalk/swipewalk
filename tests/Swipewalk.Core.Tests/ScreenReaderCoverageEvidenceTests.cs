using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class ScreenReaderCoverageEvidenceTests
{
    private static ScreenResult Screen(
        ScreenReaderCapture? capture = null, IReadOnlyList<Finding>? findings = null,
        IReadOnlyList<Announcement>? predicted = null) => new()
    {
        Platform = Platform.Android,
        ScreenName = "Home",
        Findings = findings ?? [],
        PredictedTranscript = predicted ?? [],
        ScreenReaderCapture = capture,
    };

    private static ScreenReaderCaptureItem TalkBackItem(
        int order, string spoken, string? matchedPath = "0", MatchConfidence confidence = MatchConfidence.Exact) =>
        new(order, spoken, null, null, null, null, null, null, null, matchedPath, confidence);

    private static ScreenReaderCaptureItem InspectorItem(
        int order, string? label, IReadOnlyList<string>? traits = null, string? matchedPath = "0",
        MatchConfidence confidence = MatchConfidence.Exact) =>
        new(order, null, label, null, traits, null, null, "UIButton", null, matchedPath, confidence);

    private static Finding CaptureFinding(string suffix, WcagCriterion criterion, string nodePath = "0") => new()
    {
        RuleId = $"screen-reader-capture:{suffix}",
        Kind = FindingKind.NeedsReview,
        Message = "test",
        NodePath = nodePath,
        Role = "button",
        Criteria = [criterion],
    };

    private static Finding OtherFinding(string ruleId, WcagCriterion criterion, string nodePath = "0") => new()
    {
        RuleId = ruleId,
        Kind = FindingKind.WcagIssue,
        Message = "test",
        NodePath = nodePath,
        Role = "button",
        Criteria = [criterion],
    };

    [Fact]
    public void NoCapture_ReturnsNullForEveryCriterion()
    {
        var (summary, source) = ScreenReaderCoverageEvidence.Summarize(Screen(), WcagCriteria.NameRoleValue);

        Assert.Null(summary);
        Assert.Null(source);
    }

    [Theory]
    [InlineData("2.4.3")] // Focus Order -- deliberately excluded, see ScreenReaderCoverageEvidence's remarks.
    [InlineData("1.3.2")] // Meaningful Sequence -- same.
    [InlineData("4.1.3")] // Status Messages -- a snapshot can't evidence a triggered announcement.
    public void UnmappedCriteria_ReturnNullEvenWithACapture(string number)
    {
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow,
            [TalkBackItem(1, "Button")], Complete: true, NotCompleteReason: null);
        var criterion = WcagCriteria.All.Single(c => c.Number == number);

        var (summary, _) = ScreenReaderCoverageEvidence.Summarize(Screen(capture), criterion);

        Assert.Null(summary);
    }

    [Fact]
    public void NameRoleValue_TalkBack_NamesTheToolAndCounts()
    {
        var items = new[]
        {
            TalkBackItem(1, "Submit, Button", matchedPath: "0"),
            TalkBackItem(2, "Cancel, Button", matchedPath: "1"),
            TalkBackItem(3, "Weak match", matchedPath: "2", confidence: MatchConfidence.Weak),
        };
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow, items, Complete: true, NotCompleteReason: null);
        var finding = CaptureFinding("role", WcagCriteria.NameRoleValue, nodePath: "1");
        var screen = Screen(capture, [finding]);

        var (summary, source) = ScreenReaderCoverageEvidence.Summarize(screen, WcagCriteria.NameRoleValue);

        Assert.Equal(ScreenReaderSource.TalkBack, source);
        Assert.Contains("TalkBack", summary);
        Assert.Contains("3 elements", summary);
        Assert.Contains("2 could be matched", summary); // the Weak item is excluded from "compared"
        Assert.Contains("1 difference was flagged for review", summary);
        Assert.Contains("Values and states were not compared", summary);
        AssertNoVerdictWords(summary!);
    }

    [Fact]
    public void NameRoleValue_ZeroDifferences_NeverReadsAsAPassOrNoIssues()
    {
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow,
            [TalkBackItem(1, "Submit, Button")], Complete: true, NotCompleteReason: null);

        var (summary, _) = ScreenReaderCoverageEvidence.Summarize(Screen(capture), WcagCriteria.NameRoleValue);

        Assert.Contains("0 differences were flagged for review", summary);
        Assert.DoesNotContain("no issues", summary, StringComparison.OrdinalIgnoreCase);
        AssertNoVerdictWords(summary!);
    }

    [Fact]
    public void NameRoleValue_Inspector_NamesTheInspectorAndSaysVoiceOverWasNotOn()
    {
        var capture = new ScreenReaderCapture(ScreenReaderSource.AccessibilityInspector, "27.0", DateTimeOffset.UtcNow,
            [InspectorItem(1, "Submit", ["Button"])], Complete: true, NotCompleteReason: null);

        var (summary, source) = ScreenReaderCoverageEvidence.Summarize(Screen(capture), WcagCriteria.NameRoleValue);

        Assert.Equal(ScreenReaderSource.AccessibilityInspector, source);
        Assert.Contains("Xcode's Accessibility Inspector", summary);
        Assert.Contains("VoiceOver itself was not turned on", summary);
    }

    [Fact]
    public void NameRoleValue_IncompleteCapture_SaysTheCaptureStoppedEarly()
    {
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow,
            [TalkBackItem(1, "Submit, Button")], Complete: false, NotCompleteReason: "device disconnected");

        var (summary, _) = ScreenReaderCoverageEvidence.Summarize(Screen(capture), WcagCriteria.NameRoleValue);

        Assert.Contains("stopped before the end of this screen (device disconnected)", summary);
    }

    [Fact]
    public void NameRoleValue_OverlapWithADifferentRulesFinding_IsFlagged()
    {
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow,
            [TalkBackItem(1, "Submit, Button", matchedPath: "0")], Complete: true, NotCompleteReason: null);
        var captureFinding = CaptureFinding("role", WcagCriteria.NameRoleValue, nodePath: "0");
        var treeFinding = OtherFinding("missing-name", WcagCriteria.NameRoleValue, nodePath: "0");
        var screen = Screen(capture, [captureFinding, treeFinding]);

        var (summary, _) = ScreenReaderCoverageEvidence.Summarize(screen, WcagCriteria.NameRoleValue);

        Assert.Contains("1 of these is on an element another automated check on this screen also reported under 4.1.2", summary);
        Assert.Contains("check both", summary);
    }

    [Fact]
    public void NonTextContent_ImagesCompared_NamesTheCountAndCaveat()
    {
        var predicted = new[] { new Announcement(1, "0", "Logo, Image", default, HasName: true) };
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow,
            [TalkBackItem(1, "Logo, Image", matchedPath: "0")], Complete: true, NotCompleteReason: null);
        var screen = Screen(capture, [], predicted);

        var (summary, _) = ScreenReaderCoverageEvidence.Summarize(screen, WcagCriteria.NonTextContent);

        Assert.Contains("1 image on this screen could be matched", summary);
        Assert.Contains("0 differences were flagged for review", summary);
        Assert.Contains("not whether it describes the image well", summary);
    }

    [Fact]
    public void NonTextContent_NoImagesComparedAndNoDifference_IsOmitted()
    {
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow,
            [TalkBackItem(1, "Submit, Button", matchedPath: "0")], Complete: true, NotCompleteReason: null);

        var (summary, source) = ScreenReaderCoverageEvidence.Summarize(Screen(capture), WcagCriteria.NonTextContent);

        Assert.Null(summary);
        Assert.Null(source); // no evidence to show, so no source either
    }

    [Fact]
    public void NonTextContent_DifferenceWithNoImagesConfidentlyMatched_StillReported()
    {
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow,
            [TalkBackItem(1, "something else", matchedPath: "0")], Complete: true, NotCompleteReason: null);
        var finding = CaptureFinding("name", WcagCriteria.NonTextContent, nodePath: "0");

        var (summary, _) = ScreenReaderCoverageEvidence.Summarize(Screen(capture, [finding]), WcagCriteria.NonTextContent);

        Assert.Contains("1 difference on image elements was flagged for review", summary);
    }

    [Fact]
    public void LabelInName_TalkBackComplete_CountsFindings()
    {
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow,
            [TalkBackItem(1, "Button")], Complete: true, NotCompleteReason: null);
        var finding = new Finding
        {
            RuleId = "screen-reader-label-in-name", Kind = FindingKind.NeedsReview, Message = "test",
            NodePath = "0", Role = "button", Criteria = [WcagCriteria.LabelInName],
        };

        var (summary, source) = ScreenReaderCoverageEvidence.Summarize(Screen(capture, [finding]), WcagCriteria.LabelInName);

        Assert.Equal(ScreenReaderSource.TalkBack, source);
        Assert.Contains("TalkBack", summary);
        Assert.Contains("1 control was flagged for review", summary);
        Assert.Contains("not a speech-input test", summary);
    }

    [Fact]
    public void LabelInName_InspectorSource_ReturnsNull()
    {
        var capture = new ScreenReaderCapture(ScreenReaderSource.AccessibilityInspector, "27.0", DateTimeOffset.UtcNow,
            [InspectorItem(1, "Submit")], Complete: true, NotCompleteReason: null);

        var (summary, _) = ScreenReaderCoverageEvidence.Summarize(Screen(capture), WcagCriteria.LabelInName);

        Assert.Null(summary);
    }

    [Fact]
    public void LabelInName_IncompleteTalkBackCapture_ReturnsNull()
    {
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow,
            [TalkBackItem(1, "Button")], Complete: false, NotCompleteReason: "stopped early");

        var (summary, _) = ScreenReaderCoverageEvidence.Summarize(Screen(capture), WcagCriteria.LabelInName);

        Assert.Null(summary);
    }

    [Fact]
    public void Headings_InspectorWithHeaderTraits_ListsThemAndNeverAsAFinding()
    {
        var items = new[]
        {
            InspectorItem(1, "Section one", ["Header"]),
            InspectorItem(2, "Submit", ["Button"]),
        };
        var capture = new ScreenReaderCapture(ScreenReaderSource.AccessibilityInspector, "27.0", DateTimeOffset.UtcNow, items, Complete: true, NotCompleteReason: null);

        var (summary, source) = ScreenReaderCoverageEvidence.Summarize(Screen(capture), WcagCriteria.InfoAndRelationships);

        Assert.Equal(ScreenReaderSource.AccessibilityInspector, source);
        Assert.Contains("Evidence for the manual check, not an automated check", summary);
        Assert.Contains("Header trait on 1 of the 2 elements", summary);
        Assert.Contains("Section one", summary);
        Assert.DoesNotContain("Submit", summary);
        AssertNoVerdictWords(summary!);
    }

    [Fact]
    public void Headings_InspectorWithNoHeaderTrait_StillReportsSomething()
    {
        var capture = new ScreenReaderCapture(ScreenReaderSource.AccessibilityInspector, "27.0", DateTimeOffset.UtcNow,
            [InspectorItem(1, "Submit", ["Button"])], Complete: true, NotCompleteReason: null);

        var (summary, _) = ScreenReaderCoverageEvidence.Summarize(Screen(capture), WcagCriteria.InfoAndRelationships);

        Assert.Contains("reported no Header trait on any of the 1 element", summary);
    }

    [Fact]
    public void Headings_MoreThanTenHeaderItems_TruncatesTheList()
    {
        var items = Enumerable.Range(1, 12).Select(i => InspectorItem(i, $"Heading {i}", ["Header"])).ToArray();
        var capture = new ScreenReaderCapture(ScreenReaderSource.AccessibilityInspector, "27.0", DateTimeOffset.UtcNow, items, Complete: true, NotCompleteReason: null);

        var (summary, _) = ScreenReaderCoverageEvidence.Summarize(Screen(capture), WcagCriteria.InfoAndRelationships);

        Assert.Contains("Header trait on 12 of the 12 elements", summary);
        Assert.Contains(", and 2 more", summary);
    }

    [Fact]
    public void Headings_TalkBackSource_ReturnsNull()
    {
        var capture = new ScreenReaderCapture(ScreenReaderSource.TalkBack, "17.0.1", DateTimeOffset.UtcNow,
            [TalkBackItem(1, "Heading text, Heading")], Complete: true, NotCompleteReason: null);

        var (summary, _) = ScreenReaderCoverageEvidence.Summarize(Screen(capture), WcagCriteria.InfoAndRelationships);

        Assert.Null(summary);
    }

    [Fact]
    public void Headings_IncompleteWalk_SaysElementsAfterThatPointAreNotIncluded()
    {
        var capture = new ScreenReaderCapture(ScreenReaderSource.AccessibilityInspector, "27.0", DateTimeOffset.UtcNow,
            [InspectorItem(1, "Section", ["Header"])], Complete: false, NotCompleteReason: "the walk order repeated an already-seen element");

        var (summary, _) = ScreenReaderCoverageEvidence.Summarize(Screen(capture), WcagCriteria.InfoAndRelationships);

        Assert.Contains("The walk stopped before the end of this screen (the walk order repeated an already-seen element)", summary);
        Assert.Contains("not included", summary);
    }

    private static void AssertNoVerdictWords(string text)
    {
        foreach (var word in VerdictWords.All)
            Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase);
    }
}
