using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// Screen-level check on the normal (not large-text) capture: flags interactive controls and named content
/// whose bounds lie wholly or mostly outside the screen's visible area, with no scrollable ancestor anywhere
/// between them and the root, so nothing on the screen could bring them into view by scrolling.
///
/// First found by eye on a physical iPhone SE: BuggyApp's first screen (MainPage.xaml, a plain
/// VerticalStackLayout with no ScrollView -- see <see cref="LargeTextLostContentRule"/>'s remarks on the
/// same screen) does not fit at normal/100% text size on that device, with "Save for later" and "View
/// payment history" below the bottom edge and no way to scroll to them. Confirmed by this rule on an
/// iPhone SE (3rd generation) Simulator capture (375×667 pt): "View payment history" (678-728 on the
/// 667-pt-tall screen) is flagged; "Save for later" (ending at 666) is not, correctly, since it is still
/// fully on-screen. Also confirmed on a physical iPhone SE (375×667 pt, iOS 27.0): the same finding,
/// naming "View payment history" once (not duplicated -- see <see cref="Collect"/>'s remarks on why a
/// control and its own child label node aren't both reported), matching what was seen by eye.
/// Xcode's Accessibility Inspector
/// still lists these controls even when they're off-screen (XCUITest's snapshot usually keeps an
/// element's real frame regardless of its position -- see KnownLimitations
/// "large-text-lost-content-heuristic" for the same fact, and its own hedge: lazily-created content such
/// as an unrendered table/collection cell can still be absent), and a screen reader may still reach them
/// by swiping, so a screen-reader-only check can miss that a sighted or touch user cannot reach them at
/// all. This rule is a companion to <see cref="LargeTextLostContentRule"/>, not a replacement: that rule
/// compares two captures (normal vs. enlarged text) and only fires when a large-text capture was made;
/// this one looks at a single, normal-size capture and fires whether or not large text was ever
/// requested for this run.
///
/// Restricted to iOS. On Android, uiautomator dump does not report a node's true frame: AOSP's
/// AccessibilityNodeInfoDumper writes each node's bounds through getVisibleBoundsInScreen, which clips
/// them to the screen's visible area before they're ever written out. A node that is partly off-screen
/// therefore reports bounds ending exactly at the screen edge, not beyond it -- confirmed in this
/// project's own fixture, tests/Swipewalk.Core.Tests/Fixtures/BuggyApp.Android.Pixel4a/large/uiautomator.xml,
/// where "Pay", cut off at the bottom of that capture, has bounds [55,2241][1025,2340] on a 2340 px-tall
/// screen -- ending precisely at the edge. A node that is entirely off-screen is omitted from the dump
/// altogether (see KnownLimitations "large-text-lost-content-heuristic"). Either way, this rule has
/// nothing to measure on Android: every candidate's <see cref="OutsideShare"/> would read as (at most) 0,
/// so the check would silently never fire there rather than genuinely check anything. See KnownLimitations
/// "offscreen-unreachable-android-gap".
///
/// WCAG mapping: WCAG 1.4.10 Reflow (AA). Reflow's normative text requires content to be presented
/// "without loss of information or functionality" (not only without two-dimensional scrolling), and the
/// W3C has a named failure technique for exactly this shape (F102: content disappearing and not being
/// available when content has reflowed). WCAG2ICT applies 1.4.10 to software directly, including to a
/// change in a software window's own size (its Note 5), and its Note 7 says to evaluate at the nearest
/// available size when a platform can't reach 320 CSS px. A phone's own portrait width is that nearest
/// size. WCAG's own reference viewport for this criterion is 320×256 (CSS px, treated by WCAG2ICT as
/// dp/pt); a screen at least that large finding a genuine, un-scrollable loss is a stronger case than
/// WCAG's own tested condition, so citing 1.4.10 fits this project's own rule to cite WCAG only when a
/// result implies failure at the WCAG-tested level. A screen SMALLER than 320×256 is reported instead as
/// a platform advisory with no WCAG citation, since that is beyond what 1.4.10 itself was tested against.
/// Always reported as <see cref="FindingKind.NeedsReview"/> (or <see cref="FindingKind.PlatformAdvisory"/>
/// below the WCAG reference size), never a confirmed failure: automated checks cannot tell
/// reachable-by-scroll from genuinely lost, nor rule out the app offering another way to reach the
/// content that the tree doesn't show.
/// </summary>
public sealed class OffscreenUnreachableRule : IRule
{
    private const string RuleIdValue = "offscreen-unreachable";

    public string Id => RuleIdValue;

    /// <summary>
    /// A node counts as unreachable once at least this share of its own area falls outside the screen's
    /// bounds -- "wholly or mostly outside"; a tiny sliver poking past the edge (less than half of the node)
    /// is not reported.
    /// </summary>
    private const double OutsideShareThreshold = 0.5;

    /// <summary>WCAG 1.4.10 Reflow's own reference viewport width (320 CSS px, treated as dp/pt by
    /// WCAG2ICT). A screen narrower than this is beyond what 1.4.10 was tested against.</summary>
    private const double WcagReflowMinWidth = 320;

    /// <summary>WCAG 1.4.10 Reflow's own reference viewport height (256 CSS px at 400% zoom on the 1280×1024
    /// reference display, treated as dp/pt by WCAG2ICT).</summary>
    private const double WcagReflowMinHeight = 256;

    /// <summary>Cited when a finding is seen on a screen smaller than WCAG 1.4.10's own 320×256 reference
    /// size, so it is reported as a platform advisory rather than a WCAG issue.</summary>
    public const string AdaptiveLayoutGuideline =
        "Apple Human Interface Guidelines: Layout -- adapt the layout to different screen sizes; " +
        "Android developer documentation: Support different display sizes";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        // See the class remarks: uiautomator dump clips every node's reported bounds to the visible
        // screen, so an off-screen node on Android never actually reports bounds outside the screen for
        // this rule to measure. Restricted to iOS until an unclipped Android source is available.
        if (snapshot.Platform != Platform.iOS)
            yield break;

        var screen = snapshot.Root.Bounds;
        if (screen.Width <= 0 || screen.Height <= 0)
            yield break;

        var offscreen = new List<AccessibilityNode>();
        Collect(snapshot.Root, screen, underScrollLikeAncestor: false, underReportedAncestor: false, offscreen);
        if (offscreen.Count == 0)
            yield break;

        var names = string.Join(", ", offscreen.Select(n => $"\"{ScreenReaderPredictor.AccessibleName(n)}\""));
        var count = offscreen.Count;
        var elementNoun = count == 1 ? "element" : "elements";
        var isAre = count == 1 ? "is" : "are";
        var appearAppears = count == 1 ? "appears" : "appear";
        var itThem = count == 1 ? "it" : "them"; // object position
        var itThey = count == 1 ? "it" : "they"; // subject position

        var width = Math.Round(screen.Width);
        var height = Math.Round(screen.Height);
        var meetsWcagReferenceSize = screen.Width >= WcagReflowMinWidth && screen.Height >= WcagReflowMinHeight;

        var body = $"{count} {elementNoun} present in the accessibility tree ({names}) {isAre} positioned wholly " +
                    "or mostly outside the visible screen at normal text size. None of the containers " +
                    $"holding {itThem} reports itself as scrollable, so {itThey} {appearAppears} to be out of " +
                    $"view, with nothing found on this screen that could scroll {itThem} into view (a screen " +
                    $"reader may still reach {itThem} by swiping, which is why a screen-reader-only check can " +
                    $"miss this). This screen is {width}×{height} pt at normal text size, " +
                    (meetsWcagReferenceSize
                        ? "at least as large as the 320×256 reference size WCAG 1.4.10 Reflow is tested " +
                          "against, so a genuine, unreachable loss here would also occur at that size; "
                        : "smaller than the 320×256 reference size WCAG 1.4.10 Reflow is tested against, so " +
                          "this is reported as a platform advisory rather than a WCAG issue; ") +
                    $"check by hand whether {itThey} can still be reached, for example by making the screen scrollable.";

        yield return meetsWcagReferenceSize
            ? new Finding
            {
                RuleId = RuleIdValue,
                Kind = FindingKind.NeedsReview,
                Message = body,
                Criteria = [WcagCriteria.Reflow],
                NodePath = "",
                Role = "screen",
            }
            : new Finding
            {
                RuleId = RuleIdValue,
                Kind = FindingKind.PlatformAdvisory,
                Message = body,
                PlatformGuideline = AdaptiveLayoutGuideline,
                NodePath = "",
                Role = "screen",
            };
    }

    /// <summary>
    /// Depth-first walk collecting every candidate node that is (mostly) off-screen, has no scrollable (or
    /// scrollable-like, see <see cref="IsScrollLike"/>) ancestor between it and the root, and is not itself
    /// a descendant of another node this same walk already reported (<paramref name="underReportedAncestor"/>)
    /// -- so a control and its own child label (for example a MAUI Button's text landing on a separate
    /// "text"-role child node in the tree) are reported once, as the control, not twice.
    /// </summary>
    private static void Collect(
        AccessibilityNode node, Bounds screen, bool underScrollLikeAncestor, bool underReportedAncestor,
        List<AccessibilityNode> result)
    {
        var reportable = !underScrollLikeAncestor && !underReportedAncestor
                          && IsCandidate(node) && OutsideShare(node.Bounds, screen) >= OutsideShareThreshold;
        if (reportable)
            result.Add(node);

        var childUnderScrollLikeAncestor = underScrollLikeAncestor || IsScrollLike(node);
        var childUnderReportedAncestor = underReportedAncestor || reportable;
        foreach (var child in node.Children)
            Collect(child, screen, childUnderScrollLikeAncestor, childUnderReportedAncestor, result);
    }

    /// <summary>
    /// True for an explicitly scrollable node (<see cref="AccessibilityNode.IsScrollable"/>, whatever
    /// direction it scrolls in -- the model does not distinguish horizontal from vertical, so this also
    /// covers horizontally-scrolling containers/pagers and a platform-reported off-screen-within-a-
    /// scrollable-parent case, since those are all just scrollable ancestors), and, defensively, for a
    /// web view or map: XCUITest maps a WKWebView to role "webview" but does not expose its own inner DOM
    /// scroll view as a separate "scrollView" element (see <c>XcuiTreeParser.MapRole</c>), and a map's
    /// annotations can sit outside its currently visible region the same way. Treating both as scroll-like
    /// avoids flagging ordinary below-the-fold web or map content as unreachable.
    /// </summary>
    private static bool IsScrollLike(AccessibilityNode node) =>
        node.IsScrollable
        || node.Role == "webview"
        || string.Equals(node.NativeType, "map", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Interactive controls and non-empty text, the same shape of candidate as
    /// <see cref="LargeTextLostContentRule"/>'s own Candidates method ("interactive or named content"),
    /// excluding disabled elements (nothing to reach if it can't be activated anyway -- the same convention
    /// <see cref="IconContrastRule"/> and <see cref="InputPurposeRule"/> use), elements already hidden from
    /// assistive technology, elements with no area, and system chrome such as status/navigation bars,
    /// individual keyboard keys and other IME elements. Collectors already scope their trees to the app under
    /// test's own package/process (see <c>UiAutomatorParser</c>'s package filter and XCUITest's app-scoped
    /// query), so system chrome should never actually reach this rule, but the check is kept as a safeguard
    /// rather than assumed away.
    /// </summary>
    private static bool IsCandidate(AccessibilityNode node) =>
        node.IsAccessible && node.IsEnabled && RuleFinding.HasArea(node) && !IsSystemChrome(node)
        && (node.IsInteractive || (node.Role == "text" && !string.IsNullOrWhiteSpace(node.VisibleText)))
        && ScreenReaderPredictor.AccessibleName(node) is not null;

    private static bool IsSystemChrome(AccessibilityNode node) =>
        node.Role == "key" // individual on-screen-keyboard keys (see XcuiTreeParser.MapRole)
        || ContainsAny(node.NativeType, "statusbar", "keyboard", "inputmethod");

    private static bool ContainsAny(string? value, params string[] needles) =>
        value is not null && needles.Any(n => value.Contains(n, StringComparison.OrdinalIgnoreCase));

    /// <summary>Share of <paramref name="node"/>'s own area that falls outside <paramref name="screen"/>.</summary>
    private static double OutsideShare(Bounds node, Bounds screen)
    {
        var area = node.Width * node.Height;
        if (area <= 0)
            return 0;

        var overlapWidth = Math.Max(0, Math.Min(node.X + node.Width, screen.X + screen.Width) - Math.Max(node.X, screen.X));
        var overlapHeight = Math.Max(0, Math.Min(node.Y + node.Height, screen.Y + screen.Height) - Math.Max(node.Y, screen.Y));
        var overlapArea = overlapWidth * overlapHeight;
        return (area - overlapArea) / area;
    }
}
