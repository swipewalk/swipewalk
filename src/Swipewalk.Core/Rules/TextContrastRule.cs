using Swipewalk.Core.Imaging;
using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Rules;

/// <summary>
/// WCAG 1.4.3 measured from screenshot pixels. Below 3:1 is reported as an issue; 3:1 to 4.5:1 needs
/// review because it meets the large-text threshold and text size can't be read from the tree.
/// </summary>
public sealed class TextContrastRule : IRule
{
    public const double NormalTextMinimum = 4.5;
    public const double LargeTextMinimum = 3.0;

    private const string Caveat =
        "Measured from screenshot pixels; anti-aliasing, gradients or images behind text can affect the result.";

    public string Id => "text-contrast";

    public IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot)
    {
        if (snapshot.Screenshot is not { } image)
            yield break;
        if (snapshot.ScreenshotBlocked)
        {
            yield return new Finding
            {
                RuleId = Id,
                Kind = FindingKind.NeedsReview,
                Message = "The app blocked the screenshot (it came out black; on Android this is FLAG_SECURE, used for banking, " +
                          "password or DRM screens), so text contrast could not be measured. Check contrast on the device by hand.",
                Criteria = [WcagCriteria.ContrastMinimum],
                NodePath = "",
                Role = "screen",
            };
            yield break;
        }

        foreach (var (node, path) in snapshot.Root.DescendantsAndSelfWithPath())
        {
            if (string.IsNullOrWhiteSpace(node.VisibleText) || !RuleFinding.HasArea(node) || !node.IsEnabled)
                continue;
            if (node.Children.SelectMany(c => c.DescendantsAndSelf()).Any(d => d.VisibleText == node.VisibleText))
                continue; // measured on the child text element, which has tighter bounds

            var s = snapshot.PixelScale;
            var colors = Contrast.EstimateColors(image,
                (int)Math.Round(node.Bounds.X * s), (int)Math.Round(node.Bounds.Y * s),
                (int)Math.Round(node.Bounds.Width * s), (int)Math.Round(node.Bounds.Height * s));
            if (colors is not var (fg, bg))
                continue;

            var ratio = Contrast.Ratio(fg, bg);
            if (ratio >= NormalTextMinimum)
                continue;

            // WCAG does not round; truncate so a failing 4.499 is never shown as 4.50.
            var shown = Math.Floor(ratio * 100) / 100;
            var measured = $"Text \"{node.VisibleText}\" measured about {shown:0.00}:1 ({fg} on {bg}).";
            var details = new Dictionary<string, string>
            {
                ["foreground"] = fg.ToString(),
                ["background"] = bg.ToString(),
                ["ratio"] = shown.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                ["suggestedForeground"] = Contrast.SuggestForeground(fg, bg, NormalTextMinimum).ToString(),
            };
            var finding = ratio < LargeTextMinimum
                ? RuleFinding.Create(Id, FindingKind.WcagIssue,
                    $"{measured} Below 4.5:1 for normal text and 3:1 for large text. {Caveat}",
                    node, path, [WcagCriteria.ContrastMinimum])
                : RuleFinding.Create(Id, FindingKind.NeedsReview,
                    $"{measured} Below 4.5:1 for normal text; meets 3:1 only if this is large text " +
                    $"(WCAG large text: at least 18 pt, or 14 pt bold, which is about 24 dp/pt, or about 18.7 dp/pt bold, on mobile). " +
                    $"Confirm the text size. {Caveat}",
                    node, path, [WcagCriteria.ContrastMinimum]);
            yield return finding with { Details = details };
        }
    }
}
