using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Standards;

/// <summary>
/// A law, regulation or standard that references WCAG. Used to say which findings are relevant to it.
/// Relevance means the finding's criterion is within the WCAG version and level the standard references;
/// it is not a legal conclusion.
/// </summary>
public sealed record Standard
{
    /// <summary>Stable id for --standard and JSON, e.g. "ada-title-ii".</summary>
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>Short label for chips, e.g. "ADA Title II".</summary>
    public required string ShortName { get; init; }

    public required string Jurisdiction { get; init; }

    /// <summary>Who and what it covers, in plain language.</summary>
    public required string AppliesTo { get; init; }

    public required WcagVersion WcagVersion { get; init; }
    public required WcagLevel Level { get; init; }

    /// <summary>Requirements the standard has beyond WCAG, which Swipewalk does not check.</summary>
    public string? BeyondWcag { get; init; }

    public required string Source { get; init; }

    /// <summary>When this entry was last checked against the source (yyyy-MM).</summary>
    public required string CheckedOn { get; init; }

    /// <summary>"WCAG 2.1 AA" style label.</summary>
    public string Basis => $"WCAG {WcagVersion.Display()} {Level}";

    /// <summary>
    /// Criteria the standard does not apply to non-web software such as mobile apps (for example Section 508
    /// E207.2). Swipewalk scans apps, so these are never relevant.
    /// </summary>
    public IReadOnlySet<string> NotAppliedToNonWebSoftware { get; init; } = new HashSet<string>();

    /// <summary>
    /// True when the criterion exists in the referenced WCAG version at or below the referenced level, and the
    /// standard applies it to non-web software.
    /// </summary>
    public bool Includes(WcagCriterion criterion) =>
        criterion.Since <= WcagVersion && criterion.Level <= Level && !NotAppliedToNonWebSoftware.Contains(criterion.Number);
}
