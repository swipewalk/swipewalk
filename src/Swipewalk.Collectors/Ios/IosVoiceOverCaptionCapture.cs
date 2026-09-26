using Swipewalk.Core.Model;
using Swipewalk.Core.ScreenReader;

namespace Swipewalk.Collectors.Ios;

/// <summary>
/// Turns a <see cref="VoiceOverCaptionCaptureRawResult"/> (what a person's real VoiceOver session captured, in
/// the order it was heard) into a <see cref="ScreenReaderCapture"/> matched to <paramref name="snapshot"/>'s
/// tree nodes -- the same shape <see cref="ScreenReaderCaptureComparer"/> and
/// <see cref="Rules.ScreenReaderCaptureRule"/> already consume for Android's TalkBack capture and iOS's
/// Accessibility Inspector walk. Pure and side-effect free: everything device/process-related (polling
/// screenshots, OCR) is <see cref="IosVoiceOverCaptionWalk.RunLiveAsync"/>'s job, so this can be tested with a
/// synthetic tree and a hand-built raw result, no Mac, device or VoiceOver session needed.
/// </summary>
public static class IosVoiceOverCaptionCapture
{
    /// <summary>A capture that made no attempt at all, or whose session failed outright -- see
    /// <see cref="IosCollector.RunVoiceOverCaptionCaptureAsync"/>.</summary>
    public static ScreenReaderCapture Skipped(string reason) =>
        new(ScreenReaderSource.VoiceOverCaptions, ToolVersion: "unknown", DateTimeOffset.UtcNow, [], Complete: false, NotCompleteReason: reason);

    /// <summary>
    /// Matches each of <paramref name="raw"/>'s captions, in the order they were heard, to a node in
    /// <paramref name="snapshot"/>'s tree, in two passes per item:
    /// <list type="number">
    /// <item>Its <see cref="VoiceOverCaptionItem.FocusRect"/> (a best-effort, unverified-on-hardware detection
    /// of VoiceOver's own cursor -- see harness/mac-voiceover-captions/VoiceOverCaptionCapture.swift), converted
    /// from screenshot-pixel space to tree-unit space via <see cref="ScreenSnapshot.PixelScale"/>, against every
    /// candidate's <see cref="AccessibilityNode.Bounds"/> by area overlap (IoU): the best-overlapping candidate
    /// above a low floor is <see cref="MatchConfidence.Likely"/> for a strong overlap, or
    /// <see cref="MatchConfidence.Weak"/> for a weak one -- never <see cref="MatchConfidence.Exact"/>, since the
    /// rect itself comes from an unverified heuristic, not a confirmed geometry reading.</item>
    /// <item>Falling back to the caption's own text containing exactly one candidate's predicted accessible
    /// name (VoiceOver's caption often adds its own role word after the name, e.g. "Submit, Button", so this
    /// checks containment, not equality) -- <see cref="MatchConfidence.Weak"/>, the same ceiling Android's
    /// TalkBack capture uses for a text-only match.</item>
    /// </list>
    /// An item matching neither is left unmatched (<see cref="MatchConfidence.None"/>) -- expected and normal
    /// for this source (see <see cref="ScreenReaderCaptureItem"/>'s remarks): a person can navigate, scroll or
    /// trigger a hint in ways a single tree snapshot doesn't represent.
    /// </summary>
    public static ScreenReaderCapture Build(ScreenSnapshot snapshot, VoiceOverCaptionCaptureRawResult raw, DateTimeOffset? sessionStartedAt = null)
    {
        var startedAt = sessionStartedAt ?? DateTimeOffset.UtcNow;
        if (!raw.Ok)
            return Skipped(raw.Error ?? "the VoiceOver-captions capture failed");
        var toolVersion = raw.ToolVersion ?? "unknown";
        if (raw.Items.Count == 0)
            return new ScreenReaderCapture(ScreenReaderSource.VoiceOverCaptions, toolVersion, startedAt, [], raw.Complete,
                raw.NotCompleteReason ?? (raw.Complete ? null : "no VoiceOver captions were captured"));

        var predicted = ScreenReaderPredictor.Predict(snapshot);
        var nodesByPath = snapshot.Root.DescendantsAndSelfWithPath().ToDictionary(t => t.Path, t => t.Node);
        var candidates = predicted
            .Select(a =>
            {
                var node = nodesByPath.GetValueOrDefault(a.NodePath);
                return new Candidate(a.NodePath, node, node is null ? null : ScreenReaderPredictor.AccessibleName(node));
            })
            .Where(c => c.Node is not null)
            .ToList();

        var items = raw.Items.Select(item =>
        {
            var match = FindMatch(snapshot.PixelScale, candidates, item);
            return new ScreenReaderCaptureItem(
                Order: item.Order, SpokenText: item.SpokenText, Label: null, Value: null, Traits: null, Hint: null,
                Identifier: null, ClassName: null, Timestamp: startedAt.AddSeconds(item.ElapsedSeconds),
                MatchedNodePath: match?.NodePath, MatchConfidence: match?.Confidence ?? MatchConfidence.None);
        }).ToList();

        return new ScreenReaderCapture(ScreenReaderSource.VoiceOverCaptions, toolVersion, startedAt, items, raw.Complete, raw.NotCompleteReason);
    }

    private sealed record Candidate(string NodePath, AccessibilityNode? Node, string? Name);

    /// <summary>Below this overlap fraction (intersection over union of the two rectangles), a rect "match" is
    /// no more trustworthy than noise -- the focus-rect detector is a coarse, unverified heuristic (see
    /// harness/mac-voiceover-captions/VoiceOverCaptionCapture.swift), so a weak overlap is discarded rather than
    /// reported as even a Weak match.</summary>
    private const double MinOverlapToMatch = 0.15;
    private const double StrongOverlap = 0.5;

    private static (string NodePath, MatchConfidence Confidence)? FindMatch(
        double pixelScale, IReadOnlyList<Candidate> candidates, VoiceOverCaptionItem item)
    {
        if (item.FocusRect is { } rect && pixelScale > 0)
        {
            var treeRect = new Bounds(rect.X / pixelScale, rect.Y / pixelScale, rect.W / pixelScale, rect.H / pixelScale);
            var best = candidates
                .Select(c => (c, overlap: OverlapRatio(treeRect, c.Node!.Bounds)))
                .Where(t => t.overlap >= MinOverlapToMatch)
                .OrderByDescending(t => t.overlap)
                .Select(t => ((string NodePath, MatchConfidence Confidence)?)(t.c.NodePath, t.overlap >= StrongOverlap ? MatchConfidence.Likely : MatchConfidence.Weak))
                .FirstOrDefault();
            if (best is not null)
                return best;
        }

        if (!string.IsNullOrWhiteSpace(item.SpokenText))
        {
            var byText = candidates.Where(c => !string.IsNullOrWhiteSpace(c.Name) && ContainsName(item.SpokenText, c.Name!)).ToList();
            if (byText.Count == 1)
                return (byText[0].NodePath, MatchConfidence.Weak);
        }

        return null;
    }

    private static bool ContainsName(string spokenText, string name) =>
        spokenText.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase);

    private static double OverlapRatio(Bounds a, Bounds b)
    {
        var x1 = Math.Max(a.X, b.X);
        var y1 = Math.Max(a.Y, b.Y);
        var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);
        var iw = Math.Max(0, x2 - x1);
        var ih = Math.Max(0, y2 - y1);
        var intersection = iw * ih;
        if (intersection <= 0)
            return 0;
        var union = a.Width * a.Height + b.Width * b.Height - intersection;
        return union > 0 ? intersection / union : 0;
    }
}
