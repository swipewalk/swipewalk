using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Standards;

/// <summary>
/// US states, other countries and similar jurisdictions researched for 0.3, kept separate from
/// <see cref="KnownStandards.All"/> so they never appear on every finding by default -- a report only
/// computes relevance to one of <see cref="Jurisdictions"/> when the person asks for it by id with
/// <c>--standard</c> (see <see cref="KnownStandards.Find"/> and <see cref="Reports.ScanReport.InFocus"/>).
///
/// Every entry in <see cref="Jurisdictions"/> was checked against an official government source (a statute,
/// regulation, agency policy, or in a few cases an agency's own overview/FAQ page); the citation on each entry
/// shows which kind, since they carry different legal weight. <see cref="NotYetMapped"/>,
/// <see cref="NoRequirementFound"/>, <see cref="ReferencedStandardDiffers"/> and <see cref="NotApplicableToApps"/>
/// record, honestly, every jurisdiction that was researched but did not clear the bar for a mapped standard,
/// and why -- see <see cref="JurisdictionStatus"/>. docs/standards.md lists all of it (regenerate with
/// `swipewalk standards`).
/// </summary>
public static class KnownJurisdictions
{
    private const string CheckedOn = "2026-09-25";

    /// <summary>Same set as <see cref="KnownStandards"/>'s private Section508NonWebSoftwareExceptions, reused
    /// for Alaska: its ICT policy's Section 508 clause, for software not reached by its separate "latest WCAG"
    /// clause, which is limited to websites and online applications.</summary>
    private static readonly IReadOnlySet<string> Section508NonWebSoftwareExceptions = new HashSet<string> { "2.4.1", "2.4.5", "3.2.3", "3.2.4" };

    /// <summary>1 TAC §213.10's own text (read directly) excludes only WCAG Guideline 1.2 (synchronized media,
    /// via 508 §702.10); it does not cite 36 C.F.R. §1194.22 or E207.2, so Section508NonWebSoftwareExceptions is
    /// not reused here.</summary>
    private static readonly IReadOnlySet<string> TexasNonWebSoftwareExceptions =
        new HashSet<string> { "1.2.1", "1.2.2", "1.2.3", "1.2.4", "1.2.5" };

    /// <summary>
    /// Every other jurisdiction's own possible exceptions for non-web software (the kind of gap Section 508's
    /// E207.2 and EN 301 549's clause 11 document for the federal/EU standards) were not individually
    /// researched here -- unlike the federal Section 508 and EN 301 549 entries in <see cref="KnownStandards"/>,
    /// no WCAG2ICT-equivalent reading was done state by state or country by country. "Relevant to" for these
    /// entries is therefore an approximation for native software, not a confirmed non-web-software mapping.
    /// </summary>
    private const string NonWebExceptionsCaveat =
        "This entry's own possible exceptions for non-web software (of the kind Section 508's E207.2 or EN 301 " +
        "549's clause 11 document for the federal/EU standards) were not individually researched; \"relevant to\" " +
        "here is an approximation for native software, not a confirmed non-web-software mapping.";

    private static Standard Entry(
        string id, string shortName, string name, string jurisdiction, string appliesTo,
        WcagVersion version, WcagLevel level, LegalTier tier, string source, IReadOnlySet<string>? nonWebExceptions = null) =>
        new()
        {
            Id = id,
            ShortName = shortName,
            Name = name,
            Jurisdiction = jurisdiction,
            AppliesTo = appliesTo,
            WcagVersion = version,
            Level = level,
            LegalTier = tier,
            BeyondWcag = NonWebExceptionsCaveat,
            NotAppliedToNonWebSoftware = nonWebExceptions ?? new HashSet<string>(),
            Source = source,
            CheckedOn = CheckedOn,
        };

    private static JurisdictionStatus MakeNotYetMapped(string jurisdiction, string reason, LegalTier? tier, string? source) =>
        new(jurisdiction, JurisdictionStatusKind.NotYetMapped, reason, tier, source, CheckedOn);

    private static JurisdictionStatus MakeNoRequirement(string jurisdiction, string reason, string? source) =>
        new(jurisdiction, JurisdictionStatusKind.NoRequirementFound, reason, null, source, CheckedOn);

    private static JurisdictionStatus MakeDiffers(string jurisdiction, string reason, LegalTier tier, string source) =>
        new(jurisdiction, JurisdictionStatusKind.ReferencedStandardDiffers, reason, tier, source, CheckedOn);

    private static JurisdictionStatus MakeNotApplicable(string jurisdiction, string reason, LegalTier tier, string source) =>
        new(jurisdiction, JurisdictionStatusKind.NotApplicableToApps, reason, tier, source, CheckedOn);

    /// <summary>
    /// Confirmed, mapped standards for US states and other countries -- opt-in via <c>--standard &lt;id&gt;</c>,
    /// never part of <see cref="KnownStandards.All"/>. Each was checked against an official government source on
    /// <see cref="CheckedOn"/> (its own Source field shows exactly which page or document was checked).
    /// </summary>
    public static IReadOnlyList<Standard> Jurisdictions { get; } =
    [
        // ---- US states ----
        Entry("us-tx", "Texas", "Texas (1 Texas Administrative Code §213.10)",
            "United States — Texas",
            "Texas state agencies, for software applications they procure, develop or use. §213.10 (\"Software Applications and Operating Systems\", read directly) incorporates the Revised Section 508 Standards: 36 C.F.R. Part 1194 Appendix C §702.10 (WCAG 2.0 Level AA, excluding Guideline 1.2 -- the same exclusion 1 TAC §206.70 states for higher-education websites) plus Chapter 5 §§502-504. A Texas Department of Information Resources summary page separately describes coverage of \"web and mobile applications\" and a WCAG 2.1 AA figure, but that is a secondary summary, not this regulation's own text, so it is not relied on here.",
            WcagVersion.V2_0, WcagLevel.AA, LegalTier.Regulation,
            "https://www.law.cornell.edu/regulations/texas/1-Tex-Admin-Code-SS-213-10",
            TexasNonWebSoftwareExceptions),
        Entry("us-il", "Illinois", "Illinois Information Technology Accessibility Act (IITAA)",
            "United States — Illinois",
            "All State of Illinois governmental entities (executive, legislative and judicial branches, agencies, constitutional offices and public universities) -- the state's own IITAA page states it does not directly apply to local governments, school districts, community colleges or private organizations. IITAA 2.1 (effective 2024-06-24) requires WCAG 2.1 AA, layered on federal Section 508. Covers websites, electronic documents, videos, software, computers and kiosks; the DoIT overview page read this pass does not itself use the words \"mobile app\", and no other page was found describing mobile apps as in scope, so that isn't relied on here. The WCAG 2.1 AA figure comes from the IITAA Standards DoIT adopted under 30 ILCS 587, not from the Act's own statute text, which does not name WCAG directly.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.Statute,
            "https://doit.illinois.gov/initiatives/accessibility/iitaa.html"),
        Entry("us-co", "Colorado", "Colorado HB21-1110 / HB24-1454 (Colorado Anti-Discrimination Act)",
            "United States — Colorado",
            "State agencies and local government entities (counties, cities, school districts and special districts), reaching vendors selling to Colorado public-sector entities through procurement. Explicitly covers, per the Colorado OIT FAQ (an agency FAQ, not the statute text itself): \"websites, applications, kiosks, digital signage, documents, video, audio, and third-party tools\". HB24-1454 did not itself extend the 2024-07-01 deadline -- per that same FAQ it instead gives a one-year grace period (to 2025-07-01) of immunity from liability for an entity that can show a good-faith remediation effort. Enforcement: a private right of action in state court, with a $3,500 statutory fine per violation plus possible attorney's fees -- a notably stronger mechanism than most other states here.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.Statute,
            "https://oit.colorado.gov/standards-policies-guides/guide-to-accessible-web-services/faq-hb21-1110-colorado-laws-for-persons"),
        Entry("us-ny", "New York", "New York State Technology Law §103-d / NYS-P08-005",
            "United States — New York",
            "\"State Entities\" (state government per Executive Order 117, explicitly including third parties -- local governments, consultants, vendors and contractors -- that use or access IT resources the state administers). The ITS policy's own §4.0 mandates \"the Level AA success criteria and conformance requirements ... [of] WCAG Version 2.2\" -- an official-policy-level mandate, which is what is mapped here. Two more specific tracks within the same policy: State Technology Law §103-d independently requires state-entity websites (not apps) to reach WCAG 2.2 AA \"or any successor guidelines\" by 2027-01-01 (§4.2-4.3); for mobile applications, the same policy instead cites the federal DOJ Title II rule at WCAG 2.1 AA by 2026-06-25 (the policy's own stated date for that track, not independently re-verified against the federal rule's own text). Mobile applications are named explicitly and repeatedly.",
            WcagVersion.V2_2, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://web.archive.org/web/20251207041825/https://its.ny.gov/system/files/documents/2024/10/nys-p08-005-accessibility-of-information-communication-technology_0.pdf"),
        Entry("us-mn", "Minnesota", "Minnesota Statutes §16E.03, subdivision 9",
            "United States — Minnesota",
            "Minnesota executive branch state agencies (creating, modifying or procuring ICT for internal or external use), reaching vendors through RFP/procurement. The statute itself requires incorporating Section 508 and WCAG 2.0, with the state CIO empowered to update to a later WCAG version; the current MNIT Digital Accessibility Standard reportedly sets WCAG 2.1 AA, but that standard document was not independently read, so WCAG 2.0 AA (the statute's own confirmed floor) is used here. \"Information technology\" is defined broadly (software and most hardware); \"mobile app\" is not seen verbatim in the statute text.",
            WcagVersion.V2_0, WcagLevel.AA, LegalTier.Statute,
            "https://www.revisor.mn.gov/statutes/cite/16E.03"),
        Entry("us-ma", "Massachusetts", "Massachusetts Enterprise Digital Accessibility Policy",
            "United States — Massachusetts",
            "Commonwealth executive-branch agencies; the policy's \"fully comply\" wording implies vendor/procurement reach too, though a procurement clause wasn't independently read. Issued under Executive Order 614 (2023-07-26) and the Commonwealth's designation of EOTSS as lead executive-branch IT organization. Sets WCAG 2.1 Levels A and AA as the minimum technical standard. Explicitly names \"web and desktop applications, mobile applications, multimedia content, social media content, electronic documents and artificial intelligence integrations\".",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://www.mass.gov/guides/enterprise-it-accessibility-standards"),
        Entry("us-wa", "Washington", "Washington OCIO Policy USER-01 (formerly Policy 188)",
            "United States — Washington",
            "Washington executive branch agencies, plus boards and commissions under WaTech's authority; vendor/procurement reach was not independently confirmed. Sets WCAG 2.1 AA as the minimum standard, also referencing federal Section 508. Explicitly names \"websites, web applications, mobile apps, electronic documents, software, and digital content\" -- per a WaTech summary page; the full policy document text itself was not independently read.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://watech.wa.gov/accessibility"),
        Entry("us-ga", "Georgia", "Georgia Digital Accessibility Standard (GTA PSG SM-19-002)",
            "United States — Georgia",
            "State organizations/entities directly, and vendors through procurement (the standard requires accessibility conformance be made mandatory in \"all solicitations and contracts for work involving digital properties\"). Sets WCAG 2.1 AA. Explicitly names \"websites, web applications, mobile applications, or any other form of digital information\". Requires accessibility audits at least every 36 months, with errors remediated within 9 months.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://gta-psg.georgia.gov/psg/digital-accessibility-standard-sm-19-002"),
        Entry("us-ak", "Alaska", "Alaska Accessibility of Information and Communication Technology Policy",
            "United States — Alaska",
            "All State of Alaska executive branch agencies. Approved 2022-08-04. The policy (read directly) requires each agency to \"adopt a standard for ICT that complies with Section 508\" -- mapped here via that clause (WCAG 2.0 AA, the level the Revised 508 Standards incorporate, with the same non-web-software exceptions as Section 508's own E207.2). Separately, for websites and online applications specifically, the policy directs agencies to \"train its staff and use the latest version\" of WCAG A/AA -- a self-updating reference limited to websites and online applications, not native software generally, so not used for this native-software mapping. \"Information and Communication Technology\" is defined broadly, explicitly including \"applications\", websites and software. Each agency must monthly test its ICT for accessibility issues; the State ADA Coordinator may review how well an agency has followed it.",
            WcagVersion.V2_0, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://doa.alaska.gov/ada/policy/AccessibilityofInfoandCommTechPolicy.pdf",
            Section508NonWebSoftwareExceptions),
        Entry("us-ct", "Connecticut", "Connecticut Accessibility & Inclusivity Policy for Websites and Digital Assets",
            "United States — Connecticut",
            "State agencies defined in C.G.S. §4d-1(3); the policy \"encourages\" (does not require) the Legislature and Judiciary to adopt similar standards. Version 5.0, effective 2025-07-14. Requires WCAG 2.1 Level AA. Explicitly names \"mobile applications\" alongside websites, digital documents, electronic forms, multimedia and software applications. Each agency head designates an Accessibility Designee; no penalty or private right of action was found.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://portal.ct.gov/-/media/sitecore-center/accessibility/it-accessibility-policy-final-for-release-07142025.pdf"),
        Entry("us-ks", "Kansas", "Kansas ITEC Policy 1210-P, Revision 4",
            "United States — Kansas",
            "All boards, commissions, departments, divisions and agencies of the executive branch (\"entities\"); the legislative and judicial branches are only \"encouraged\", not bound. Effective 2025-04-24. Section 6.2 of the policy requires each entity's ICT to conform to three things: the Revised Section 508 Standards (6.2.1), the ADA Title II rule's Subpart H (6.2.2), and \"WCAG 2.1 levels A and AA\" (6.2.3) -- WCAG 2.1 AA is an operative requirement of this policy, not merely supporting context. \"Platform software\" is defined to include \"embedded operating systems, including mobile systems\".",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://web.archive.org/web/20260814164939/https://www.ebit.ks.gov/resources/governance/it-executive-council/itec-policies-standards/1210-information-and-communication-technology-accessibility-standards"),
        Entry("us-la", "Louisiana", "Louisiana Policy and Procedure Memorandum No. 74 (LAC 4:Chapter 61)",
            "United States — Louisiana",
            "All boards, commissions, departments, agencies, institutions and offices of the executive branch. Formally promulgated as an administrative rule (Louisiana Administrative Code Title 4, Part V, Chapter 61) through the Louisiana Register, not merely an internal memo -- amended 2025-06 (lowered from an earlier WCAG 2.2 target \"due to unexpected administrative challenges\"). Requires \"at a minimum\" WCAG 2.1 Level AA. Explicitly and repeatedly names \"websites and mobile applications\". §6109(A)(6) sets 2026-04-24 as the date by which web content not otherwise exempted must meet that standard -- that date is the federal DOJ Title II rule's own deadline for entities serving 50,000 or more people (which every state, including Louisiana, falls into), not a smaller-entity date, and §6109(A)(6) does not itself name apps for that specific date.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.Regulation,
            "https://www.doa.la.gov/media/gs0hee2m/ppm-74-web-accessibility-compliance-r6-25-a11y.pdf"),
        Entry("us-me", "Maine", "Maine Digital Accessibility and Usability Policy",
            "United States — Maine",
            "Digital information/services under the purview of the Chief Information Officer (Maine Revised Statutes Title 5, §1973 per the policy's own footnote). Requires the revised Section 508 Standards and WCAG 2.1 Level A/AA for public-facing content, and extends the same standard to nine categories of internal content and to agency software. The policy's own \"Software\" definition (§5.8.2) names \"web, desktop, server, and mobile client application\" directly, so mobile apps are an explicitly covered category. A waiver process (Equally Effective Alternative Access Plan) exists but does not itself excuse other legal obligations.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://www.maine.gov/oit/sites/maine.gov.oit/files/inline-files/DigitalAccessibilityPolicy.pdf"),
        Entry("us-mi", "Michigan", "Michigan SOM IT Technical Standard 1360.00.11, SOM Digital Standards for Websites and Applications",
            "United States — Michigan",
            "All websites and applications, both web and mobile, for executive branch state departments, agencies and sub-units. Adopted WCAG 2.1 AA in May 2024, converted to a formal IT Technical Standard effective 2025-05-27, following the DOJ's ADA Title II rule change. Names mobile applications repeatedly, including a separate \"SOM Native Mobile Application Guidelines\" document. Conformance is checked via application reviews using both automated tools (axe DevTools, WAVE, Siteimprove) and manual screen-reader testing (JAWS, NVDA, VoiceOver, TalkBack).",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://www.michigan.gov/som/digitalstandards"),
        Entry("us-ne", "Nebraska", "Nebraska NITC Standard 2-101, Accessibility Policy",
            "United States — Nebraska",
            "State agencies (\"ICT that is procured, developed, maintained, or used by state agencies\"). Adopted 2001-10-31, most recently amended 2025-07-11. The standard's own text incorporates the Revised 508 Standards and, separately, the federal DOJ Title II rule (28 CFR Part 35, Subpart H -- Web and Mobile Accessibility) -- which is what carries the WCAG 2.1 AA requirement used here, rather than the standard naming a WCAG version directly. \"ICT\" is defined broadly and the Web and Mobile Accessibility subsection explicitly covers both web and mobile.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://nitc.nebraska.gov/docs/2-101.pdf"),
        Entry("us-nv", "Nevada", "Nevada ADA Technology Accessibility Guidelines (OCIO Control No. 101, Rev. 2.1)",
            "United States — Nevada",
            "\"All information and communication technology acquired or developed for all department, agencies, board, commissions and other Nevada entities\", explicitly including outside vendors and services. This is a set of guidelines (its own title), Rev. 2.1, dated 2019-07-22, hosted on judicial.nv.gov rather than a state statute site, and it itself says guidance \"may vary based on the entity's situation\". Requires WCAG 2.1 conformance levels A and AA. Covers websites, electronic documents, video/multimedia and software applications (internal and public-facing). Note: a commonly repeated \"NRS 242.131\" citation for this program was checked directly and found to be about an unrelated IT-services statute, not accessibility -- it is not cited here.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://judicial.nv.gov/uploadedFiles/adanewnvgov/content/Partners/Policies/ADA_WebsiteGuidelines_7-22-19.pdf"),
        Entry("us-nh", "New Hampshire", "New Hampshire Information Technology Accessibility Policy (NHS0305, v4)",
            "United States — New Hampshire",
            "All State of New Hampshire agency IT solutions, internal and external, whether developed internally or procured from a third party. Effective 2024-06-25. Requires web content and mobile applications to comply with WCAG 2.1 Level AA (\"Mobile Applications\" is a defined term). DoIT's User Experience Division regularly monitors/spot-checks agency web content; employees who don't follow the policy are subject to internal disciplinary action, not a public complaint process.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://www.nh.gov/sites/g/files/ehbemt936/files/documents/information-technology-accessibility-policy.pdf"),
        Entry("us-nc", "North Carolina", "North Carolina Digital Accessibility and Usability Standard, v1.1",
            "United States — North Carolina",
            "North Carolina state agencies' websites and digital services that are maintained by, or on behalf of, an agency and intended for use by the public; internal-facing sites are only \"encouraged\". Published 2025-01-16. Requires WCAG 2.1 Level AA. The standard's own defined term \"Digital Service\" explicitly includes \"web applications, and mobile applications\", so native apps are named directly, not left to its \"Mobile-First Design that Scales Across Varying Device Sizes\" responsive-web-design section alone.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://it.nc.gov/documents/digital-accessibility-usability-standard/open"),
        Entry("us-oh", "Ohio", "Ohio Administrative Policy IT-09, Digital Accessibility",
            "United States — Ohio",
            "All state agencies, boards and commissions under the Governor's authority, for Web Content and Mobile Applications \"made available to the public\" (the policy's own scope; internal-only systems aren't covered by this clause); reaches vendors via contract/COTS-certification clauses requiring suppliers to certify WCAG 2.1 AA conformance. Effective 2025-01-10, with a deadline of 2027-04-26 tracking the federal DOJ rule. Requires \"Web Content and Mobile Applications\" to comply with WCAG 2.1 Level AA, named explicitly and repeatedly.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://das.ohio.gov/technology-and-strategy/policies/it-09"),
        Entry("us-pa", "Pennsylvania", "Pennsylvania Digital Accessibility Policy",
            "United States — Pennsylvania",
            "All offices, departments, boards, commissions and councils under the Governor's jurisdiction, and any other entity connecting to the Commonwealth Network; procurement contracts must require third-party vendors to conform to WCAG 2.1 levels A and AA. Effective 2025-11-12. Requires digital content and services to meet, at minimum, WCAG 2.1 Levels A and AA (encouraged, not required, up to AAA). \"Mobile app\" is not named verbatim, but \"digital content and services\" and \"applications\" are used broadly.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.OfficialPolicy,
            "https://www.pa.gov/content/dam/copapwp-pagov/en/oa/documents/policies/it-policies/digital%20accessibility%20policy.pdf"),

        // ---- Other countries ----
        Entry("germany-bitv", "Germany (public sector)", "Germany — Barrierefreie-Informationstechnik-Verordnung (BITV 2.0)",
            "Germany",
            "Federal public bodies (the German states/Länder have their own similar ordinances, not researched here). Issued in 2011; mobile applications (§2 Nr. 2, \"mobile Anwendungen\") were added by the 2019 amendment transposing EU Directive 2016/2102. §3 itself does not name EN 301 549 by number -- it requires conformance with whichever \"harmonized standard\" is currently cited in the EU Official Journal for this purpose, which is EN 301 549 v3.2.1 (WCAG 2.1 AA-aligned) as of this check under the Web Accessibility Directive; mapped here on that basis, consistent with how this project's own en-301-549 entry already handles the same EU indirection.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.Regulation,
            "https://www.gesetze-im-internet.de/bitv_2_0/__2.html"),
        Entry("france-rgaa", "France", "France — Référentiel Général d'Amélioration de l'Accessibilité (RGAA 4.1.2)",
            "France",
            "French public administrations, under Article 47 of Loi n°2005-102 (the founding statute, which names \"les sites internet, intranet, extranet, les applications mobiles, les progiciels et le mobilier urbain numérique\" -- mobile applications, software packages and digital street furniture -- directly, though it delegates the technical standard itself to decree/methodology level). Article 47 II separately extends similar accessibility duties to large private companies (reportedly above a €250M turnover threshold, not independently re-verified this pass). RGAA's own scope page states the current version, RGAA 4.1.2, references \"WCAG 2.1 de niveau simple A (A) et double A (AA)\" directly -- confirmed as WCAG 2.1 Levels A and AA, not 2.0 or 2.2. A separate 2023 transposition of the EU's European Accessibility Act extends further duties to some private-sector B2C companies from 2025-06-28; that transposition's own text (and its SME exemption threshold) was not independently read this pass, so is not relied on here.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.Statute,
            "https://accessibilite.numerique.gouv.fr/obligations/champ-application/"),
        Entry("italy-eaa", "Italy (EAA, private sector)", "Italy — Legislative Decree 82/2022 (EAA transposition, private-sector services)",
            "Italy",
            "Private-sector services in scope of the EU's European Accessibility Act -- banking, e-commerce, e-books, self-service terminals, passenger transport and similar -- under Legislative Decree 82/2022 (in force 2022-07-16; duties for products/services placed on the market from 2025-06-28), with a microenterprise exemption (fewer than 10 persons and turnover/balance sheet at or under €2M, confirmed directly in AgID's own Guidelines' definitions section). The decree's own Article 1 names \"servizi per dispositivi mobili, comprese le applicazioni mobili\" (mobile applications) directly for passenger transport services. AgID's Guidelines for this EAA transposition (2026-03-04) name EN 301 549 and WCAG 2.1 Levels A and AA as the technical standard, and explicitly cover both \"siti web\" (websites) and \"applicazioni mobili\" (mobile applications). This entry covers the EAA/private-sector duty only -- Legge Stanca's separate, older public-sector duty (Law 4/2004) is governed by AgID's own public-sector guidelines, a different document that was not read this pass, so it is not relied on here.",
            WcagVersion.V2_1, WcagLevel.AA, LegalTier.Statute,
            "https://www.agid.gov.it/sites/agid/files/2026-03/Linee_Guida_accessibilit%C3%A0_dei_servizi_(EAA).pdf"),
    ];

    /// <summary>Confirmed instruments that reference something other than a WCAG 2.x version/level Swipewalk
    /// can honestly map criterion-by-criterion -- either WCAG 1.0 (Rhode Island, Vermont), or Section 508 cited
    /// without a specific version, where whether that picks up the 2017 Revised 508 Standards (WCAG 2.0 AA) is
    /// a legal reading this project has not settled (only Arkansas's statute is explicitly pinned to the
    /// pre-2017 technical standards). See <see cref="JurisdictionStatusKind.ReferencedStandardDiffers"/>.
    /// Listed for information; findings are never matched to these per finding.</summary>
    public static IReadOnlyList<JurisdictionStatus> ReferencedStandardDiffers { get; } =
    [
        MakeDiffers("California",
            "Government Code §7405(a) (read directly on leginfo.legislature.ca.gov) requires state agencies to " +
            "comply with \"Section 508 of the federal Rehabilitation Act of 1973, as amended ... and " +
            "regulations ... set forth in Part 1194\" -- almost the same generic, undated Section 508 citation " +
            "as Florida's §282.603, so whether it picks up the 2017 Revised 508 Standards is the same unsettled " +
            "legal reading. The state's separate §11546.7 (AB 434) does set a specific figure, WCAG 2.0 Level AA " +
            "\"or a subsequent version\", but only for each agency's own Internet Web site, not apps or the " +
            "agency's software generally.",
            LegalTier.Statute, "https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?lawCode=GOV&sectionNum=7405"),
        MakeDiffers("Arizona",
            "GITA Statewide Policy P130 (\"Web Site Accessibility\", effective 2008-09-12) is framed entirely " +
            "around the pre-2017-refresh Section 508 technical standards (1194.22 paragraph citations); WCAG is " +
            "not named anywhere in the 36-page policy read directly. It also binds only \"budget units\" -- " +
            "explicitly excluding universities, community colleges and the legislative/judicial branches.",
            LegalTier.OfficialPolicy, "https://aztaxes.gov/PDF/WebSiteAccessibilityPolicy.pdf"),
        MakeDiffers("Arkansas",
            "Arkansas Code §§25-26-201 through 25-26-205 (Act 1227 of 1999, amended by Act 308 of 2013) requires " +
            "nonvisual access technology under the pre-2017-refresh 36 C.F.R. §§1194.21/1194.22 \"as it existed " +
            "on January 1, 2013\"; WCAG is not named anywhere in the statute text read directly. This is the one " +
            "entry in this list explicitly pinned to the pre-2017 technical standards, rather than citing " +
            "Section 508 generically.",
            LegalTier.Statute, "https://arkleg.state.ar.us"),
        MakeDiffers("Florida",
            "Florida Statutes §282.603 (read directly) requires state agencies to conform to \"Section 508 of " +
            "the Rehabilitation Act of 1973 ... as amended\" and 36 C.F.R. part 1194. \"As amended\" suggests " +
            "this may dynamically pick up the 2017 Revised 508 Standards (which incorporate WCAG 2.0 AA) rather " +
            "than being pinned to the pre-2017 technical standards -- that reading is a legal question this " +
            "project has not settled, so no WCAG version/level is mapped here; WCAG itself is not named in the " +
            "statute text.",
            LegalTier.Statute,
            "http://www.leg.state.fl.us/Statutes/index.cfm?App_mode=Display_Statute&URL=0200-0299/0282/Sections/0282.603.html"),
        MakeDiffers("Indiana",
            "Indiana Code §4-13.1-3-1 (added 2005) requires standards \"compatible with\" the federal Section " +
            "508 accessibility standards, cited generically rather than pinned to a specific vintage -- whether " +
            "that picks up the 2017 Revised 508 Standards (WCAG 2.0 AA) is a legal reading this project has not " +
            "settled; WCAG itself is not named in the statute text read directly. Unusually broad reach: it " +
            "binds the executive, legislative, judicial and administrative branches of state AND local " +
            "government.",
            LegalTier.Statute,
            "https://web.archive.org/web/20241216161434/https://law.justia.com/codes/indiana/title-4/article-13-1/chapter-3/section-4-13-1-3-1/"),
        MakeDiffers("Kentucky",
            "Kentucky Revised Statutes §§61.980-61.988 (the Accessible Information Technology Act, 2000) " +
            "require access \"equivalent\" to that of non-disabled individuals, defined by reference to Section " +
            "255 of the Telecommunications Act of 1996 and Section 508 of the Workforce Investment Act of 1998, " +
            "cited generically rather than pinned to a specific vintage -- whether that picks up the 2017 " +
            "Revised 508 Standards (WCAG 2.0 AA) is a legal reading this project has not settled; WCAG itself is " +
            "not named in the sections read directly. Reaches \"state-assisted organizations\" -- schools, " +
            "universities and political subdivisions -- not just core executive agencies.",
            LegalTier.Statute, "https://apps.legislature.ky.gov/law/statutes/statute.aspx?id=23111"),
        MakeDiffers("Missouri",
            "Missouri Revised Statutes §161.935 (1999, transferred 2014) requires IT access \"comparable\" to " +
            "that of non-disabled individuals, referencing \"Section 508 of the Workforce Investment Act of " +
            "1998\" generically rather than a specific vintage -- whether that picks up the 2017 Revised 508 " +
            "Standards (WCAG 2.0 AA) is a legal reading this project has not settled; WCAG itself is not named " +
            "in the statute text read directly. A separate Missouri State Accessibility Standard reportedly sets " +
            "WCAG 2.2 AA, but that standard document was not independently read.",
            LegalTier.Statute, "https://revisor.mo.gov/main/OneSection.aspx?section=161.935"),
        MakeDiffers("Montana",
            "Montana Code Annotated §18-5-605 (1999, last amended 2009) requires a technology-access contract " +
            "clause addressing compatibility with nonvisual access technology; WCAG is not named in the statute " +
            "text read directly. A separate SITSD program reportedly sets WCAG AA, but that program's own " +
            "document was not independently read.",
            LegalTier.Statute,
            "https://mca.legmt.gov/bills/mca/title_0180/chapter_0050/part_0060/section_0050/0180-0050-0060-0050.html"),
        MakeDiffers("Oklahoma",
            "The Electronic and Information Technology Accessibility Act (Title 62 §§34.28-34.30, enacted 2004) " +
            "requires state conformance with \"Section 508 of the Workforce Investment Act of 1998\" cited " +
            "generically rather than a specific vintage -- whether that picks up the 2017 Revised 508 Standards " +
            "(WCAG 2.0 AA) is a legal reading this project has not settled; WCAG itself is not named in the " +
            "statute text read directly.",
            LegalTier.Statute, "https://oksenate.gov/sites/default/files/2019-12/os62.pdf"),
        MakeDiffers("Rhode Island",
            "Rhode Island's own current accessibility page (read directly) still states its statewide standard, " +
            "unrevised since 1999, as \"the World Wide Web Consortium (W3C) Priority 1 Checkpoints\" -- WCAG " +
            "1.0's Priority 1 level, not WCAG 2.x -- so findings are not translated into it per this project's " +
            "own rule (old standards are shown as-is). The policy is also framed entirely around websites, with " +
            "no mobile-app language.",
            LegalTier.OfficialPolicy, "https://www.ri.gov/resource/accessibility/"),
        MakeDiffers("Vermont",
            "Vermont's own \"Web Accessibility Requirements\" policy (adopted 2006, updated 2017, read directly) " +
            "sets \"all Section 508 requirements and all WCAG Priority 1 checkpoints and Priority 2 and 3 " +
            "checkpoints as needed\" -- WCAG 1.0, not WCAG 2.x -- so findings are not translated into it per " +
            "this project's own rule (old standards are shown as-is). It is website-only, with no mobile-app " +
            "language, and its own text cites no Vermont statute as authority (federal law only).",
            LegalTier.OfficialPolicy,
            "https://digitalservices.vermont.gov/sites/digitalservices/files/documents/web-policy/ADS-WebAccessibilityPolicy2017.pdf"),
    ];

    /// <summary>Confirmed instruments whose own scope, read directly, does not reach native apps -- see
    /// <see cref="JurisdictionStatusKind.NotApplicableToApps"/>.</summary>
    public static IReadOnlyList<JurisdictionStatus> NotApplicableToApps { get; } =
    [
        MakeNotApplicable("Idaho",
            "Idaho Technology Authority Enterprise Standard S5120 (\"Web Publishing\") sets WCAG 2.1 AA plus " +
            "WCAG 2.1 AAA success criterion 3.2.5, but its own scope, read directly, is \"all public-facing web " +
            "pages registered under the idaho.gov domain\" -- a web-publishing standard alongside sibling " +
            "standards for branding and domain names, with no mobile-app or native-software language.",
            LegalTier.OfficialPolicy, "https://ita.idaho.gov/wp-content/uploads/2025/07/S5120-ADA.pdf"),
        MakeNotApplicable("Iowa",
            "The Iowa Enterprise Operational Standard: Website Accessibility (adopted 2012, revised 2017) sets " +
            "WCAG 2.0 Levels A and AA, but its own defined scope, read directly, is \"Website (Web Content)\": " +
            "\"web pages, electronic documents, images, videos or other digital assets\" reachable by a common " +
            "domain or IP address -- no mobile-app language.",
            LegalTier.OfficialPolicy, "https://ocio.iowa.gov/sites/default/files/standards/2017-07/website_accessibility_standard_2017.pdf"),
        MakeNotApplicable("Ontario, Canada",
            "Ontario's Integrated Accessibility Standards Regulation (O. Reg. 191/11 under the AODA) requires " +
            "the Government of Ontario's and designated organizations' internet sites to conform to WCAG 2.0 " +
            "Level AA, read directly, and also binds \"large organizations\" (reaching some private-sector " +
            "entities) under s.14(2). Section 14(5)(a)'s own text names only \"websites and web content, " +
            "including web-based applications\" -- confirmed, on the regulation's own text, to reach web-based " +
            "applications delivered through a browser, never a native or mobile app; \"application\" outside " +
            "that specific phrase, or \"app\"/\"mobile\"/general \"software\", does not appear.",
            LegalTier.Regulation, "https://www.ontario.ca/laws/docs/110191_e.doc"),
    ];

    /// <summary>Researched but not confirmed enough to ship -- a blocked or unreadable primary source, or a
    /// disputed reading. Never means "no requirement"; only that verification isn't finished. See
    /// <see cref="JurisdictionStatusKind.NotYetMapped"/>.</summary>
    public static IReadOnlyList<JurisdictionStatus> NotYetMapped { get; } =
    [
        MakeNotYetMapped("Maryland",
            "Two documents were identified as Maryland's likely legal basis (COMAR 14.33.02, \"Nonvisual Access " +
            "Standards\"; the state's Digital Accessibility Policy, reportedly setting WCAG 2.1 AA by " +
            "2027-04-26) but neither's actual text could be read this pass -- both PDF fetches returned " +
            "unreadable binary content -- and a related procurement-clause citation is unresolved between two " +
            "different section numbers (§3.5-311 vs. the older §3A-311).",
            LegalTier.Regulation, "https://doit.maryland.gov/policies/Accessibility/Documents/Digital-Accessibility-Policy-Final.pdf"),
        MakeNotYetMapped("Delaware",
            "Delaware's Department of Technology and Information states its own Digital Accessibility Policy " +
            "(WCAG 2.1 Levels A and AA) is based on \"Delaware Code Title 6, Chapter 45 (includes §4504)\" -- " +
            "but §4504's actual statute text (a general public-accommodations \"Equal Accommodations\" law) " +
            "could not be read this pass (JS-rendered pages, no archived snapshot found), so whether it " +
            "actually addresses digital/ICT accessibility, or is being invoked for general disability-" +
            "nondiscrimination color only, is unconfirmed.",
            LegalTier.Statute, "https://accessibility.dti.delaware.gov/state-of-delaware-digital-accessibility-policy/"),
        MakeNotYetMapped("Virginia",
            "Virginia's Information Technology Access Act (Va. Code §2.2-3500 et seq., substantially modernized " +
            "by HB2541 in 2025) is a real, confirmed statute -- read directly -- but its findings section does " +
            "not itself name a WCAG version, and secondary sources disagree on whether the practical benchmark " +
            "is WCAG 2.0 or 2.1 AA. That version discrepancy was not resolved by reading the statute's own " +
            "operative sections (§§2.2-3501 through 3504) this pass.",
            LegalTier.Statute, "https://law.lis.virginia.gov/vacode/title2.2/chapter35/section2.2-3500/"),
        MakeNotYetMapped("Hawaii",
            "Hawaii's Electronic Information Technology Accessibility Act (Act 172, Session Laws of Hawaii " +
            "2022) is a real, confirmed statute -- read directly, and unusually broad (it explicitly reaches " +
            "public K-12 schools and the University of Hawaii) -- but it delegates the specific WCAG version " +
            "and level to a \"Hawaii Electronic Information Technology Disability Access Standards\" document " +
            "the Office of Enterprise Technology Services was directed to publish; that standards document was " +
            "not found or read this pass, so no version/level can be confirmed yet.",
            LegalTier.Statute, "https://health.hawaii.gov/dcab/files/2025/04/Act-172-SLH-2022.pdf"),
        MakeNotYetMapped("Utah",
            "Utah Code §63A-16-209 (read directly) only directs the chief information officer to set standards " +
            "\"by rule\", \"at minimum, consistent with the most recent\" WCAG -- the statute names no level, " +
            "and the operative instrument is Utah Administrative Code rule R895-14, which reportedly pins WCAG " +
            "2.1 AA but was not read this pass. Coverage is also unclear from the statute alone: it names agency " +
            "websites, procured hardware/software, and employee information systems, not clearly apps an agency " +
            "builds for the public.",
            LegalTier.Statute, "https://le.utah.gov/xcode/Title63A/Chapter16/63A-16-S209.html"),
        MakeNotYetMapped("New Jersey",
            "The NJ Web Presence Guidelines (read directly) state that \"WCAG 2.1 Level AA is the technical " +
            "standard for state and local governments' web content and mobile apps\" -- but that sentence " +
            "appears alongside a reference to ADA.gov and to being \"accessible ... in accordance with Section " +
            "508\", reading as a restatement of the federal DOJ Title II rule rather than an independent New " +
            "Jersey mandate, and the document itself is a branding/usability guideline written in \"should\" " +
            "language. A separate statute, N.J.S.A. 18A:36-35.1 (2021), binds specific education entities but " +
            "was not read this pass.",
            LegalTier.OfficialPolicy, "https://nj.gov/it/it/docs/NJ_Web_Presence_Guidelines.pdf"),
        MakeNotYetMapped("Mississippi",
            "No standalone Mississippi law comparable to other states' was independently confirmed this pass; " +
            "the Department of ITS reportedly has accessibility policies for state agencies, but that document " +
            "was not found or read.",
            null, null),
        MakeNotYetMapped("Canada (federal)",
            "The Accessible Canada Regulations' ICT and mobile-app provisions -- the \"ICT Standard\" " +
            "requirement for web pages and, separately, for mobile applications (ss.19.4/19.5/19.51) -- sit in a " +
            "part of the consolidated SOR/2021-241 text marked \"AMENDMENTS NOT IN FORCE\" (added by " +
            "SOR/2025-255) as of 2026-09-25, so whether these duties currently bind anyone is unconfirmed. " +
            "Section 19.51 also excludes broadcasting/telecommunications entities and transportation service " +
            "providers from the mobile-app duty, and it would apply only to federally-regulated private-sector " +
            "entities averaging 500 or more employees (s.7(1)(e)). The one WCAG duty independently confirmed as " +
            "currently in force is for accessibility plans, feedback-process descriptions and progress reports " +
            "(WCAG Level AA, \"most recent version\": ss.6, 10, 15).",
            LegalTier.Regulation, "https://laws.justice.gc.ca/eng/regulations/SOR-2021-241/FullText.html"),
        MakeNotYetMapped("Germany (private sector)",
            "BFSG §4 (read directly) gives a presumption of conformity for harmonised standards \"deren " +
            "Fundstellen im Amtsblatt der Europäischen Union veröffentlicht worden sind\" (whose reference " +
            "numbers have been published in the EU Official Journal) -- but no EAA-specific harmonised standard " +
            "was confirmed as actually cited there as of this check (EN 301 549 v3.2.1 is OJ-cited under the " +
            "separate Web Accessibility Directive, not confirmed under the EAA). BFSG's own §1 Abs. 3 \"mobiler " +
            "Anwendungen\" wording, read in context, is specifically about passenger-transport services, not a " +
            "general statement covering every in-scope service. A microenterprise exemption also wasn't checked.",
            LegalTier.Statute, "https://www.gesetze-im-internet.de/bfsg/BJNR297010021.html"),
        MakeNotYetMapped("Australia",
            "Australia's Disability Discrimination Act 1992 and its AS EN 301 549 procurement standard were " +
            "identified, but every attempt to fetch a primary Australian government page (digital.gov.au, " +
            "finance.gov.au, dta.gov.au, humanrights.gov.au) failed this pass -- a pattern consistent with a " +
            "network-level block rather than the pages being unavailable -- and AS EN 301 549 itself is a " +
            "paywalled Standards Australia publication that was not obtained. The exact WCAG version behind " +
            "current Australian government guidance (2.1 vs. 2.2 AA) was not confirmed by a primary source, and " +
            "even the legal tier of the operative instrument (a statute vs. a procurement standard) is " +
            "unconfirmed, so no tier is recorded here.",
            null, null),
        MakeNotYetMapped("India",
            "India's Rights of Persons with Disabilities (Amendment) Rules 2023 reportedly make IS 17802 " +
            "(aligned with WCAG 2.1 AA per secondary sources) mandatory for websites, apps and ICT-based " +
            "products and services -- corroborated by two primary government pages (a PIB press release, and " +
            "DEPwD's own notification listing) -- but the operative gazette notification's own text (a specific " +
            "subdomain that was consistently unreachable this pass) and IS 17802's own text (a Bureau of Indian " +
            "Standards publication) were not read, so the exact requirement and WCAG version are not confirmed " +
            "from a primary source read directly.",
            LegalTier.Statute, "https://www.pib.gov.in/PressReleasePage.aspx?PRID=1942363"),
    ];

    /// <summary>Checked, with nothing jurisdiction-specific found -- see
    /// <see cref="JurisdictionStatusKind.NoRequirementFound"/>. The jurisdiction is still reachable through
    /// the federal/international standards in <see cref="KnownStandards.All"/> (e.g. ADA Title II for a US
    /// state) where those apply.</summary>
    public static IReadOnlyList<JurisdictionStatus> NoRequirementFound { get; } =
    [
        MakeNoRequirement("Alabama",
            "No Alabama statute or administrative rule requiring WCAG/Section 508 conformance for state " +
            "websites or apps was found; only individual agencies' own voluntary accessibility statements " +
            "exist. Not exhaustively confirmed: only a search pass was done, not a full check of the Alabama " +
            "Administrative Code.", null),
        MakeNoRequirement("Oregon",
            "No Oregon-specific statute or administrative rule requiring WCAG conformance was found. The one " +
            "candidate citation found in secondary sources, OAR 125-090, was read directly and is about parking " +
            "facilities, not accessibility. Oregon's own \"Guidance on Accessibility for E-Government Program " +
            "Services\" (read directly) uses only hortatory language (\"should strive to comply\", \"strongly " +
            "recommends\" WCAG 2.1 AA) and states it \"does not apply to web pages outside of the control of " +
            "the State of Oregon\" -- i.e. Oregon's own guidance confirms it is non-binding, not a rule or " +
            "mandate.", "https://www.oregon.gov/eis/shared-services/Documents/eis-ss-guidance-egov-accessibility.pdf"),
        MakeNoRequirement("Wyoming",
            "No dedicated Wyoming statute was found. The Department of Administration and Information has an " +
            "internal \"Web Communication Accessibility Standard\" (WCAG 2.1 AA-aligned), but this is informal " +
            "agency guidance, not law.", "https://ai.wyo.gov/about-us/accessibility"),
        MakeNoRequirement("New Mexico",
            "No enacted New Mexico statute was found. HB 295 (2026), which would have required WCAG 2.1 AA by " +
            "2027-04-01, was postponed indefinitely on 2026-02-17 and did not pass.",
            "https://www.nmlegis.gov/Sessions/26%20Regular/bills/house/HB0295.html"),
        MakeNoRequirement("North Dakota",
            "North Dakota Century Code Chapter 54-59 (Information Technology Department) was read directly in " +
            "full and searched for \"accessib\", \"disabilit\" and \"WCAG\" -- zero matches. The state's own " +
            "\"Digital Accessibility Hub\" is a resource/training portal responding to the federal deadline, " +
            "not an independent North Dakota standard.", "https://ndlegis.gov/cencode/t54c59.pdf"),
        MakeNoRequirement("South Carolina",
            "No dedicated South Carolina statute was found; an advisory ASCIT program recommends WCAG 2.0/2.1 " +
            "A/AA without a central mandate.", null),
        MakeNoRequirement("South Dakota",
            "No South Dakota codified-law provision was found; the Bureau of Information and Telecommunications " +
            "maintains internal web-development standards referencing Section 508/WCAG generically.", null),
        MakeNoRequirement("Tennessee",
            "No Tennessee-specific digital/ICT accessibility statute was found; the Tennessee Public Buildings " +
            "Accessibility Act addresses physical, not digital, accessibility.", null),
        MakeNoRequirement("West Virginia",
            "No unified West Virginia statute or policy was found; agencies appear to self-select WCAG targets " +
            "with no central mandate.", null),
        MakeNoRequirement("Wisconsin",
            "No enacted Wisconsin statute was found (SB 831/AB 904's status is unconfirmed); the University of " +
            "Wisconsin System has its own WCAG 2.1 AA policy, separate from general state government.", null),
        MakeNoRequirement("District of Columbia",
            "The DC.gov Accessibility Policy page, read directly, cites only federal Section 508 -- no WCAG " +
            "version and no DC Official Code section.", null),
    ];
}
