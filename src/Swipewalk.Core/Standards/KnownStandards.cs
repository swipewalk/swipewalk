using Swipewalk.Core.Model;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Standards;

/// <summary>
/// Laws and standards that reference WCAG, for "relevant to" labels in reports. Single source for
/// reports, results.json and docs/standards.md (regenerate with `swipewalk standards`).
/// Add a standard only with a primary source; record the date it was checked.
/// </summary>
public static class KnownStandards
{
    public const string Disclaimer =
        "\"Relevant to\" means a finding's WCAG criterion is within the WCAG version and level that the standard " +
        "references. It is not legal advice or a legal conclusion. Which requirements apply to you depends on your " +
        "jurisdiction, contracts and the standard's own exceptions and additional requirements.";

    /// <summary>WCAG criteria that Section 508 (E207.2) does not apply to non-web software.</summary>
    private static readonly IReadOnlySet<string> Section508NonWebSoftwareExceptions = new HashSet<string> { "2.4.1", "2.4.5", "3.2.3", "3.2.4" };

    /// <summary>WCAG criteria that EN 301 549 v3.2.1 clause 11 marks "Void" for non-web software.</summary>
    private static readonly IReadOnlySet<string> En301549V3NonWebSoftwareExceptions = new HashSet<string> { "2.4.1", "2.4.2", "2.4.5", "3.1.2", "3.2.3", "3.2.4" };

    /// <summary>
    /// WCAG criteria that EN 301 549 v4.1.1 clause 11 marks "Void" for non-web software. Unlike v3.2.1, 2.4.2 applies
    /// (11.2.4.2 Non-web software titled) and 3.2.4 applies to the software as a whole.
    /// </summary>
    private static readonly IReadOnlySet<string> En301549V4NonWebSoftwareExceptions = new HashSet<string> { "2.4.1", "2.4.5", "3.1.2", "3.2.3", "3.2.6" };

    public static IReadOnlyList<Standard> All { get; } =
    [
        new()
        {
            Id = "ada-title-ii",
            ShortName = "ADA Title II",
            Name = "ADA Title II (DOJ 2024 rule)",
            Jurisdiction = "United States",
            AppliesTo = "Web content and mobile apps that state and local governments provide or make available, directly or through contracts, licenses or other arrangements.",
            WcagVersion = WcagVersion.V2_1,
            Level = WcagLevel.AA,
            BeyondWcag = "The rule has exceptions (archived web content, preexisting conventional electronic documents, some third-party content, individualized password-protected documents, preexisting social media posts) that automated checks cannot evaluate.",
            Source = "https://www.ada.gov/resources/2024-03-08-web-rule/",
            CheckedOn = "2026-09",
        },
        new()
        {
            Id = "section-508",
            ShortName = "Section 508",
            Name = "Section 508 (Revised 508 Standards)",
            Jurisdiction = "United States (federal)",
            AppliesTo = "Information and communication technology that federal agencies develop, procure, maintain or use, including software and mobile apps.",
            WcagVersion = WcagVersion.V2_0,
            Level = WcagLevel.AA,
            BeyondWcag = "Applies WCAG 2.0 to non-web software with exceptions (E207.2: 2.4.1, 2.4.5, 3.2.3, 3.2.4 and complete processes do not apply). Chapter 5 adds software requirements beyond WCAG (for example 502 interoperability with assistive technology and 503.2 user preferences), which are not checked.",
            NotAppliedToNonWebSoftware = Section508NonWebSoftwareExceptions,
            Source = "https://www.access-board.gov/ict/",
            CheckedOn = "2026-09",
        },
        new()
        {
            Id = "en-301-549",
            ShortName = "EN 301 549",
            Name = "EN 301 549 v3.2.1",
            Jurisdiction = "European Union (and EEA)",
            AppliesTo = "Version cited for the Web Accessibility Directive (public sector websites and mobile apps). Clause 11 applies WCAG 2.1 to non-web software, including mobile apps. ETSI published v4.1.1 (aligned with WCAG 2.2) in September 2026; it confers a presumption of conformity only once cited in the EU Official Journal. Check which version your contract or regulator references.",
            WcagVersion = WcagVersion.V2_1,
            Level = WcagLevel.AA,
            BeyondWcag = "Clause 11 applies WCAG to non-web software; 2.4.1, 2.4.2, 2.4.5, 3.1.2, 3.2.3 and 3.2.4 are void there, and closed functionality has separate requirements. Clauses 5 and 11 add requirements beyond WCAG (for example user preferences, assistive technology interoperability and biometrics), which are not checked.",
            NotAppliedToNonWebSoftware = En301549V3NonWebSoftwareExceptions,
            Source = "https://www.etsi.org/deliver/etsi_en/301500_301599/301549/03.02.01_60/en_301549v030201p.pdf",
            CheckedOn = "2026-09",
        },
        new()
        {
            Id = "en-301-549-v4",
            ShortName = "EN 301 549 v4",
            Name = "EN 301 549 v4.1.1",
            Jurisdiction = "European Union (and EEA)",
            AppliesTo = "Published by ETSI in September 2026, with clauses 9 to 11 aligned to WCAG 2.2. It confers a presumption of conformity only once cited in the EU Official Journal; that citation was not verified when this entry was checked. Check which version your contract or regulator references.",
            WcagVersion = WcagVersion.V2_2,
            Level = WcagLevel.AA,
            BeyondWcag = "Clause 11 applies WCAG 2.2 to non-web software; 2.4.1, 2.4.5, 3.1.2, 3.2.3 and 3.2.6 are void there, 2.4.2 applies as \"non-web software titled\" and 3.2.4 applies to the software as a whole. Clauses 5 and 11 add requirements beyond WCAG, which are not checked.",
            NotAppliedToNonWebSoftware = En301549V4NonWebSoftwareExceptions,
            Source = "https://www.etsi.org/deliver/etsi_en/301500_301599/301549/04.01.01_60/en_301549v040101p.pdf",
            CheckedOn = "2026-09",
        },
        new()
        {
            Id = "uk-public-sector",
            ShortName = "UK public sector",
            Name = "Public Sector Bodies (Websites and Mobile Applications) (No. 2) Accessibility Regulations 2018",
            Jurisdiction = "United Kingdom",
            AppliesTo = "Public sector websites, and mobile apps developed for use by the public (apps for specific groups such as employees or students are not covered). The regulations do not name a WCAG version; GOV.UK guidance tells public sector bodies to meet WCAG 2.2 AA.",
            WcagVersion = WcagVersion.V2_2,
            Level = WcagLevel.AA,
            BeyondWcag = "The regulations also require an accessibility statement and list exemptions (for example some organisations and content types), which are not evaluated.",
            Source = "https://www.gov.uk/guidance/accessibility-requirements-for-public-sector-websites-and-apps",
            CheckedOn = "2026-09",
        },
    ];

    public static Standard? Find(string id) =>
        All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Ids of the standards a finding is relevant to. Platform advisories are relevant to none.</summary>
    public static IReadOnlyList<string> RelevantTo(Finding finding) =>
        finding.Kind == FindingKind.PlatformAdvisory
            ? []
            : [.. All.Where(s => finding.Criteria.Any(s.Includes)).Select(s => s.Id)];
}
