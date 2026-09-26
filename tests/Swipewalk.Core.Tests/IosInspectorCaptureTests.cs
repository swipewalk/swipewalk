using Swipewalk.Collectors.Ios;
using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;

namespace Swipewalk.Core.Tests;

/// <summary>
/// <see cref="IosInspectorCapture.Build"/> matches the Accessibility Inspector's own walk order (no
/// frame/geometry field -- see that type's remarks) to <see cref="ScreenSnapshot"/> tree nodes by identifier,
/// then label, then position. These fixtures exercise each rung of that fallback, without a Mac or the
/// Inspector itself -- see <see cref="IosInspectorWalk"/> for the process boundary this deliberately stays on
/// the pure side of.
/// </summary>
public class IosInspectorCaptureTests
{
    private static AccessibilityNode Button(double y, string? label, string? automationId = null) => new()
    {
        Role = "button",
        Label = label,
        AutomationId = automationId,
        IsInteractive = true,
        IsFocusable = true,
        Bounds = new Bounds(0, y, 100, 30),
    };

    /// <summary>An icon-only button: no label, no visible text, no hint -- <see cref="ScreenReaderPredictor.AccessibleName"/> is null.</summary>
    private static AccessibilityNode IconButton(double y) => Button(y, label: null);

    private static ScreenSnapshot Snapshot(params AccessibilityNode[] children) => new()
    {
        Platform = Platform.iOS,
        ScreenName = "Test",
        Root = new AccessibilityNode { Role = "container", Children = children },
    };

    private static InspectorWalkItem Item(string? label, string? identifier = null, string? className = "UIButton") =>
        new(label, Value: null, Traits: [], identifier, Hint: null, className);

    private static InspectorWalkResult Result(params InspectorWalkItem[] items) =>
        new(Ok: true, Error: null, items, Complete: true, NotCompleteReason: null, ToolVersion: "26.0");

    [Fact]
    public void NotOk_ReturnsSkippedCaptureWithTheError()
    {
        var raw = new InspectorWalkResult(Ok: false, Error: "the Inspector is not running", [], Complete: false, "the Inspector is not running", null);

        var capture = IosInspectorCapture.Build(Snapshot(Button(0, "Search")), raw);

        Assert.Equal(ScreenReaderSource.AccessibilityInspector, capture.Source);
        Assert.Empty(capture.Items);
        Assert.False(capture.Complete);
        Assert.Equal("the Inspector is not running", capture.NotCompleteReason);
    }

    [Fact]
    public void NoItems_KeepsCompleteAndReasonFromTheRawResult()
    {
        var raw = new InspectorWalkResult(Ok: true, Error: null, [], Complete: true, NotCompleteReason: null, ToolVersion: "26.0");

        var capture = IosInspectorCapture.Build(Snapshot(Button(0, "Search")), raw);

        Assert.Empty(capture.Items);
        Assert.True(capture.Complete);
        Assert.Null(capture.NotCompleteReason);
    }

    [Fact]
    public void Identifier_MatchesExactlyEvenWithADuplicateLabelElsewhere()
    {
        var search = Button(0, "Search", automationId: "btnSearch");
        var snapshot = Snapshot(search, Button(40, "Search")); // two "Search" buttons; only one has the identifier

        var capture = IosInspectorCapture.Build(snapshot, Result(Item("Search", identifier: "btnSearch")));

        var only = Assert.Single(capture.Items);
        Assert.Equal("0", only.MatchedNodePath);
        Assert.Equal(MatchConfidence.Exact, only.MatchConfidence);
    }

    [Fact]
    public void UniqueLabel_MatchesLikely()
    {
        var snapshot = Snapshot(Button(0, "Search"), Button(40, "Cancel"));

        var capture = IosInspectorCapture.Build(snapshot, Result(Item("Cancel")));

        var only = Assert.Single(capture.Items);
        Assert.Equal("1", only.MatchedNodePath);
        Assert.Equal(MatchConfidence.Likely, only.MatchConfidence);
    }

    [Fact]
    public void DuplicateLabelAndClass_TieBreaksByNearestToTheCursorAsWeak_ThenTheNextOneIsLikely()
    {
        // Two "Save" buttons with nothing else to tell them apart: the first walk step can't confidently
        // say which is which (Weak), but once it's picked, only one unused "Save" candidate is left, so the
        // second step is confident again (Likely) -- see IosInspectorCapture.FindMatch's remarks.
        var snapshot = Snapshot(Button(0, "Search", automationId: "btnSearch"), Button(40, "Save"), Button(80, "Save"));

        var capture = IosInspectorCapture.Build(snapshot,
            Result(Item("Search", identifier: "btnSearch"), Item("Save"), Item("Save")));

        Assert.Collection(capture.Items,
            first => { Assert.Equal("0", first.MatchedNodePath); Assert.Equal(MatchConfidence.Exact, first.MatchConfidence); },
            second => { Assert.Equal("1", second.MatchedNodePath); Assert.Equal(MatchConfidence.Weak, second.MatchConfidence); },
            third => { Assert.Equal("2", third.MatchedNodePath); Assert.Equal(MatchConfidence.Likely, third.MatchConfidence); });
    }

    [Fact]
    public void DuplicateLabel_DisambiguatedByClassName_IsLikely()
    {
        var snapshot = new ScreenSnapshot
        {
            Platform = Platform.iOS,
            ScreenName = "Test",
            Root = new AccessibilityNode
            {
                Role = "container",
                Children =
                [
                    new AccessibilityNode { Role = "text", Label = "Search", Bounds = new Bounds(0, 0, 100, 20) }, // a heading, not a button
                    Button(40, "Search"),
                ],
            },
        };

        // The Inspector reports a UIButton class: only the second "Search" (the real button) has a
        // matching role, so this is resolved confidently even though the label alone is ambiguous.
        var capture = IosInspectorCapture.Build(snapshot, Result(Item("Search", className: "UIButton")));

        var only = Assert.Single(capture.Items);
        Assert.Equal("1", only.MatchedNodePath);
        Assert.Equal(MatchConfidence.Likely, only.MatchConfidence);
    }

    [Fact]
    public void NoUsableLabel_FallsBackToPositionAtOrAfterTheCursorAsWeak()
    {
        var snapshot = Snapshot(Button(0, "Search", automationId: "btnSearch"), IconButton(40));

        var capture = IosInspectorCapture.Build(snapshot,
            Result(Item("Search", identifier: "btnSearch"), Item(label: null, className: null)));

        Assert.Collection(capture.Items,
            first => Assert.Equal(MatchConfidence.Exact, first.MatchConfidence),
            second =>
            {
                Assert.Equal("1", second.MatchedNodePath);
                Assert.Equal(MatchConfidence.Weak, second.MatchConfidence);
            });
    }

    [Fact]
    public void NothingLeftToMatch_IsUnmatchedRatherThanGuessed()
    {
        var snapshot = Snapshot(Button(0, "Search", automationId: "btnSearch"));

        var capture = IosInspectorCapture.Build(snapshot,
            Result(Item("Search", identifier: "btnSearch"), Item(label: null, className: null)));

        var second = capture.Items[1];
        Assert.Null(second.MatchedNodePath);
        Assert.Equal(MatchConfidence.None, second.MatchConfidence);
    }

    [Fact]
    public void ItemsCarryTheInspectorsOwnStructuredFieldsAndOrder()
    {
        var snapshot = Snapshot(Button(0, "Search", automationId: "btnSearch"));
        var item = new InspectorWalkItem("Search", Value: "3 of 5", Traits: ["Button"], "btnSearch", Hint: "double-tap to search", "UIButton");

        var capture = IosInspectorCapture.Build(snapshot, Result(item));

        var only = Assert.Single(capture.Items);
        Assert.Equal(1, only.Order);
        Assert.Null(only.SpokenText); // the Inspector reports structured fields, never speech -- see ScreenReaderCapture's remarks.
        Assert.Equal("Search", only.Label);
        Assert.Equal("3 of 5", only.Value);
        Assert.Equal(["Button"], only.Traits);
        Assert.Equal("btnSearch", only.Identifier);
        Assert.Equal("double-tap to search", only.Hint);
        Assert.Equal("UIButton", only.ClassName);
    }

    /// <summary>
    /// Items copied (trimmed to the relevant fields, no device identifiers) from a real physical-iPhone
    /// capture of samples/BuggyApp's first screen (2026-09-25). The Inspector's own panel read the literal
    /// text "None" for this screen's genuinely empty label/value/hint/identifier fields, and "Empty string"
    /// for an empty text field's value -- before normalization, this made BuggyApp's known unlabeled Cancel
    /// button (bug B7: no label, only an AutomationId) look like it was named "None", and the comparer
    /// reported a false "Predicted the name "(none)"; ... reported "None"" finding.
    /// </summary>
    [Fact]
    public void RealDeviceCapture_NormalizesInspectorPlaceholders_SoNoFalseNameMismatchIsReported()
    {
        var snapshot = Snapshot(
            Button(0, "Submit"),
            Button(40, label: null, automationId: "btnCancelPayment"), // BuggyApp bug B7: unlabeled, only an AutomationId
            new AccessibilityNode { Role = "textfield", Label = "Ticket number", IsFocusable = true, Bounds = new Bounds(0, 80, 100, 30) });
        var items = new[]
        {
            new InspectorWalkItem("Submit", Value: "None", Traits: ["Button"], Identifier: "None", Hint: "None", ClassName: "UIButton"),
            new InspectorWalkItem("None", Value: "None", Traits: ["Button"], Identifier: "btnCancelPayment", Hint: "None", ClassName: "UIButton"),
            new InspectorWalkItem("Ticket number", Value: "Empty string", Traits: [], Identifier: "None", Hint: "None", ClassName: "Microsoft_Maui_Platform_MauiTextField"),
        };

        var capture = IosInspectorCapture.Build(snapshot, new InspectorWalkResult(Ok: true, Error: null, items, Complete: true, NotCompleteReason: null, ToolVersion: "5.0"));

        Assert.Collection(capture.Items,
            submit =>
            {
                Assert.Equal("Submit", submit.Label);
                Assert.Null(submit.Value);
                Assert.Null(submit.Hint);
                Assert.Null(submit.Identifier);
                Assert.Equal(MatchConfidence.Likely, submit.MatchConfidence);
            },
            cancel =>
            {
                Assert.Null(cancel.Label); // "None" normalized to null, never a literal name
                Assert.Null(cancel.Value);
                Assert.Null(cancel.Hint);
                Assert.Equal("btnCancelPayment", cancel.Identifier); // a real identifier is never normalized away
                Assert.Equal(MatchConfidence.Exact, cancel.MatchConfidence); // still matched, by identifier, despite the null label
            },
            ticketNumber =>
            {
                Assert.Equal("Ticket number", ticketNumber.Label);
                Assert.Null(ticketNumber.Value); // "Empty string" normalized to null
                Assert.NotNull(ticketNumber.MatchedNodePath);
            });

        // The real false positive this fixes: comparing the (correctly null) predicted name for the
        // unlabeled Cancel button against the Inspector's literal "None" text.
        var predicted = ScreenReaderPredictor.Predict(snapshot);
        var diffs = ScreenReaderCaptureComparer.Compare(predicted, capture);
        Assert.Empty(diffs);
    }

    /// <summary>
    /// From a second real physical-iPhone capture (2026-09-25, samples/NativeiOS's root menu): the person's
    /// Accessibility Inspector selection had apparently landed on the app's window or background rather than
    /// an element. The walk captured exactly one item, entirely empty (no label/value/hint/identifier,
    /// empty traits), and reported it as a normal, complete walk (most likely because a "Next" press with no
    /// real element selected has no effect, so the walk saw its own starting position again and read that as
    /// a wrap -- not confirmed, just the most likely explanation). Read at face value, "complete" told the
    /// comparer the whole screen was covered, turning every one of the screen's real named/focusable elements
    /// into a false "was not reported" review item.
    /// </summary>
    [Fact]
    public void RealDeviceCapture_OneEmptyItemMarkedComplete_IsTreatedAsIncompleteInstead()
    {
        // A stand-in for samples/NativeiOS's root menu: a title plus two named buttons -- enough predicted
        // stops that finding only one, empty item is implausible.
        var snapshot = Snapshot(
            new AccessibilityNode { Role = "text", Label = "NativeiOS", Bounds = new Bounds(0, 0, 100, 20) },
            Button(40, "Views screen"),
            Button(80, "SwiftUI screen"));
        var items = new[] { new InspectorWalkItem(Label: null, Value: null, Traits: [], Identifier: null, Hint: null, ClassName: null) };

        var capture = IosInspectorCapture.Build(snapshot,
            new InspectorWalkResult(Ok: true, Error: null, items, Complete: true, NotCompleteReason: null, ToolVersion: "5.0"));

        Assert.False(capture.Complete);
        Assert.Contains("found only 1 element", capture.NotCompleteReason);
        Assert.Contains("click one element on the app's screen", capture.NotCompleteReason);

        // Complete being corrected to false must actually suppress the false "Missing" findings, not just
        // change a flag nothing reads.
        var predicted = ScreenReaderPredictor.Predict(snapshot);
        var diffs = ScreenReaderCaptureComparer.Compare(predicted, capture);
        Assert.DoesNotContain(diffs, d => d.Kind == ScreenReaderDifferenceKind.Missing);
    }

    [Fact]
    public void FewItemsOnAGenuinelySmallScreen_IsNotTreatedAsIncomplete()
    {
        // Guard against over-triggering: a screen with exactly one real, named, matched element must not be
        // flagged just for being small.
        var snapshot = Snapshot(Button(0, "Continue"));

        var capture = IosInspectorCapture.Build(snapshot, Result(Item("Continue")));

        Assert.True(capture.Complete);
        Assert.Null(capture.NotCompleteReason);
    }

    [Fact]
    public void RealNamedItems_ButFarFewerThanPredicted_IsTreatedAsIncomplete_WithADifferentReasonThanAllEmpty()
    {
        // Distinct from the all-empty case: these items are real, named, and matched -- the walk just found
        // far fewer of them than the screen has predicted stops, so the wording must not claim "none with a
        // label, value or traits" (which would be false here).
        var snapshot = Snapshot(Button(0, "Search", automationId: "btnSearch"), Button(40, "Save"), Button(80, "Cancel"), Button(120, "Help"));

        var capture = IosInspectorCapture.Build(snapshot,
            new InspectorWalkResult(Ok: true, Error: null, [Item("Search", identifier: "btnSearch")], Complete: true, NotCompleteReason: null, ToolVersion: "5.0"));

        Assert.False(capture.Complete);
        Assert.Contains("found only 1 element of about 4 expected", capture.NotCompleteReason);
        Assert.DoesNotContain("none with a label", capture.NotCompleteReason);

        var predicted = ScreenReaderPredictor.Predict(snapshot);
        var diffs = ScreenReaderCaptureComparer.Compare(predicted, capture);
        Assert.DoesNotContain(diffs, d => d.Kind == ScreenReaderDifferenceKind.Missing);
    }

    [Fact]
    public void WalkAlreadyIncomplete_KeepsItsOwnReason_RatherThanTheGuess()
    {
        // A walk the script already reported as incomplete (a timeout, the Inspector closing mid-walk) must
        // keep its own, more specific reason -- CheckPlausible's guess must never overwrite a real one.
        var snapshot = Snapshot(Button(0, "Search", automationId: "btnSearch"), Button(40, "Save"), Button(80, "Cancel"), Button(120, "Help"));

        var capture = IosInspectorCapture.Build(snapshot,
            new InspectorWalkResult(Ok: true, Error: null, [Item("Search", identifier: "btnSearch")], Complete: false,
                NotCompleteReason: "the walk timed out before it finished this screen", ToolVersion: "5.0"));

        Assert.False(capture.Complete);
        Assert.Equal("the walk timed out before it finished this screen", capture.NotCompleteReason);
    }
}
