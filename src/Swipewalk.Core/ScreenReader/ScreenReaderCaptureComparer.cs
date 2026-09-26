using System.Text.RegularExpressions;
using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;

namespace Swipewalk.Core.ScreenReader;

/// <summary>What kind of difference was found between the predicted transcript and real screen-reader
/// evidence. See <see cref="ScreenReaderCaptureComparer.Compare"/>.</summary>
public enum ScreenReaderDifferenceKind
{
    /// <summary>A predicted stop with a name was never reported by the real reader.</summary>
    Missing,

    /// <summary>A captured item matched to a tree node the predicted transcript does not include a stop for.</summary>
    Extra,

    /// <summary>The matched pair's accessible name differs (after normalizing known non-differences).</summary>
    TextMismatch,

    /// <summary>The matched pair's relative position among all matched pairs differs between the predicted
    /// order and the reader's own order.</summary>
    OrderMismatch,

    /// <summary>The reader confidently reported a different role/trait than predicted (for example no
    /// button trait where Swipewalk predicted one).</summary>
    RoleMismatch,

    /// <summary>A captured item could not be matched to any scanned tree node at all (see
    /// <see cref="ScreenReaderCaptureItem.MatchedNodePath"/>). Not itself evidence of a WCAG issue --
    /// <see cref="Rules.ScreenReaderCaptureRule"/> does not turn this into a finding -- but shown in the
    /// report so a reader can see what the capture couldn't line up to the tree.</summary>
    Unmatched,
}

/// <summary>One difference between <see cref="ScreenReaderPredictor"/>'s prediction and real evidence.</summary>
/// <param name="Kind">What kind of difference this is.</param>
/// <param name="Predicted">The predicted stop involved, when there is one (null for <see cref="ScreenReaderDifferenceKind.Extra"/>
/// and <see cref="ScreenReaderDifferenceKind.Unmatched"/>).</param>
/// <param name="Actual">The captured item involved, when there is one (null for <see cref="ScreenReaderDifferenceKind.Missing"/>).</param>
/// <param name="Description">Plain-words explanation, naming the tool the evidence came from.</param>
public sealed record ScreenReaderDifference(
    ScreenReaderDifferenceKind Kind, Announcement? Predicted, ScreenReaderCaptureItem? Actual, string Description);

/// <summary>
/// Compares Swipewalk's predicted screen-reader transcript (<see cref="ScreenReaderPredictor.Predict"/>)
/// against real evidence (<see cref="ScreenReaderCapture"/>) and reports what differs. Pure function: no
/// side effects, no I/O; works the same for every <see cref="ScreenReaderSource"/> -- see that enum for what
/// each source fills in.
///
/// Two things are normalized before anything is compared, so known non-differences are never reported as
/// mismatches:
/// <list type="bullet">
/// <item>Swipewalk's own "Unlabeled" placeholder (<see cref="ScreenReaderPredictor.Announce"/>) is never
/// itself spoken by a real reader -- it is stripped from the predicted text before comparing.</item>
/// <item>A trailing role word is recognized whether it's introduced by "<c>, </c>" or "<c>. </c>" (with or
/// without a trailing period), since different tools/routes punctuate this differently -- but nothing else
/// in the text is touched, so punctuation inside a name (for example "Dr. Smith") survives intact.</item>
/// </list>
/// Role words are read from the same fixed vocabulary <see cref="ScreenReaderPredictor.RoleWord"/> uses (for
/// spoken text) and, for the Accessibility Inspector's <see cref="ScreenReaderCaptureItem.Traits"/>, from a
/// smaller, deliberately conservative subset (see <see cref="ConfidentRoleWords"/>) that this comparer is
/// confident the Inspector's trait vocabulary can positively confirm or deny -- a role word or trait this
/// comparer doesn't recognize is treated as "not confidently known" rather than guessed, so this can
/// under-report a role mismatch but should not over-report one.
///
/// Order is compared by relative rank among matched pairs only (not by the captured/predicted order numbers
/// directly): a single unmatched or extra item elsewhere on the screen must not shift every later item's
/// apparent order.
/// </summary>
public static class ScreenReaderCaptureComparer
{
    public static IReadOnlyList<ScreenReaderDifference> Compare(IReadOnlyList<Announcement> predicted, ScreenReaderCapture capture)
    {
        var predictedByPath = predicted
            .GroupBy(a => a.NodePath)
            .ToDictionary(g => g.Key, g => g.First()); // NodePath is unique per Predict(); First() is defensive.
        var diffs = new List<ScreenReaderDifference>();
        var tool = ToolLabel(capture.Source);
        var matched = new List<(Announcement Stop, ScreenReaderCaptureItem Item)>();
        var matchedPaths = new HashSet<string>();

        foreach (var item in capture.Items)
        {
            if (item.MatchedNodePath is null)
            {
                diffs.Add(new(ScreenReaderDifferenceKind.Unmatched, null, item,
                    $"{tool} reported {Describe(item)} as its stop {item.Order}, which could not be matched to a scanned element."));
                continue;
            }

            if (!predictedByPath.TryGetValue(item.MatchedNodePath, out var stop))
            {
                diffs.Add(new(ScreenReaderDifferenceKind.Extra, null, item,
                    $"{tool} reported {Describe(item)} as its stop {item.Order}, which the predicted transcript has no stop for."));
                continue;
            }

            matched.Add((stop, item));
            matchedPaths.Add(item.MatchedNodePath);
        }

        // Rank each matched pair among the matched pairs only, independently by predicted order and by
        // capture order, so an item the capture couldn't match (Unmatched/Extra) never shifts everyone
        // else's apparent order -- see this type's remarks.
        var predictedRank = matched.OrderBy(p => p.Stop.Order).Select((p, i) => (p.Item.MatchedNodePath!, Rank: i + 1))
            .ToDictionary(x => x.Item1, x => x.Rank);
        var actualRank = matched.OrderBy(p => p.Item.Order).Select((p, i) => (p.Item.MatchedNodePath!, Rank: i + 1))
            .ToDictionary(x => x.Item1, x => x.Rank);

        foreach (var (stop, item) in matched)
        {
            var path = item.MatchedNodePath!;
            if (predictedRank[path] != actualRank[path])
                diffs.Add(new(ScreenReaderDifferenceKind.OrderMismatch, stop, item,
                    $"Predicted as stop {stop.Order} (“{stop.Text}”); {tool} actually reported it as stop {item.Order}."));

            // A weak match (order alone, on a busy screen) is too uncertain a basis for a name/role
            // difference to mean anything -- it may well be the wrong element entirely; order differences
            // and Missing/Extra/Unmatched are unaffected, since those don't depend on which specific pair matched.
            if (item.MatchConfidence == MatchConfidence.Weak)
                continue;

            var (predictedName, predictedRole) = ParseAnnouncement(stop.Text);
            var (actualName, actualRole) = ActualNameAndRole(item, capture.Source);

            if (!NamesMatch(predictedName, actualName, capture.Source, capture.Language))
                diffs.Add(new(ScreenReaderDifferenceKind.TextMismatch, stop, item,
                    $"Predicted the name “{predictedName ?? "(none)"}”; {tool} reported {Describe(item)}."));

            if (predictedRole is not null && RoleIsConfidentlyKnown(item, capture.Source, predictedRole) && !RolesMatch(predictedRole, actualRole))
                diffs.Add(new(ScreenReaderDifferenceKind.RoleMismatch, stop, item,
                    $"Predicted the role “{predictedRole}”; {tool} reported {Describe(item)}{(actualRole is null ? " with no matching role/trait" : $", role/trait “{actualRole}” instead")}."));
        }

        // Predicted stops with a name that no captured item matched -- only meaningful when the capture
        // covered the whole screen (see ScreenReaderCapture.Complete): on an incomplete capture, an
        // unmatched predicted stop may simply be past where the capture stopped, not a real absence.
        if (capture.Complete)
        {
            foreach (var stop in predicted.Where(a => a.HasName && !matchedPaths.Contains(a.NodePath)))
                diffs.Add(new(ScreenReaderDifferenceKind.Missing, stop, null,
                    $"Predicted stop {stop.Order} (“{stop.Text}”) was not reported by {tool}."));
        }

        return diffs;
    }

    /// <summary>The name a person reads for a difference source.</summary>
    public static string ToolLabel(ScreenReaderSource source) => source switch
    {
        ScreenReaderSource.TalkBack => "TalkBack",
        ScreenReaderSource.AccessibilityInspector => "Xcode's Accessibility Inspector",
        ScreenReaderSource.VoiceOverCaptions => "VoiceOver",
        _ => source.ToString(),
    };

    private static string Describe(ScreenReaderCaptureItem item)
    {
        if (item.SpokenText is { Length: > 0 } spoken)
            return $"“{spoken}”";
        var name = item.Label ?? item.Value;
        var traits = item.Traits is { Count: > 0 } t ? $" ({string.Join(", ", t)})" : "";
        return name is null ? $"an unnamed element{traits}" : $"“{name}”{traits}";
    }

    /// <summary>Fixed vocabulary of role words <see cref="ScreenReaderPredictor.Announce"/> can append,
    /// longest first so "Radio button" isn't cut short by a shorter prefix match.</summary>
    private static readonly string[] RoleWords =
    [
        "Radio button", "Edit box", "Text field", "Web view",
        "Button", "Link", "Image", "Checkbox", "Switch", "Slider", "Heading",
    ];

    /// <summary>Role words this comparer is confident the Accessibility Inspector's trait vocabulary
    /// (<see cref="TraitRoleWords"/>) can positively confirm or deny. Deliberately smaller than
    /// <see cref="RoleWords"/>: the Inspector's coverage of "Text field", "Checkbox", "Switch", "Radio
    /// button" and "Web view" traits is not established, so a predicted role outside this set is never
    /// treated as confidently confirmed or denied by an Inspector capture -- see this type's remarks.</summary>
    private static readonly HashSet<string> ConfidentRoleWords = ["Button", "Link", "Image", "Heading", "Slider"];

    /// <summary>Splits an announcement's text into its name and trailing role word. Only a recognized
    /// trailing role word (see <see cref="RoleWords"/>) is stripped -- nothing else in the text is touched,
    /// so punctuation inside a name is never altered. Returns a null name for Swipewalk's own "Unlabeled"
    /// placeholder or empty text.</summary>
    internal static (string? Name, string? Role) ParseAnnouncement(string text)
    {
        text = text.Trim();
        if (TryStripTrailingWord(text, "disabled", out var withoutDisabled))
            text = withoutDisabled;

        foreach (var role in RoleWords)
        {
            if (TryStripTrailingWord(text, role, out var name))
                return (IsUnlabeled(name) ? null : name, role);
        }

        return (IsUnlabeled(text) ? null : text, null);
    }

    /// <summary>Strips a trailing "<c>, word</c>" or "<c>. word</c>" (optionally followed by a period), or
    /// "<c>word</c>" alone, from the end of <paramref name="text"/>. Leaves everything else untouched.</summary>
    private static bool TryStripTrailingWord(string text, string word, out string remainder)
    {
        if (text.Equals(word, StringComparison.Ordinal))
        {
            remainder = "";
            return true;
        }
        foreach (var separator in new[] { ", ", ". " })
        {
            var suffix = separator + word;
            if (text.EndsWith(suffix + ".", StringComparison.Ordinal))
            {
                remainder = text[..^(suffix.Length + 1)];
                return true;
            }
            if (text.EndsWith(suffix, StringComparison.Ordinal))
            {
                remainder = text[..^suffix.Length];
                return true;
            }
        }
        remainder = text;
        return false;
    }

    private static bool IsUnlabeled(string name) => name is "" or "Unlabeled";

    /// <summary>Separator harness/android's <c>TalkBackCollector.kt</c> joins multiple utterances with,
    /// when an older TalkBack version splits one element's announcement into several (or a separate,
    /// later usage hint arrives before the walk moves on) -- must match its
    /// <c>UtteranceJoinSeparator</c> exactly.</summary>
    public const string UtteranceJoinSeparator = " || ";

    /// <summary>The effective (name, role) for a captured item: parsed from
    /// <see cref="ScreenReaderCaptureItem.SpokenText"/> for spoken sources, or read from the Accessibility
    /// Inspector's structured fields. A TalkBack item can be several utterances joined with
    /// <see cref="UtteranceJoinSeparator"/> (see that constant's remarks) -- each is parsed on its own, and
    /// the name and role are taken independently: the role is the last segment recognized as a role word
    /// (see <see cref="RoleWords"/>); the name is every OTHER segment's own text, joined back together in
    /// order (a segment that parsed to nothing but a recognized role word contributes nothing -- it IS the
    /// role, not part of the name). Needed because an older TalkBack version was observed (the emulator,
    /// TalkBack 16) to speak an element's name and role as two SEPARATE utterances ("Ticket number" then
    /// "Edit box") rather than one combined "Ticket number, Edit box" -- taking name and role from the same
    /// (last) segment would find "Edit box" alone (a bare role word parses as an empty name), discarding
    /// the real name and causing a false TextMismatch against every such element. Joining every non-role
    /// segment back together (rather than keeping only the last non-empty one) also covers a device set to
    /// a language <see cref="RoleWords"/> doesn't know: TalkBack's OWN role/hint word there (for example
    /// Spanish "Botón") doesn't match any entry in that English-only list, so it parses as if it were a
    /// name -- if that segment came after the real name it would otherwise silently replace it. Joining
    /// keeps the real name in the result; <see cref="NamesMatch"/>'s substring fallback then still matches
    /// it even with the untranslated role word trailing alongside it.
    /// <para>Internal (not private) so <see cref="Rules.ScreenReaderLabelInNameRule"/> can read the same
    /// (name, role) pair this comparer uses, rather than re-implementing TalkBack's utterance-joining and
    /// role-word stripping a second time.</para></summary>
    internal static (string? Name, string? Role) ActualNameAndRole(ScreenReaderCaptureItem item, ScreenReaderSource source)
    {
        if (source == ScreenReaderSource.AccessibilityInspector)
        {
            var name = item.Label ?? item.Value;
            return (string.IsNullOrEmpty(name) ? null : name, RoleFromTraits(item.Traits));
        }

        if (item.SpokenText is null)
            return (null, null);
        if (source != ScreenReaderSource.TalkBack || !item.SpokenText.Contains(UtteranceJoinSeparator, StringComparison.Ordinal))
            return ParseAnnouncement(item.SpokenText);

        var parsed = item.SpokenText.Split(UtteranceJoinSeparator).Select(ParseAnnouncement).ToList();
        string? mergedRole = null;
        var nameParts = new List<string>();
        foreach (var (segmentName, segmentRole) in parsed)
        {
            if (segmentRole is not null) mergedRole = segmentRole;
            if (segmentName is not null) nameParts.Add(segmentName);
        }
        return (nameParts.Count > 0 ? string.Join(" ", nameParts) : null, mergedRole);
    }

    /// <summary>
    /// Whether this item's role can confidently be compared against <paramref name="predictedRole"/> at
    /// all. For a spoken source, only when the utterance itself parsed out a recognized role word (the
    /// reader said a role, whether or not it agrees with the prediction) -- an utterance ending in something
    /// this comparer doesn't recognize (a usage hint, a state description) says nothing confident about
    /// role. For the Accessibility Inspector, only when traits were actually read
    /// (<see cref="ScreenReaderCaptureItem.Traits"/> non-null) and the predicted role is one
    /// <see cref="ConfidentRoleWords"/> covers.
    /// </summary>
    private static bool RoleIsConfidentlyKnown(ScreenReaderCaptureItem item, ScreenReaderSource source, string predictedRole)
    {
        if (source == ScreenReaderSource.AccessibilityInspector)
            return item.Traits is not null && ConfidentRoleWords.Contains(predictedRole);

        return item.SpokenText is not null && ActualNameAndRole(item, source).Role is not null;
    }

    /// <summary>Maps a deliberately small subset of Apple's Accessibility Inspector trait names to the same
    /// role-word vocabulary <see cref="ScreenReaderPredictor.RoleWord"/> uses. Only the traits named in
    /// <see cref="ConfidentRoleWords"/> are covered here -- see that field's remarks.</summary>
    private static readonly IReadOnlyDictionary<string, string> TraitRoleWords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Button"] = "Button",
        ["Link"] = "Link",
        ["Image"] = "Image",
        ["Adjustable"] = "Slider",
        ["Header"] = "Heading",
    };

    private static string? RoleFromTraits(IReadOnlyList<string>? traits) =>
        traits is null ? null : traits.Select(t => TraitRoleWords.GetValueOrDefault(t)).FirstOrDefault(r => r is not null);

    /// <summary>
    /// For TalkBack, also accepts a whole-word match (every word of the predicted name appears as its own
    /// consecutive run of words, in order, somewhere in what TalkBack said -- punctuation and spacing
    /// between those words is ignored, but nothing else may come between them) as well as exact equality:
    /// predicted names are always in <see cref="ScreenReaderPredictor"/>'s own (English) wording, built
    /// straight from the app's own tree text, but TalkBack's ROLE WORDS and hints are in the device's
    /// language -- verified with the device's system language set to English, Spanish, Hindi, Arabic and
    /// Japanese (see docs/case-study.md, "Real TalkBack capture, in five languages"): TalkBack did not
    /// translate the app's own accessible names in any of them, only its own chrome around them, so an
    /// app's English name still appeared verbatim even with TalkBack set to another language; a strict
    /// equality check would misread that surrounding, localized text as a name mismatch. Matching by
    /// WHOLE words (not a raw substring) matters: predicted "Pay" is not a match inside "Payment
    /// history" -- an earlier version of this check used plain substring containment and would have
    /// missed exactly this kind of real difference (a false negative, worse than the false positive it
    /// was trying to avoid). This still deliberately trades a little precision for not over-reporting
    /// across languages (a predicted name that happens to recur as its own consecutive words in an
    /// unrelated announcement would also "match") -- consistent with this comparer's existing under-
    /// rather than over-reporting design (see this type's remarks). One language-specific limit this
    /// doesn't solve: <see cref="Words"/> splits on anything that ISN'T a letter, mark or digit (so
    /// punctuation splits words, but nothing else does -- there's no notion of whitespace specifically), so
    /// a language written with no separators between words at all (Japanese and Chinese) tokenizes a whole
    /// run of adjacent script as ONE word -- an app's own name immediately followed by a role/hint word
    /// with no separator in between, in one of those scripts, would not match even though TalkBack said
    /// the name verbatim.
    /// Every Japanese announcement seen in the five-language verification above had a plain space or
    /// this comparer's own <see cref="UtteranceJoinSeparator"/> between the app's name and TalkBack's own
    /// chrome, never the two scripts running directly together with nothing between them, so this limit
    /// was not exercised there.
    /// <para>
    /// When Swipewalk predicted NO name at all (an icon-only control with nothing for
    /// <see cref="ScreenReaderPredictor"/> to read) and TalkBack's own role/hint vocabulary
    /// (<see cref="RoleWords"/>) is English only, a single untranslated role word on a non-English device
    /// (Spanish "Botón" for "Button") parses as if it were a name -- there is no vocabulary to recognize
    /// and strip it (see <see cref="ActualNameAndRole"/>'s remarks). With nothing predicted to compare it
    /// against, this can't tell that role word apart from a genuine but foreign-language accessible name,
    /// so on a non-English <paramref name="language"/> this reports a match rather than guess -- the same
    /// under- rather than over-reporting choice as the substring fallback above. English is unaffected: a
    /// recognized role word there is already stripped to an empty name by <see cref="ParseAnnouncement"/>
    /// before this is ever called.</para>
    /// <para>
    /// This forgiveness stops at anything that looks like a developer identifier
    /// (<see cref="IdentifierNameRule.LooksLikeIdentifier"/>, e.g. "btnCancelPayment"): that is never a
    /// legitimate localized role word in any language, and it is real evidence a real capture can find
    /// that a tree scan alone cannot -- samples/BuggyApp has a button with no accessible name in the
    /// tree (an AutomationId only, no label) that TalkBack nonetheless announces by that AutomationId's
    /// text, on a real device and the emulator alike; exactly how TalkBack sources that text wasn't
    /// determined. Losing that finding on a non-English capture would defeat the point of testing in more
    /// than one language, so it is still reported there.</para>
    /// <para>Internal (not private) so <see cref="Rules.ScreenReaderLabelInNameRule"/> can reuse this exact
    /// predicate for WCAG 2.5.3 Label in Name: there, the "predicted" side is a control's own visible text
    /// (never empty, since that rule requires one) rather than <see cref="ScreenReaderPredictor"/>'s
    /// announcement text, and the "actual" side is still <see cref="ActualNameAndRole"/>'s parsed name --
    /// otherwise the exact same whole-word, role-word-aware and language-aware matching this comparer
    /// already uses.</para>
    /// </summary>
    internal static bool NamesMatch(string? predicted, string? actual, ScreenReaderSource source, string? language)
    {
        var (p, a) = (Norm(predicted), Norm(actual));
        if (string.Equals(p, a, StringComparison.OrdinalIgnoreCase))
            return true;
        if (source != ScreenReaderSource.TalkBack)
            return false;
        if (p.Length > 0)
            return ContainsWholeWords(a, p);
        return language is not null && !language.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            && !IdentifierNameRule.LooksLikeIdentifier(a, automationId: null);
    }

    /// <summary>Splits into runs of letters, combining marks and digits -- Unicode-aware (includes marks
    /// so a Devanagari or Arabic vowel/diacritic sign stays attached to its base letter as one word, not
    /// its own token), and splits on anything else (punctuation, spaces alike) without treating whitespace
    /// as special: a script with no separators between words at all (Japanese, Chinese) still tokenizes a
    /// whole run of adjacent script as a single word -- see <see cref="NamesMatch"/>'s
    /// remarks for what that means for those two languages specifically. This is the unit
    /// <see cref="ContainsWholeWords"/> compares.</summary>
    private static readonly Regex Words = new(@"[\p{L}\p{M}\p{N}]+", RegexOptions.Compiled);

    /// <summary>
    /// Whether every word of <paramref name="needle"/> appears, in order and CONSECUTIVELY (nothing else
    /// in between), as its own word (not merely as a substring of a longer word) somewhere in
    /// <paramref name="haystack"/> -- for example "Ticket number" found inside "Ticket number Edit box",
    /// but "Pay" NOT found inside "Payment history" (a real difference a plain <c>Contains</c> check would
    /// have hidden). Word-by-word rather than whole-phrase substring matching only so punctuation/spacing
    /// differences WITHIN the predicted phrase itself (a comma vs. a period, one space vs. two) don't
    /// defeat the match against TalkBack's own phrasing -- a word from something else landing in the
    /// middle of the predicted phrase (TalkBack inserting its own word between two words of the app's own
    /// name, which hasn't been observed) would still not match.
    /// </summary>
    private static bool ContainsWholeWords(string haystack, string needle)
    {
        var haystackWords = Words.Matches(haystack).Select(m => m.Value).ToList();
        var needleWords = Words.Matches(needle).Select(m => m.Value).ToList();
        if (needleWords.Count == 0)
            return false;
        for (var start = 0; start + needleWords.Count <= haystackWords.Count; start++)
        {
            var allMatch = true;
            for (var i = 0; i < needleWords.Count; i++)
            {
                if (!string.Equals(haystackWords[start + i], needleWords[i], StringComparison.InvariantCultureIgnoreCase))
                {
                    allMatch = false;
                    break;
                }
            }
            if (allMatch)
                return true;
        }
        return false;
    }

    private static bool RolesMatch(string? predicted, string? actual) =>
        string.Equals(Norm(predicted), Norm(actual), StringComparison.OrdinalIgnoreCase);

    private static string Norm(string? s) => (s ?? "").Trim();
}
