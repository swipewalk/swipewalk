using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// WCAG 1.3.4 Orientation, needs-review automated check (<c>scan --orientation both</c>): flags a screen that
/// still looks the same shape after the device was rotated to the other orientation. Reads the second
/// capture through <see cref="ScreenSnapshot.Orientation"/> (attached by <c>Swipewalk.Engine.ScanService</c>
/// before rules run, the same nested-capture shape <see cref="ScreenSnapshot.LargeText"/> uses for
/// <see cref="LargeTextLostContentRule"/>) and decides using <see cref="OrientationChangeDetector.Rotated"/>,
/// which compares the screenshot's aspect ratio (falling back to the tree's root bounds) between the two
/// captures.
///
/// Always <see cref="FindingKind.NeedsReview"/>, never a confirmed WCAG failure: 1.3.4 explicitly allows a
/// single orientation when it is essential to the screen's function (WCAG's own examples: a piano keyboard, a
/// bank cheque deposit, slides meant for a projector or television, or virtual reality content) -- something
/// a scan cannot tell from the outside, so the
/// message names the exception and asks a person to judge whether it applies here.
///
/// Silent (no finding either way) when the device did rotate -- that is the ordinary, working case, the same
/// convention <see cref="TextResizeRule"/> uses for text that does grow -- and also when
/// <see cref="OrientationChangeDetector.Rotated"/> found no usable evidence in one of the two captures (no
/// screenshot and no usable bounds): a scan never reports "restricted to one orientation" without positive
/// evidence that nothing changed.
/// </summary>
public sealed class OrientationRestrictedRule : IRule
{
    private const string RuleIdValue = "orientation-restricted";

    public string Id => RuleIdValue;

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        if (snapshot.Orientation is not { } other || OrientationChangeDetector.Rotated(snapshot, other) is not false)
            yield break;

        var from = snapshot.OrientationLabel ?? "the same orientation";
        var to = snapshot.OtherOrientationLabel ?? "the other orientation";

        yield return new Finding
        {
            RuleId = RuleIdValue,
            Kind = FindingKind.NeedsReview,
            Message = $"The screen still looked like {from} after the device was rotated to {to}: its content or " +
                      "layout did not visibly follow the rotation. This can be fine if a single orientation is " +
                      "essential to this screen (for example a piano keyboard, a bank cheque deposit, slides meant for a " +
                      "projector or TV, or VR content) -- check by hand whether that applies here, or whether this " +
                      "screen (or its activity/view controller) is simply locked to one orientation with no reason to be.",
            Criteria = [WcagCriteria.Orientation],
            NodePath = "",
            Role = "screen",
        };
    }
}
