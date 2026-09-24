using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Swipewalk.Core.Model;

namespace Swipewalk.Collectors.Android;

/// <summary>
/// Translates <c>uiautomator dump</c> XML into the platform-neutral model. The full dump gives the
/// view hierarchy; the <c>--compressed</c> dump lists only nodes important for accessibility, which
/// is what TalkBack traverses, and decides <see cref="AccessibilityNode.IsAccessible"/>.
/// </summary>
public static partial class UiAutomatorParser
{
    /// <param name="fullXml">Output of <c>uiautomator dump</c>.</param>
    /// <param name="compressedXml">Output of <c>uiautomator dump --compressed</c>, or null to treat every node as accessible.</param>
    /// <param name="density">Screen density in dpi (<c>wm density</c>); bounds are converted from px to dp.</param>
    /// <param name="package">App package to keep; null guesses one with <see cref="GuessPackage"/>. Also becomes
    /// the returned root node's <see cref="AccessibilityNode.NativeType"/> and <see cref="AccessibilityNode.AppId"/>,
    /// which Swipewalk.Collectors.Android.AndroidCollector.Load surfaces on the snapshot for run history to
    /// group by, when no <c>--package</c> was typed.</param>
    /// <param name="atfExtras">Extra per-node properties from the Android instrumentation harness (see
    /// Swipewalk.Collectors.Android.AndroidHarness), keyed by <see cref="Key"/> so they line up with the same
    /// node here -- this is the only place that identity is computed for both. Null when the harness didn't
    /// run (see AndroidCollector.Load), the same as an empty dictionary.</param>
    public static AccessibilityNode Parse(
        string fullXml, string? compressedXml, int density, string? package = null,
        IReadOnlyDictionary<string, AndroidHarness.NodeExtras>? atfExtras = null)
    {
        var hierarchy = XDocument.Parse(fullXml).Root ?? throw new FormatException("Empty uiautomator dump.");
        var top = hierarchy.Elements("node").ToList();
        package ??= GuessPackage(hierarchy);

        var accessibleKeys = compressedXml is null
            ? null
            : XDocument.Parse(compressedXml).Descendants("node").Select(Key).ToHashSet();

        var scale = density / 160.0;
        var children = top
            .Where(n => (string?)n.Attribute("package") == package)
            .Select(n => Convert(n, package, accessibleKeys, scale, atfExtras))
            .ToList();

        var screen = children.Count > 0 ? children[0].Bounds : default;
        return new AccessibilityNode
        {
            Role = "window",
            NativeType = package,
            Bounds = screen,
            IsAccessible = false,
            Children = children,
        };
    }

    private static AccessibilityNode Convert(
        XElement node, string? package, HashSet<string>? accessibleKeys, double scale,
        IReadOnlyDictionary<string, AndroidHarness.NodeExtras>? atfExtras)
    {
        var extras = atfExtras is not null && atfExtras.TryGetValue(Key(node), out var found) ? found : null;
        var nativeType = (string?)node.Attribute("class") ?? "";
        var clickable = Flag(node, "clickable") || Flag(node, "long-clickable") || Flag(node, "checkable");
        var role = MapRole(nativeType, clickable);
        // SeekBar is adjustable (screen readers swipe up/down to change it) even though it usually
        // reports clickable="false"; iOS's "slider" role is unconditionally interactive for the same
        // control, so treat it the same way here.
        var interactive = clickable || role == "slider";
        var resourceId = NullIfEmpty((string?)node.Attribute("resource-id"));
        var text = NullIfEmpty((string?)node.Attribute("text"));
        var label = NullIfEmpty((string?)node.Attribute("content-desc"));

        var children = node.Elements("node")
            .Where(c => (string?)c.Attribute("package") == package)
            .Select(c => Convert(c, package, accessibleKeys, scale, atfExtras))
            .ToList();

        // See KnownLimitations "android-compose-merged-name": Jetpack Compose's default merged semantics
        // (Modifier.semantics(mergeDescendants = true), set by Button/IconButton) can leave the clickable,
        // focusable node's own content-desc empty in the uiautomator dump, with the name uiautomator does
        // capture landing on a separate, non-clickable child instead. TryMergeSingleDescendantName gives
        // the outer node that name back, conservatively. missing-name already found these buttons named
        // through ScreenReaderPredictor.AccessibleName's own descendant-walk fallback, so this makes no
        // difference to it; identifier-name reads AccessibilityNode.Label directly rather than through that
        // fallback, so this is what fixes its role (previously the non-clickable child's role, e.g.
        // "group", instead of the button's own). target-size and label-in-name are unaffected either way
        // (see KnownLimitations for why).
        if (interactive && label is null && text is null && TryMergeSingleDescendantName(children) is { } merge)
        {
            label = merge.Name;
            children = merge.Children;
        }

        return new AccessibilityNode
        {
            Role = role,
            NativeType = nativeType,
            Label = label,
            // "text" is whatever is rendered in the control, so it stays VisibleText for every role
            // (contrast and resize checks read pixels regardless of editability). For an editable field
            // it is also the entered value: ScreenReaderPredictor.AccessibleName excludes it there, since
            // TalkBack announces it ("15, Edit box") without treating it as the field's name.
            VisibleText = text,
            Value = role == "textfield" ? text : null,
            // The harness's getHintText() (API 26) fills in the hint when uiautomator's own "hint"
            // attribute is empty: seen missing on an Android 13 phone even though the field has one (see
            // KnownLimitations "android-edittext-text"). Independent of IsShowingHintText below -- a hint
            // can exist (and give the field a name via ScreenReaderPredictor.AccessibleName's fallback)
            // whether or not it's the text currently displayed.
            Hint = NullIfEmpty((string?)node.Attribute("hint")) ?? extras?.HintText,
            AutomationId = resourceId is null ? null : ResourceIdName(resourceId),
            Bounds = ParseBounds((string?)node.Attribute("bounds"), scale),
            IsInteractive = interactive,
            IsFocusable = Flag(node, "focusable"),
            IsScrollable = Flag(node, "scrollable"),
            IsEnabled = Flag(node, "enabled"),
            IsAccessible = accessibleKeys?.Contains(Key(node)) ?? true,
            IsShowingHintText = extras?.IsShowingHintText,
            IsHeading = extras?.IsHeading,
            PaneTitle = extras?.PaneTitle,
            StateDescription = extras?.StateDescription,
            IsImportantForAccessibility = extras?.IsImportantForAccessibility,
            Children = children,
        };
    }

    /// <summary>
    /// Looks for exactly one descendant (any depth) carrying its own name, with no descendant anywhere in
    /// the subtree that is independently clickable, long-clickable, checkable or focusable -- i.e. nothing
    /// else under this node is its own screen-reader stop, so a screen reader is expected to have nothing
    /// else to read when it lands on the merged node (see AOSP's AccessibilityNodeInfoDumper/UiAutomator,
    /// which reads through the same accessibility API TalkBack uses, and Jetpack Compose's semantics
    /// merging: https://developer.android.com/reference/kotlin/androidx/compose/ui/semantics/package-summary).
    /// Deliberately conservative: several named descendants (for example an icon's content-desc alongside
    /// a separate visible-text child under one merged Button) are left alone -- see KnownLimitations
    /// "android-compose-merged-name" for why that case isn't handled here.
    /// </summary>
    private static (string Name, List<AccessibilityNode> Children)? TryMergeSingleDescendantName(List<AccessibilityNode> children)
    {
        var descendants = children.SelectMany(c => c.DescendantsAndSelf()).ToList();
        if (descendants.Any(d => d.IsAccessible && (d.IsInteractive || d.IsFocusable)))
            return null;

        // A zero-size descendant (collapsed, or a stale node uiautomator still dumps) is not something a
        // person actually sees or reaches, so its leftover content-desc/text is not treated as a real name.
        var named = descendants
            .Where(d => d.IsAccessible && d.Bounds.Width > 0 && d.Bounds.Height > 0
                        && (d.Label is not null || d.VisibleText is not null))
            .ToList();
        if (named.Count != 1)
            return null;

        var candidate = named[0];
        var name = candidate.Label ?? candidate.VisibleText!;

        // Only a content-desc source is cleared from the descendant: left in place, it would still carry
        // its own Label and be re-evaluated by rules that scan every node regardless of interactivity (for
        // example IdentifierNameRule), double-reporting the same name at two roles once this node's own
        // clickable role also has it. Visible text is left untouched -- other rules (for example
        // TextContrastRule) still need to check it as real on-screen text, and no rule flags a plain text
        // node just for carrying readable text, so there is nothing to double-report there.
        var newChildren = candidate.Label is not null
            ? children.Select(c => ClearLabel(c, candidate)).ToList()
            : children;

        return (name, newChildren);
    }

    private static AccessibilityNode ClearLabel(AccessibilityNode subtree, AccessibilityNode target) =>
        ReferenceEquals(subtree, target)
            ? subtree with { Label = null }
            : subtree with { Children = subtree.Children.Select(c => ClearLabel(c, target)).ToList() };

    /// <summary>
    /// Maps every node's identity (<see cref="Key"/>) to its child-index path in the tree <see cref="Parse"/>
    /// would build from the same <paramref name="fullXml"/> and <paramref name="package"/> -- same path
    /// scheme as <see cref="AccessibilityNode.DescendantsAndSelfWithPath"/> ("" for the synthetic root,
    /// "0", "0/1", ... below it). Used to match the Android instrumentation harness's screen-reader capture
    /// (harness/android's <c>TalkBackCollector</c>, keyed the same way as <see cref="Key"/>) to a node path
    /// without needing <see cref="Parse"/> to know about screen-reader capture at all -- see
    /// <see cref="Swipewalk.Collectors.Android.AndroidCollector.Load"/>. Walks the raw XML directly (not the
    /// built <see cref="AccessibilityNode"/> tree) so there's no floating-point bounds round-trip: dp bounds
    /// on the built tree would have to be converted back to raw pixels to compare against the harness's own
    /// raw-pixel keys.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> KeyPathsByPackage(string fullXml, string? package)
    {
        var hierarchy = XDocument.Parse(fullXml).Root ?? throw new FormatException("Empty uiautomator dump.");
        var top = hierarchy.Elements("node").Where(n => (string?)n.Attribute("package") == package).ToList();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < top.Count; i++)
            Walk(top[i], package, $"{i}", result);
        return result;
    }

    private static void Walk(XElement node, string? package, string path, Dictionary<string, string> result)
    {
        result[Key(node)] = path; // last one wins on a duplicate key, same as a duplicate would be ambiguous anyway
        var children = node.Elements("node").Where(c => (string?)c.Attribute("package") == package).ToList();
        for (var i = 0; i < children.Count; i++)
            Walk(children[i], package, $"{path}/{i}", result);
    }

    internal static string MapRole(string nativeType, bool interactive)
    {
        var name = nativeType[(nativeType.LastIndexOf('.') + 1)..];
        return name switch
        {
            "EditText" or "AutoCompleteTextView" or "MultiAutoCompleteTextView" => "textfield",
            "CheckBox" or "CheckedTextView" => "checkbox",
            "Switch" or "SwitchCompat" or "ToggleButton" => "switch",
            "RadioButton" => "radio",
            "SeekBar" => "slider",
            "ProgressBar" => "progressbar",
            "WebView" => "webview",
            _ when name.EndsWith("Button", StringComparison.Ordinal) => "button",
            "ImageView" => interactive ? "button" : "image",
            "TextView" => interactive ? "button" : "text",
            _ => interactive ? "button" : "group",
        };
    }

    /// <summary>
    /// Packages that are never the app under test even though uiautomator can dump their nodes alongside it --
    /// the on-screen keyboard, and the launcher when it briefly shows during a transition -- so they don't win
    /// the guess in <see cref="GuessPackage"/> just because their nodes happen to sort first.
    /// </summary>
    private static readonly HashSet<string> NonAppPackages = new(StringComparer.Ordinal)
    {
        "com.android.inputmethod.latin",
        "com.google.android.inputmethod.latin",
        "com.google.android.inputmethod.pinyin",
        "com.android.launcher3",
        "com.google.android.apps.nexuslauncher",
        "com.android.systemui",
    };

    /// <summary>
    /// Guesses the package under test from the dump when the caller didn't pass one: the most common package
    /// among interactive (clickable) nodes anywhere in the tree. This is more reliable than just taking the
    /// first top-level node's package, which can be the on-screen keyboard or another overlay if one happens
    /// to be dumped first; excluding <see cref="NonAppPackages"/> guards against the same overlays winning by
    /// node count instead. Falls back to the most common package overall when there are no interactive nodes
    /// (an unusual screen with nothing to tap), then to null when the dump has no package attributes at all.
    /// </summary>
    private static string? GuessPackage(XElement hierarchy)
    {
        var nodes = hierarchy.Descendants("node").ToList();
        var interactive = nodes.Where(n => Flag(n, "clickable") || Flag(n, "long-clickable") || Flag(n, "checkable")).ToList();
        return MostCommonPackage(interactive) ?? MostCommonPackage(nodes);
    }

    private static string? MostCommonPackage(IEnumerable<XElement> nodes) =>
        nodes.Select(n => (string?)n.Attribute("package"))
            .Where(p => !string.IsNullOrEmpty(p) && !NonAppPackages.Contains(p))
            .GroupBy(p => p, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

    private static Bounds ParseBounds(string? value, double scale)
    {
        var match = BoundsPattern().Match(value ?? "");
        if (!match.Success)
            return default;
        var v = Enumerable.Range(1, 4).Select(i => double.Parse(match.Groups[i].Value, CultureInfo.InvariantCulture)).ToArray();
        return new Bounds(v[0] / scale, v[1] / scale, (v[2] - v[0]) / scale, (v[3] - v[1]) / scale);
    }

    /// <summary>Identity of a node across the full and compressed dumps.</summary>
    private static string Key(XElement node) => string.Join('|',
        (string?)node.Attribute("class"), (string?)node.Attribute("bounds"), (string?)node.Attribute("resource-id"),
        (string?)node.Attribute("text"), (string?)node.Attribute("content-desc"));

    /// <summary>"com.app:id/btnPay" → "btnPay" (MAUI maps AutomationId to the resource id).</summary>
    private static string ResourceIdName(string resourceId)
    {
        var i = resourceId.IndexOf(":id/", StringComparison.Ordinal);
        return i >= 0 ? resourceId[(i + 4)..] : resourceId;
    }

    private static bool Flag(XElement node, string name) => (string?)node.Attribute(name) == "true";

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    [GeneratedRegex(@"\[(-?\d+),(-?\d+)\]\[(-?\d+),(-?\d+)\]")]
    private static partial Regex BoundsPattern();
}
