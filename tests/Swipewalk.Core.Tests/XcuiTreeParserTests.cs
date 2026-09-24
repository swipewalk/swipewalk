using System.Text.Json;
using Swipewalk.Collectors.Ios;

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
