using System.Text.Json;
using Swipewalk.Collectors.Ios;
using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;

namespace Swipewalk.Core.Tests;

public class XcuiTreeParserTests
{
    [Fact]
    public void Parse_ShallowTree_RoundTripsBasicFields()
    {
        const string json = """
            {
              "scale": 3,
              "tree": {
                "type": "application", "identifier": "", "label": "", "title": "", "enabled": true,
                "frame": [0, 0, 390, 844],
                "children": [
                  {
                    "type": "button", "identifier": "submit", "label": "Submit", "title": "", "enabled": true,
                    "frame": [10, 10, 100, 44],
                    "children": [
                      { "type": "staticText", "identifier": "", "label": "Submit", "title": "", "enabled": true, "frame": [10, 10, 100, 44], "children": [] }
                    ]
                  }
                ]
              },
              "auditIssues": []
            }
            """;

        var result = XcuiTreeParser.Parse(json);

        Assert.Equal(3, result.Scale);
        Assert.Single(result.Root.Children);
        var button = result.Root.Children[0];
        Assert.Equal("button", button.Role);
        Assert.Equal("Submit", button.VisibleText);
        Assert.True(button.IsInteractive);
    }

    /// <summary>
    /// A WKWebView mirrors its whole DOM into the accessibility tree, and rich embedded web content (e.g. a
    /// map) nests deep enough to exceed JsonDocument's default 64-level MaxDepth -- reproduced 2026-09-23 on
    /// WeatherTwentyOne's Map tab, where this reached the person as an unhandled
    /// System.Text.Json.JsonReaderException with a raw stack trace instead of a clear message. This builds a
    /// synthetic tree well past that default (200 levels, no real app data) and checks it parses cleanly and
    /// converts without a stack overflow (Convert walks the tree iteratively for exactly this reason).
    /// </summary>
    [Fact]
    public void Parse_TreeDeeperThanDefaultJsonMaxDepth_ParsesWithoutThrowing()
    {
        const int depth = 200;
        var json = BuildDeepTreeJson(depth);

        var result = XcuiTreeParser.Parse(json);

        var node = result.Root;
        var levels = 0;
        while (node.Children.Count > 0)
        {
            node = node.Children[0];
            levels++;
        }
        Assert.Equal(depth, levels);
    }

    /// <summary>
    /// A document deeper than <see cref="XcuiTreeParser"/>'s raised MaxDepth (a pathological or corrupted
    /// capture) must still fail as a clear, actionable error -- never an unhandled JsonReaderException with a
    /// raw stack trace reaching the person (see CaptureAsync/MaskStatusBar, which scan mode's top-level error
    /// handling reports as "Scan failed: {message}" only for InvalidOperationException).
    /// </summary>
    [Fact]
    public void Parse_TreeDeeperThanRaisedMaxDepth_ThrowsInvalidOperationExceptionWithClearMessage()
    {
        var json = BuildDeepTreeJson(5000);

        var ex = Assert.Throws<InvalidOperationException>(() => XcuiTreeParser.Parse(json));

        Assert.StartsWith(XcuiTreeParser.UnreadableTreeMessage, ex.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<JsonException>(ex.InnerException);
    }

    /// <summary>
    /// Valid JSON that is nonetheless missing an expected property (e.g. a truncated or corrupted capture)
    /// must fail the same clear way as a too-deep tree, not with a raw KeyNotFoundException.
    /// </summary>
    [Fact]
    public void Parse_TreeMissingAnExpectedProperty_ThrowsInvalidOperationExceptionWithClearMessage()
    {
        const string json = """{ "scale": 1, "tree": { "type": "application", "children": [] }, "auditIssues": [] }""";

        var ex = Assert.Throws<InvalidOperationException>(() => XcuiTreeParser.Parse(json));

        Assert.StartsWith(XcuiTreeParser.UnreadableTreeMessage, ex.Message, StringComparison.Ordinal);
        Assert.IsType<KeyNotFoundException>(ex.InnerException);
    }

    /// <summary>
    /// Seen on a real physical-iPhone capture (2026-09-25, samples/NativeiOS): a UIToolbar's own container
    /// and a scroll view's built-in scroll-position indicator each get a real XCUITest accessibility label
    /// ("Toolbar", "Vertical scroll bar, 1 page") -- which, before this fix, made
    /// <see cref="ScreenReaderPredictor"/> treat them as ordinary named, reachable stops. Xcode's
    /// Accessibility Inspector's own Next/Previous Item walk never stopped on either one in that capture
    /// (VoiceOver itself was not run, and it may still reach a scroll bar's indicator by touch even though
    /// the Inspector's keyboard-style walk did not): this produced predicted stops with nothing for the
    /// capture to match, reported as false "was not reported by Xcode's Accessibility Inspector" findings.
    /// A sibling button must stay reachable -- this only affects the toolbar/scroll-bar containers themselves,
    /// not their neighbors or (for the toolbar) the real buttons it contains.
    /// </summary>
    [Fact]
    public void Parse_ToolbarAndScrollBar_AreNotAccessible()
    {
        const string json = """
            {
              "scale": 3,
              "tree": {
                "type": "application", "identifier": "", "label": "", "title": "", "enabled": true,
                "frame": [0, 0, 390, 844],
                "children": [
                  { "type": "toolbar", "identifier": "", "label": "Toolbar", "title": "", "enabled": true,
                    "frame": [0, 0, 390, 44], "children": [
                      { "type": "button", "identifier": "", "label": "Search", "title": "", "enabled": true, "frame": [10, 10, 44, 24], "children": [] }
                    ] },
                  { "type": "scrollBar", "identifier": "", "label": "Vertical scroll bar, 1 page", "title": "", "enabled": true, "frame": [380, 0, 10, 800], "children": [] },
                  { "type": "button", "identifier": "", "label": "Submit", "title": "", "enabled": true, "frame": [10, 100, 100, 44], "children": [] }
                ]
              },
              "auditIssues": []
            }
            """;

        var result = XcuiTreeParser.Parse(json);

        var toolbar = result.Root.Children[0];
        var toolbarButton = toolbar.Children[0];
        var scrollBar = result.Root.Children[1];
        var submit = result.Root.Children[2];
        Assert.False(toolbar.IsAccessible);
        Assert.False(scrollBar.IsAccessible);
        Assert.True(toolbarButton.IsAccessible); // a real button inside the toolbar is still reachable
        Assert.True(submit.IsAccessible);

        // End to end: neither container should produce a predicted screen-reader stop at all.
        var snapshot = new ScreenSnapshot { Platform = Platform.iOS, ScreenName = "Test", Root = result.Root };
        var predicted = ScreenReaderPredictor.Predict(snapshot);
        Assert.DoesNotContain(predicted, a => a.Text.Contains("Toolbar", StringComparison.Ordinal));
        Assert.DoesNotContain(predicted, a => a.Text.Contains("scroll bar", StringComparison.Ordinal));
        Assert.Contains(predicted, a => a.Text.Contains("Search", StringComparison.Ordinal));
        Assert.Contains(predicted, a => a.Text.Contains("Submit", StringComparison.Ordinal));
    }

    private static string BuildDeepTreeJson(int depth)
    {
        var leaf = """{ "type": "staticText", "identifier": "", "label": "leaf", "title": "", "enabled": true, "frame": [0, 0, 10, 10], "children": [] }""";
        var tree = leaf;
        for (var i = 0; i < depth; i++)
        {
            tree = $$"""
                { "type": "other", "identifier": "", "label": "", "title": "", "enabled": true, "frame": [0, 0, 10, 10], "children": [{{tree}}] }
                """;
        }
        return $$"""{ "scale": 1, "tree": {{tree}}, "auditIssues": [] }""";
    }
}
