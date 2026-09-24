namespace Swipewalk.Core.Model;

/// <summary>
/// An issue reported by a platform's own accessibility engine (Apple's performAccessibilityAudit,
/// Google ATF, axe-windows). Rules map these to findings so WCAG mapping stays in one place.
/// </summary>
/// <param name="Engine">Engine display name, e.g. "Apple accessibility audit".</param>
/// <param name="Type">The engine's issue type, e.g. "contrast", "dynamicType".</param>
/// <param name="NodePath">Path of the matching tree node, when the issue's element was found in the tree.</param>
public sealed record EngineIssue(string Engine, string Type, string Description, string? NodePath, string? Label, Bounds Bounds);
