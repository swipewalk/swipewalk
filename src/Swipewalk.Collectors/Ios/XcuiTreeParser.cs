using System.Text.Json;
using Swipewalk.Core.Model;

namespace Swipewalk.Collectors.Ios;

/// <summary>
/// Translates the harness's tree.json (an XCUIElementSnapshot dump plus Apple audit issues) into the
/// platform-neutral model. Frames are in points, which are the tree units on iOS.
/// </summary>
public static class XcuiTreeParser
{
    public const string AppleAudit = "Apple accessibility audit";

    /// <summary>
    /// The default <see cref="JsonDocumentOptions.MaxDepth"/> (64) is too shallow for a real accessibility tree:
    /// a WKWebView mirrors its whole DOM into the tree, and a map or other rich web content can nest well past
    /// 64 levels (seen on WeatherTwentyOne's Map tab, an embedded map WKWebView, 2026-09-23 -- an unhandled
    /// JsonReaderException reached the person as a raw stack trace). 2048 comfortably covers real WKWebView DOM
    /// depth while still bounding a runaway or malformed document.
    /// </summary>
    internal static readonly JsonDocumentOptions TreeJsonOptions = new() { MaxDepth = 2048 };

    /// <summary>
    /// Shared message for a tree.json that couldn't be read -- by <see cref="Parse"/> here, or by
    /// <see cref="IosCollector.ReadForegroundFlags"/>, which reads the same file: both should fail the same
    /// way, not drift apart. No cause is named (a truncated or corrupted capture is at least as likely as
    /// content too deep for <see cref="TreeJsonOptions"/>'s limit); ex.Message (raw System.Text.Json wording:
    /// depth, byte offsets) is appended separately as a detail, not folded into the sentence itself.
    /// </summary>
    internal const string UnreadableTreeMessage =
        "Could not read this screen's accessibility tree: the captured data was incomplete or too deeply nested " +
        "to process. Try scanning this screen again; if it keeps happening on this screen, please report it on " +
        "the Swipewalk GitHub repository.";

    /// <param name="StatusBar">Frame of the system status bar in points, when the harness reported it.</param>
    public sealed record Result(AccessibilityNode Root, double Scale, IReadOnlyList<EngineIssue> EngineIssues, Bounds? StatusBar = null);

    /// <summary>
    /// Parses the harness's tree.json. A <see cref="JsonException"/> (an unreadable, truncated or -- even past
    /// <see cref="TreeJsonOptions"/>'s raised limit -- too deeply nested document) or a
    /// <see cref="KeyNotFoundException"/> (valid JSON missing an expected property, e.g. "tree" or "children")
    /// is turned into an <see cref="InvalidOperationException"/> with <see cref="UnreadableTreeMessage"/>:
    /// callers throughout the iOS collector already treat that type as an expected, reportable failure (scan
    /// prints "Scan failed: {message}" instead of a raw stack trace), where either unwrapped exception type
    /// would not have been caught.
    /// </summary>
    public static Result Parse(string json)
    {
        try
        {
            return ParseCore(json);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            throw new InvalidOperationException($"{UnreadableTreeMessage} (details: {ex.Message})", ex);
        }
    }

    private static Result ParseCore(string json)
    {
        using var doc = JsonDocument.Parse(json, TreeJsonOptions);
        var rootElement = doc.RootElement;
        var root = Convert(rootElement.GetProperty("tree"));
        var scale = rootElement.TryGetProperty("scale", out var s) ? s.GetDouble() : 1.0;

        var issues = new List<EngineIssue>();
        if (rootElement.TryGetProperty("auditIssues", out var auditIssues))
        {
            foreach (var issue in auditIssues.EnumerateArray())
            {
                var bounds = issue.TryGetProperty("frame", out var frame) ? ParseFrame(frame) : default;
                issues.Add(new EngineIssue(
                    AppleAudit,
                    issue.GetProperty("type").GetString() ?? "unknown",
                    issue.GetProperty("compactDescription").GetString() ?? "",
                    FindPath(root, bounds),
                    NullIfEmpty(issue.TryGetProperty("label", out var l) ? l.GetString() : null),
                    bounds));
            }
        }

        Bounds? statusBar = rootElement.TryGetProperty("statusBar", out var sb) ? ParseFrame(sb) : EstimateStatusBar(root);
        return new Result(root, scale, issues, statusBar);
    }

    /// <summary>
    /// Converts one element and its whole subtree. Walked iteratively (post-order, with an explicit stack)
    /// rather than by recursing into <c>Convert</c> per child: a WKWebView's mirrored DOM can nest deep enough
    /// (see <see cref="TreeJsonOptions"/>) that a recursive descent risks an uncatchable StackOverflowException,
    /// which -- unlike the JsonReaderException raising MaxDepth above fixes -- cannot be caught and turned into
    /// a clear message.
    /// </summary>
    private static AccessibilityNode Convert(JsonElement root)
    {
        var frames = new Stack<(JsonElement Element, List<AccessibilityNode> Built, JsonElement.ArrayEnumerator Children)>();
        frames.Push((root, [], root.GetProperty("children").EnumerateArray()));
        AccessibilityNode? result = null;
        while (frames.Count > 0)
        {
            // Pop and, if there's another child, push the frame back with its enumerator advanced (a struct,
            // so the advance must be written back explicitly) before pushing the child's own frame.
            var (element, built, children) = frames.Pop();
            if (children.MoveNext())
            {
                var child = children.Current;
                frames.Push((element, built, children));
                frames.Push((child, [], child.GetProperty("children").EnumerateArray()));
                continue;
            }
            var node = BuildNode(element, built);
            if (frames.Count > 0)
                frames.Peek().Built.Add(node);
            else
                result = node;
        }
        return result!;
    }

    private static AccessibilityNode BuildNode(JsonElement element, List<AccessibilityNode> children)
    {
        var type = element.GetProperty("type").GetString() ?? "other";
        var label = NullIfEmpty(element.GetProperty("label").GetString());
        var role = MapRole(type);
        var interactive = role is "button" or "key" or "link" or "textfield" or "switch" or "slider" or "radio" or "checkbox";

        // UILabel's accessibility label is its text; for controls, visible text comes from child labels.
        string? visibleText = role == "text" ? label : null;
        if (interactive && role != "textfield")
        {
            var childText = string.Join(" ", children.Where(c => c.Role == "text").Select(c => c.VisibleText));
            visibleText = NullIfEmpty(childText);
        }

        return new AccessibilityNode
        {
            Role = role,
            NativeType = type,
            Label = role == "text" ? null : label,
            VisibleText = visibleText,
            Value = element.TryGetProperty("value", out var v) ? NullIfEmpty(v.GetString()) : null,
            Hint = element.TryGetProperty("placeholder", out var p) ? NullIfEmpty(p.GetString()) : null,
            AutomationId = NullIfEmpty(element.GetProperty("identifier").GetString()),
            Bounds = ParseFrame(element.GetProperty("frame")),
            IsInteractive = interactive,
            IsFocusable = false,
            IsScrollable = type is "scrollView" or "table" or "collectionView",
            IsEnabled = element.GetProperty("enabled").GetBoolean(),
            // Approximation of isAccessibilityElement, which XCUITest does not expose: controls, text and
            // labelled images/containers are reachable by VoiceOver; unlabelled images and containers are not.
            IsAccessible = type != "application" && (interactive || role == "text" || label is not null),
            Children = children,
        };
    }

    /// <summary>
    /// When SpringBoard doesn't report the status bar (seen on iOS 26), everything above the app's navigation
    /// bar is status bar: use that band. Without a navigation bar there is no estimate.
    /// </summary>
    private static Bounds? EstimateStatusBar(AccessibilityNode root)
    {
        var navigationBar = root.DescendantsAndSelf()
            .FirstOrDefault(n => n.NativeType == "navigationBar" && n.Bounds.Y is > 0 and < 120);
        return navigationBar is null ? null : new Bounds(0, 0, root.Bounds.Width, navigationBar.Bounds.Y);
    }

    internal static string MapRole(string type) => type switch
    {
        "button" or "menuItem" or "tab" or "stepper" => "button",
        "key" or "other:20" => "key", // keyboard-style keys (e.g. Calculator); raw type 20 from older harness output
        "radioButton" => "radio",
        "checkBox" => "checkbox",
        "link" => "link",
        "staticText" => "text",
        "image" => "image",
        "textField" or "secureTextField" or "searchField" or "textView" => "textfield",
        "switch" or "toggle" => "switch",
        "slider" => "slider",
        "progressIndicator" or "activityIndicator" => "progressbar",
        "webView" => "webview",
        _ => "group",
    };

    private static Bounds ParseFrame(JsonElement frame)
    {
        var v = frame.EnumerateArray().Select(e => e.GetDouble()).ToArray();
        return v.Length == 4 ? new Bounds(v[0], v[1], v[2], v[3]) : default;
    }

    /// <summary>The deepest node whose frame matches the issue's frame.</summary>
    private static string? FindPath(AccessibilityNode root, Bounds bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return null;
        return root.DescendantsAndSelfWithPath()
            .Where(e => Near(e.Node.Bounds, bounds))
            .Select(e => e.Path)
            .LastOrDefault();
    }

    private static bool Near(Bounds a, Bounds b) =>
        Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5
        && Math.Abs(a.Width - b.Width) < 0.5 && Math.Abs(a.Height - b.Height) < 0.5;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
