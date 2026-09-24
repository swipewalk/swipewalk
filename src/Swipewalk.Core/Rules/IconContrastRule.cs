using Swipewalk.Core.Imaging;
using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// WCAG 1.4.11 Non-text Contrast, iOS only, measured from screenshot pixels the same way
/// <see cref="TextContrastRule"/> measures text. Android is covered instead by Google's Accessibility
/// Test Framework's ImageContrastCheck, run through the instrumentation harness (see
/// <see cref="AtfIssueRule"/> and KnownLimitations "android-atf-harness"); this rule only runs on iOS so
/// the two never duplicate the same finding (<see cref="RuleRunner.MergeEngineDuplicates"/> only merges
/// findings on the same screen, and ATF findings only ever appear on Android captures).
///
/// Scope: interactive, image-based controls only (icon buttons with no visible text) -- an element that is
/// both <see cref="AccessibilityNode.IsInteractive"/> and <see cref="RuleFinding.IsImageBased"/>. 1.4.11
/// exempts purely decorative images, but for an icon-only control the icon is the graphical object needed
/// to identify the component (there's no visible text alongside it to do that instead), so restricting to
/// interactive elements avoids having to guess which images are decorative (the same ambiguity
/// <see cref="MissingNameRule"/> documents for 1.1.1 -- see KnownLimitations "decorative-images" -- does
/// not arise here, because a control that responds to taps is never decorative).
///
/// Always <see cref="FindingKind.NeedsReview"/>, never a WcagIssue: colors are only estimated from pixels
/// (see <see cref="Imaging.Contrast.EstimateColors"/>), the same kind of approximation as
/// <see cref="TextContrastRule"/> and ATF's own ImageContrastCheck (a separate implementation, not shared code).
/// </summary>
public sealed class IconContrastRule : IRule
{
    public const double Minimum = 3.0;

    private const string Caveat =
        "Measured from screenshot pixels; anti-aliasing, or an icon over a photo or gradient background, can affect the result.";

    public string Id => "icon-contrast";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        if (snapshot.Platform != Platform.iOS)
            yield break;
        if (snapshot.Screenshot is not { } image || snapshot.ScreenshotBlocked)
            yield break;

        foreach (var (node, path) in snapshot.Root.DescendantsAndSelfWithPath())
        {
            if (!node.IsInteractive || !node.IsAccessible || !node.IsEnabled || !RuleFinding.HasArea(node)
                || !RuleFinding.IsImageBased(node))
                continue;

            var s = snapshot.PixelScale;
            var colors = Contrast.EstimateColors(image,
                (int)Math.Round(node.Bounds.X * s), (int)Math.Round(node.Bounds.Y * s),
                (int)Math.Round(node.Bounds.Width * s), (int)Math.Round(node.Bounds.Height * s));
            if (colors is not var (fg, bg))
                continue;

            var ratio = Contrast.Ratio(fg, bg);
            if (ratio >= Minimum)
                continue;

            var shown = Math.Floor(ratio * 100) / 100;
            var name = node.Label ?? node.AutomationId ?? node.Role;
            var finding = RuleFinding.Create(Id, FindingKind.NeedsReview,
                $"Icon or icon-only control \"{name}\" measured about {shown:0.00}:1 ({fg} on {bg}) against its " +
                $"background, below the WCAG 1.4.11 minimum of 3:1. {Caveat} Confirm with a color picker; " +
                "purely decorative images are exempt from 1.4.11, but this element responds to taps.",
                node, path, [WcagCriteria.NonTextContrast]);
            yield return finding with
            {
                Details = new Dictionary<string, string>
                {
                    ["foreground"] = fg.ToString(),
                    ["background"] = bg.ToString(),
                    ["ratio"] = shown.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                },
            };
        }
    }
}
