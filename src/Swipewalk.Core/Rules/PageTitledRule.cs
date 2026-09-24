using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// WCAG 2.4.2 Page Titled, Android only, using the pane title the Android instrumentation harness
/// reads (<c>AccessibilityNodeInfo#getPaneTitle()</c>, API 28; see <see cref="AccessibilityNode.PaneTitle"/>
/// and KnownLimitations "android-atf-harness"). WCAG2ICT's recommended substitute wording for 2.4.2 on
/// non-web software ("Non-web Software Titled") asks for a title through the platform's own title
/// property for each window or screen; Android's pane title is exactly that property for a
/// single-Activity, multi-page app (Google recommends it for apps, like MAUI Shell/NavigationPage ones,
/// that host several pages/fragments in one Activity, since the Activity's own title does not change as
/// the user navigates). This rule doesn't read the separate window title
/// (<c>AccessibilityWindowInfo#getTitle()</c>, API 24) -- see KnownLimitations "android-page-title" -- so
/// a missing pane title isn't automatically a failure: the window title may still convey the screen's
/// purpose in a way this rule can't see. A visible toolbar title or on-screen heading is a different WCAG
/// concern (1.3.1/2.4.6), not a title exposed to screen readers by itself, and does not clear this
/// finding -- confirmed against samples/BuggyApp's MainPage, which sets a MAUI Page.Title shown in its
/// NavigationPage toolbar and has two headings (SemanticProperties.HeadingLevel), yet still has no pane
/// title anywhere in its captured tree. So a screen with no pane title is reported as NeedsReview, never a
/// WcagIssue.
///
/// Only evaluated when <see cref="ScreenSnapshot.AtfRan"/> is true: PaneTitle is exclusively populated by
/// the same harness pass that produces ATF issues (see AndroidCollector.LoadAtfHarness), so when the
/// harness didn't run for this capture, Swipewalk never read pane titles at all and firing "no title
/// exposed" would describe something it never checked, not something it observed. When the harness did run
/// but the device is Android 8.0/8.1 (API 26-27, below getPaneTitle's API 28), no node will ever carry a
/// PaneTitle either way, so this rule still reports NeedsReview on those OS versions even for a screen that
/// has a title -- documented as a caveat in KnownLimitations "android-page-title" rather than guessed away
/// silently; the message only states what was found (no pane title exposed), not why.
///
/// iOS: not implemented. XCUITest does expose a "navigationBar" element (see
/// Swipewalk.Collectors.Ios.XcuiTreeParser, currently used only to crop the status/nav bar area out of
/// screenshots) whose child elements can include the screen's title text, but nothing in this codebase has
/// verified that a navigationBar's presence/title reliably corresponds to WCAG2ICT's "software titled"
/// concept across screen types (tab bar-only screens, sheets, screens with no navigation bar by design).
/// Building that mapping needs its own device verification, so this rule stays Android-only rather than
/// guess at an iOS equivalent; see KnownLimitations "android-page-title".
/// </summary>
public sealed class PageTitledRule : IRule
{
    public string Id => "page-titled";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        if (snapshot.Platform != Platform.Android || !snapshot.AtfRan)
            yield break;

        var hasTitle = snapshot.Root.DescendantsAndSelf().Any(n => !string.IsNullOrWhiteSpace(n.PaneTitle));
        if (hasTitle)
            yield break;

        yield return RuleFinding.Create(Id, FindingKind.NeedsReview,
            "No pane title is set anywhere on this screen (Android's accessibility pane title), so " +
            "Swipewalk found no screen-specific title exposed to screen readers. Check with TalkBack that a " +
            "title describing this screen is announced when it appears, for example from the window title. " +
            "A visible toolbar title or heading is not the same as a title exposed to screen readers.",
            snapshot.Root, "", [WcagCriteria.PageTitled]);
    }
}
