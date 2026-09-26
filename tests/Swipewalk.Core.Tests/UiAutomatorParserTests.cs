using Swipewalk.Collectors.Android;
using Swipewalk.Core.Model;
using Swipewalk.Core.Rules;

namespace Swipewalk.Core.Tests;

public class UiAutomatorParserTests
{
    // Density 420 dpi -> scale 420/160 = 2.625; bounds below are chosen to convert to clean dp values.
    private const string FullXml = """
        <hierarchy rotation="0">
          <node index="0" text="" resource-id="" class="android.widget.FrameLayout" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][750,1334]">
            <node index="0" text="Submit" resource-id="com.example.app:id/btnSubmit" class="android.widget.Button" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,105]">
              <node index="0" text="Overlay" resource-id="" class="android.widget.TextView" package="com.other.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][10,10]" />
            </node>
            <node index="1" text="" resource-id="" class="android.widget.EditText" package="com.example.app" content-desc="" hint="Email" checkable="false" checked="false" clickable="false" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,105][420,210]" />
            <node index="2" text="" resource-id="com.example.app:id/imgProfile" class="android.widget.ImageView" package="com.example.app" content-desc="Profile" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,210][105,315]" />
            <node index="3" text="" resource-id="" class="android.widget.ImageView" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,315][105,420]" />
            <node index="4" text="Hello" resource-id="" class="android.widget.TextView" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,420][210,525]" />
          </node>
          <node index="1" text="" resource-id="" class="android.widget.FrameLayout" package="com.other.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][750,1334]" />
        </hierarchy>
        """;

    // Only the nodes TalkBack considers important: the decorative ImageView (index 3 in the full
    // dump) is intentionally left out, so it should end up IsAccessible = false.
    private const string CompressedXml = """
        <hierarchy rotation="0">
          <node index="0" text="Submit" resource-id="com.example.app:id/btnSubmit" class="android.widget.Button" content-desc="" bounds="[0,0][210,105]" />
          <node index="1" text="" resource-id="" class="android.widget.EditText" content-desc="" bounds="[0,105][420,210]" />
          <node index="2" text="" resource-id="com.example.app:id/imgProfile" class="android.widget.ImageView" content-desc="Profile" bounds="[0,210][105,315]" />
          <node index="3" text="Hello" resource-id="" class="android.widget.TextView" content-desc="" bounds="[0,420][210,525]" />
        </hierarchy>
        """;

    [Fact]
    public void Parse_DropsOtherPackageTopLevelNode_AndKeepsOnlyMatchingPackage()
    {
        var root = UiAutomatorParser.Parse(FullXml, CompressedXml, density: 420);

        Assert.Equal("window", root.Role);
        Assert.False(root.IsAccessible);
        var app = Assert.Single(root.Children);
        Assert.Equal("android.widget.FrameLayout", app.NativeType);
        Assert.Equal(root.Bounds, app.Bounds);
    }

    [Fact]
    public void Parse_MapsRolesFromNativeType()
    {
        var app = UiAutomatorParser.Parse(FullXml, CompressedXml, density: 420).Children[0];

        Assert.Equal(5, app.Children.Count);
        Assert.Equal("button", app.Children[0].Role); // Button
        Assert.Equal("textfield", app.Children[1].Role); // EditText
        Assert.Equal("button", app.Children[2].Role); // clickable ImageView
        Assert.Equal("image", app.Children[3].Role); // non-clickable ImageView
        Assert.Equal("text", app.Children[4].Role); // non-clickable TextView
    }

    [Fact]
    public void Parse_ConvertsPixelBoundsToDpUsingDensity()
    {
        var app = UiAutomatorParser.Parse(FullXml, CompressedXml, density: 420).Children[0];

        AssertBounds(0, 0, 80, 40, app.Children[0].Bounds);
        AssertBounds(0, 40, 160, 40, app.Children[1].Bounds);
        AssertBounds(0, 80, 40, 40, app.Children[2].Bounds);
        AssertBounds(0, 120, 40, 40, app.Children[3].Bounds);
        AssertBounds(0, 160, 80, 40, app.Children[4].Bounds);
    }

    [Fact]
    public void Parse_MapsContentDescToLabel_AndTextToVisibleText()
    {
        var app = UiAutomatorParser.Parse(FullXml, CompressedXml, density: 420).Children[0];

        Assert.Equal("Submit", app.Children[0].VisibleText);
        Assert.Null(app.Children[0].Label);
        Assert.Equal("Profile", app.Children[2].Label);
        Assert.Equal("Hello", app.Children[4].VisibleText);
    }

    [Fact]
    public void Parse_MapsResourceIdToAutomationId_StrippingPackageAndIdPrefix()
    {
        var app = UiAutomatorParser.Parse(FullXml, CompressedXml, density: 420).Children[0];

        Assert.Equal("btnSubmit", app.Children[0].AutomationId);
        Assert.Equal("imgProfile", app.Children[2].AutomationId);
        Assert.Null(app.Children[1].AutomationId);
    }

    [Fact]
    public void Parse_DropsNestedNodesFromOtherPackages()
    {
        var app = UiAutomatorParser.Parse(FullXml, CompressedXml, density: 420).Children[0];

        Assert.Empty(app.Children[0].Children); // the "Overlay" node (com.other.app) is dropped
    }

    [Fact]
    public void Parse_NodesAbsentFromCompressedDump_AreNotAccessible()
    {
        var app = UiAutomatorParser.Parse(FullXml, CompressedXml, density: 420).Children[0];

        Assert.True(app.Children[0].IsAccessible); // Button, present
        Assert.True(app.Children[1].IsAccessible); // EditText, present
        Assert.True(app.Children[2].IsAccessible); // clickable ImageView, present
        Assert.False(app.Children[3].IsAccessible); // decorative ImageView, absent from compressed dump
        Assert.True(app.Children[4].IsAccessible); // TextView, present
    }

    [Fact]
    public void Parse_WithoutCompressedDump_TreatsEveryNodeAsAccessible()
    {
        var app = UiAutomatorParser.Parse(FullXml, compressedXml: null, density: 160).Children[0];

        Assert.All(app.Children, c => Assert.True(c.IsAccessible));
    }

    [Fact]
    public void Parse_InteractiveFlags_ReflectClickableAndFocusable()
    {
        var app = UiAutomatorParser.Parse(FullXml, CompressedXml, density: 420).Children[0];

        Assert.True(app.Children[0].IsInteractive); // clickable Button
        Assert.True(app.Children[0].IsFocusable);
        Assert.False(app.Children[1].IsInteractive); // EditText is focusable but not clickable
        Assert.True(app.Children[1].IsFocusable);
        Assert.Equal("Email", app.Children[1].Hint);
    }

    [Theory]
    [InlineData("android.widget.EditText", false, "textfield")]
    [InlineData("android.widget.AutoCompleteTextView", false, "textfield")]
    [InlineData("android.widget.CheckBox", false, "checkbox")]
    [InlineData("android.widget.Switch", false, "switch")]
    [InlineData("android.widget.RadioButton", false, "radio")]
    [InlineData("android.widget.SeekBar", false, "slider")]
    [InlineData("android.widget.ProgressBar", false, "progressbar")]
    [InlineData("android.webkit.WebView", false, "webview")]
    [InlineData("com.example.CustomButton", false, "button")]
    [InlineData("android.widget.ImageView", false, "image")]
    [InlineData("android.widget.ImageView", true, "button")]
    [InlineData("android.widget.TextView", false, "text")]
    [InlineData("android.widget.TextView", true, "button")]
    [InlineData("android.view.ViewGroup", false, "group")]
    [InlineData("android.view.ViewGroup", true, "button")]
    public void MapRole_MapsNativeTypesToNormalizedRoles(string nativeType, bool interactive, string expectedRole)
    {
        Assert.Equal(expectedRole, UiAutomatorParser.MapRole(nativeType, interactive));
    }

    private static void AssertBounds(double x, double y, double width, double height, Swipewalk.Core.Model.Bounds actual)
    {
        Assert.Equal(x, actual.X, 3);
        Assert.Equal(y, actual.Y, 3);
        Assert.Equal(width, actual.Width, 3);
        Assert.Equal(height, actual.Height, 3);
    }

    [Fact]
    public void Parse_EditTextWithTypedValueAndNoHint_SetsValue_KeepsVisibleTextForPixelChecks()
    {
        // A real device capture (TipCalc): an EditText holding a typed value with no hint and no
        // content-desc. TalkBack announces the value ("15, Edit box"), which is why VisibleText still
        // carries it for contrast/resize checks, but it must not double as the field's accessible name
        // (see ScreenReaderPredictor.AccessibleName), so it is exposed separately as Value too.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="15" resource-id="" class="android.widget.EditText" package="com.example.app" content-desc="" hint="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="true" password="false" selected="false" bounds="[0,0][210,105]" />
            </hierarchy>
            """;

        var field = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Equal("textfield", field.Role);
        Assert.Equal("15", field.VisibleText);
        Assert.Equal("15", field.Value);
        Assert.Null(field.Hint);
        Assert.Null(field.Label);
    }

    [Fact]
    public void Parse_EditTextWithHint_SetsHint_AndValueFromText()
    {
        // A real device capture (TipCalc): the platform hint attribute is populated, so it becomes
        // Hint (the field's name source); the duplicated text (Android repeats the hint into "text"
        // while it is showing) still becomes Value like any other editable field's rendered content.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="Subtotal" resource-id="" class="android.widget.EditText" package="com.example.app" content-desc="" hint="Subtotal" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="true" password="false" selected="false" bounds="[0,0][210,105]" />
            </hierarchy>
            """;

        var field = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Equal("Subtotal", field.Hint);
        Assert.Equal("Subtotal", field.Value);
        Assert.Equal("Subtotal", field.VisibleText);
    }

    [Fact]
    public void Parse_NonTextfieldWithText_DoesNotSetValue()
    {
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="Hello" resource-id="" class="android.widget.TextView" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,105]" />
            </hierarchy>
            """;

        var text = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Equal("Hello", text.VisibleText);
        Assert.Null(text.Value);
    }

    [Fact]
    public void Parse_GuessesPackage_FromMostCommonInteractivePackage_NotTheFirstTopLevelNode()
    {
        // A real dump can list the on-screen keyboard as a top-level node before the app's own, e.g. while a
        // text field has focus; guessing "the first top-level node's package" would then pick the keyboard.
        // The app's package should still win because most of the interactive (clickable) nodes are its own.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.inputmethodservice.SoftInputWindow" package="com.google.android.inputmethod.latin" clickable="true" bounds="[0,1000][1080,2400]" />
              <node index="1" text="" resource-id="" class="android.widget.FrameLayout" package="com.example.app" clickable="false" bounds="[0,0][1080,1000]">
                <node index="0" text="Submit" resource-id="" class="android.widget.Button" package="com.example.app" clickable="true" bounds="[0,0][200,80]" />
                <node index="1" text="" resource-id="" class="android.widget.ImageView" package="com.example.app" content-desc="Profile" clickable="true" bounds="[0,80][80,160]" />
              </node>
            </hierarchy>
            """;

        var root = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160);

        Assert.Equal("com.example.app", root.NativeType);
        var app = Assert.Single(root.Children);
        Assert.Equal(2, app.Children.Count);
    }

    [Fact]
    public void Parse_GuessesPackage_FallsBackToMostCommonOverall_WhenNothingIsInteractive()
    {
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="Hello" resource-id="" class="android.widget.TextView" package="com.example.app" clickable="false" bounds="[0,0][200,80]" />
              <node index="1" text="World" resource-id="" class="android.widget.TextView" package="com.example.app" clickable="false" bounds="[0,80][200,160]" />
              <node index="2" text="" resource-id="" class="android.widget.FrameLayout" package="com.other.app" clickable="false" bounds="[0,160][200,240]" />
            </hierarchy>
            """;

        var root = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160);

        Assert.Equal("com.example.app", root.NativeType);
    }

    [Fact]
    public void Parse_SeekBar_IsInteractiveEvenWhenNotClickable()
    {
        // A real device capture (TipCalc): SeekBar reports clickable="false" (it is dragged, not
        // tapped) but is still an adjustable control that TalkBack stops on; treat it as interactive
        // like iOS's unconditionally-interactive "slider" role, so an unnamed one is flagged.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.widget.SeekBar" package="com.example.app" content-desc="" hint="" checkable="false" checked="false" clickable="false" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,105]" />
            </hierarchy>
            """;

        var slider = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Equal("slider", slider.Role);
        Assert.True(slider.IsInteractive);
    }

    [Fact]
    public void Parse_MergesAtfExtras_ByTheSameIdentityAsTheCompressedDumpMatch()
    {
        // Same identity UiAutomatorParser's own Key() computes for the compressed-dump match above:
        // class|bounds|resource-id|text|content-desc, in raw pixels, "" (not "null") for anything unset.
        var extras = new Dictionary<string, AndroidHarness.NodeExtras>
        {
            ["android.widget.Button|[0,0][210,105]|com.example.app:id/btnSubmit|Submit|"] =
                new(IsShowingHintText: null, HintText: null, IsHeading: true, PaneTitle: null, StateDescription: "pressed", IsImportantForAccessibility: true),
        };

        var root = UiAutomatorParser.Parse(FullXml, CompressedXml, density: 420, atfExtras: extras);

        var submit = root.Children[0].Children[0];
        Assert.True(submit.IsHeading);
        Assert.Equal("pressed", submit.StateDescription);
        Assert.True(submit.IsImportantForAccessibility);
        Assert.Null(submit.IsShowingHintText);

        // A node with no matching key gets no extras at all (not zeroed-out false values).
        var editText = root.Children[0].Children[1];
        Assert.Null(editText.IsHeading);
        Assert.Null(editText.IsShowingHintText);
    }

    [Fact]
    public void Parse_FillsInHintFromTheHarness_WhenTheDumpsOwnHintAttributeIsEmpty()
    {
        // The EditText at index 1 in FullXml has hint="Email" already in the dump, so use the SeekBar
        // fixture (no hint attribute at all) to exercise the "dump has none" path the harness fixes --
        // e.g. the Android 13 quirk where a device's dump omits "hint" even though the field has one.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.widget.EditText" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,105]" />
            </hierarchy>
            """;
        var extras = new Dictionary<string, AndroidHarness.NodeExtras>
        {
            ["android.widget.EditText|[0,0][210,105]|||"] =
                new(IsShowingHintText: true, HintText: "Email", IsHeading: null, PaneTitle: null, StateDescription: null, IsImportantForAccessibility: null),
        };

        var field = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160, atfExtras: extras).Children[0];

        Assert.Equal("Email", field.Hint);
    }

    [Fact]
    public void Parse_KeepsTheDumpsOwnHint_OverTheHarnesss()
    {
        var extras = new Dictionary<string, AndroidHarness.NodeExtras>
        {
            // Same identity as the index-1 EditText in FullXml, which already has hint="Email" in the dump.
            ["android.widget.EditText|[0,105][420,210]|||"] =
                new(IsShowingHintText: true, HintText: "Different hint from the harness", IsHeading: null, PaneTitle: null, StateDescription: null, IsImportantForAccessibility: null),
        };

        var field = UiAutomatorParser.Parse(FullXml, compressedXml: null, density: 420, atfExtras: extras).Children[0].Children[1];

        Assert.Equal("Email", field.Hint);
    }

    // --- Compose merged-name gap (KnownLimitations "android-compose-merged-name") ---
    // Jetpack Compose's merged semantics (Modifier.semantics(mergeDescendants = true), the IconButton/Button
    // default) can leave the clickable, focusable node's own content-desc/text empty in the uiautomator
    // dump, with the name landing on a separate, non-clickable child instead. These test a clickable
    // "IconButton"-shaped node (an outer android.view.View, clickable and focusable, no content-desc/text
    // of its own) wrapping the icon child(ren) uiautomator actually names, matching the shape confirmed in
    // samples/NativeAndroid's Compose screen (see tests/Swipewalk.Core.Tests/Fixtures/NativeAndroid.Compose).

    [Fact]
    public void Parse_ClickableNodeWithOneNamedNonFocusableDescendant_TakesThatNameAsItsOwnLabel()
    {
        // The passing case: exactly one named descendant (an icon's content-desc), no other focusable
        // descendant -- a screen reader is expected to have nothing else to read, so the outer node gets
        // the name.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,210]">
                <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="More info" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[42,42][168,168]" />
                <node index="1" text="" resource-id="" class="android.widget.Button" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[42,42][168,168]" />
              </node>
            </hierarchy>
            """;

        var button = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Equal("More info", button.Label);
        Assert.Equal("button", button.Role);
        // The source descendant's own content-desc is cleared, so a rule that scans every node (e.g.
        // IdentifierNameRule) does not also match it separately and double-report the same name.
        Assert.Null(button.Children[0].Label);
    }

    [Fact]
    public void Parse_ClickableNodeWithOneNamedTextDescendant_TakesTheNameButKeepsTheDescendantsVisibleText()
    {
        // A single named descendant carrying VISIBLE text (not a content-desc) rather than an icon --
        // e.g. Compose's "View payment history" button. VisibleText is left in place on the descendant:
        // other rules (text-contrast) still need to see it as real on-screen text.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][400,120]">
                <node index="0" text="View payment history" resource-id="" class="android.widget.TextView" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[42,20][358,100]" />
              </node>
            </hierarchy>
            """;

        var button = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Equal("View payment history", button.Label);
        Assert.Equal("View payment history", button.Children[0].VisibleText);
    }

    [Fact]
    public void Parse_ClickableNodeWithOneContentDescDescendantAndOneTextDescendant_MergesBoth()
    {
        // The N5 shape (samples/NativeAndroid: Modifier.semantics { contentDescription = "Submit" } set
        // directly on the Button itself -- not on an icon, N5 has no icon -- alongside a separate
        // Text("Pay") child; Compose's tree export still splits the two across separate descendants).
        // Real TalkBack capture of exactly this button (docs/case-study.md "Real TalkBack capture, in five
        // languages") announced "Submit || Pay || Button" -- both parts are spoken -- so this merges both
        // onto the outer node: Label carries the full joined name (so label-in-name doesn't report a
        // mismatch TalkBack's capture didn't show) and VisibleText carries just the visible part (so
        // label-in-name can evaluate the node at all -- it couldn't while both stayed null; identifier-name
        // could always evaluate the content-desc descendant directly, and still can, see the next test).
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,105]">
                <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="Submit" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][105,105]" />
                <node index="1" text="Pay" resource-id="" class="android.widget.TextView" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[105,0][210,105]" />
              </node>
            </hierarchy>
            """;

        var button = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Equal("Submit, Pay", button.Label);
        Assert.Equal("Pay", button.VisibleText);
        // Unlike the single-descendant case, the content-desc source is NOT cleared here (see the next
        // test for why): both descendants keep their own fields untouched.
        Assert.Equal("Submit", button.Children[0].Label);
        Assert.Equal("Pay", button.Children[1].VisibleText);
    }

    [Fact]
    public void Parse_ClickableNodeWithIdentifierLookingContentDescAndTextDescendant_KeepsTheChildLabelToAvoidSilencingIt()
    {
        // Same shape as the N5 test above, but the content-desc looks like a developer identifier
        // ("img_btn_pay"). Clearing it (as the single-descendant merge does) would silence
        // IdentifierNameRule entirely, since the merged name ("img_btn_pay, Pay") contains a space and
        // IdentifierNameRule.LooksLikeIdentifier never matches a name with a space in it -- so nothing
        // would be reported at all. Leaving the descendant's own Label in place means IdentifierNameRule
        // still evaluates it directly (with the pre-existing wrong, non-clickable role -- not fixed for
        // this shape, unlike the single-descendant case), which is a smaller problem than reporting
        // nothing.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,105]">
                <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="img_btn_pay" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][105,105]" />
                <node index="1" text="Pay" resource-id="" class="android.widget.TextView" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[105,0][210,105]" />
              </node>
            </hierarchy>
            """;

        var button = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Equal("img_btn_pay, Pay", button.Label);
        Assert.Equal("Pay", button.VisibleText);
        Assert.Equal("img_btn_pay", button.Children[0].Label);

        var findings = new IdentifierNameRule().Evaluate(new ScreenSnapshot
        {
            Platform = Platform.Android,
            ScreenName = "Screen",
            Root = new AccessibilityNode { Role = "window", Children = [button] },
        }).ToList();

        var finding = Assert.Single(findings);
        Assert.Equal("img_btn_pay", finding.Label);
    }

    [Fact]
    public void Parse_ClickableNodeWithOneDescendantCarryingBothLabelAndVisibleText_DoesNotMerge()
    {
        // A descendant that carries its OWN content-desc and its OWN visible text ("Submit" / "X"),
        // alongside a separate visible-text-only descendant ("Pay"): this has two named descendants, but
        // isn't the N5 shape (one content-desc-only candidate, one text-only candidate) -- merging it the
        // same way would silently drop "X" (it isn't part of either the content-desc or the text-only
        // group), so this declines rather than guessing which of the three names to keep.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,105]">
                <node index="0" text="X" resource-id="" class="android.widget.TextView" package="com.example.app" content-desc="Submit" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][105,105]" />
                <node index="1" text="Pay" resource-id="" class="android.widget.TextView" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[105,0][210,105]" />
              </node>
            </hierarchy>
            """;

        var button = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Null(button.Label);
        Assert.Null(button.VisibleText);
    }

    [Fact]
    public void Parse_ClickableNodeWithTwoContentDescDescendants_DoesNotMerge()
    {
        // Ambiguous: two different content-desc candidates -- which one (or both, in what order) a screen
        // reader actually announces isn't established, so this doesn't guess.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,105]">
                <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="Submit" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][105,105]" />
                <node index="1" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="Confirm" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[105,0][210,105]" />
              </node>
            </hierarchy>
            """;

        var button = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Null(button.Label);
        Assert.Null(button.VisibleText);
    }

    [Fact]
    public void Parse_ClickableNodeWithTwoDistinctTextDescendants_DoesNotMerge()
    {
        // Ambiguous: two different plain-text candidates, no content-desc anywhere -- not the N5 shape
        // (which needs exactly one of each), so this stays unmerged too.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,105]">
                <node index="0" text="Pay" resource-id="" class="android.widget.TextView" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][105,105]" />
                <node index="1" text="Now" resource-id="" class="android.widget.TextView" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[105,0][210,105]" />
              </node>
            </hierarchy>
            """;

        var button = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Null(button.Label);
        Assert.Null(button.VisibleText);
    }

    [Fact]
    public void Parse_ClickableNodeWithOneContentDescAndTwoTextDescendants_DoesNotMerge()
    {
        // Ambiguous: a content-desc candidate plus two distinct text candidates -- not the N5 shape either
        // (needs exactly one of each), so nothing is merged.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][315,105]">
                <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="Submit" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][105,105]" />
                <node index="1" text="Pay" resource-id="" class="android.widget.TextView" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[105,0][210,105]" />
                <node index="2" text="Now" resource-id="" class="android.widget.TextView" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[210,0][315,105]" />
              </node>
            </hierarchy>
            """;

        var button = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Null(button.Label);
        Assert.Null(button.VisibleText);
    }

    [Fact]
    public void Parse_ClickableNodeWithAnIndependentlyFocusableDescendant_DoesNotMerge()
    {
        // Conservative: a named descendant exists, but another descendant is itself focusable/clickable --
        // a screen reader is expected to stop on that one separately, so the outer node is not assumed to
        // be a single stop.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,105]">
                <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="More info" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][105,105]" />
                <node index="1" text="" resource-id="" class="android.widget.Button" package="com.example.app" content-desc="Nested action" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[105,0][210,105]" />
              </node>
            </hierarchy>
            """;

        var button = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Null(button.Label);
    }

    [Fact]
    public void Parse_ClickableNodeWithNoNamedDescendants_StaysUnlabeled()
    {
        // The failing case: nothing anywhere in the subtree carries a name -- a genuinely unlabeled icon
        // button, which should still be reported as missing a name.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.widget.Button" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][105,105]" />
            </hierarchy>
            """;

        var button = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Null(button.Label);
    }

    [Fact]
    public void Parse_NamedDescendantHiddenFromAccessibility_IsNotUsedAsTheMergedName()
    {
        // Hidden-from-AT edge case: the only named descendant is absent from the compressed dump (not
        // important for accessibility), so a screen reader is not expected to reach it -- it must not
        // supply a name to the outer node either.
        const string full = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,105]">
                <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="More info" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][105,105]" />
              </node>
            </hierarchy>
            """;
        const string compressed = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.view.View" content-desc="" bounds="[0,0][210,105]" />
            </hierarchy>
            """;

        var button = UiAutomatorParser.Parse(full, compressed, density: 160).Children[0];

        Assert.Null(button.Label);
        Assert.False(button.Children[0].IsAccessible);
    }

    [Fact]
    public void Parse_NamedDescendantWithZeroSizeBounds_IsNotUsedAsTheMergedName()
    {
        // Zero-size edge case: a stale or collapsed descendant with leftover content-desc but no rendered
        // area is not something a person can reach, so it must not supply the outer node's name.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,105]">
                <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="More info" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][0,0]" />
              </node>
            </hierarchy>
            """;

        var button = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Null(button.Label);
    }

    [Fact]
    public void Parse_DisabledClickableNodeWithOneNamedDescendant_StillMerges()
    {
        // A disabled control is still expected to be announced by a screen reader (with "disabled"), so
        // the merge is not conditioned on IsEnabled.
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="" checkable="false" checked="false" clickable="true" enabled="false" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,105]">
                <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="More info" checkable="false" checked="false" clickable="false" enabled="false" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][105,105]" />
              </node>
            </hierarchy>
            """;

        var button = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Equal("More info", button.Label);
        Assert.False(button.IsEnabled);
    }

    [Fact]
    public void Parse_ClickableNodeWithItsOwnLabelOrText_DoesNotMergeADescendantsName()
    {
        // Not applicable case: the outer node already has its own content-desc, so no merge is attempted
        // even though a descendant also carries a name (nothing to fix here).
        const string xml = """
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="Info button" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][210,105]">
                <node index="0" text="" resource-id="" class="android.view.View" package="com.example.app" content-desc="More info" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][105,105]" />
              </node>
            </hierarchy>
            """;

        var button = UiAutomatorParser.Parse(xml, compressedXml: null, density: 160).Children[0];

        Assert.Equal("Info button", button.Label);
        Assert.Equal("More info", button.Children[0].Label);
    }

    [Fact]
    public void KeyPathsByPackage_MapsEachNodesIdentityToItsChildIndexPath()
    {
        var keyPaths = UiAutomatorParser.KeyPathsByPackage(FullXml, "com.example.app");

        // Same path scheme AccessibilityNode.DescendantsAndSelfWithPath assigns walking the tree Parse
        // builds from the same XML/package: "0" is the top-level app node, "0/1" its second child, etc.
        Assert.Equal("0/0", keyPaths["android.widget.Button|[0,0][210,105]|com.example.app:id/btnSubmit|Submit|"]);
        Assert.Equal("0/1", keyPaths["android.widget.EditText|[0,105][420,210]|||"]);
        Assert.Equal("0/2", keyPaths["android.widget.ImageView|[0,210][105,315]|com.example.app:id/imgProfile||Profile"]);
    }

    [Fact]
    public void KeyPathsByPackage_ExcludesNodesFromAnotherPackage()
    {
        var keyPaths = UiAutomatorParser.KeyPathsByPackage(FullXml, "com.example.app");

        // The "Overlay" TextView nested under the Submit button is package="com.other.app", filtered out
        // the same way Convert filters children by package at every level -- it must not show up at all,
        // not even under a path that skips over it.
        Assert.DoesNotContain(keyPaths, kv => kv.Key.Contains("Overlay"));
    }
}
