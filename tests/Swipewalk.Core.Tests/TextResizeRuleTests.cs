using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Tests;

public class TextResizeRuleTests
{
    private static AccessibilityNode Text(string text, double y, double height, double x = 0, double width = 300) => new()
    {
        Role = "text",
        VisibleText = text,
        Bounds = new Bounds(x, y, width, height),
    };

    private static ScreenSnapshot Snapshot(
        AccessibilityNode[] normal, AccessibilityNode[]? large, double? scale = 2.0, string setting = "Android font scale 2.0",
        Platform platform = Platform.Android, AppFramework framework = AppFramework.Unknown, bool? largeTextRestartCaptured = null,
        string? frameworkVersion = null) => new()
    {
        Platform = platform,
        ScreenName = "Screen",
        Framework = framework,
        FrameworkVersion = frameworkVersion,
        Root = new AccessibilityNode { Role = "window", Children = normal },
        LargeTextSetting = setting,
        LargeTextScale = scale,
        LargeTextRestartCaptured = largeTextRestartCaptured,
        LargeText = large is null
            ? null
            : new ScreenSnapshot
            {
                Platform = platform,
                ScreenName = "Screen",
                Root = new AccessibilityNode { Role = "window", Children = large },
            },
    };

    /// <summary>iOS accessibility text size AX3, about 235%: beyond the 200% WCAG 1.4.4 Resize Text asks for.</summary>
    private static ScreenSnapshot Ax3Snapshot(AccessibilityNode[] normal, AccessibilityNode[]? large) =>
        Snapshot(normal, large, scale: 2.35, setting: "iOS accessibility text size AX3 (about 235%)");

    private static List<Finding> Evaluate(ScreenSnapshot snapshot) => new TextResizeRule().Evaluate(snapshot).ToList();

    [Fact]
    public void TextThatGrows_Passes()
    {
        var findings = Evaluate(Snapshot([Text("Title", 0, 20), Text("Body", 30, 20)], [Text("Title", 0, 40), Text("Body", 50, 40)]));

        Assert.Empty(findings);
    }

    [Fact]
    public void TextThatDoesNotGrow_NeedsReview()
    {
        var findings = Evaluate(Snapshot([Text("Late fees apply", 0, 20)], [Text("Late fees apply", 0, 20)]));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.ResizeText], finding.Criteria);
        Assert.Equal("largeText", finding.Details["screenshot"]);
    }

    [Fact]
    public void TextThatNewlyOverlaps_AtAndroid200Percent_NeedsReview()
    {
        // Scale 2.0 (Android's font scale 2.0, exactly the 200% WCAG 1.4.4 asks for): overlap is a WCAG issue.
        var findings = Evaluate(Snapshot(
            [Text("A", 0, 20), Text("B", 30, 20)],
            [Text("A", 0, 40), Text("B", 30, 40)]));

        var finding = Assert.Single(findings);
        Assert.Contains("overlaps", finding.Message);
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.ResizeText], finding.Criteria);
        Assert.Null(finding.PlatformGuideline);
    }

    [Fact]
    public void TextThatNewlyOverlaps_AtIosAx3_IsPlatformAdvisoryNotWcag()
    {
        // Scale 2.35 (iOS AX3, beyond the 200% WCAG 1.4.4 asks for): overlap seen only there is an advisory
        // against Apple's Dynamic Type guidance, not a WCAG finding.
        var findings = Evaluate(Ax3Snapshot(
            [Text("A", 0, 20), Text("B", 30, 20)],
            [Text("A", 0, 40), Text("B", 30, 40)]));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.PlatformAdvisory, finding.Kind);
        Assert.Empty(finding.Criteria);
        Assert.Equal(TextResizeRule.DynamicTypeGuideline, finding.PlatformGuideline);
        Assert.Contains("AX3", finding.Message);
        Assert.Contains("200%", finding.Message);
        Assert.Contains("overlaps", finding.Message);
    }

    [Fact]
    public void TextThatGrows_AtIosAx3_Passes()
    {
        var findings = Evaluate(Ax3Snapshot(
            [Text("Title", 0, 20), Text("Body", 30, 20)], [Text("Title", 0, 47), Text("Body", 50, 47)]));

        Assert.Empty(findings);
    }

    [Fact]
    public void TextThatDoesNotGrow_AtIosAx3_StillCitesWcag()
    {
        // Not growing at all implies the text can't reach 200% by this mechanism, regardless of the tested
        // scale being beyond 200%: still a 1.4.4 finding, not a platform advisory.
        var findings = Evaluate(Ax3Snapshot([Text("Late fees apply", 0, 20)], [Text("Late fees apply", 0, 20)]));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.ResizeText], finding.Criteria);
        Assert.Null(finding.PlatformGuideline);
    }

    [Fact]
    public void OverlapWithUnknownScale_DefaultsToWcagIssue()
    {
        // Captures made before LargeTextScale was recorded: no evidence the tested scale went beyond 200%,
        // so this keeps today's behavior instead of guessing.
        var findings = Evaluate(Snapshot(
            [Text("A", 0, 20), Text("B", 30, 20)],
            [Text("A", 0, 40), Text("B", 30, 40)],
            scale: null));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
        Assert.Equal([WcagCriteria.ResizeText], finding.Criteria);
    }

    [Fact]
    public void TextMissingAtLargeSize_IsIgnored()
    {
        // Scrolled off screen at the larger size: not evidence of a problem.
        var findings = Evaluate(Snapshot([Text("Footer", 800, 20)], []));

        Assert.Empty(findings);
    }

    [Fact]
    public void NoLargeTextCapture_ProducesNothing()
    {
        Assert.Empty(Evaluate(Snapshot([Text("Title", 0, 20)], null)));
    }

    [Fact]
    public void TextAlreadyOverlappingAtNormalSize_IsNotFlagged()
    {
        var findings = Evaluate(Snapshot(
            [Text("A", 0, 20), Text("B", 10, 20)],
            [Text("A", 0, 40), Text("B", 10, 40)]));

        Assert.Empty(findings);
    }

    [Fact]
    public void AppThatIgnoresTheSettingWhileRunning_GetsOneScreenLevelFinding()
    {
        // Five texts: four unchanged, one (a system-drawn title) grew slightly.
        var findings = Evaluate(Snapshot(
            [Text("Title", 0, 20), Text("A", 40, 20), Text("B", 80, 20), Text("C", 120, 20), Text("D", 160, 20)],
            [Text("Title", 0, 25), Text("A", 40, 20), Text("B", 80, 20), Text("C", 120, 20), Text("D", 160, 20)]));

        var finding = Assert.Single(findings);
        Assert.Equal("screen", finding.Role);
        Assert.Contains("4 of the 5 texts", finding.Message);
    }

    [Fact]
    public void OneClippedTextAmongGrowingTexts_IsReportedIndividually()
    {
        var findings = Evaluate(Snapshot(
            [Text("A", 0, 20), Text("B", 40, 20), Text("C", 80, 20), Text("Clipped", 120, 20)],
            [Text("A", 0, 40), Text("B", 50, 40), Text("C", 100, 40), Text("Clipped", 150, 20)]));

        var finding = Assert.Single(findings);
        Assert.Contains("\"Clipped\"", finding.Message);
    }

    private static AccessibilityNode[] FourUnchangedTexts() =>
        [Text("One", 0, 20), Text("Two", 30, 20), Text("Three", 60, 20), Text("Four", 90, 20)];

    [Fact]
    public void ScreenLevelNotGrown_OnIosMaui_RestartConfirmed_CitesFontAutoScalingAndUnscaledControls_NotTheOldVersion()
    {
        // Case 2: a restart was captured and text still did not grow, so the pre-10.0.100 "grows after a
        // restart" issue is ruled out; only causes that would not grow even after a restart apply.
        var normal = FourUnchangedTexts();
        var findings = Evaluate(Snapshot(normal, normal, platform: Platform.iOS, framework: AppFramework.Maui, largeTextRestartCaptured: true));

        var finding = Assert.Single(findings);
        Assert.Equal("screen", finding.Role);
        // The base sentence reflects that a restart was already tried and still didn't help: it must not
        // contradict that by saying "may apply only after it restarts" / "relaunch and check".
        Assert.Contains("even after the app was restarted", finding.Message);
        Assert.Contains("Check this screen at 200% text size by hand", finding.Message);
        Assert.DoesNotContain("while the app was running", finding.Message);
        Assert.DoesNotContain("may apply the new size only after it restarts", finding.Message);
        Assert.DoesNotContain("Relaunch the app", finding.Message);
        Assert.Contains("FontAutoScalingEnabled=\"False\"", finding.Message);
        Assert.Contains("Shell tab bar title", finding.Message);
        Assert.DoesNotContain("before 10.0.100", finding.Message);
        Assert.DoesNotContain("dotnet/maui#34445", finding.Message);
        Assert.Equal("true", finding.Details["largeTextRestartCaptured"]);
    }

    [Fact]
    public void ScreenLevelNotGrown_RestartConfirmed_MessageFlowsThroughRuleRunner()
    {
        // End-to-end check of the wiring ScanService/Recorder rely on: they set LargeTextRestartCaptured on
        // the base snapshot (copied up from the enlarged capture's Snapshot.LargeTextRestartCaptured -- see
        // AndroidScreenSource.DecideAfterRestart / IosCollector.CapturedAfterRestart), and TextResizeRule reads
        // it from that same base snapshot, not from snapshot.LargeText. Going through RuleRunner (not calling
        // the rule directly) proves the field the collectors set is the one the rule actually reads at runtime.
        var normal = FourUnchangedTexts();
        var snapshot = Snapshot(normal, normal, platform: Platform.iOS, framework: AppFramework.Maui, largeTextRestartCaptured: true);

        var result = new RuleRunner([new TextResizeRule()]).Run(snapshot);

        var finding = Assert.Single(result.Findings);
        Assert.Contains("even after the app was restarted", finding.Message);
        Assert.Contains("Check this screen at 200% text size by hand", finding.Message);
        Assert.DoesNotContain("Relaunch the app", finding.Message);
    }

    [Fact]
    public void ScreenLevelNotGrown_RestartConfirmed_AboveWcagScale_StillChecksAt200Percent()
    {
        // Case 2 at a tested scale beyond 200% (iOS AX3, about 235%): text that doesn't grow at AX3 doesn't
        // grow at 200% either, so the manual check is still against 200% -- the WCAG 1.4.4 level -- even
        // though the tested level ({setting}) is named in the first sentence.
        var normal = FourUnchangedTexts();
        var findings = Evaluate(Snapshot(
            normal, normal, scale: 2.35, setting: "iOS accessibility text size AX3 (about 235%)",
            platform: Platform.iOS, framework: AppFramework.Unknown, largeTextRestartCaptured: true));

        var finding = Assert.Single(findings);
        Assert.Contains("changed to iOS accessibility text size AX3 (about 235%), even after the app was restarted", finding.Message);
        Assert.Contains("Check this screen at 200% text size by hand", finding.Message);
        Assert.Equal([WcagCriteria.ResizeText], finding.Criteria);
        Assert.Equal(FindingKind.NeedsReview, finding.Kind);
    }

    [Fact]
    public void ScreenLevelNotGrown_RestartConfirmed_AppliesToAnyPlatformOrFramework_ButOnlyIosMauiGetsCauseHints()
    {
        // The base "even after the app was restarted" wording is a fact about what was observed, not a MAUI
        // cause, so it applies everywhere LargeTextRestartCaptured == true -- but the extra cause sentence and
        // the "largeTextRestartCaptured" evidence detail stay iOS + MAUI only.
        var normal = FourUnchangedTexts();
        var findings = Evaluate(Snapshot(normal, normal, platform: Platform.Android, framework: AppFramework.Maui, largeTextRestartCaptured: true));

        var finding = Assert.Single(findings);
        Assert.Contains("even after the app was restarted", finding.Message);
        Assert.Contains("Check this screen at 200% text size by hand", finding.Message);
        Assert.DoesNotContain("MAUI", finding.Message);
        Assert.DoesNotContain("FontAutoScalingEnabled", finding.Message);
        Assert.False(finding.Details.ContainsKey("largeTextRestartCaptured"));
    }

    [Fact]
    public void ScreenLevelNotGrown_OnIosMaui_RestartNotConfirmed_ListsBothCausesAsUncertain()
    {
        // Case 3: the restart follow-up could not be confirmed (LargeTextRestartCaptured is null/false), so
        // both the pre-10.0.100 known issue and the causes that never grow are listed, with the uncertainty stated.
        // The base sentence stays the original ("while the app was running" / "relaunch and check"): whether the
        // app was actually relaunched is exactly what's unconfirmed here.
        var normal = FourUnchangedTexts();
        var findings = Evaluate(Snapshot(normal, normal, platform: Platform.iOS, framework: AppFramework.Maui, largeTextRestartCaptured: null));

        var finding = Assert.Single(findings);
        Assert.Contains("while the app was running", finding.Message);
        Assert.Contains("Relaunch the app at 200% text size", finding.Message);
        Assert.DoesNotContain("even after the app was restarted", finding.Message);
        Assert.Contains("could not confirm whether restarting", finding.Message);
        Assert.Contains("before 10.0.100", finding.Message);
        Assert.Contains("dotnet/maui#34445", finding.Message);
        Assert.Contains("FontAutoScalingEnabled=\"False\"", finding.Message);
        Assert.Equal("false", finding.Details["largeTextRestartCaptured"]);
    }

    [Fact]
    public void ScreenLevelNotGrown_OnIosMaui_RestartNotConfirmed_KnownVersionBeforeFix_KeepsBothCauses()
    {
        // Case 3, known version before the fix: the restart outcome is unconfirmed either way, so the known
        // pre-10.0.100 issue and the causes that never grow are both still possible; the version is named.
        var normal = FourUnchangedTexts();
        var findings = Evaluate(Snapshot(
            normal, normal, platform: Platform.iOS, framework: AppFramework.Maui, largeTextRestartCaptured: null,
            frameworkVersion: "10.0.60"));

        var finding = Assert.Single(findings);
        Assert.Contains("This app uses .NET MAUI 10.0.60", finding.Message);
        Assert.Contains("before 10.0.100", finding.Message);
        Assert.Contains("dotnet/maui#34445", finding.Message);
        Assert.Contains("FontAutoScalingEnabled=\"False\"", finding.Message);
        Assert.Contains("could not confirm whether restarting the app would make this text grow", finding.Message);
    }

    [Fact]
    public void ScreenLevelNotGrown_OnIosMaui_RestartNotConfirmed_KnownVersionAtOrAfterFix_DropsTheOldVersionCause()
    {
        // Case 3, known version at/after the fix: the pre-10.0.100 issue is ruled out even though the restart
        // outcome itself is unconfirmed, so only the causes that would not grow even after a restart remain.
        var normal = FourUnchangedTexts();
        var findings = Evaluate(Snapshot(
            normal, normal, platform: Platform.iOS, framework: AppFramework.Maui, largeTextRestartCaptured: null,
            frameworkVersion: "10.0.100"));

        var finding = Assert.Single(findings);
        Assert.Contains("This app uses .NET MAUI 10.0.100", finding.Message);
        Assert.Contains("includes the fix for live text-size changes", finding.Message);
        Assert.Contains("FontAutoScalingEnabled=\"False\"", finding.Message);
        Assert.DoesNotContain("before 10.0.100 this is a known issue", finding.Message);
        Assert.DoesNotContain("could be an older .NET MAUI version", finding.Message);
    }

    [Fact]
    public void ScreenLevelNotGrown_OnIosMaui_RestartConfirmed_KnownVersion_IsUnaffectedByVersion()
    {
        // Case 2 does not depend on the version at all (it already only lists causes that would not grow
        // even after a restart): a known version must not change this message.
        var normal = FourUnchangedTexts();
        var findings = Evaluate(Snapshot(
            normal, normal, platform: Platform.iOS, framework: AppFramework.Maui, largeTextRestartCaptured: true,
            frameworkVersion: "10.0.60"));

        var finding = Assert.Single(findings);
        Assert.DoesNotContain("This app uses .NET MAUI", finding.Message);
        Assert.Contains("FontAutoScalingEnabled=\"False\"", finding.Message);
        Assert.DoesNotContain("before 10.0.100", finding.Message);
    }

    [Fact]
    public void ScreenLevelNotGrown_OnIosMaui_RestartExplicitlyFalse_SameAsUnknown()
    {
        var normal = FourUnchangedTexts();
        var findings = Evaluate(Snapshot(normal, normal, platform: Platform.iOS, framework: AppFramework.Maui, largeTextRestartCaptured: false));

        var finding = Assert.Single(findings);
        Assert.Contains("while the app was running", finding.Message);
        Assert.Contains("could not confirm whether restarting", finding.Message);
    }

    [Fact]
    public void ScreenLevelNotGrown_OnIosNonMaui_GetsNoMauiCauseHints()
    {
        var normal = FourUnchangedTexts();
        var findings = Evaluate(Snapshot(normal, normal, platform: Platform.iOS, framework: AppFramework.Unknown, largeTextRestartCaptured: true));

        var finding = Assert.Single(findings);
        Assert.Contains("even after the app was restarted", finding.Message);
        Assert.DoesNotContain("MAUI", finding.Message);
        Assert.DoesNotContain("dotnet/maui", finding.Message);
        Assert.DoesNotContain("FontAutoScalingEnabled", finding.Message);
        Assert.False(finding.Details.ContainsKey("largeTextRestartCaptured"));
    }

    [Fact]
    public void ScreenLevelNotGrown_OnIosMaui_ZeroSizeNodesAreIgnored_StillCitesMauiCauses()
    {
        // A zero-size (collapsed) node must not be counted as a paired text, and must not crash the added
        // iOS + MAUI wording branch.
        var normal = new[]
        {
            Text("One", 0, 20), Text("Two", 30, 20), Text("Three", 60, 20), Text("Four", 90, 20),
            new AccessibilityNode { Role = "text", VisibleText = "Zero", Bounds = new Bounds(0, 120, 0, 0) },
        };
        var large = new[]
        {
            Text("One", 0, 20), Text("Two", 30, 20), Text("Three", 60, 20), Text("Four", 90, 20),
            new AccessibilityNode { Role = "text", VisibleText = "Zero", Bounds = new Bounds(0, 120, 0, 0) },
        };
        var findings = Evaluate(Snapshot(normal, large, platform: Platform.iOS, framework: AppFramework.Maui, largeTextRestartCaptured: true));

        var finding = Assert.Single(findings);
        Assert.Contains("4 of the 4 texts", finding.Message);
        Assert.Contains("FontAutoScalingEnabled=\"False\"", finding.Message);
    }

    [Fact]
    public void ScreenLevelNotGrown_OnAndroidMaui_RestartNotConfirmed_KeepsTheOriginalWording()
    {
        // Android gets no MAUI-specific cause hints (those are iOS-only); with LargeTextRestartCaptured unset
        // (the default -- Android's own DecideAfterRestart hasn't been wired up to set it yet either), the
        // message stays exactly the original wording.
        var normal = FourUnchangedTexts();
        var findings = Evaluate(Snapshot(normal, normal, platform: Platform.Android, framework: AppFramework.Maui));

        var finding = Assert.Single(findings);
        Assert.Equal("4 of the 4 texts on this screen did not get taller when the system text size was changed to Android font scale 2.0 " +
                     "while the app was running. The app may apply the new size only after it restarts, or may not support it. " +
                     "Relaunch the app at 200% text size and check that text grows and is not clipped.", finding.Message);
        Assert.False(finding.Details.ContainsKey("largeTextRestartCaptured"));
    }
}
