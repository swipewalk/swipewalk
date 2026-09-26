namespace Swipewalk.Core.Rules;

/// <summary>
/// Extra metadata about Swipewalk's own rules (not the wrapped engines -- see
/// <see cref="EngineIssueRule.Catalog"/> and <see cref="AtfIssueRule.Catalog"/> for those), used only to
/// render docs/checks.md (see Swipewalk.Core.Rules.ChecksMarkdown). This reuses
/// <see cref="DefaultRules.Coverage"/> for what each rule checks and which WCAG criteria it maps to, adding
/// only the two things that catalog doesn't already record: which finding kinds a rule can report (read from
/// each rule's own Evaluate method; a rule that reports more than one kind lists when each applies) and which
/// platforms it runs on. Every rule here reads the shared AccessibilityNode tree, so all of them could run on
/// either collector in principle; a few restrict themselves in their own Evaluate method instead, for reasons
/// specific to that rule (text-resize-navigation to Android; icon-contrast and offscreen-unreachable to iOS --
/// see each rule's own remarks for why).
/// </summary>
public static class RuleCatalog
{
    /// <summary>One of Swipewalk's own rules, for docs/checks.md. <see cref="RuleId"/> matches a
    /// <see cref="DefaultRules.Coverage"/> entry's <c>RuleId</c>, which supplies the "what it checks" text
    /// and the WCAG criteria; <see cref="Kinds"/> and <see cref="Platforms"/> are this catalog's own.</summary>
    public sealed record Entry(string RuleId, string Kinds, string Platforms);

    private const string BothPlatforms = "Android, iOS";

    public static IReadOnlyList<Entry> OwnRules { get; } =
    [
        new("missing-name",
            "WCAG issue (an interactive control with no accessible name); needs review (a text field whose name is ambiguous, or an image that may be decorative)",
            BothPlatforms),
        new("target-size",
            "WCAG issue (below 24×24 with no spacing exception); needs review (below 24×24 but nested in another target, a near-miss risk); platform advisory (not reported as a WCAG issue -- e.g. at least 24×24, or below 24×24 but meeting the spacing exception -- yet below the platform's own 44 pt / 48 dp guideline)",
            BothPlatforms),
        new("identifier-name", "Needs review", BothPlatforms),
        new("label-in-name", "WCAG issue", BothPlatforms),
        new("text-contrast",
            "WCAG issue (below 3:1); needs review (3:1-4.5:1, since text size can't always be read from the screenshot; or the screenshot was blocked)",
            BothPlatforms),
        new("text-resize",
            "Needs review (text that did not grow, or overlap seen at or below 200%); platform advisory (overlap seen only above 200%, iOS AX3 only)",
            BothPlatforms),
        new("text-resize-live", "Platform advisory", BothPlatforms),
        new("text-resize-navigation", "Platform advisory", "Android"),
        new("page-titled", "Needs review (no pane title found anywhere in the tree)", "Android (needs the instrumentation harness)"),
        new("input-purpose", "Needs review", BothPlatforms),
        new("icon-contrast", "Needs review (below 3:1)", "iOS"),
        new("large-text-lost-content",
            "Needs review (missing at the larger size with no scrollable container anywhere on the screen to reach it); platform advisory (loss seen only beyond 200%, iOS AX3 only)",
            BothPlatforms),
        new("offscreen-unreachable",
            "Needs review (wholly or mostly off-screen at normal text size with no scrollable ancestor, on a screen at least as large as WCAG 1.4.10's own 320×256 reference size); platform advisory (same finding on a smaller screen)",
            "iOS"),
        new("orientation-restricted",
            "Needs review (the screen still looked the same shape after the device was rotated -- always for review, never a confirmed failure, since a single orientation can be essential to a screen)",
            "Android; iOS (Simulator only)"),
        new("screen-reader-capture", "Needs review (every difference from the predicted transcript is reported for a person to check, never as a confirmed WCAG failure by itself)",
            "Android (needs --screen-reader and the instrumentation harness; TalkBack)"),
        new("screen-reader-label-in-name", "Needs review (a real screen-reader capture is evidence of the accessible name a speech-input user relies on, not proof of it)",
            "Android (needs --screen-reader and the instrumentation harness; TalkBack)"),
    ];
}
