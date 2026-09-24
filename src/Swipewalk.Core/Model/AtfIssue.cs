namespace Swipewalk.Core.Model;

/// <summary>
/// One issue reported by Google's Accessibility Test Framework (ATF) 4.1.1, run over an unmodified app by
/// the Android instrumentation harness (harness/android). Mirrors <see cref="EngineIssue"/> (Apple's audit
/// on iOS), but kept as its own type/list on <see cref="ScreenSnapshot.AtfIssues"/> rather than merged into
/// <see cref="ScreenSnapshot.EngineIssues"/>: <see cref="Rules.AtfIssueRule"/> needs its own WCAG mapping
/// table (ATF's checks are not Apple's) and its own "did this even run" status (see
/// <see cref="ScreenSnapshot.AtfSkippedReason"/>), which <see cref="Rules.EngineIssueRule"/> has no
/// equivalent of.
/// </summary>
/// <param name="CheckName">ATF's check class name, e.g. "TextContrastCheck".</param>
/// <param name="NodePath">Path of the matching tree node, when the issue's element could be matched to one
/// (see Swipewalk.Collectors.Android.AndroidHarness); null when it couldn't.</param>
public sealed record AtfIssue(string CheckName, string Description, string? NodePath, string? Label, Bounds Bounds);
