using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// WCAG 2.5.8: targets at least 24×24 (dp/pt/DIP treated as CSS px), unless the spacing exception
/// applies. Separately, platform advisories for Android (48 dp) and Apple (44 pt) guidelines.
/// </summary>
public sealed class TargetSizeRule : IRule
{
    public const double WcagMinimum = 24;
    private const double Tolerance = 0.5; // absorbs px→dp rounding

    public string Id => "target-size";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        var targets = snapshot.Root.DescendantsAndSelfWithPath()
            .Where(e => e.Node.IsInteractive && e.Node.IsAccessible && RuleFinding.HasArea(e.Node))
            .ToList();
        var undersized = targets.Where(t => IsBelow(t.Node.Bounds, WcagMinimum)).ToList();

        foreach (var (node, path) in targets)
        {
            var size = $"{node.Bounds.Width:0}×{node.Bounds.Height:0} dp/pt";
            if (IsBelow(node.Bounds, WcagMinimum))
            {
                var others = targets.Select(t => t.Node).Where(n => !ReferenceEquals(n, node)).ToList();
                if (!MeetsSpacingException(node, others.Where(o => !Contains(o.Bounds, node.Bounds)), undersized.Select(u => u.Node)))
                {
                    yield return RuleFinding.Create(Id, FindingKind.WcagIssue,
                        $"Target is {size}, below the WCAG 2.5.8 minimum of 24×24. A 24-diameter circle centered on it " +
                        "intersects another target or another undersized target's circle, so the spacing exception does not apply. " +
                        "Other exceptions (equivalent control, inline, user-agent control, essential) were not checked.",
                        node, path, [WcagCriteria.TargetSizeMinimum]);
                    continue;
                }
                if (others.Any(o => Contains(o.Bounds, node.Bounds)))
                {
                    yield return RuleFinding.Create(Id, FindingKind.NeedsReview,
                        $"Target is {size} and sits inside another tappable element; check whether a near miss activates the wrong action.",
                        node, path, [WcagCriteria.TargetSizeMinimum]);
                    continue;
                }
            }

            if (PlatformMinimum(snapshot.Platform) is var (minimum, guideline) && IsBelow(node.Bounds, minimum))
            {
                yield return RuleFinding.Create(Id, FindingKind.PlatformAdvisory,
                    $"Target is {size}, below the platform guideline of {minimum:0}×{minimum:0}.",
                    node, path, guideline: guideline);
            }
        }
    }

    private static bool IsBelow(Bounds b, double minimum) =>
        b.Width + Tolerance < minimum || b.Height + Tolerance < minimum;

    /// <summary>
    /// WCAG 2.5.8 spacing exception: a 24-diameter circle centered on the target's bounding box must not
    /// intersect another target, or the circle of another undersized target.
    /// </summary>
    internal static bool MeetsSpacingException(
        AccessibilityNode target, IEnumerable<AccessibilityNode> allTargets, IEnumerable<AccessibilityNode> undersized)
    {
        const double radius = WcagMinimum / 2;
        var (cx, cy) = Center(target.Bounds);

        foreach (var other in allTargets)
        {
            if (ReferenceEquals(other, target))
                continue;
            if (CircleIntersectsRect(cx, cy, radius, other.Bounds))
                return false;
        }

        foreach (var other in undersized)
        {
            if (ReferenceEquals(other, target))
                continue;
            var (ox, oy) = Center(other.Bounds);
            if (Math.Sqrt((cx - ox) * (cx - ox) + (cy - oy) * (cy - oy)) < 2 * radius)
                return false;
        }

        return true;
    }

    private static (double, double) Center(Bounds b) => (b.X + b.Width / 2, b.Y + b.Height / 2);

    internal static bool Contains(Bounds outer, Bounds inner) =>
        outer.X <= inner.X && outer.Y <= inner.Y
        && outer.X + outer.Width >= inner.X + inner.Width && outer.Y + outer.Height >= inner.Y + inner.Height;

    private static bool CircleIntersectsRect(double cx, double cy, double r, Bounds b)
    {
        var nearestX = Math.Clamp(cx, b.X, b.X + b.Width);
        var nearestY = Math.Clamp(cy, b.Y, b.Y + b.Height);
        var dx = cx - nearestX;
        var dy = cy - nearestY;
        return dx * dx + dy * dy < r * r;
    }

    private static (double Minimum, string Guideline)? PlatformMinimum(Platform platform) => platform switch
    {
        Platform.Android => (48, "Android accessibility guidelines: touch targets at least 48×48 dp"),
        Platform.iOS => (44, "Apple Human Interface Guidelines: hit targets at least 44×44 pt"),
        _ => null,
    };
}
