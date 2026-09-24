using System.Security.Cryptography;
using System.Text;
using Swipewalk.Core.Model;

namespace Swipewalk.Core.ScreenReader;

/// <summary>Recognizes screens across captures, for record mode.</summary>
public static class ScreenIdentity
{
    /// <summary>
    /// A fingerprint of what is on screen: the roles and names of reachable elements. Text typed into
    /// fields and element positions are ignored, so typing or scrolling slightly does not count as a new screen.
    /// </summary>
    public static string Signature(AccessibilityNode root)
    {
        var sb = new StringBuilder();
        foreach (var node in root.DescendantsAndSelf().Where(n => n.IsAccessible))
        {
            var name = node.Role == "textfield" ? node.Label : node.Label ?? node.VisibleText;
            sb.Append(node.Role).Append('|').Append(name).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16];
    }

    /// <summary>A name for the screen: the first reachable plain text in reading order, usually the title.</summary>
    public static string GuessTitle(ScreenSnapshot snapshot)
    {
        var texts = snapshot.Root.DescendantsAndSelf()
            .Where(n => n.IsAccessible && n.Role == "text" && !string.IsNullOrWhiteSpace(n.VisibleText))
            .ToList();
        // iOS reads by position, so prefer the topmost text there; elsewhere use tree order.
        var title = snapshot.Platform == Platform.iOS ? texts.MinBy(n => n.Bounds.Y) : texts.FirstOrDefault();
        return title?.VisibleText?.Trim() ?? "Untitled screen";
    }

    /// <summary>
    /// Whether two captures show the same screen even though layout changed (for example after enlarging
    /// text pushed content off screen): same title, and at least half of the first capture's names remain.
    /// </summary>
    public static bool IsSameScreen(ScreenSnapshot first, ScreenSnapshot second) => IsSameScreen(Fingerprint(first), second);

    /// <summary>Same test as <see cref="IsSameScreen(ScreenSnapshot,ScreenSnapshot)"/>, given an earlier
    /// screen's saved <see cref="ScreenFingerprint"/> instead of its full snapshot -- used by record mode to
    /// recognize a rescan of a screen captured in an earlier process (see Swipewalk.Engine.RecordState), where
    /// the original tree is no longer in memory.</summary>
    public static bool IsSameScreen(ScreenFingerprint first, ScreenSnapshot second)
    {
        if (first.Title != GuessTitle(second))
            return false;
        var remaining = Names(second.Root);
        return first.Names.Count == 0 || first.Names.Count(remaining.Contains) * 2 >= first.Names.Count;
    }

    /// <summary>A lightweight, JSON-friendly fingerprint of a screen -- its title and element names -- kept
    /// separately from the full node tree so <see cref="IsSameScreen(ScreenFingerprint,ScreenSnapshot)"/> can
    /// run again without the tree. Never shown in the report; it's bookkeeping for record mode's same-screen
    /// replacement, which only ever applies within the same run.</summary>
    public static ScreenFingerprint Fingerprint(ScreenSnapshot snapshot) => new(GuessTitle(snapshot), [.. Names(snapshot.Root)]);

    private static HashSet<string> Names(AccessibilityNode root) =>
        [.. root.DescendantsAndSelf()
            .Where(n => n.IsAccessible && n.Role != "textfield")
            .Select(n => n.Label ?? n.VisibleText)
            .OfType<string>()];
}

/// <summary>See <see cref="ScreenIdentity.Fingerprint"/>.</summary>
public sealed record ScreenFingerprint(string Title, IReadOnlyList<string> Names)
{
    /// <summary>A fingerprint that never matches a real screen (its title can't come from
    /// <see cref="ScreenIdentity.GuessTitle"/>) -- used to pad a continued run's identities when an earlier
    /// screen's real fingerprint wasn't saved (a run started before this feature existed, or a damaged
    /// record-state.json): those screens just won't be recognized as a same-screen rescan, rather than risking
    /// a false match.</summary>
    public static readonly ScreenFingerprint Unknown = new("\u0000 unknown screen (identity not saved)", []);
}
