namespace Swipewalk.Core.Standards;

/// <summary>
/// Why a jurisdiction (a US state, country or similar) is not a mapped <see cref="Standard"/>, or -- for
/// <see cref="ReferencedStandardDiffers"/> and <see cref="NotApplicableToApps"/> -- is a confirmed, binding
/// instrument that still isn't usable for per-finding "relevant to" matching. Never a guess: every entry here
/// records exactly what was and wasn't confirmed, so the gap is honest rather than silent.
/// </summary>
public enum JurisdictionStatusKind
{
    /// <summary>A law or policy may exist, but its primary source text wasn't read directly (a blocked fetch,
    /// an unreadable document, or a disputed/unresolved reading) -- so nothing is claimed about what it
    /// requires. Never "no requirement": this only means verification isn't finished.</summary>
    NotYetMapped,

    /// <summary>Checked and no jurisdiction-specific digital-accessibility statute, regulation or policy was
    /// found (the jurisdiction is still reachable through the federal/international standards in
    /// <see cref="KnownStandards.All"/>, e.g. ADA Title II for a US state).</summary>
    NoRequirementFound,

    /// <summary>A real, binding instrument was confirmed by reading its primary text, but it references
    /// something other than a WCAG 2.x version and level that Swipewalk can honestly map criterion-by-criterion
    /// (for example the pre-2017 Section 508 technical standards, or WCAG 1.0). Findings are not matched to it
    /// per finding; it is still listed for information.</summary>
    ReferencedStandardDiffers,

    /// <summary>A real, binding instrument was confirmed, but its own scope (read directly) does not reach the
    /// kind of software Swipewalk scans -- for example a regulation that covers only web content, with no
    /// mention of native software or mobile apps.</summary>
    NotApplicableToApps,
}

/// <summary>
/// One jurisdiction Swipewalk researched but did not ship as a mapped <see cref="Standard"/>, with the reason
/// recorded plainly. Listed in docs/standards.md and the report's known-limitations text so the gap reads as
/// "not yet mapped"/"no requirement found"/etc., never as silence. See <see cref="KnownJurisdictions"/>.
/// </summary>
/// <param name="Jurisdiction">Plain name, e.g. "Oregon" or "Australia".</param>
/// <param name="Kind">Why this isn't a mapped standard.</param>
/// <param name="Reason">Plain-language explanation, specific to what was and wasn't confirmed.</param>
/// <param name="LegalTier">The confirmed instrument's legal tier, when one exists and is known (null for
/// <see cref="JurisdictionStatusKind.NoRequirementFound"/>, and for <see cref="JurisdictionStatusKind.NotYetMapped"/>
/// entries where even the tier isn't confirmed).</param>
/// <param name="Source">Primary source URL, when one was found (even if not fully read/confirmed). Null when
/// nothing citable was found (a <see cref="JurisdictionStatusKind.NoRequirementFound"/> negative).</param>
/// <param name="CheckedOn">When this was last checked ("yyyy-MM-dd" or "yyyy-MM").</param>
public sealed record JurisdictionStatus(
    string Jurisdiction,
    JurisdictionStatusKind Kind,
    string Reason,
    LegalTier? LegalTier,
    string? Source,
    string CheckedOn);
