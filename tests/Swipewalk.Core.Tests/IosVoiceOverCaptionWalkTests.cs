using Swipewalk.Collectors.Ios;

namespace Swipewalk.Core.Tests;

/// <summary>
/// <see cref="IosVoiceOverCaptionWalk.AnalyzeImageAsync"/> actually runs
/// harness/mac-voiceover-captions/VoiceOverCaptionCapture.swift (via <c>xcrun swift</c>) against fixture PNGs
/// in Fixtures/voiceover-captions/ -- a real process invocation exercising the real OCR (Vision) and
/// focus-rect detection, deliberately not mocked, so this needs a Mac with Xcode's command-line tools (see
/// .github/workflows/ci.yml). The fixtures are entirely synthetic (see
/// Fixtures/voiceover-captions/generate_fixtures.swift) -- a drawn background, caption bar and cursor stroke --
/// never a real device screenshot.
/// </summary>
public class IosVoiceOverCaptionWalkTests
{
    private static readonly string FixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "voiceover-captions");
    private static string Fixture(string name) => Path.Combine(FixturesDir, name);

    [Fact]
    public async Task AnalyzeImage_EnglishCaptionWithACursorOverAMatchingCard_FindsBoth()
    {
        var result = await IosVoiceOverCaptionWalk.AnalyzeImageAsync(Fixture("en_submit_button.png"));

        Assert.True(result.Ok, result.Error);
        Assert.Equal("Submit, Button", result.CapturedText);
        Assert.NotNull(result.FocusRect);
        // The fixture drew the cursor at (40,180,1090,120) in top-left-origin pixels (1170x2532 image) --
        // allow a modest tolerance for the coarse sampling grid the detector uses.
        AssertClose(40, result.FocusRect!.X, 10);
        AssertClose(180, result.FocusRect.Y, 10);
        AssertClose(1090, result.FocusRect.W, 20);
        AssertClose(120, result.FocusRect.H, 20);
    }

    [Fact]
    public async Task AnalyzeImage_NonLatinScriptCaption_IsReadAndTheCursorLocationDiffersFromTheOtherFixture()
    {
        var result = await IosVoiceOverCaptionWalk.AnalyzeImageAsync(Fixture("ja_button.png"), languages: ["ja-JP"]);

        Assert.True(result.Ok, result.Error);
        Assert.NotNull(result.CapturedText);
        Assert.Contains("ボタン", result.CapturedText); // "Button" -- OCR can misread the preceding kanji; the role word is the stable part
        Assert.NotNull(result.FocusRect);
        AssertClose(340, result.FocusRect!.Y, 10); // this fixture's cursor sits higher on the screen than en_submit_button's
    }

    [Fact]
    public async Task AnalyzeImage_CaptionWithNoCursorDrawn_FindsTheCaptionButNoFocusRect()
    {
        // Exercises the false-positive guard directly: this fixture has two background cards (an ordinary,
        // low-contrast rectangle boundary) but no cursor stroke at all.
        var result = await IosVoiceOverCaptionWalk.AnalyzeImageAsync(Fixture("es_enviar_boton.png"));

        Assert.True(result.Ok, result.Error);
        Assert.Equal("Enviar, Boton", result.CapturedText);
        Assert.Null(result.FocusRect);
    }

    [Fact]
    public async Task AnalyzeImage_IdleFrameWithNeitherCaptionNorCursor_FindsNeither()
    {
        var result = await IosVoiceOverCaptionWalk.AnalyzeImageAsync(Fixture("idle_no_caption.png"));

        Assert.True(result.Ok, result.Error);
        Assert.Null(result.CapturedText);
        Assert.Null(result.CaptionRegion);
        Assert.Null(result.FocusRect);
    }

    [Fact]
    public async Task AnalyzeImage_MissingFile_FailsCleanlyInsteadOfThrowing()
    {
        var result = await IosVoiceOverCaptionWalk.AnalyzeImageAsync("/does/not/exist.png");

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void FindScript_Checkout_UsesTheScriptInPlace()
    {
        using var temp = new TempDir();
        var scriptDir = Directory.CreateDirectory(Path.Combine(temp.Root, "repo", "harness", "mac-voiceover-captions")).FullName;
        File.WriteAllText(Path.Combine(scriptDir, "VoiceOverCaptionCapture.swift"), "// script");
        var subdir = Directory.CreateDirectory(Path.Combine(temp.Root, "repo", "src")).FullName;

        var found = IosVoiceOverCaptionWalk.FindScript(subdir, baseDirectory: Path.Combine(temp.Root, "elsewhere"));

        Assert.Equal(Path.Combine(scriptDir, "VoiceOverCaptionCapture.swift"), found);
    }

    [Fact]
    public void FindScript_NotFoundAnywhere_ReturnsNull()
    {
        using var temp = new TempDir();

        Assert.Null(IosVoiceOverCaptionWalk.FindScript(temp.Root, temp.Root));
    }

    [Fact]
    public async Task RunLiveAsync_MissingScript_FailsWithAClearReasonInstead()
    {
        var result = await IosVoiceOverCaptionWalk.RunLiveAsync(
            "not-a-real-udid", TimeSpan.FromSeconds(1), CancellationToken.None, scriptPath: "/does/not/exist/VoiceOverCaptionCapture.swift");

        Assert.False(result.Ok);
        Assert.Contains("VoiceOverCaptionCapture.swift", result.Error);
    }

    private static void AssertClose(double expected, double actual, double tolerance) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"expected {expected} within {tolerance} of {actual}");

    private sealed class TempDir : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("cf-voc-walk-").FullName;
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
