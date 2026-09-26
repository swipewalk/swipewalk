using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.ScreenReader;

/// <summary>
/// Builds the "Captured evidence" sentence <see cref="Coverage.ScreenCoverageBuilder"/> attaches to a
/// (screen, criterion) row when this screen has a real <see cref="ScreenReaderCapture"/> that exercises that
/// criterion -- named, worded and scoped exactly as reviewed for WCAG mapping and wording (0.3.0 stream C).
/// Distinct from, and always shown alongside, <see cref="Coverage.GuidedChecksDisplay.PartlyCheckedAutomatically"/>:
/// that generic sentence already counts every finding (from any rule) citing this criterion on this screen;
/// this one names the tool and gives real-device counts specific to what was actually captured, never
/// "passed" and never a bare "0 issues" reading as a clean result -- a 0 always reads as "0 ... flagged for
/// review", exactly like the generic wording it sits next to.
///
/// Deliberately narrow in scope, decided by review rather than guessed:
/// <list type="bullet">
/// <item>4.1.2 Name, Role, Value and 1.1.1 Non-text Content -- both real capture sources (TalkBack, the
/// Accessibility Inspector), reusing the findings <see cref="Rules.ScreenReaderCaptureRule"/> already
/// produces so nothing is recomputed twice.</item>
/// <item>2.5.3 Label in Name -- TalkBack only, and only for a <see cref="ScreenReaderCapture.Complete"/>
/// capture, mirroring <see cref="Rules.ScreenReaderLabelInNameRule"/>'s own gate exactly.</item>
/// <item>1.3.1 Info and Relationships -- Accessibility Inspector only, and INFORMATIONAL only: this is
/// evidence for the existing manual check (<see cref="Coverage.CoverageCatalog"/> keeps 1.3.1 as
/// <see cref="Coverage.CoverageBaseStatus.Manual"/>), never a finding and never a status change. The
/// Inspector's Header trait is the only heading information Swipewalk has for iOS at all (iOS never sets
/// <see cref="AccessibilityNode.IsHeading"/> -- only Android's uiautomator dump does), but the evidence
/// can only show elements the Inspector called headings, not text that LOOKS like a heading but isn't
/// exposed as one -- spotting that still needs a person to look at the screen, so this is never turned into
/// a rule/finding (that would misrepresent "the Inspector saw a real heading, probably correctly" as
/// something needing review, and would make a tester's recorded Pass for 1.3.1 look contradicted on every
/// screen that simply has a real heading).</item>
/// <item>2.4.3 Focus Order, 1.3.2 Meaningful Sequence -- deliberately excluded: neither capture source's own
/// "order" is a real navigation order today (see <see cref="Rules.ScreenReaderCaptureRule"/>'s remarks), so
/// there is nothing honest to say here, not even a hedged version of it.</item>
/// <item>4.1.3 Status Messages -- excluded: a single walk/snapshot cannot evidence a status change being
/// announced without a triggered event over time.</item>
/// </list>
/// </summary>
public static class ScreenReaderCoverageEvidence
{
    /// <summary>The one rule id every 4.1.2/1.1.1 difference finding carries, with a ":suffix"
    /// (<see cref="Rules.ScreenReaderCaptureRule.Id"/>).</summary>
    private const string CaptureRuleId = "screen-reader-capture";

    /// <summary>The sentence for this (screen, criterion), and which capture source it came from (for the
    /// report/desktop UI badge) -- null, null when this screen has no capture, or this criterion isn't one
    /// of the four this type covers.</summary>
    public static (string? Summary, ScreenReaderSource? Source) Summarize(ScreenResult screen, WcagCriterion criterion)
    {
        if (screen.ScreenReaderCapture is not { Items.Count: > 0 } capture)
            return (null, null);

        var summary = criterion == WcagCriteria.NameRoleValue ? NameRoleValue(screen, capture)
            : criterion == WcagCriteria.NonTextContent ? NonTextContent(screen, capture)
            : criterion == WcagCriteria.LabelInName ? LabelInName(screen, capture)
            : criterion == WcagCriteria.InfoAndRelationships ? Headings(capture)
            : null;

        return summary is null ? (null, null) : (summary, capture.Source);
    }

    private static string NameRoleValue(ScreenResult screen, ScreenReaderCapture capture)
    {
        var n = capture.Items.Count;
        var compared = ComparedCount(capture);
        var differ = DifferCount(screen, WcagCriteria.NameRoleValue);
        var text = capture.Source == ScreenReaderSource.TalkBack
            ? $"Swipewalk moved TalkBack's focus to {n} focusable or interactive element{Plural(n)} on this " +
              "screen and recorded what it said (plain text was not captured). " +
              $"{compared} could be matched to an element confidently enough to compare its name and role with " +
              $"Swipewalk's prediction; {differ} difference{Plural(differ)} {WasWere(differ)} flagged for review. " +
              "Values and states were not compared; manual check still needed."
            : $"Xcode's Accessibility Inspector reported the label, value and traits of {n} element{Plural(n)} on " +
              "this screen (VoiceOver itself was not turned on). " +
              $"{compared} could be matched to an element confidently enough to compare its name and role with " +
              $"Swipewalk's prediction; {differ} difference{Plural(differ)} {WasWere(differ)} flagged for review. " +
              "Values and states were not compared; manual check still needed.";
        text = AppendIncomplete(text, capture);
        return AppendOverlap(text, screen, WcagCriteria.NameRoleValue);
    }

    private static string? NonTextContent(ScreenResult screen, ScreenReaderCapture capture)
    {
        var predictedImagePaths = screen.PredictedTranscript
            .Where(a => ScreenReaderCaptureComparer.ParseAnnouncement(a.Text).Role == "Image")
            .Select(a => a.NodePath)
            .ToHashSet();
        var imagesCompared = capture.Items.Count(i =>
            i.MatchedNodePath is { } path && predictedImagePaths.Contains(path)
            && i.MatchConfidence is MatchConfidence.Exact or MatchConfidence.Likely);
        var differ = DifferCount(screen, WcagCriteria.NonTextContent);
        var toolSaid = capture.Source == ScreenReaderSource.TalkBack ? "TalkBack said" : "Xcode's Accessibility Inspector reported";

        string text;
        if (imagesCompared > 0)
            text = $"{imagesCompared} image{Plural(imagesCompared)} on this screen could be matched to what {toolSaid} " +
                   $"and compared by name with Swipewalk's prediction; {differ} difference{Plural(differ)} {WasWere(differ)} " +
                   "flagged for review. This shows whether a name was exposed, not whether it describes the image " +
                   "well; manual check still needed.";
        else if (differ > 0)
            text = $"{differ} difference{Plural(differ)} on image elements {WasWere(differ)} flagged for review " +
                   $"from what {toolSaid}. Manual check still needed.";
        else
            return null; // Nothing to say: no images were confidently matched, and no image difference was found.

        if (capture.Source == ScreenReaderSource.AccessibilityInspector)
            text += " VoiceOver itself was not turned on.";
        else
            text += " Swipewalk's TalkBack capture only moves focus to focusable or interactive elements, so an image that isn't focusable itself (decorative or informative) was not captured here.";
        text = AppendIncomplete(text, capture);
        return AppendOverlap(text, screen, WcagCriteria.NonTextContent);
    }

    private static string? LabelInName(ScreenResult screen, ScreenReaderCapture capture)
    {
        // Mirrors ScreenReaderLabelInNameRule's own gate exactly (TalkBack only, Complete only) -- see this
        // type's remarks.
        if (capture.Source != ScreenReaderSource.TalkBack || !capture.Complete)
            return null;

        var differ = screen.Findings.Count(f => f.RuleId == "screen-reader-label-in-name");
        return "Swipewalk compared what TalkBack said for this screen's controls with their visible text, for " +
               $"controls matched confidently enough; {differ} control{Plural(differ)} {WasWere(differ)} flagged " +
               "for review because TalkBack's announcement didn't include that text. TalkBack is a screen reader, " +
               "not speech input, so this is evidence of the accessible name, not a speech-input test; manual " +
               "check still needed.";
    }

    private static string? Headings(ScreenReaderCapture capture)
    {
        if (capture.Source != ScreenReaderSource.AccessibilityInspector)
            return null;

        var headerItems = capture.Items
            .Where(i => i.Traits is { } traits && traits.Any(t => string.Equals(t, "Header", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var n = capture.Items.Count;
        var h = headerItems.Count;

        string text;
        if (h > 0)
        {
            var labels = headerItems.Take(10).Select(i => $"“{i.Label ?? "(no label)"}”").ToList();
            var list = string.Join(", ", labels) + (h > 10 ? $", and {h - 10} more" : "");
            text = "Evidence for the manual check, not an automated check: Xcode's Accessibility Inspector " +
                   $"reported the Header trait on {h} of the {n} element{Plural(n)} it reached on this screen " +
                   $"(VoiceOver itself was not turned on): {list}. Check that every text that looks like a " +
                   "heading is in this list, and nothing that isn't a heading is.";
        }
        else
        {
            text = "Evidence for the manual check, not an automated check: Xcode's Accessibility Inspector " +
                   $"reported no Header trait on any of the {n} element{Plural(n)} it reached on this screen " +
                   "(VoiceOver itself was not turned on). If any text on this screen looks like a heading, " +
                   "check it is exposed as one.";
        }

        if (!capture.Complete)
            text += $" The walk stopped before the end of this screen ({capture.NotCompleteReason ?? "reason not recorded"}), " +
                    "so elements after that point are not included.";
        return text;
    }

    /// <summary>Items matched to a tree node confidently enough for a name/role comparison to mean anything --
    /// see <see cref="ScreenReaderCaptureComparer"/>'s own remarks on why a <see cref="MatchConfidence.Weak"/>
    /// match is too uncertain a basis for that.</summary>
    private static int ComparedCount(ScreenReaderCapture capture) =>
        capture.Items.Count(i => i.MatchedNodePath is not null && i.MatchConfidence is MatchConfidence.Exact or MatchConfidence.Likely);

    private static bool IsCaptureFinding(Finding f) =>
        f.RuleId == CaptureRuleId || f.RuleId.StartsWith(CaptureRuleId + ":", StringComparison.Ordinal);

    private static int DifferCount(ScreenResult screen, WcagCriterion criterion) =>
        screen.Findings.Count(f => IsCaptureFinding(f) && f.Criteria.Contains(criterion));

    /// <summary>How many of this criterion's capture-evidence differences sit on the same element (by
    /// <see cref="Finding.NodePath"/>) as a finding from a DIFFERENT rule citing the same criterion -- a real
    /// screen-reader difference and a tree-only rule disagreeing about the same control, which must be
    /// flagged rather than left as two unlinked findings (owner: contradictions with automated findings are
    /// flagged, not silently resolved).</summary>
    private static int CountOverlap(ScreenResult screen, WcagCriterion criterion)
    {
        var others = screen.Findings.Where(f => !IsCaptureFinding(f) && f.Criteria.Contains(criterion)).ToList();
        if (others.Count == 0)
            return 0;
        var otherPaths = others.Select(f => f.NodePath).ToHashSet();
        return screen.Findings.Count(f => IsCaptureFinding(f) && f.Criteria.Contains(criterion) && otherPaths.Contains(f.NodePath));
    }

    /// <summary>Appends a caveat when this capture hit a real problem -- gated on <see cref="ScreenReaderCapture.Complete"/>
    /// alone: for a TalkBack capture, Complete now genuinely means "every focusable/interactive element this
    /// walk found was reached and said something" (the harness's own scope, always
    /// <see cref="ScreenReaderCaptureScope.FocusableElementsOnly"/>, is stated separately in the sentences
    /// above, not treated as a reason to caveat here), so this only fires for a real early stop or refusal --
    /// never on every TalkBack capture the way an earlier version of this method did before Complete's own
    /// meaning for TalkBack was fixed (see the harness's own history for that bug).</summary>
    private static string AppendIncomplete(string text, ScreenReaderCapture capture) =>
        capture.Complete
            ? text
            : text + $" The capture did not cover everything on this screen ({capture.NotCompleteReason ?? "reason not recorded"}), " +
                     "so some elements were not compared.";

    private static string AppendOverlap(string text, ScreenResult screen, WcagCriterion criterion)
    {
        var overlap = CountOverlap(screen, criterion);
        if (overlap == 0)
            return text;
        return text + $" {overlap} of these {(overlap == 1 ? "is" : "are")} on an element another automated check " +
               $"on this screen also reported under {criterion.Number}; the two may disagree, so check both.";
    }

    private static string Plural(int n) => n == 1 ? "" : "s";
    private static string WasWere(int n) => n == 1 ? "was" : "were";
}
