using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Rules;

namespace Swipewalk.Core.Tests;

public class FixGuidanceTests
{
    private static Finding Finding(
        string ruleId, string role = "text", FindingKind kind = FindingKind.NeedsReview,
        IReadOnlyDictionary<string, string>? details = null) => new()
    {
        RuleId = ruleId,
        Kind = kind,
        Message = "",
        NodePath = "0",
        Role = role,
        Criteria = [],
        Details = details ?? new Dictionary<string, string>(),
    };

    [Fact]
    public void Maui_GetsMauiExampleAndCauses()
    {
        var fix = FixGuidance.For(Finding("text-resize"), Platform.Android, AppFramework.Maui)!;

        Assert.Equal("MAUI XAML", fix.CodeLanguage);
        Assert.Equal(".NET MAUI", fix.CausesFor);
        Assert.Contains(fix.LikelyCauses, c => c.Contains("HeightRequest"));
    }

    [Fact]
    public void UnknownFramework_FallsBackToNativeCauses()
    {
        var fix = FixGuidance.For(Finding("text-resize"), Platform.iOS, AppFramework.Unknown)!;

        Assert.Equal("iOS (UIKit and SwiftUI)", fix.CausesFor);
        Assert.Contains(fix.LikelyCauses, c => c.Contains("adjustsFontForContentSizeCategory") || c.Contains("preferredFont"));
    }

    [Fact]
    public void ScreenLevelResize_ExplainsMauiRuntimeBehavior()
    {
        var fix = FixGuidance.For(Finding("text-resize", role: "screen"), Platform.Android, AppFramework.Maui)!;

        Assert.Contains(fix.LikelyCauses, c => c.Contains("ConfigChanges.FontScale"));
    }

    [Fact]
    public void EveryRuleWithFindings_HasGuidance()
    {
        // "engine", "atf" and "screen-reader-capture" findings use RuleId "engine:<type>"/"atf:<checkName>"/
        // "screen-reader-capture:<kind>", never the bare rule id (see EngineIssueRule/AtfIssueRule/
        // ScreenReaderCaptureRule), so there's nothing for FixGuidance to look up under the bare id itself.
        foreach (var rule in DefaultRules.All.Where(r => r.Id is not ("engine" or "atf" or "screen-reader-capture")))
            Assert.NotNull(FixGuidance.For(Finding(rule.Id), Platform.Android, AppFramework.Maui));
    }

    [Fact]
    public void PlatformSpecificCauses_OnlyShowOnThatPlatform()
    {
        var ios = FixGuidance.For(Finding("text-resize", role: "screen"), Platform.iOS, AppFramework.Maui)!;

        Assert.DoesNotContain(ios.LikelyCauses, c => c.StartsWith("Android:"));
        Assert.Contains(ios.LikelyCauses, c => c.StartsWith("iOS:"));
    }

    [Fact]
    public void ScreenLevelResize_OnIosMaui_RestartCapturedDetailMissing_CitesBothCausesAsUncertain()
    {
        // No "largeTextRestartCaptured" detail (e.g. an older rule version, or Details not set): fall back to
        // the more conservative wording rather than ruling out the pre-10.0.100 restart issue.
        var fix = FixGuidance.For(Finding("text-resize", role: "screen"), Platform.iOS, AppFramework.Maui)!;

        Assert.Contains(fix.LikelyCauses, c => c.Contains("dotnet/maui#34445"));
        Assert.Contains(fix.LikelyCauses, c => c.Contains("FontAutoScalingEnabled"));
    }

    [Fact]
    public void ScreenLevelResize_OnIosMaui_RestartConfirmedCaptured_DropsTheVersionCause()
    {
        var fix = FixGuidance.For(
            Finding("text-resize", role: "screen", details: new Dictionary<string, string> { ["largeTextRestartCaptured"] = "true" }),
            Platform.iOS, AppFramework.Maui)!;

        Assert.DoesNotContain(fix.LikelyCauses, c => c.Contains("dotnet/maui#34445"));
        Assert.Contains(fix.LikelyCauses, c => c.Contains("FontAutoScalingEnabled"));
        Assert.Contains(fix.LikelyCauses, c => c.Contains("Shell tab bar title"));
    }

    [Fact]
    public void TextResizeLive_MauiIos_MentionsTheVersionFixAndTheWorkaround()
    {
        var fix = FixGuidance.For(Finding("text-resize-live", role: "screen"), Platform.iOS, AppFramework.Maui)!;

        Assert.Equal(".NET MAUI", fix.CausesFor);
        Assert.Equal("MAUI XAML", fix.CodeLanguage);
        Assert.Contains("MauiVersion", fix.Code);
        Assert.DoesNotContain("//", fix.Code); // pure XML: the C# workaround lives in LikelyCauses instead
        Assert.Contains(fix.LikelyCauses, c => c.Contains("dotnet/maui#34445"));
        Assert.Contains(fix.LikelyCauses, c => c.Contains("can't read the app's MAUI version"));
        Assert.Contains(fix.LikelyCauses, c => c.Contains("custom handler") && c.Contains("FontSize"));
        Assert.Contains(fix.LikelyCauses, c => c.Contains("AdjustsFontForContentSizeCategory"));
        Assert.DoesNotContain(fix.LikelyCauses, c => c.StartsWith("Android:"));
    }

    [Fact]
    public void TextResizeLive_MauiIos_KnownVersionAtOrAfterFix_DropsTheVersionUncertaintyCauses()
    {
        var fix = FixGuidance.For(Finding("text-resize-live", role: "screen"), Platform.iOS, AppFramework.Maui, "10.0.100")!;

        Assert.Contains(fix.LikelyCauses, c => c.Contains("10.0.100") && c.Contains("includes the fix"));
        Assert.DoesNotContain(fix.LikelyCauses, c => c.Contains("can't read the app's MAUI version"));
        Assert.DoesNotContain(fix.LikelyCauses, c => c.Contains("new projects can resolve an older"));
    }

    [Fact]
    public void TextResizeLive_MauiIos_KnownVersionBeforeFix_NamesTheVersion()
    {
        var fix = FixGuidance.For(Finding("text-resize-live", role: "screen"), Platform.iOS, AppFramework.Maui, "10.0.60")!;

        Assert.Contains(fix.LikelyCauses, c => c.Contains("10.0.60") && c.Contains("dotnet/maui#34445"));
        Assert.DoesNotContain(fix.LikelyCauses, c => c.Contains("can't read the app's MAUI version"));
    }

    [Fact]
    public void ScreenLevelResize_OnIosMaui_RestartNotConfirmed_KnownVersionAtOrAfterFix_DropsTheOldVersionCause()
    {
        var fix = FixGuidance.For(Finding("text-resize", role: "screen"), Platform.iOS, AppFramework.Maui, "10.0.100")!;

        Assert.DoesNotContain(fix.LikelyCauses, c => c.Contains("before Microsoft.Maui.Controls 10.0.100"));
        Assert.Contains(fix.LikelyCauses, c => c.Contains("10.0.100") && c.Contains("includes the fix"));
        Assert.Contains(fix.LikelyCauses, c => c.Contains("FontAutoScalingEnabled"));
    }

    [Fact]
    public void ScreenLevelResize_OnIosMaui_RestartConfirmedCaptured_KnownVersion_IsUnaffectedByVersion()
    {
        // Case 2 (restart confirmed) must not change with a known version -- only case 3 does.
        var fix = FixGuidance.For(
            Finding("text-resize", role: "screen", details: new Dictionary<string, string> { ["largeTextRestartCaptured"] = "true" }),
            Platform.iOS, AppFramework.Maui, "10.0.60")!;

        Assert.DoesNotContain(fix.LikelyCauses, c => c.Contains("dotnet/maui#34445"));
        Assert.DoesNotContain(fix.LikelyCauses, c => c.Contains("10.0.60"));
        Assert.Contains(fix.LikelyCauses, c => c.Contains("FontAutoScalingEnabled"));
    }

    [Fact]
    public void TextResizeLive_MauiAndroid_MentionsConfigurationChangesOnly()
    {
        var fix = FixGuidance.For(Finding("text-resize-live", role: "screen"), Platform.Android, AppFramework.Maui)!;

        Assert.Contains("ConfigChanges.FontScale", fix.Code);
        Assert.DoesNotContain(fix.LikelyCauses, c => c.StartsWith("iOS:"));
        Assert.Contains(fix.LikelyCauses, c => c.StartsWith("Android:"));
    }

    [Fact]
    public void TextResizeLive_NativeAndroid_UsesConfigChangesApi()
    {
        var fix = FixGuidance.For(Finding("text-resize-live", role: "screen"), Platform.Android, AppFramework.Unknown)!;

        Assert.Equal("Android (Views and Compose)", fix.CausesFor);
        Assert.Contains("configChanges", fix.Code);
    }

    [Fact]
    public void TextResizeLive_NativeIos_UsesAdjustsFontForContentSizeCategory()
    {
        var fix = FixGuidance.For(Finding("text-resize-live", role: "screen"), Platform.iOS, AppFramework.Unknown)!;

        Assert.Equal("iOS (UIKit and SwiftUI)", fix.CausesFor);
        Assert.Contains("adjustsFontForContentSizeCategory", fix.Code);
    }

    [Fact]
    public void TextResizeLive_MauiAndroid_DoesNotClaimOnSaveInstanceStateAndDescribesKeepingThePlace()
    {
        // MAUI apps don't save/restore page state via OnSaveInstanceState; the fix must describe the verified
        // keep-place pattern (remember the current page, push it again in App.CreateWindow) instead.
        var fix = FixGuidance.For(Finding("text-resize-live", role: "screen"), Platform.Android, AppFramework.Maui)!;

        Assert.DoesNotContain("OnSaveInstanceState) so users don't lose their place", fix.Code);
        Assert.Contains("App.CreateWindow", fix.Code);
        Assert.Contains("OnAppearing", fix.Code);
        Assert.Contains("Shell", fix.Code);
        Assert.Contains("not a drop-in", fix.Code);
    }

    [Fact]
    public void ScreenLevelResize_MauiAndroid_RecommendsRemovingFontScaleAndCheckingFontAutoScaling()
    {
        // ConfigChanges.FontScale alone does not make MAUI re-apply font sizes (verified 2026-09-23): the fix
        // must say to remove it (not add it), and also name FontAutoScalingEnabled as a cause of text that
        // never grows -- this is the "text doesn't grow at all" finding, not the separate lost-place finding
        // (text-resize-navigation), so it must not assert on the keep-place pattern's own code.
        var fix = FixGuidance.For(Finding("text-resize", role: "screen"), Platform.Android, AppFramework.Maui)!;

        Assert.DoesNotContain("then re-apply font sizes", fix.Code);
        Assert.Contains("remove it", fix.Code);
        Assert.Contains("FontScale", fix.Code);
        Assert.Contains(fix.LikelyCauses, c => c.Contains("FontAutoScalingEnabled"));
    }

    [Fact]
    public void TextResizeNavigation_MauiAndroid_DescribesKeepingThePlace()
    {
        var fix = FixGuidance.For(Finding("text-resize-navigation", role: "screen"), Platform.Android, AppFramework.Maui)!;

        Assert.Equal(".NET MAUI", fix.CausesFor);
        Assert.Contains("App.CreateWindow", fix.Code);
        Assert.Contains(fix.LikelyCauses, c => c.Contains("ConfigChanges.FontScale"));
    }

    [Fact]
    public void TextResizeNavigation_NativeAndroid_SuggestsRestoringTheDestination()
    {
        var fix = FixGuidance.For(Finding("text-resize-navigation", role: "screen"), Platform.Android, AppFramework.Unknown)!;

        Assert.Equal("Android (Views and Compose)", fix.CausesFor);
        Assert.Null(fix.Code);
        Assert.Contains(fix.LikelyCauses, c => c.Contains("restart", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fix.LikelyCauses, c => c.Contains("ViewModel") || c.Contains("rememberSaveable"));
    }
}
