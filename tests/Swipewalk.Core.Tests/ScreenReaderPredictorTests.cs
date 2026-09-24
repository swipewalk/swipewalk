using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;

namespace Swipewalk.Core.Tests;

public class ScreenReaderPredictorTests
{
    [Fact]
    public void AccessibleName_PrefersLabel_OverVisibleTextChildTextAndHint()
    {
        var node = new AccessibilityNode { Role = "button", Label = "Pay now", VisibleText = "Pay", Hint = "Double tap to pay" };

        Assert.Equal("Pay now", ScreenReaderPredictor.AccessibleName(node));
    }

    [Fact]
    public void AccessibleName_FallsBackToVisibleText_WhenNoLabel()
    {
        var node = new AccessibilityNode { Role = "text", VisibleText = "Pay", Hint = "Double tap to pay" };

        Assert.Equal("Pay", ScreenReaderPredictor.AccessibleName(node));
    }

    [Fact]
    public void AccessibleName_InteractiveElement_FallsBackToAccessibleChildText()
    {
        var node = new AccessibilityNode
        {
            Role = "button",
            IsInteractive = true,
            Children =
            [
                new AccessibilityNode { Role = "image", Label = "Icon" },
                new AccessibilityNode { Role = "text", VisibleText = "Pay now" },
            ],
        };

        Assert.Equal("Icon, Pay now", ScreenReaderPredictor.AccessibleName(node));
    }

    [Fact]
    public void AccessibleName_NonInteractiveElement_DoesNotFallBackToChildText()
    {
        var node = new AccessibilityNode
        {
            Role = "group",
            IsInteractive = false,
            IsFocusable = false,
            Hint = "A hint",
            Children = [new AccessibilityNode { Role = "text", VisibleText = "Pay now" }],
        };

        // Not interactive/focusable, so child text is not gathered; falls straight to the hint.
        Assert.Equal("A hint", ScreenReaderPredictor.AccessibleName(node));
    }

    [Fact]
    public void AccessibleName_FallsBackToHint_WhenNothingElseAvailable()
    {
        var node = new AccessibilityNode { Role = "button", IsInteractive = true, Hint = "Double tap to pay" };

        Assert.Equal("Double tap to pay", ScreenReaderPredictor.AccessibleName(node));
    }

    [Fact]
    public void AccessibleName_ReturnsNull_WhenNothingIsAvailable()
    {
        var node = new AccessibilityNode { Role = "button", IsInteractive = true };

        Assert.Null(ScreenReaderPredictor.AccessibleName(node));
    }

    [Fact]
    public void AccessibleName_TextField_DoesNotUseVisibleTextAsName()
    {
        // TalkBack announces an EditText's typed value, but the value is not an accessible name: a
        // field showing "15" with no label or hint has no accessible name.
        var node = new AccessibilityNode { Role = "textfield", IsInteractive = true, VisibleText = "15", Value = "15" };

        Assert.Null(ScreenReaderPredictor.AccessibleName(node));
    }

    [Fact]
    public void AccessibleName_TextField_StillUsesHintAsName()
    {
        var node = new AccessibilityNode { Role = "textfield", IsInteractive = true, VisibleText = "Subtotal", Value = "Subtotal", Hint = "Subtotal" };

        Assert.Equal("Subtotal", ScreenReaderPredictor.AccessibleName(node));
    }

    [Fact]
    public void AccessibleName_TextField_StillUsesLabelAsName()
    {
        var node = new AccessibilityNode { Role = "textfield", IsInteractive = true, Label = "Ticket number", VisibleText = "15", Value = "15" };

        Assert.Equal("Ticket number", ScreenReaderPredictor.AccessibleName(node));
    }

    [Fact]
    public void Announce_UnnamedTextFieldWithValue_AnnouncesValueAndRole()
    {
        // The predicted transcript still reads "15, Edit box" (what TalkBack actually announces),
        // even though "15" is not the field's accessible name for the missing-name rule.
        var node = new AccessibilityNode { Role = "textfield", IsInteractive = true, VisibleText = "15", Value = "15" };

        Assert.Equal("15, Edit box", ScreenReaderPredictor.Announce(node));
    }

    [Fact]
    public void Announce_UnnamedEmptyTextField_AnnouncesUnlabeledAndRole()
    {
        var node = new AccessibilityNode { Role = "textfield", IsInteractive = true };

        Assert.Equal("Unlabeled, Edit box", ScreenReaderPredictor.Announce(node));
    }

    [Fact]
    public void Announce_UnlabeledButton_AnnouncesUnlabeledAndRole()
    {
        var node = new AccessibilityNode { Role = "button", IsInteractive = true };

        Assert.Equal("Unlabeled, Button", ScreenReaderPredictor.Announce(node));
    }

    [Fact]
    public void Announce_DisabledElement_AppendsDisabled()
    {
        var node = new AccessibilityNode { Role = "button", IsInteractive = true, Label = "Save", IsEnabled = false };

        Assert.Equal("Save, Button, disabled", ScreenReaderPredictor.Announce(node));
    }

    [Fact]
    public void Predict_SwipeOrder_MergesFocusableChildren_AndSkipsInaccessibleNodes()
    {
        var tree = new AccessibilityNode
        {
            Role = "window",
            Children =
            [
                // 0: an interactive button whose name is drawn from its own accessible children;
                // because the walk returns as soon as it records this stop, those children are
                // not walked (and thus not announced) separately.
                new AccessibilityNode
                {
                    Role = "button",
                    IsInteractive = true,
                    Children =
                    [
                        new AccessibilityNode { Role = "image", Label = "Icon" },
                        new AccessibilityNode { Role = "text", VisibleText = "Pay now" },
                    ],
                },
                // 1: a wrapper hidden from assistive technology. It produces no stop of its own,
                // but its accessible children are still walked individually.
                new AccessibilityNode
                {
                    Role = "group",
                    IsAccessible = false,
                    Children =
                    [
                        new AccessibilityNode { Role = "button", IsInteractive = true, Label = "Submit" },
                        new AccessibilityNode { Role = "text", VisibleText = "Note" },
                    ],
                },
                // 2: hidden from assistive technology and has no accessible children: contributes no stop.
                new AccessibilityNode { Role = "button", IsInteractive = true, IsAccessible = false, Label = "Ignored" },
            ],
        };
        var snapshot = new ScreenSnapshot { Platform = Platform.Android, ScreenName = "Screen", Root = tree };

        var stops = ScreenReaderPredictor.Predict(snapshot);

        Assert.Equal(3, stops.Count);

        Assert.Equal(1, stops[0].Order);
        Assert.Equal("0", stops[0].NodePath);
        Assert.Equal("Icon, Pay now, Button", stops[0].Text);
        Assert.True(stops[0].HasName);

        Assert.Equal(2, stops[1].Order);
        Assert.Equal("1/0", stops[1].NodePath);
        Assert.Equal("Submit, Button", stops[1].Text);

        Assert.Equal(3, stops[2].Order);
        Assert.Equal("1/1", stops[2].NodePath);
        Assert.Equal("Note", stops[2].Text);
    }

    [Fact]
    public void Predict_ScrollingList_StopsOnEachRow()
    {
        var root = new AccessibilityNode
        {
            Role = "window",
            Children =
            [
                new AccessibilityNode
                {
                    Role = "group", IsFocusable = true, IsScrollable = true, IsAccessible = true,
                    Children =
                    [
                        new AccessibilityNode { Role = "button", IsInteractive = true, Children = [new AccessibilityNode { Role = "text", VisibleText = "Network & internet" }] },
                        new AccessibilityNode { Role = "button", IsInteractive = true, Children = [new AccessibilityNode { Role = "text", VisibleText = "Connected devices" }] },
                    ],
                },
            ],
        };

        var stops = ScreenReaderPredictor.Predict(new ScreenSnapshot { Platform = Platform.Android, ScreenName = "", Root = root });

        Assert.Equal(["Network & internet, Button", "Connected devices, Button"], stops.Select(s => s.Text));
    }
}
