using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;

namespace Swipewalk.Core.Tests;

public class ScreenIdentityTests
{
    private static AccessibilityNode Screen(string title, string fieldValue, double y = 0) => new()
    {
        Role = "window",
        Children =
        [
            new AccessibilityNode { Role = "text", VisibleText = title, Bounds = new Bounds(0, y, 100, 20) },
            new AccessibilityNode { Role = "textfield", Label = "Name", VisibleText = fieldValue, IsInteractive = true, Bounds = new Bounds(0, y + 30, 100, 40) },
        ],
    };

    [Fact]
    public void Signature_IgnoresTypedTextAndPosition()
    {
        Assert.Equal(ScreenIdentity.Signature(Screen("Checkout", "")), ScreenIdentity.Signature(Screen("Checkout", "Ada", y: 12)));
    }

    [Fact]
    public void Signature_ChangesWithContent()
    {
        Assert.NotEqual(ScreenIdentity.Signature(Screen("Checkout", "")), ScreenIdentity.Signature(Screen("Receipt", "")));
    }

    [Fact]
    public void GuessTitle_UsesFirstNamedStop()
    {
        var snapshot = new ScreenSnapshot { Platform = Platform.Android, ScreenName = "", Root = Screen("Checkout", "") };

        Assert.Equal("Checkout", ScreenIdentity.GuessTitle(snapshot));
    }

    [Fact]
    public void GuessTitle_SkipsButtonsBeforeTheTitle()
    {
        var root = new AccessibilityNode
        {
            Role = "window",
            Children =
            [
                new AccessibilityNode { Role = "button", Label = "Navigate up", IsInteractive = true, Bounds = new Bounds(0, 0, 48, 48) },
                new AccessibilityNode { Role = "text", VisibleText = "Payment history", Bounds = new Bounds(60, 0, 200, 48) },
            ],
        };

        Assert.Equal("Payment history", ScreenIdentity.GuessTitle(new ScreenSnapshot { Platform = Platform.Android, ScreenName = "", Root = root }));
    }

    [Fact]
    public void IsSameScreen_ToleratesContentPushedOffScreen()
    {
        Assert.True(ScreenIdentity.IsSameScreen(Snap("Title", "A", "B", "C"), Snap("Title", "A", "B")));
        Assert.False(ScreenIdentity.IsSameScreen(Snap("Title", "A", "B", "C"), Snap("Other", "A", "B", "C")));
    }

    private static ScreenSnapshot Snap(params string[] texts) => new()
    {
        Platform = Platform.Android,
        ScreenName = "",
        Root = new AccessibilityNode
        {
            Role = "window",
            Children = [.. texts.Select((t, i) => new AccessibilityNode { Role = "text", VisibleText = t, Bounds = new Bounds(0, i * 30, 100, 20) })],
        },
    };

    /// <summary>
    /// <see cref="ScreenIdentity.Fingerprint"/> and the <see cref="ScreenIdentity.IsSameScreen(ScreenFingerprint,ScreenSnapshot)"/>
    /// overload: what record mode's same-screen replacement uses when the earlier screen's full tree isn't
    /// available any more (a continued run's earlier session -- see Swipewalk.Engine.RecordState). Must behave
    /// exactly like the full-snapshot comparison it's a lightweight stand-in for.
    /// </summary>
    [Fact]
    public void Fingerprint_IsSameScreen_MatchesTheFullSnapshotComparison()
    {
        var earlier = ScreenIdentity.Fingerprint(Snap("Title", "A", "B", "C"));

        Assert.True(ScreenIdentity.IsSameScreen(earlier, Snap("Title", "A", "B")));
        Assert.False(ScreenIdentity.IsSameScreen(earlier, Snap("Other", "A", "B", "C")));
    }

    [Fact]
    public void ScreenFingerprint_Unknown_NeverMatchesARealScreen()
    {
        // Pads a continued run's identities when an earlier screen's real fingerprint wasn't saved (see
        // RecordContinuation.PriorIdentities): must never accidentally match a real capture, even one with no
        // title text and no other elements (the least distinctive case).
        Assert.False(ScreenIdentity.IsSameScreen(ScreenFingerprint.Unknown, Snap()));
        Assert.False(ScreenIdentity.IsSameScreen(ScreenFingerprint.Unknown, Snap("Untitled screen")));
    }
}
