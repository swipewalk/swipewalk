using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// WCAG 2.2.2 Pause, Stop, Hide, needs-review automated check (<c>scan --auto-update-content</c>): flags a
/// screen where content kept changing on its own, with no input, across more than one interval between
/// captures taken a few seconds apart (see <see cref="ScreenSnapshot.AutoUpdateCaptures"/>, attached by
/// <c>Swipewalk.Engine.ScanService</c> before rules run, and <see cref="AutoUpdateChangeDetector"/>, which
/// decides using the accessibility trees, not screenshot pixels). Reports the REAL elapsed time between the
/// first and last capture (from each capture's own <see cref="ScreenSnapshot.CapturedAt"/>), not the
/// requested interval multiplied by the capture count: a full screen capture itself takes time on top of the
/// wait (seconds on Android, sometimes tens of seconds on iOS via the XCUITest harness), so the requested
/// interval alone would understate how far apart the captures actually were.
///
/// Always <see cref="FindingKind.NeedsReview"/>, never a confirmed WCAG failure: automated checks can tell
/// content changed on its own, but not whether a pause/stop/hide control exists somewhere on the screen (a
/// button this rule doesn't recognize as one), whether anything else is shown alongside it (2.2.2 only
/// applies to content "presented in parallel with other content" -- content that is the only thing on the
/// screen, like a preloader with nothing else on the page, is exempt, per the W3C Understanding document's own
/// example), or whether the update is essential to an activity (2.2.2's other exception; the Understanding
/// document's own examples of exempt-as-essential content -- an explanatory animation, or a stock ticker --
/// still ship with their own pause/restart buttons, so "essential" is not itself a reason to skip a control,
/// only a reason automated checks can't rule on). A person has to judge all of that.
///
/// Silent (no finding) when nothing changed, when a change was seen in only one interval (a spinner or a
/// one-off load settling -- see <see cref="AutoUpdateChangeDetector"/>'s remarks for why that's not reported),
/// and when the check wasn't requested at all (<see cref="ScreenSnapshot.AutoUpdateCaptures"/> has fewer than
/// two entries) -- the same "never guessed" convention <see cref="OrientationRestrictedRule"/> uses.
/// </summary>
public sealed class AutoUpdatingContentRule : IRule
{
    private const string RuleIdValue = "auto-updating-content";
    private const int MaxElementsListed = 3;

    public string Id => RuleIdValue;

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        if (!AutoUpdateChangeDetector.IsSustained(snapshot, snapshot.AutoUpdateCaptures, out var evidence))
            yield break;

        var lastCapturedAt = snapshot.AutoUpdateCaptures[^1].CapturedAt;
        var elapsedSeconds = Math.Max((lastCapturedAt - snapshot.CapturedAt).TotalSeconds, 0);
        var described = evidence.Take(MaxElementsListed).Select(Describe).ToList();
        var whatChanged = described.Count == 0 ? "" : ": " + string.Join("; ", described);

        yield return new Finding
        {
            RuleId = RuleIdValue,
            Kind = FindingKind.NeedsReview,
            Message =
                $"Content changed on its own over about {elapsedSeconds:0.#} seconds while this screen was captured " +
                $"again {snapshot.AutoUpdateCaptures.Count} time(s), with no input in between{whatChanged}. WCAG 2.2.2 " +
                "requires a way to pause, stop or hide moving, blinking or scrolling content that starts " +
                "automatically, lasts more than 5 seconds and is shown alongside other content on the screen; for " +
                "auto-updating content shown alongside other content, a way to pause, stop or hide it, or to " +
                "control how often it updates (no 5-second minimum for auto-updating content). Two exceptions: " +
                "content that is the only thing on the screen, not shown alongside anything else (for example a " +
                "loading animation with nothing else on the page); and content essential to an activity -- though " +
                "the Understanding document's own examples of essential content (an explanatory animation, a " +
                "stock ticker) still ship with their own pause/restart buttons, so \"essential\" alone doesn't " +
                "rule out needing a control. Check by hand: is this shown alongside other content, does a " +
                "pause/stop/hide control already exist somewhere on the screen, and could this be essential here " +
                "even so. If this is a countdown or a time limit on completing a task rather than moving or " +
                "updating content, WCAG 2.2.1 Timing Adjustable may also be relevant.",
            Criteria = [WcagCriteria.PauseStopHide],
            NodePath = "",
            Role = "screen",
            Details = evidence.Count == 0
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["ChangedElements"] = string.Join("; ", evidence.Select(Describe)) },
        };
    }

    private static string Describe(AutoUpdateChangeDetector.ChangedElement c) =>
        c.Label is { Length: > 0 } label ? $"{c.Role} \"{label}\" {c.What}" : $"{c.Role} {c.What}";
}
