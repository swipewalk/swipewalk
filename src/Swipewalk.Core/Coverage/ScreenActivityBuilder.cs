using Swipewalk.Core.Model;

namespace Swipewalk.Core.Coverage;

/// <summary>
/// Builds <see cref="ScreenActivity"/> from a scanned <see cref="ScreenResult"/>, using only data already
/// stored on it (screenshot path, large-text setting/skip reason, platform, findings). Kept separate from
/// <see cref="Rules.RuleRunner"/> so a screen's coverage activity can be recomputed from a saved
/// results.json too, without re-running the rules.
/// </summary>
public static class ScreenActivityBuilder
{
    /// <summary>Rule ids that always evaluate every scanned screen's tree unconditionally (no data they
    /// depend on can be missing), so they always count as having run.</summary>
    private static readonly string[] AlwaysRun =
        ["missing-name", "target-size", "identifier-name", "label-in-name", "input-purpose"];

    /// <summary>
    /// The exact text of <c>Swipewalk.Collectors.LargeTextCapture.DifferentScreen</c>, duplicated because
    /// Swipewalk.Core cannot reference Swipewalk.Collectors (see <see cref="Rules.TextResizeNavigationRule"/>'s
    /// remarks). This is scan mode's own skip-reason signal for "this screen's own live large-text attempt
    /// showed a different screen before Swipewalk did anything about it" (see
    /// <c>Swipewalk.Engine.ScanService.AndroidLargeTextWentToAnotherScreen</c>). Record mode has no equivalent
    /// skip reason for the same situation -- see the "text-resize-navigation" gate below, which also checks
    /// for a finding, for how that case is covered. A test in Swipewalk.Core.Tests asserts this literal stays
    /// equal to the Collectors constant.
    /// </summary>
    internal const string LargeTextWentToAnotherScreenReason = "a different screen was showing at the larger text size";

    public static ScreenActivity For(ScreenResult screen)
    {
        var ran = new HashSet<string>(AlwaysRun);
        var skipped = new Dictionary<string, string>();

        // text-contrast (TextContrastRule) needs a decoded screenshot; ScreenshotPath is only set when one
        // was captured (see RuleRunner.Run), whether or not it was later found to be blocked.
        if (screen.ScreenshotPath is not null)
            ran.Add("text-contrast");
        else
            skipped["text-contrast"] = "no usable screenshot";

        // text-resize (TextResizeRule), large-text-lost-content (LargeTextLostContentRule) and text-resize-live
        // (TextResizeLiveUpdateRule) all need a large-text capture; LargeTextSetting is only set when
        // snapshot.LargeText was present (see RuleRunner.Run). All three rules read fields only populated
        // alongside snapshot.LargeText (LargeText itself, and LargeTextAppliedLive -- see each rule's own
        // Evaluate), so they share this exact gate.
        if (screen.LargeTextSetting is not null)
        {
            ran.Add("text-resize");
            ran.Add("large-text-lost-content");
            ran.Add("text-resize-live");
        }
        else
        {
            skipped["text-resize"] = screen.LargeTextSkippedReason ?? "large-text check not requested";
            skipped["large-text-lost-content"] = screen.LargeTextSkippedReason ?? "large-text check not requested";
            skipped["text-resize-live"] = screen.LargeTextSkippedReason ?? "large-text check not requested";
        }

        // text-resize-navigation (TextResizeNavigationRule) is Android-only, and unlike the three rules
        // above it doesn't need the large-text capture to have SUCCEEDED: it can report a finding, or count
        // as run with nothing found, in three situations:
        //   - a successful capture (LargeTextSetting set): whatever mechanism produced it, the stored capture
        //     was matched to the intended screen by Recorder's screen-identity check (a heuristic -- see
        //     TextResizeNavigationRule's own remarks), so this screen was not left showing a different one;
        //   - scan mode's own signal that it was (LargeTextSkippedReason == LargeTextWentToAnotherScreenReason);
        //   - a finding with this rule id already on the screen: record mode sets
        //     ScreenSnapshot.LargeTextWentToAnotherScreen (and so this finding) from this screen's own live
        //     attempt regardless of what the person goes on to decide afterwards (see Swipewalk.Engine.Recorder),
        //     so the finding itself is evidence the check ran even when the eventual LargeTextSkippedReason
        //     (declined, not checked this run, a different screen again after Swipewalk's own restart) doesn't
        //     say so on its own.
        // On a recording that has already "learned" this app loses its place, later screens are asked BEFORE
        // the text size changes and skip the OS's own naive attempt entirely: for those screens, a successful
        // capture means "not re-reported" (the rule is deliberately silent past the first screen it saw this
        // on -- see TextResizeNavigationRule's remarks), not "independently confirmed no loss". That is an
        // acceptable approximation only because this rule currently maps to no WCAG criterion, so nothing
        // reads "ran" here as a pass/fail signal; if it is ever criterion-mapped, this gate needs a real
        // "was this screen's own live attempt observed" field instead of inferring from what's on ScreenResult.
        if (screen.Platform != Platform.Android)
            skipped["text-resize-navigation"] = CoverageDisplay.TextResizeNavigationAndroidOnlyReason;
        else if (screen.LargeTextSetting is not null
                 || screen.LargeTextSkippedReason == LargeTextWentToAnotherScreenReason
                 || screen.Findings.Any(f => f.RuleId == "text-resize-navigation"))
            ran.Add("text-resize-navigation");
        else
            skipped["text-resize-navigation"] = screen.LargeTextSkippedReason ?? "large-text check not requested";

        // orientation-restricted (OrientationRestrictedRule) needs a second capture in the other orientation
        // (ScreenSnapshot.Orientation, attached only when scan --orientation both ran); OtherOrientation is
        // only set once that capture succeeded (see Swipewalk.Engine.ScanService), whether or not the rule
        // itself found the screen restricted -- the same "attempted, not just found something" shape as
        // text-contrast's screenshot gate above. A finding on the screen counts as ran too, the same
        // belt-and-suspenders as text-resize-navigation, in case a future caller sets the finding without also
        // setting OtherOrientation.
        if (screen.OtherOrientation is not null || screen.Findings.Any(f => f.RuleId == "orientation-restricted"))
            ran.Add("orientation-restricted");
        else
            skipped["orientation-restricted"] = screen.OrientationSkippedReason ?? "orientation check not requested (pass --orientation both)";

        // The platform's own accessibility audit (EngineIssueRule) only ever runs as part of the iOS
        // collector (see XcuiTreeParser); Android and Windows collectors never populate EngineIssues, so a
        // screen with zero engine findings there means the audit doesn't exist, not that it ran and found
        // nothing. Recording the reason even here (rather than leaving it out) is what lets CoverageDisplay
        // phrase it as a calm, factual "(iOS only)" instead of a silent gap.
        if (screen.Platform == Platform.iOS)
            ran.Add("engine");
        else
            skipped["engine"] = CoverageDisplay.EngineIosOnlyReason;

        // Google's Accessibility Test Framework, via the Android instrumentation harness (AndroidHarness):
        // Android-only, like the iOS audit above is iOS-only. AtfRan (see ScreenSnapshot.AtfRan) is the
        // explicit "the harness produced a result" flag -- never inferred from AtfIssues being non-empty,
        // which a screen with zero findings would also have.
        // page-titled (PageTitledRule) reads pane titles the harness reads in the same pass as ATF, so it
        // shares AtfRan's gate exactly: Android only, and only once the harness actually produced a result.
        if (screen.Platform != Platform.Android)
        {
            skipped["atf"] = CoverageDisplay.AtfAndroidOnlyReason;
            skipped["page-titled"] = CoverageDisplay.PageTitledAndroidOnlyReason;
        }
        else if (screen.AtfRan)
        {
            ran.Add("atf");
            ran.Add("page-titled");
        }
        else
        {
            skipped["atf"] = screen.AtfSkippedReason ?? "no Android accessibility harness result was recorded for this capture";
            skipped["page-titled"] = screen.AtfSkippedReason ?? "no Android accessibility harness result was recorded for this capture";
        }

        // icon-contrast (IconContrastRule) needs a decoded screenshot, like text-contrast, but only ever
        // runs on iOS -- Android icon contrast is covered instead by atf's ImageContrastCheck.
        if (screen.Platform != Platform.iOS)
            skipped["icon-contrast"] = CoverageDisplay.IconContrastIosOnlyReason;
        else if (screen.ScreenshotPath is not null)
            ran.Add("icon-contrast");
        else
            skipped["icon-contrast"] = "no usable screenshot";

        // offscreen-unreachable (OffscreenUnreachableRule) only evaluates iOS captures: Android's
        // uiautomator dump clips every node's reported bounds to the visible screen (see the rule's own
        // remarks), so it has nothing to measure there and never runs at all, not merely "runs and finds
        // nothing" -- the same distinction icon-contrast draws above.
        if (screen.Platform != Platform.iOS)
            skipped["offscreen-unreachable"] = CoverageDisplay.OffscreenUnreachableIosOnlyReason;
        else
            ran.Add("offscreen-unreachable");

        // screen-reader-capture (ScreenReaderCaptureRule) needs a real capture with at least one item:
        // AndroidCollector.Load builds a non-null ScreenReaderCapture (with empty Items and
        // NotCompleteReason set) both when the harness genuinely ran and captured nothing walkable, AND
        // when it never ran at all (TalkBack not installed, its settings screen not found, ...) -- non-null
        // alone can't tell those apart, unlike AtfRan's dedicated flag for the ATF harness above. Items
        // being non-empty is the closest available signal that a real capture happened; an empty capture
        // (either meaning) is reported as skipped, with whatever reason is on it.
        if (screen.ScreenReaderCapture is { Items.Count: > 0 })
            ran.Add("screen-reader-capture");
        else if (screen.ScreenReaderCapture is { } emptyCapture)
            skipped["screen-reader-capture"] = emptyCapture.NotCompleteReason ?? "no screen-reader output was captured";
        else
            skipped["screen-reader-capture"] = screen.Platform == Platform.iOS
                ? "screen-reader capture is Android-only for now"
                : "screen-reader capture not requested for this run (pass --screen-reader)";

        // screen-reader-label-in-name (ScreenReaderLabelInNameRule) reads the same
        // ScreenSnapshot.ScreenReaderCapture as screen-reader-capture above, but -- unlike that rule --
        // bails out entirely (reports nothing at all) on a capture that didn't cover the whole screen (see
        // that rule's own remarks), so an incomplete-but-non-empty capture is its own skip reason here
        // rather than "ran": otherwise the coverage table would say this rule ran and confirmed nothing,
        // when it actually never evaluated anything for this screen.
        if (screen.ScreenReaderCapture is { Items.Count: > 0, Complete: true })
            ran.Add("screen-reader-label-in-name");
        else if (screen.ScreenReaderCapture is { Items.Count: > 0 } incompleteCapture)
            skipped["screen-reader-label-in-name"] = incompleteCapture.NotCompleteReason ?? "the screen-reader capture did not cover the whole screen";
        else if (screen.ScreenReaderCapture is { } emptyCapture2)
            skipped["screen-reader-label-in-name"] = emptyCapture2.NotCompleteReason ?? "no screen-reader output was captured";
        else
            skipped["screen-reader-label-in-name"] = screen.Platform == Platform.iOS
                ? "screen-reader capture is Android-only for now"
                : "screen-reader capture not requested for this run (pass --screen-reader)";

        return new ScreenActivity(ran, skipped, screen.Findings);
    }

    public static RunActivity For(IReadOnlyList<ScreenResult> screens) => new([.. screens.Select(For)]);
}
