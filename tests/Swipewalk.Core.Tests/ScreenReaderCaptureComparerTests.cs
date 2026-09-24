using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;

namespace Swipewalk.Core.Tests;

/// <summary>
/// Fixtures modeled on real-world screen-reader behavior a scanner needs to handle without false positives:
/// a reader speaking only a role word for an unlabeled control (never a mismatch against Swipewalk's own
/// "Unlabeled" placeholder, since that placeholder is never itself spoken); an element reported with no
/// interactive trait where Swipewalk's tree heuristics predicted one; and elements read in a different
/// relative order than predicted.
/// </summary>
public class ScreenReaderCaptureComparerTests
{
    private static readonly Bounds AnyBounds = new(0, 0, 100, 40);

    private static ScreenReaderCaptureItem TalkBackItem(int order, string spokenText, string? matchedNodePath) =>
        new(order, spokenText, null, null, null, null, null, null, null, matchedNodePath,
            matchedNodePath is null ? MatchConfidence.None : MatchConfidence.Exact);

    private static ScreenReaderCaptureItem InspectorItem(
        int order, string? label, IReadOnlyList<string>? traits, string? matchedNodePath, string? className = null) =>
        new(order, null, label, null, traits, null, null, className, null, matchedNodePath,
            matchedNodePath is null ? MatchConfidence.None : MatchConfidence.Exact);

    private static ScreenReaderCapture Capture(
        ScreenReaderSource source, IReadOnlyList<ScreenReaderCaptureItem> items, bool complete = true,
        string? notCompleteReason = null, string? language = null) =>
        new(source, "test", DateTimeOffset.UtcNow, items, complete, notCompleteReason, language);

    [Fact]
    public void TalkBack_RealButtonAloneAgainstPredictedUnlabeledButton_IsNotAMismatch()
    {
        // "Unlabeled" is Swipewalk's own placeholder (ScreenReaderPredictor.Announce), never itself spoken.
        var predicted = new[] { new Announcement(1, "0", "Unlabeled, Button", AnyBounds, HasName: false) };
        var capture = Capture(ScreenReaderSource.TalkBack, [TalkBackItem(1, "Button", "0")]);

        Assert.Empty(ScreenReaderCaptureComparer.Compare(predicted, capture));
    }

    [Fact]
    public void Inspector_TermsWithNoButtonTrait_IsARoleMismatchOnly_NotATextMismatch()
    {
        var predicted = new[] { new Announcement(1, "0/3", "Terms, Button", AnyBounds, HasName: true) };
        var capture = Capture(ScreenReaderSource.AccessibilityInspector,
            [InspectorItem(1, "Terms", traits: [], matchedNodePath: "0/3", className: "Static Text")]);

        var diff = Assert.Single(ScreenReaderCaptureComparer.Compare(predicted, capture));

        Assert.Equal(ScreenReaderDifferenceKind.RoleMismatch, diff.Kind);
        Assert.Equal(predicted[0], diff.Predicted);
        Assert.Contains("Terms", diff.Description);
    }

    [Fact]
    public void Inspector_TwoOrderSwaps_ReportOrderMismatchOnly_NoNameOrRoleDifference()
    {
        var predicted = new[]
        {
            new Announcement(1, "0/0", "Icon, Button", AnyBounds, HasName: true),
            new Announcement(2, "0/1", "City of Exampleville", AnyBounds, HasName: true),
            new Announcement(3, "0/2", "Help, Button", AnyBounds, HasName: true),
            new Announcement(4, "0/3", "Need help?", AnyBounds, HasName: true),
        };
        var capture = Capture(ScreenReaderSource.AccessibilityInspector,
        [
            InspectorItem(1, "City of Exampleville", traits: null, matchedNodePath: "0/1"),
            InspectorItem(2, "Icon", traits: ["Button"], matchedNodePath: "0/0"),
            InspectorItem(3, "Need help?", traits: null, matchedNodePath: "0/3"),
            InspectorItem(4, "Help", traits: ["Button"], matchedNodePath: "0/2"),
        ]);

        var diffs = ScreenReaderCaptureComparer.Compare(predicted, capture);

        Assert.Equal(4, diffs.Count);
        Assert.All(diffs, d => Assert.Equal(ScreenReaderDifferenceKind.OrderMismatch, d.Kind));
    }

    [Fact]
    public void PeriodSeparatedSpeech_IsNormalizedTheSameAsCommaSeparated()
    {
        var predicted = new[] { new Announcement(1, "0", "Save, Button", AnyBounds, HasName: true) };
        // Some tools/routes punctuate the name/role boundary with a period instead of a comma.
        var capture = Capture(ScreenReaderSource.TalkBack, [TalkBackItem(1, "Save. Button.", "0")]);

        Assert.Empty(ScreenReaderCaptureComparer.Compare(predicted, capture));
    }

    [Fact]
    public void UnmatchedItem_IsReportedAsUnmatched_NotAsAnotherKind()
    {
        var capture = Capture(ScreenReaderSource.VoiceOverCaptions, [TalkBackItem(1, "Something", matchedNodePath: null)]);

        var diff = Assert.Single(ScreenReaderCaptureComparer.Compare([], capture));

        Assert.Equal(ScreenReaderDifferenceKind.Unmatched, diff.Kind);
        Assert.Null(diff.Predicted);
    }

    [Fact]
    public void CapturedItemMatchedToANodeWithNoPredictedStop_IsExtra()
    {
        var capture = Capture(ScreenReaderSource.TalkBack, [TalkBackItem(1, "Something else", "0/9")]);

        var diff = Assert.Single(ScreenReaderCaptureComparer.Compare([], capture));

        Assert.Equal(ScreenReaderDifferenceKind.Extra, diff.Kind);
    }

    [Fact]
    public void CompleteCapture_PredictedNamedStopNeverReported_IsMissing()
    {
        var predicted = new[]
        {
            new Announcement(1, "0/0", "Save, Button", AnyBounds, HasName: true),
            new Announcement(2, "0/1", "Cancel, Button", AnyBounds, HasName: true),
        };
        var capture = Capture(ScreenReaderSource.TalkBack, [TalkBackItem(1, "Save. Button.", "0/0")], complete: true);

        var diff = Assert.Single(ScreenReaderCaptureComparer.Compare(predicted, capture));

        Assert.Equal(ScreenReaderDifferenceKind.Missing, diff.Kind);
        Assert.Equal(predicted[1], diff.Predicted);
    }

    [Fact]
    public void IncompleteCapture_PredictedStopNotReported_IsNotFlaggedAsMissing()
    {
        // The capture stopped early (see ScreenReaderCapture.Complete): an unreported stop may simply be
        // past where it stopped, not a real absence.
        var predicted = new[]
        {
            new Announcement(1, "0/0", "Save, Button", AnyBounds, HasName: true),
            new Announcement(2, "0/1", "Cancel, Button", AnyBounds, HasName: true),
        };
        var capture = Capture(ScreenReaderSource.TalkBack, [TalkBackItem(1, "Save. Button.", "0/0")],
            complete: false, notCompleteReason: "device disconnected mid-capture");

        Assert.Empty(ScreenReaderCaptureComparer.Compare(predicted, capture));
    }

    [Fact]
    public void DifferentName_IsATextMismatch()
    {
        var predicted = new[] { new Announcement(1, "0/0", "Save, Button", AnyBounds, HasName: true) };
        var capture = Capture(ScreenReaderSource.TalkBack, [TalkBackItem(1, "Submit, Button", "0/0")]);

        var diff = Assert.Single(ScreenReaderCaptureComparer.Compare(predicted, capture));

        Assert.Equal(ScreenReaderDifferenceKind.TextMismatch, diff.Kind);
    }

    [Fact]
    public void ToolLabel_UsesTheThreeSourceSpecificPhrasings()
    {
        Assert.Equal("TalkBack", ScreenReaderCaptureComparer.ToolLabel(ScreenReaderSource.TalkBack));
        Assert.Equal("Xcode's Accessibility Inspector", ScreenReaderCaptureComparer.ToolLabel(ScreenReaderSource.AccessibilityInspector));
        Assert.Equal("VoiceOver", ScreenReaderCaptureComparer.ToolLabel(ScreenReaderSource.VoiceOverCaptions));
    }

    [Fact]
    public void ExtraItemBeforeTheRest_DoesNotShiftTheOrderOfLaterMatchedItems()
    {
        // Order is compared by rank among matched pairs only, never by the raw Order numbers: one extra
        // item earlier in the capture must not make every later, correctly-ordered item look shifted.
        var predicted = new[]
        {
            new Announcement(1, "0/0", "Save, Button", AnyBounds, HasName: true),
            new Announcement(2, "0/1", "Cancel, Button", AnyBounds, HasName: true),
        };
        var capture = Capture(ScreenReaderSource.TalkBack,
        [
            TalkBackItem(1, "Something extra", "0/9"),
            TalkBackItem(2, "Save, Button", "0/0"),
            TalkBackItem(3, "Cancel, Button", "0/1"),
        ]);

        var diffs = ScreenReaderCaptureComparer.Compare(predicted, capture);

        var diff = Assert.Single(diffs);
        Assert.Equal(ScreenReaderDifferenceKind.Extra, diff.Kind);
    }

    [Fact]
    public void SpokenTextWithAnUnrecognizedTrailingPhrase_DoesNotFalselyReportARoleMismatch()
    {
        // The utterance ends in something outside the role-word vocabulary (a trailing state, a usage
        // hint): the role isn't confidently known either way, so it must not be reported as a mismatch,
        // even though the whole-string name comparison will separately flag a text difference.
        var predicted = new[] { new Announcement(1, "0", "Remember me, Checkbox", AnyBounds, HasName: true) };
        var capture = Capture(ScreenReaderSource.TalkBack, [TalkBackItem(1, "Remember me, Checkbox, not checked", "0")]);

        var diffs = ScreenReaderCaptureComparer.Compare(predicted, capture);

        Assert.DoesNotContain(diffs, d => d.Kind == ScreenReaderDifferenceKind.RoleMismatch);
    }

    [Fact]
    public void Inspector_RoleOutsideTheConfidentVocabulary_IsNeitherConfirmedNorDenied()
    {
        // "Text field" is not in the comparer's confident-role-word set for Inspector traits (its coverage
        // of that trait is unverified), so a text field with no matching trait must not be flagged.
        var predicted = new[] { new Announcement(1, "0", "Search, Text field", AnyBounds, HasName: true) };
        var capture = Capture(ScreenReaderSource.AccessibilityInspector,
            [InspectorItem(1, "Search", traits: [], matchedNodePath: "0")]);

        Assert.Empty(ScreenReaderCaptureComparer.Compare(predicted, capture));
    }

    [Fact]
    public void PunctuationInsideAName_SurvivesRoleWordStripping()
    {
        // Only a recognized trailing role word is stripped from the predicted text -- nothing else in the
        // string is touched, so a period inside a name (unrelated to the role-word separator) must survive.
        var predicted = new[] { new Announcement(1, "0", "Dr. Smith, Button", AnyBounds, HasName: true) };
        var capture = Capture(ScreenReaderSource.AccessibilityInspector,
            [InspectorItem(1, "Dr. Smith", traits: ["Button"], matchedNodePath: "0")]);

        Assert.Empty(ScreenReaderCaptureComparer.Compare(predicted, capture));
    }

    [Fact]
    public void WeaklyMatchedItem_NameAndRoleDifferencesAreSuppressed()
    {
        // A weak match (order alone) is too uncertain a basis for a name/role difference to mean anything --
        // it may simply be the wrong element.
        var predicted = new[] { new Announcement(1, "0", "Save, Button", AnyBounds, HasName: true) };
        var item = new ScreenReaderCaptureItem(1, "Cancel, Checkbox", null, null, null, null, null, null, null, "0", MatchConfidence.Weak);
        var capture = Capture(ScreenReaderSource.TalkBack, [item]);

        Assert.Empty(ScreenReaderCaptureComparer.Compare(predicted, capture));
    }

    [Fact]
    public void TalkBack_JoinedUtterances_UsesTheLastSegmentWithARecognizedRole()
    {
        // harness/android's TalkBackCollector.kt joins a separate, earlier usage hint and the element's
        // real name/role utterance with " || " (observed order on a Pixel 4a: hint first, then the real
        // announcement) -- the hint segment must not be mistaken for the name.
        var predicted = new[] { new Announcement(1, "0", "Ticket number, Edit box", AnyBounds, HasName: true) };
        var capture = Capture(ScreenReaderSource.TalkBack,
            [TalkBackItem(1, "Double-tap to edit text. Double-tap and hold to long press." + ScreenReaderCaptureComparer.UtteranceJoinSeparator + "Ticket number. Edit box", "0")]);

        Assert.Empty(ScreenReaderCaptureComparer.Compare(predicted, capture));
    }

    [Fact]
    public void TalkBack_LocalizedRoleWordAfterTheName_NameStillMatches_RoleMismatchNotClaimed()
    {
        // A non-English TalkBack speaks its own role word/hint vocabulary but does not translate the
        // app's own accessible name -- reproduced by standing in for a language this comparer has no role
        // vocabulary for: the app's English name is still there verbatim, just followed by unrecognized
        // text, so a strict whole-string equality would wrongly call this a name mismatch. Role mismatch
        // must also not fire: the trailing text isn't one of this comparer's known (English) role words,
        // so it isn't confidently comparable either way (see RoleIsConfidentlyKnown's remarks) --
        // consistent with "names-only for a language this comparer doesn't have role vocabulary for".
        var predicted = new[] { new Announcement(1, "0", "Save for later, Button", AnyBounds, HasName: true) };
        var capture = Capture(ScreenReaderSource.TalkBack, [TalkBackItem(1, "Save for later. Botón", "0")]);

        Assert.Empty(ScreenReaderCaptureComparer.Compare(predicted, capture));
    }

    [Fact]
    public void TalkBack_NameAndRoleAsSeparateUtterances_BothStillRecovered()
    {
        // Reproduced on the emulator (TalkBack 16.0.0): an element's name and role are sometimes spoken
        // as two entirely separate utterances ("Ticket number" then "Edit box"), not one combined "Ticket
        // number, Edit box" -- taking name+role from the same (last) segment would find "Edit box" alone
        // (a bare role word parses as an empty name) and lose the real name, wrongly reporting every such
        // element as a text mismatch.
        var predicted = new[] { new Announcement(1, "0", "Ticket number, Edit box", AnyBounds, HasName: true) };
        var capture = Capture(ScreenReaderSource.TalkBack,
            [TalkBackItem(1, "Ticket number" + ScreenReaderCaptureComparer.UtteranceJoinSeparator + "Edit box", "0")]);

        Assert.Empty(ScreenReaderCaptureComparer.Compare(predicted, capture));
    }

    [Fact]
    public void TalkBack_UnrelatedName_StillReportedAsTextMismatch()
    {
        // The substring-match relaxation for language robustness must not swallow a genuinely different
        // name: "Cancel" is not a substring of anything TalkBack reported here.
        var predicted = new[] { new Announcement(1, "0", "Cancel, Button", AnyBounds, HasName: true) };
        var capture = Capture(ScreenReaderSource.TalkBack, [TalkBackItem(1, "Submit. Button", "0")]);

        var diffs = ScreenReaderCaptureComparer.Compare(predicted, capture);

        Assert.Contains(diffs, d => d.Kind == ScreenReaderDifferenceKind.TextMismatch);
    }

    [Fact]
    public void TalkBack_UntranslatedRoleWordAloneOnANonEnglishDevice_IsNotAMismatch()
    {
        // Reproduced on the emulator with the system language set to Spanish: an icon-only button with no
        // predicted name is announced with TalkBack's own (untranslated, single-utterance) Spanish role
        // word "Botón" -- this comparer's role vocabulary is English only, so it can't recognize and strip
        // that word the way it would "Button". With nothing predicted to compare it against, it can't tell
        // a role word in a language it doesn't know from a genuine name, so it reports a match rather than
        // guess -- consistent with the substring fallback's under- rather than over-reporting choice.
        var predicted = new[] { new Announcement(1, "0", "Unlabeled, Button", AnyBounds, HasName: false) };
        var capture = Capture(ScreenReaderSource.TalkBack, [TalkBackItem(1, "Botón", "0")], language: "es-ES");

        Assert.Empty(ScreenReaderCaptureComparer.Compare(predicted, capture));
    }

    [Fact]
    public void TalkBack_DeveloperIdentifierAloneOnANonEnglishDevice_StillReportedAsTextMismatch()
    {
        // The non-English forgiveness above must not swallow a genuine finding a real capture alone can
        // make: some views fall back to speaking their own resource id when they have no name or text at
        // all (seen on a real device, independent of the device's language) -- "btnCancelPayment" is never
        // a legitimate localized role word in any language, so it is still reported here even though the
        // predicted name is empty and the device is not in English.
        var predicted = new[] { new Announcement(1, "0", "Unlabeled, Button", AnyBounds, HasName: false) };
        var capture = Capture(ScreenReaderSource.TalkBack, [TalkBackItem(1, "btnCancelPayment", "0")], language: "es-ES");

        var diffs = ScreenReaderCaptureComparer.Compare(predicted, capture);

        Assert.Contains(diffs, d => d.Kind == ScreenReaderDifferenceKind.TextMismatch);
    }

    [Fact]
    public void TalkBack_ShortNameInsideALongerUnrelatedWord_IsNotFalselyMatched()
    {
        // A plain substring check would find "Pay" inside "Payment history" and wrongly call this a
        // match -- a real difference (the app renamed the control) going unreported is worse than the
        // false positive the substring relaxation was trying to avoid. Whole-word matching catches it.
        var predicted = new[] { new Announcement(1, "0", "Pay, Button", AnyBounds, HasName: true) };
        var capture = Capture(ScreenReaderSource.TalkBack, [TalkBackItem(1, "Payment history. Button", "0")]);

        var diffs = ScreenReaderCaptureComparer.Compare(predicted, capture);

        Assert.Contains(diffs, d => d.Kind == ScreenReaderDifferenceKind.TextMismatch);
    }

    [Fact]
    public void TalkBack_WholeWordInsideALongerAnnouncement_StillMatches()
    {
        // The whole-word fix above must not become "exact phrase equality": "Save for later" should still
        // match when TalkBack's announcement has other words around it, as long as each predicted word
        // appears as its own word, in order.
        var predicted = new[] { new Announcement(1, "0", "Save for later, Button", AnyBounds, HasName: true) };
        var capture = Capture(ScreenReaderSource.TalkBack, [TalkBackItem(1, "Save for later. Double-tap to activate. Button", "0")]);

        Assert.Empty(ScreenReaderCaptureComparer.Compare(predicted, capture));
    }
}
