using System.Text.Json;
using Swipewalk.Collectors.Android;
using Swipewalk.Collectors.Ios;
using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;

namespace Swipewalk.Core.Tests;

/// <summary>
/// Acceptance test: scanning saved captures of samples/BuggyApp must produce exactly the findings
/// listed in samples/BuggyApp/ground-truth.json, no more and no fewer.
/// </summary>
public class GroundTruthTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Theory]
    [InlineData("android")]
    [InlineData("ios")]
    public void BuggyApp_FindingsMatchGroundTruth(string platform)
    {
        var snapshot = platform == "ios"
            ? IosCollector.Load(Path.Combine(Fixtures, "BuggyApp.iOS"), "Pay a parking ticket")
            : AndroidCollector.Load(Path.Combine(Fixtures, "BuggyApp.Android"), "Pay a parking ticket");

        var findings = new RuleRunner(DefaultRules.All).Run(snapshot).Findings;
        var actual = findings
            .Select(f => Key(f.RuleId, JsonNamingPolicy.CamelCase.ConvertName(f.Kind.ToString()), f.Role, f.Label))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Expected(platform), actual);

        if (platform == "android")
        {
            // Guards the merge itself (RuleRunner.MergeEngineDuplicates), not just the finding list: the
            // fixture's atf-harness.json (Google's Accessibility Test Framework, via the Android
            // instrumentation harness) must still overlap at least one of Swipewalk's own
            // findings on this screen (the flat list can't tell a duplicate that was merged into
            // Swipewalk's finding from one that was dropped without a trace; this checks the merge
            // recorded it).
            Assert.Contains(findings, f => f.AlsoReportedBy.Contains(AtfIssueRule.EngineName));
        }
    }

    /// <summary>
    /// samples/BuggyApp/ground-truth.json is scoped to MainPage ("Pay a parking ticket") only (see its
    /// description). The "Payment history" screen's back arrow is flagged by Google's Accessibility Test
    /// Framework alone (ImageContrastCheck, not merged into any of Swipewalk's own findings -- the back
    /// arrow already has a name, so missing-name doesn't fire on it), which no MainPage fixture can cover,
    /// so it gets its own small fixture (Fixtures/BuggyApp.Android.History) and test instead of extending
    /// ground-truth.json's single-screen schema. Verified by a live scan of the emulator on 2026-09-23.
    /// </summary>
    [Fact]
    public void HistoryScreen_AtfBackArrowContrastFinding()
    {
        var snapshot = AndroidCollector.Load(Path.Combine(Fixtures, "BuggyApp.Android.History"), "Payment history");

        var findings = new RuleRunner(DefaultRules.All).Run(snapshot).Findings;

        Assert.Contains(findings, f =>
            f.RuleId == "atf:ImageContrastCheck" && f.Kind == FindingKind.NeedsReview && f.Role == "element" && f.Label == "Navigate up");
    }

    [Fact]
    public void BuggyApp_LargeTextFindingsMatchGroundTruth()
    {
        var dir = Path.Combine(Fixtures, "BuggyApp.Android");
        var snapshot = AndroidCollector.Load(dir, "Pay a parking ticket") with
        {
            LargeText = AndroidCollector.Load(Path.Combine(dir, "large"), "Pay a parking ticket"),
            LargeTextSetting = "Android font scale 2.0 (200%)",
        };

        var actual = new TextResizeRule().Evaluate(snapshot)
            .Select(f => Key(f.RuleId, JsonNamingPolicy.CamelCase.ConvertName(f.Kind.ToString()), f.Role, f.Label))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Expected("androidLargeText"), actual);
    }

    /// <summary>
    /// The emulator's screen (1080x2424) is tall enough that everything on this fixture's screen still fits
    /// at Android font scale 2.0 (unlike the Pixel 4a's 1080x2340 -- see
    /// <see cref="BuggyApp_Pixel4a_LargeTextLostContentFindingMatchesDeviceCapture"/>), so
    /// <see cref="LargeTextLostContentRule"/> must find nothing here: a true-negative check on a real
    /// emulator capture, so the rule's screen-level "any scrollable container" heuristic isn't only verified
    /// on the device where it fires.
    /// </summary>
    [Fact]
    public void BuggyApp_LargeText_NoLostContentOnEmulator()
    {
        var dir = Path.Combine(Fixtures, "BuggyApp.Android");
        var snapshot = AndroidCollector.Load(dir, "Pay a parking ticket") with
        {
            LargeText = AndroidCollector.Load(Path.Combine(dir, "large"), "Pay a parking ticket"),
            LargeTextSetting = "Android font scale 2.0 (200%)",
        };

        Assert.Empty(new LargeTextLostContentRule().Evaluate(snapshot));
    }

    /// <summary>
    /// samples/BuggyApp/ground-truth.json's own large-text fixture (used by
    /// <see cref="BuggyApp_LargeTextFindingsMatchGroundTruth"/>) was captured on the Android emulator, whose
    /// screen (1080x2424) is tall enough that everything below "Pay" still fits at Android font scale 2.0 --
    /// so it can't exercise <see cref="LargeTextLostContentRule"/>. This fixture (BuggyApp.Android.Pixel4a)
    /// is a real capture from a physical Pixel 4a (1080x2340, 2026-09-23): at the same 200% scale, "Save for
    /// later", "View payment history" and an icon button (accessible name "img_email_receipt") are all
    /// completely absent from <c>uiautomator dump</c>'s tree -- not just their leaves either, the whole
    /// off-screen container holding the icon button is gone too. The only ScrollView-class node on this
    /// screen is MAUI's own navigation layout (holding the app bar and page host, not the page's own
    /// content -- MainPage.xaml has no ScrollView at all), and it reports <c>scrollable="false"</c> at both
    /// sizes (confirmed live: an <c>adb shell input swipe</c> on the screen produced no visible scroll).
    /// This is a genuine, previously-undocumented BuggyApp bug, not a fixture built to order.
    /// </summary>
    [Fact]
    public void BuggyApp_Pixel4a_LargeTextLostContentFindingMatchesDeviceCapture()
    {
        var dir = Path.Combine(Fixtures, "BuggyApp.Android.Pixel4a");
        var snapshot = AndroidCollector.Load(dir, "Pay a parking ticket") with
        {
            LargeText = AndroidCollector.Load(Path.Combine(dir, "large"), "Pay a parking ticket"),
            LargeTextSetting = "Android font scale 2.0 (200%)",
            LargeTextScale = 2.0,
        };

        var finding = Assert.Single(new LargeTextLostContentRule().Evaluate(snapshot));

        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([Swipewalk.Core.Wcag.WcagCriteria.ResizeText], finding.Criteria);
        Assert.Contains("3 element(s)", finding.Message);
        Assert.Contains("\"Save for later\"", finding.Message);
        Assert.Contains("\"View payment history\"", finding.Message);
        Assert.Contains("\"img_email_receipt\"", finding.Message);
        Assert.Contains("reports itself as scrollable", finding.Message);
    }

    private static List<string> Expected(string platform)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "ground-truth.json")));
        var keys = new List<string>();
        foreach (var entry in doc.RootElement.GetProperty("expected").EnumerateArray())
        {
            if (!entry.TryGetProperty(platform, out var findings) || findings.ValueKind == JsonValueKind.Null)
                continue;
            foreach (var f in findings.EnumerateArray())
                keys.Add(Key(
                    f.GetProperty("rule").GetString()!, f.GetProperty("kind").GetString()!,
                    f.GetProperty("role").GetString()!, f.GetProperty("label").GetString()));
        }
        return [.. keys.Order(StringComparer.Ordinal)];
    }

    private static string Key(string rule, string kind, string role, string? label) =>
        $"{rule} | {kind} | {role} | {label ?? "(none)"}";
}
