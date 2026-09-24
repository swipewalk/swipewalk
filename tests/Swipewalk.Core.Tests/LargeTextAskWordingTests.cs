using Swipewalk.Collectors;
using Swipewalk.Core.Model;
using Swipewalk.Engine;

namespace Swipewalk.Core.Tests;

/// <summary>
/// <see cref="LargeTextAskWording"/>: shared by the CLI console prompt and the desktop dialog, for both record
/// (later screens, "navigate back twice") and scan (one screen, Swipewalk restarts and tries again on its own),
/// plus <see cref="LargeTextAskWording.DeveloperNote"/>, the short line pointing developers to the report's
/// advice on avoiding the restart.
/// </summary>
public class LargeTextAskWordingTests
{
    [Fact]
    public void Message_Scan_DoesNotMentionNavigatingBack()
    {
        var message = LargeTextAskWording.Message("Login", LargeTextCapture.DidNotGrowLive, learnedFromEarlierScreen: false, Platform.Android, recordMode: false);

        // Scan does the restart-and-recapture itself; there's nobody to navigate anywhere.
        Assert.DoesNotContain("navigate", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Swipewalk will restart the app", message);
    }

    [Fact]
    public void Message_Record_MentionsNavigatingBackTwice()
    {
        var message = LargeTextAskWording.Message("Login", LargeTextCapture.DidNotGrowLive, learnedFromEarlierScreen: false, Platform.Android, recordMode: true);

        Assert.Contains("navigate back twice", message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Message_AlwaysIncludesTheDeveloperNote_HedgedAndWithoutFontScale(bool recordMode)
    {
        var message = LargeTextAskWording.Message("Login", LargeTextCapture.DidNotGrowLive, learnedFromEarlierScreen: false, Platform.Android, recordMode);

        Assert.Contains(LargeTextAskWording.DeveloperNote, message);
        // Hedged per wcag-reviewer (2026-09-23): "avoid" alone doesn't hedge anything ("remove" claims the
        // same ability) -- the "if the text grows after the restart" condition does, since only then does the
        // report have fix guidance for this screen to point at.
        Assert.Contains("If the text grows after the restart", LargeTextAskWording.DeveloperNote);
        Assert.Contains("avoid", LargeTextAskWording.DeveloperNote, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("remove", LargeTextAskWording.DeveloperNote, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FontScale", message);
    }
}
