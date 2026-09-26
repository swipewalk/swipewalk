using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// WCAG 2.5.8: targets at least 24×24 (dp/pt/DIP treated as CSS px), unless the spacing exception applies.
/// Separately, platform advisories for Android (48 dp) and Apple (44 pt) guidelines.
///
/// The inline exception ("the target is in a sentence or its size is otherwise constrained by the
/// line-height of non-target text") is NOT applied automatically: the tree gives no way to tell a target
/// that is genuinely part of a sentence or run of text from one that merely happens to sit next to an
/// unrelated text label (a "Remember me" checkbox beside a "Forgot password?" link is not in a sentence
/// with it). When the tree shows a target shaped like a plain-text link (see
/// <see cref="LooksLikeInlineTextLink"/>) sitting beside other text, and the spacing exception does not
/// already explain why there is no WCAG issue, this is reported as needs-review instead of a WCAG issue,
/// so a person confirms it rather than Swipewalk asserting an exception it cannot verify -- see
/// KnownLimitations "target-size-exceptions".
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
            var isUndersized = IsBelow(node.Bounds, WcagMinimum);

            if (isUndersized)
            {
                var others = targets.Select(t => t.Node).Where(n => !ReferenceEquals(n, node)).ToList();
                if (!MeetsSpacingException(node, others.Where(o => !Contains(o.Bounds, node.Bounds)), undersized.Select(u => u.Node)))
                {
                    if (LooksLikeInlineTextLink(snapshot.Root, node, path))
                    {
                        yield return RuleFinding.Create(Id, FindingKind.NeedsReview,
                            $"Target is {size}, below the WCAG 2.5.8 minimum of 24×24, and the spacing exception does " +
                            "not apply. It looks like a text link on a line with other text, so WCAG 2.5.8's inline " +
                            "exception may apply if it is part of a sentence or run of text; check by hand.",
                            node, path, [WcagCriteria.TargetSizeMinimum]);
                    }
                    else
                    {
                        yield return RuleFinding.Create(Id, FindingKind.WcagIssue,
                            $"Target is {size}, below the WCAG 2.5.8 minimum of 24×24. A 24-diameter circle centered on it " +
                            "intersects another target or another undersized target's circle, so the spacing exception does not apply. " +
                            "Swipewalk could not tell from the accessibility tree whether this target sits within a sentence " +
                            "(WCAG 2.5.8's inline exception), so check that by hand. Other exceptions (equivalent control, " +
                            "user-agent control, essential) were not checked.",
                            node, path, [WcagCriteria.TargetSizeMinimum]);
                    }
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

            // The spacing exception already explains any undersized target that reaches here (or the
            // target meets 24×24 outright), so the message below never claims anything about the inline
            // exception -- it is unchanged whether or not the target happens to look like a text link.
            if (PlatformMinimum(snapshot.Platform) is { } pm && IsBelow(node.Bounds, pm.Minimum))
            {
                yield return RuleFinding.Create(Id, FindingKind.PlatformAdvisory,
                    $"Target is {size}, below the platform guideline of {pm.Minimum:0}×{pm.Minimum:0}.",
                    node, path, guideline: pm.Guideline);
            }
        }
    }

    /// <summary>
    /// True when the tree shows a target shaped like a plain-text link, sitting beside other text -- the
    /// pattern a genuine WCAG 2.5.8 inline link (one "in a sentence or block of text",
    /// w3.org/WAI/WCAG22/Understanding/target-size-minimum.html, checked 2026-09-26) would also show, but
    /// not proof of it: a short text-styled control next to an unrelated text label (for example a
    /// "Remember me" checkbox's label next to a "Forgot password?" link) looks the same to the tree. Used
    /// only to raise a needs-review flag, never to grant the exception automatically -- see
    /// <see cref="Evaluate"/> and KnownLimitations "target-size-exceptions". Requires both:
    ///  1. the target's own tap area is plain text with no separate button styling (see
    ///     <see cref="IsTextStyledTarget"/>): Android a clickable TextView (not a purpose-built
    ///     Button/ImageButton); iOS an element with the "link" trait, or one with no accessibility label of
    ///     its own whose whole accessible name comes from a single text child sized the same as the
    ///     element -- i.e. nothing pads the tap area beyond the text itself (e.g. a MAUI Label with a
    ///     TapGestureRecognizer);
    ///  2. it sits beside a sibling, under the same parent, that is not itself a target and carries text
    ///     overlapping the same vertical band (the same visual line).
    /// A real inline link inside a UITextView's paragraph or an Android ClickableSpan usually looks
    /// different from this again -- see KnownLimitations "target-size-exceptions" for what this still
    /// misses.
    /// </summary>
    internal static bool LooksLikeInlineTextLink(AccessibilityNode root, AccessibilityNode node, string path) =>
        IsTextStyledTarget(node) && HasSiblingLineText(root, node, path);

    private static bool IsTextStyledTarget(AccessibilityNode node)
    {
        // Android: UiAutomatorParser.MapRole gives role "button" to any clickable TextView (and its
        // subclasses, e.g. AppCompatTextView) that isn't a purpose-built Button/ImageButton widget -- a
        // plain TextView made clickable has no dedicated button chrome enlarging its tap area.
        if (node.Role == "button" && node.NativeType is { } nativeType && ClassName(nativeType).EndsWith("TextView", StringComparison.Ordinal))
            return true;

        // iOS: XcuiTreeParser.MapRole already gives "link" only to XCUITest's own link trait, which marks
        // the element as textual/hyperlink-styled.
        if (node.Role == "link")
            return true;

        // iOS: a "button"-typed element with no accessibility label of its own, whose whole accessible name
        // (VisibleText) comes from exactly one "text" child sized the same as the element itself -- see
        // XcuiTreeParser.BuildNode. No button background or padding pads the tap area beyond the text.
        if (node.Role == "button" && node.Label is null)
        {
            var textChildren = node.Children.Where(c => c.Role == "text").ToList();
            if (textChildren.Count == 1 && BoundsApproximatelyEqual(textChildren[0].Bounds, node.Bounds))
                return true;
        }

        return false;
    }

    /// <summary>True when a sibling under <paramref name="node"/>'s own parent (found by walking
    /// <paramref name="path"/> from <paramref name="root"/>, the same scheme as
    /// <see cref="AccessibilityNode.DescendantsAndSelfWithPath"/>) is not itself a target and has text
    /// whose vertical band overlaps <paramref name="node"/>'s -- the "non-target text" sharing its line.
    /// The sibling's text may be nested (e.g. under its own platform wrapper), so its whole subtree is
    /// searched, not just its own properties.</summary>
    private static bool HasSiblingLineText(AccessibilityNode root, AccessibilityNode node, string path)
    {
        if (path.Length == 0)
            return false; // the root itself has no parent

        var indices = path.Split('/');
        var parent = root;
        for (var i = 0; i < indices.Length - 1; i++)
            parent = parent.Children[int.Parse(indices[i])];

        return parent.Children.Any(sibling =>
            !ReferenceEquals(sibling, node)
            && sibling.DescendantsAndSelf().Any(d =>
                d.Role == "text" && !d.IsInteractive && !string.IsNullOrWhiteSpace(d.VisibleText)
                && VerticalBandsOverlap(d.Bounds, node.Bounds)));
    }

    private static bool VerticalBandsOverlap(Bounds a, Bounds b) =>
        a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    private static bool BoundsApproximatelyEqual(Bounds a, Bounds b) =>
        Math.Abs(a.X - b.X) < 1 && Math.Abs(a.Y - b.Y) < 1
        && Math.Abs(a.Width - b.Width) < 1 && Math.Abs(a.Height - b.Height) < 1;

    private static string ClassName(string nativeType) => nativeType[(nativeType.LastIndexOf('.') + 1)..];

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
