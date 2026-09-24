using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// WCAG 1.4.4 Resize Text, partial automated check. Flags interactive controls and text present at normal
/// text size that are gone from the accessibility tree at the larger size, distinct from
/// <see cref="TextResizeRule"/>, which only asks whether a *matched* element grew.
///
/// Verified on a Pixel 4a (2026-09-23, BuggyApp's first screen at Android font scale 2.0): three controls
/// present and reachable at normal text size ("Save for later", "View payment history", and an icon button
/// whose accessible name is "img_email_receipt") are completely absent from <c>uiautomator dump</c>'s tree
/// at the larger size -- not merely off-screen leaves either: an entire off-screen container (the
/// HorizontalStackLayout holding two icon buttons) is missing too, so <c>uiautomator dump</c> does not keep
/// a placeholder for content it can't currently see, container or leaf. This was confirmed through the
/// Android instrumentation harness -- <c>UiAutomation#getWindows()</c> does still see the leaves, each
/// reporting <c>isVisibleToUser() == false</c> -- but a rule built only on <c>uiautomator dump</c> (what
/// Swipewalk's Android collector actually uses) can't rely on that; it has to work from what the dump
/// itself omits. The only ScrollView-class node on this screen is MAUI's own navigation layout (Android
/// resource id <c>navigation_layout</c>, holding the app bar and the page host) -- not a scroll container
/// the page's own content sits in: MainPage.xaml has no ScrollView at all. That node reports
/// <c>scrollable="false"</c> at the larger size (and, for that matter, at normal size too, since nothing
/// needed scrolling yet -- Android's scrollable flag means "has something to scroll right now", not "is a
/// scroll container"), and a live <c>adb shell input swipe</c> test produced no visible scroll at all: this
/// screen has no way to bring the missing controls into view. The same screen on the emulator (a taller
/// display, 1080x2424 vs. the Pixel 4a's 1080x2340) does not lose anything at the same 200% scale --
/// content loss here depends on how much extra space a device's screen has, not only on the app.
///
/// Distinguishing "gone but reachable by scrolling" from "gone for good": since a whole off-screen
/// subtree -- not just its leaves -- can be missing from the large-text capture, this rule can't reliably
/// walk a specific missing element's ancestors position-by-position (an early spike tried that and
/// misclassified the email-receipt button, whose immediate parent container had vanished too, as merely
/// "ambiguous" instead of unreachable, splitting one bug into two findings). Instead it asks one question
/// per screen: does the large-text tree contain *any* node reporting itself scrollable
/// (<see cref="AccessibilityNode.IsScrollable"/>) at all?
///   - Yes: every missing element on this screen is treated as likely just scrolled out of view -- no
///     finding, the same as <see cref="TextResizeRule"/> already treats an unmatched text (see its Pair
///     method). This can miss a real loss on a screen that has some unrelated scrollable region elsewhere,
///     but avoids guessing which container a vanished element belonged to.
///   - No: nothing on this screen can reveal any of the missing elements, which is what 1.4.4's "content or
///     functionality" wording, per WCAG's Understanding page, is about -- one NeedsReview finding names all
///     of them (a missing node is stronger evidence than an unmatched text is on its own, but still not
///     certain: a node could have been renamed rather than removed, or the app could offer another way to
///     reach it that the tree doesn't show).
///
/// Scale-dependent like <see cref="TextResizeRule"/>'s overlap findings, not like its "did not grow"
/// findings: more of a screen's content going past its edges is a *larger* problem at a *larger* scale, so
/// loss seen only beyond 200% (iOS AX3, about 235%) does not show it would also happen at the 200% WCAG
/// 1.4.4 asks for, and is reported as a platform advisory against Apple's Dynamic Type guidance instead.
/// (This is the opposite relationship from text that never grows at all, which -- if it doesn't grow at a
/// smaller tested scale -- can't grow at 200% either.)
/// </summary>
public sealed class LargeTextLostContentRule : IRule
{
    private const string RuleIdValue = "large-text-lost-content";

    public string Id => RuleIdValue;

    /// <summary>WCAG 1.4.4 Resize Text asks for 200%; above this, a loss finding is a platform advisory.</summary>
    public const double WcagScale = TextResizeRule.WcagScale;

    /// <summary>Cited on findings seen only beyond the WCAG 1.4.4 scale (200%).</summary>
    public const string DynamicTypeGuideline = TextResizeRule.DynamicTypeGuideline;

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        if (snapshot.LargeText is not { } large)
            yield break;

        var normalCandidates = Candidates(snapshot.Root);
        if (normalCandidates.Count == 0)
            yield break;

        var largeKeys = Candidates(large.Root).Select(c => c.Key).ToHashSet();
        var missing = normalCandidates.Where(c => !largeKeys.Contains(c.Key)).Select(c => c.Node).ToList();
        if (missing.Count == 0)
            yield break;

        // A scrollable container anywhere on the large-text screen means the missing elements likely just
        // scrolled out of view (see the class remarks for why this is a per-screen question, not a
        // per-element ancestor walk).
        if (large.Root.DescendantsAndSelf().Any(n => n.IsScrollable))
            yield break;

        var setting = snapshot.LargeTextSetting ?? "enlarged system text";
        var isWcagIssue = snapshot.LargeTextScale is not (> WcagScale);
        var names = string.Join(", ", missing.Select(n => $"\"{ScreenReaderPredictor.AccessibleName(n)}\""));
        var count = missing.Count;
        var isAre = count == 1 ? "is" : "are";
        var wasWere = count == 1 ? "was" : "were";
        var itThem = count == 1 ? "it" : "them"; // object position ("bring ... back into view")
        var itThey = count == 1 ? "it" : "they"; // subject position ("whether ... can")
        var evidence = new Dictionary<string, string> { ["screenshot"] = "largeText" };

        // "(beyond the 200%...)" is folded into the same "at {setting}" clause instead of a separate leading
        // sentence, so the setting is named once, not twice (a duplicate "At {setting} ... at {setting}"
        // wording was caught in review).
        var advisoryNote = isWcagIssue ? "" : " (beyond the 200% WCAG 1.4.4 Resize Text asks for)";
        var body = $"{count} element(s) present at normal text size ({names}) {isAre} missing from the " +
                    $"accessibility tree at {setting}{advisoryNote}, and nothing on this screen reports itself " +
                    $"as scrollable, so scrolling can't bring {itThem} back into view. This can mean content or " +
                    $"functionality is lost at the larger text size (or that {itThey} {wasWere} renamed, or the " +
                    $"screen changed between the two captures, for example a timer or live count); check by hand " +
                    $"whether {itThey} can still be reached, for example by making the screen scrollable.";

        yield return isWcagIssue
            ? new Finding
            {
                RuleId = RuleIdValue,
                Kind = FindingKind.NeedsReview,
                Message = body,
                Criteria = [WcagCriteria.ResizeText],
                NodePath = "",
                Role = "screen",
                Details = evidence,
            }
            : new Finding
            {
                RuleId = RuleIdValue,
                Kind = FindingKind.PlatformAdvisory,
                Message = body,
                PlatformGuideline = DynamicTypeGuideline,
                NodePath = "",
                Role = "screen",
                Details = evidence,
            };
    }

    /// <summary>Interactive controls and non-empty text, each keyed by role + accessible name so the same
    /// element can be matched between the normal and large-text trees. An element with no accessible name
    /// is skipped: <see cref="MissingNameRule"/> already reports that separately, and without a name there's
    /// nothing to say went missing.</summary>
    private static List<(AccessibilityNode Node, string Key)> Candidates(AccessibilityNode root) =>
        root.DescendantsAndSelf()
            .Where(n => n.IsAccessible && RuleFinding.HasArea(n)
                        && (n.IsInteractive || (n.Role == "text" && !string.IsNullOrWhiteSpace(n.VisibleText))))
            .Select(n => (Node: n, Name: ScreenReaderPredictor.AccessibleName(n)))
            .Where(e => e.Name is not null)
            .Select(e => (e.Node, Key: $"{e.Node.Role}|{e.Name}"))
            .ToList();
}
