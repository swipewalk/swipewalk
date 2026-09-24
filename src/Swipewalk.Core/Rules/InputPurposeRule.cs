using System.Text.RegularExpressions;
using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// WCAG 1.3.5 Identify Input Purpose: text fields whose label, hint or identifier text suggests they
/// collect one of the user's own personal details -- 1.3.5 applies only to the person using the
/// software's own information, not a field for someone else's -- from WCAG 1.3.5's list of input
/// purposes (name, email, phone, postal address, username, password, organization, birth date, website),
/// but where Swipewalk cannot confirm the app declared an autofill/content-type hint for the field.
///
/// Neither platform's accessibility tree exposes that declaration to Swipewalk today: Android's
/// AccessibilityNodeInfo (read via uiautomator dump or the instrumentation harness -- see
/// KnownLimitations "android-atf-harness") has no method for a field's declared autofill hint (that data
/// (View#getAutofillHints()) is only visible to the OS Autofill framework's own ViewNode, not to
/// AccessibilityNodeInfo, uiautomator or UiAutomation); AccessibilityNodeInfo#getInputType() exists but
/// describes only the on-screen keyboard variation (e.g. "email keyboard"), which is a different, weaker
/// signal than a declared autofill hint and is not read here to avoid implying a stronger check than
/// exists. iOS's XCUITest does not expose UITextContentType at all. So on both platforms this rule can
/// only guess a field's purpose from its visible label/hint/identifier text -- the same limitation the
/// task that added this rule called out for iOS and which turned out to apply equally to Android -- and
/// every finding is NeedsReview, never a WcagIssue: a field can decline to match a keyword and still
/// declare its purpose correctly, and a field can match a keyword and already declare it correctly too.
///
/// Conservative by design: only a fixed, common set of WCAG 1.3.5 purposes is matched, each requiring a
/// specific enough phrase (e.g. "first name", not bare "name", which is too broad -- see
/// KnownLimitations "input-purpose-heuristic"), so many real personal-data fields are not flagged at all
/// rather than risk flagging fields that are not personal data.
/// </summary>
public sealed partial class InputPurposeRule : IRule
{
    public string Id => "input-purpose";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        foreach (var (node, path) in snapshot.Root.DescendantsAndSelfWithPath())
        {
            if (node.Role != "textfield" || !node.IsAccessible || !node.IsEnabled || !RuleFinding.HasArea(node))
                continue;

            var match = Candidates(node)
                .Select(c => (c.Source, c.Text, Purpose: Purposes.FirstOrDefault(p => p.Pattern.IsMatch(c.Text))))
                .FirstOrDefault(m => m.Purpose is not null);
            if (match.Purpose is null)
                continue;

            var platformNote = snapshot.Platform == Platform.Android
                ? "Android's accessibility tree does not expose a field's declared autofill hint outside the app (AccessibilityNodeInfo has no such method)"
                : "iOS's XCUITest does not expose a field's declared UITextContentType";
            yield return RuleFinding.Create(Id, FindingKind.NeedsReview,
                $"This field's {match.Source} (\"{match.Text}\") suggests it collects {match.Purpose.Phrase}, " +
                $"one of WCAG 1.3.5's input purposes (1.3.5 applies only when a field collects the user's own " +
                $"information, not someone else's). {platformNote}, so Swipewalk can only guess the purpose " +
                "from its label, hint or identifier. Confirm the field declares its purpose (Android " +
                "autofillHints, iOS textContentType) so autofill and personalization tools can identify it.",
                node, path, [WcagCriteria.IdentifyInputPurpose]);
        }
    }

    /// <summary>Candidate (source, text) pairs to match keywords against, in the order checked: the
    /// field's own label, its hint, then its AutomationId split into words (e.g. "txtEmailAddress" ->
    /// "txt Email Address"). Never the field's visible text: for a text field that already has a value
    /// typed in, <see cref="AccessibilityNode.VisibleText"/> is that entered value, not a description of
    /// the field's purpose (see <see cref="RuleFinding"/> and <see cref="MissingNameRule"/> for the same
    /// distinction). The finding message quotes whichever of these actually matched, and names its source,
    /// rather than always quoting the label (which may not be what matched, or may be null).</summary>
    private static IEnumerable<(string Source, string Text)> Candidates(AccessibilityNode node)
    {
        if (node.Label is { Length: > 0 } label)
            yield return ("label", label);
        if (node.Hint is { Length: > 0 } hint)
            yield return ("hint", hint);
        if (node.AutomationId is { Length: > 0 } id)
        {
            var words = string.Join(' ', SplitWords().Matches(id).Select(m => m.Value));
            if (words.Length > 0)
                yield return ("identifier", words);
        }
    }

    private sealed record Purpose(string Phrase, Regex Pattern);

    // Every phrase names the user's OWN information: WCAG 1.3.5 only covers input purposes about the
    // person using the software, not a field for someone else's details (e.g. "Recipient email",
    // "Emergency contact phone" are out of scope and not specifically targeted by these patterns).
    private static readonly IReadOnlyList<Purpose> Purposes =
    [
        new("the user's own name", NamePattern()),
        new("the user's own email address", EmailPattern()),
        new("the user's own phone number", PhonePattern()),
        new("the user's own postal address", AddressPattern()),
        new("the user's own username", UsernamePattern()),
        new("the user's own password", PasswordPattern()),
        new("the user's own organization or company name", OrganizationPattern()),
        new("the user's own birth date", BirthDatePattern()),
        new("the user's own website address", UrlPattern()),
    ];

    [GeneratedRegex(@"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|[0-9]+")]
    private static partial Regex SplitWords();

    // "name" alone is deliberately excluded: too many non-personal fields ("file name", "team name",
    // "event name", "pet name") share the word. Only phrases specific to the user's own name are matched.
    [GeneratedRegex(@"\b(full|first|given|last|family|middle|legal|your|display|nick)\s*name\b|\bsurname\b", RegexOptions.IgnoreCase)]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"\be[\s-]?mail\b", RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();

    // Bare "phone"/"mobile" are excluded: a settings screen can have a "Phone model" or "Mobile data"
    // field that isn't a contact number. "telephone" alone is kept -- rarely used for anything else.
    [GeneratedRegex(@"\bphone\s*number\b|\btelephone\b|\bmobile\s*number\b|\bcell\s*(phone|number)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"\b(street|mailing|shipping|billing|home)\s*address\b|\baddress\s*line\b|\bpostal\s*code\b|\bzip\s*code\b|\bzipcode\b", RegexOptions.IgnoreCase)]
    private static partial Regex AddressPattern();

    [GeneratedRegex(@"\buser\s*name\b|\bscreen\s*name\b", RegexOptions.IgnoreCase)]
    private static partial Regex UsernamePattern();

    [GeneratedRegex(@"\bpassword\b", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordPattern();

    // Bare "company"/"organization" are excluded: a field like "Company size" isn't personal data.
    [GeneratedRegex(@"\b(company|organization|organisation|employer)\s*name\b", RegexOptions.IgnoreCase)]
    private static partial Regex OrganizationPattern();

    [GeneratedRegex(@"\bdate\s*of\s*birth\b|\bbirth\s*date\b|\bbirthday\b|\bdob\b", RegexOptions.IgnoreCase)]
    private static partial Regex BirthDatePattern();

    [GeneratedRegex(@"\bweb\s*site\b|\bwebsite\b|\bweb\s*address\b", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();
}
