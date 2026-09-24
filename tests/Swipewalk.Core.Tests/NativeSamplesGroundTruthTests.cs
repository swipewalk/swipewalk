using System.Text.Json;
using Swipewalk.Collectors.Android;
using Swipewalk.Collectors.Ios;
using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;

namespace Swipewalk.Core.Tests;

/// <summary>
/// Acceptance tests for samples/NativeAndroid and samples/NativeiOS: two native (no .NET MAUI) sample
/// apps, each with a classic-toolkit screen and a modern-toolkit screen (Views/Compose on Android,
/// UIKit/SwiftUI on iOS), planting the same 8 bug classes idiomatically per toolkit. Unlike
/// <see cref="GroundTruthTests"/>'s single ground-truth.json (one app scanned on both platforms), each
/// screen here has its own ground-truth file scoped to a single platform, since neither sample app has
/// a build for the other platform. Mirrors GroundTruthTests's structure and Key/Expected helpers.
/// Verified 2026-09-23 against live scans (Android emulator + a physical Pixel 4a; iPhone 17 Simulator
/// + a physical iPhone) -- see each ground-truth file's own notes for confirmed findings, corrected
/// draft assumptions and framework differences the live scans uncovered.
/// </summary>
public class NativeSamplesGroundTruthTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Fact]
    public void NativeAndroid_Views_FindingsMatchGroundTruth()
    {
        var snapshot = AndroidCollector.Load(Path.Combine(Fixtures, "NativeAndroid.Views"), "Pay a parking ticket (Views)");
        var findings = new RuleRunner(DefaultRules.All).Run(snapshot).Findings;

        Assert.Equal(Expected("ground-truth.native-android-views.json", "android"), Actual(findings));
        Assert.Contains(findings, f => f.AlsoReportedBy.Contains(AtfIssueRule.EngineName));
    }

    [Fact]
    public void NativeAndroid_Views_LargeTextFindingsMatchGroundTruth()
    {
        var dir = Path.Combine(Fixtures, "NativeAndroid.Views");
        var snapshot = AndroidCollector.Load(dir, "Pay a parking ticket (Views)") with
        {
            LargeText = AndroidCollector.Load(Path.Combine(dir, "large"), "Pay a parking ticket (Views)"),
            LargeTextSetting = "Android font scale 2.0 (200%)",
        };

        var actual = new TextResizeRule().Evaluate(snapshot)
            .Select(f => Key(f.RuleId, JsonNamingPolicy.CamelCase.ConvertName(f.Kind.ToString()), f.Role, f.Label))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(ExpectedForKey("ground-truth.native-android-views.json", "androidLargeText"), actual);
    }

    [Fact]
    public void NativeAndroid_Compose_FindingsMatchGroundTruth()
    {
        var snapshot = AndroidCollector.Load(Path.Combine(Fixtures, "NativeAndroid.Compose"), "Pay a parking ticket (Compose)");
        var findings = new RuleRunner(DefaultRules.All).Run(snapshot).Findings;

        Assert.Equal(Expected("ground-truth.native-android-compose.json", "android"), Actual(findings));
    }

    [Fact]
    public void NativeiOS_UIKit_FindingsMatchGroundTruth()
    {
        var snapshot = IosCollector.Load(Path.Combine(Fixtures, "NativeiOS.UIKit"), "Pay a parking ticket (Views screen)");
        var findings = new RuleRunner(DefaultRules.All).Run(snapshot).Findings;

        Assert.Equal(Expected("ground-truth.native-ios-uikit.json", "ios"), Actual(findings));
    }

    [Fact]
    public void NativeiOS_SwiftUI_FindingsMatchGroundTruth()
    {
        var snapshot = IosCollector.Load(Path.Combine(Fixtures, "NativeiOS.SwiftUI"), "Pay a parking ticket (SwiftUI screen)");
        var findings = new RuleRunner(DefaultRules.All).Run(snapshot).Findings;

        Assert.Equal(Expected("ground-truth.native-ios-swiftui.json", "ios"), Actual(findings));
    }

    private static List<string> Actual(IEnumerable<Finding> findings) => findings
        .Select(f => Key(f.RuleId, JsonNamingPolicy.CamelCase.ConvertName(f.Kind.ToString()), f.Role, f.Label))
        .Order(StringComparer.Ordinal)
        .ToList();

    private static List<string> Expected(string fixtureFile, string platform) => ExpectedForKey(fixtureFile, platform);

    private static List<string> ExpectedForKey(string fixtureFile, string key)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, fixtureFile)));
        var keys = new List<string>();
        foreach (var entry in doc.RootElement.GetProperty("expected").EnumerateArray())
        {
            if (!entry.TryGetProperty(key, out var found) || found.ValueKind == JsonValueKind.Null)
                continue;
            foreach (var f in found.EnumerateArray())
                keys.Add(Key(
                    f.GetProperty("rule").GetString()!, f.GetProperty("kind").GetString()!,
                    f.GetProperty("role").GetString()!, f.GetProperty("label").GetString()));
        }
        return [.. keys.Order(StringComparer.Ordinal)];
    }

    private static string Key(string rule, string kind, string role, string? label) =>
        $"{rule} | {kind} | {role} | {label ?? "(none)"}";
}
