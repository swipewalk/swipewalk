using System.Text;
using Swipewalk.Core.Wcag;

namespace Swipewalk.Core.Standards;

/// <summary>Renders docs/standards.md: the standards table and which checked criteria each includes.</summary>
public static class StandardsMarkdown
{
    public static string Render(IEnumerable<Standard> standards, IEnumerable<WcagCriterion> criteria) =>
        Render(standards, criteria, KnownBeyondWcagClauses.All, KnownJurisdictions.Jurisdictions,
            KnownJurisdictions.ReferencedStandardDiffers, KnownJurisdictions.NotApplicableToApps,
            KnownJurisdictions.NotYetMapped, KnownJurisdictions.NoRequirementFound, StoreGuidance.All);

    public static string Render(IEnumerable<Standard> standards, IEnumerable<WcagCriterion> criteria, IEnumerable<BeyondWcagClause> beyondWcagClauses) =>
        Render(standards, criteria, beyondWcagClauses, KnownJurisdictions.Jurisdictions,
            KnownJurisdictions.ReferencedStandardDiffers, KnownJurisdictions.NotApplicableToApps,
            KnownJurisdictions.NotYetMapped, KnownJurisdictions.NoRequirementFound, StoreGuidance.All);

    public static string Render(
        IEnumerable<Standard> standards, IEnumerable<WcagCriterion> criteria, IEnumerable<BeyondWcagClause> beyondWcagClauses,
        IEnumerable<Standard> jurisdictions, IEnumerable<JurisdictionStatus> referencedStandardDiffers,
        IEnumerable<JurisdictionStatus> notApplicableToApps, IEnumerable<JurisdictionStatus> notYetMapped,
        IEnumerable<JurisdictionStatus> noRequirementFound, IEnumerable<StoreGuidanceNote> storeGuidance)
    {
        var list = standards.ToList();
        var md = new StringBuilder();
        md.AppendLine("# Standards and laws");
        md.AppendLine();
        md.AppendLine("<!-- Generated from src/Swipewalk.Core/Standards/KnownStandards.cs and KnownJurisdictions.cs.");
        md.AppendLine("     Regenerate: dotnet run --project src/Swipewalk.Cli -- standards > docs/standards.md -->");
        md.AppendLine();
        md.AppendLine(KnownStandards.Disclaimer);
        md.AppendLine();
        md.AppendLine("| Standard | Legal tier | Jurisdiction | Based on | Applies to | Checked |");
        md.AppendLine("|---|---|---|---|---|---|");
        foreach (var s in list)
            md.AppendLine($"| [{s.Name}]({s.Source}) (`{s.Id}`) | {s.LegalTier.Display()} | {s.Jurisdiction} | {s.Basis} | {s.AppliesTo} | {s.CheckedOn} |");

        md.AppendLine();
        md.AppendLine($"## Rule sources (ruleset {RuleSources.RulesetVersion})");
        md.AppendLine();
        md.AppendLine("Reports record these versions. Later changes to WCAG, laws or platform guidelines are not reflected until " +
                      "Swipewalk is updated; reports warn when a mapping was last reviewed more than a year earlier.");
        md.AppendLine();
        md.AppendLine("| Source | Version | Mapping reviewed |");
        md.AppendLine("|---|---|---|");
        foreach (var r in RuleSources.All)
            md.AppendLine($"| [{r.Name}]({r.Source}) | {r.Version} | {r.CheckedOn} |");

        md.AppendLine();
        md.AppendLine("## Exceptions and requirements beyond WCAG (not checked)");
        md.AppendLine();
        foreach (var s in list.Where(s => s.BeyondWcag is not null))
            md.AppendLine($"- **{s.Name}:** {s.BeyondWcag}");

        var beyondWcagList = beyondWcagClauses.ToList();
        if (beyondWcagList.Count > 0)
        {
            md.AppendLine();
            md.AppendLine("## Beyond-WCAG clauses");
            md.AppendLine();
            md.AppendLine("Individual clauses behind the summaries above: what each requires, whether it normally applies to a " +
                          "typical native app, and how Swipewalk could check it (or why it can't). Every clause's per-run status " +
                          "in a report never claims the app meets it -- see the report's own \"Beyond WCAG\" section.");
            md.AppendLine();
            md.AppendLine(KnownBeyondWcagClauses.SkippedNote);
            foreach (var s in list)
            {
                var clauses = beyondWcagList.Where(c => c.StandardId == s.Id).ToList();
                if (clauses.Count == 0)
                    continue;
                md.AppendLine();
                md.AppendLine($"### {s.Name}");
                md.AppendLine();
                md.AppendLine("| Clause | Applies | How Swipewalk could check |");
                md.AppendLine("|---|---|---|");
                foreach (var c in clauses)
                    md.AppendLine($"| {c.ClauseLabel} | {Applies(c)} | {CheckMethodText(c)} |");
            }
        }

        var jurisdictionList = jurisdictions.ToList();
        if (jurisdictionList.Count > 0)
        {
            md.AppendLine();
            md.AppendLine("## US states, other countries and similar jurisdictions");
            md.AppendLine();
            md.AppendLine("Not part of the default standards above, so a report never lists every state on every finding. " +
                          "Pass `--standard <id>` to add one of these to a report (its own row then appears in \"Relevance to " +
                          "laws and standards\", and headline counts narrow to it, the same way `--standard` already works for " +
                          "the default standards). Each entry below was checked against an official government source; the " +
                          "citation shows which kind (a statute, regulation, agency policy, or in a few cases an agency's own " +
                          "overview/FAQ page), since they carry different legal weight.");
            md.AppendLine();
            md.AppendLine("| Standard | Legal tier | Jurisdiction | Based on | Applies to | Checked |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var s in jurisdictionList)
                md.AppendLine($"| [{s.Name}]({s.Source}) (`{s.Id}`) | {s.LegalTier.Display()} | {s.Jurisdiction} | {s.Basis} | {s.AppliesTo} | {s.CheckedOn} |");
        }

        AppendJurisdictionStatusSection(md, "Confirmed, but the referenced standard differs from WCAG 2.x (not mapped per finding)",
            "A real, binding instrument was confirmed by reading its primary text, but it references something other than a " +
            "WCAG 2.x version and level Swipewalk can honestly map criterion by criterion -- either WCAG 1.0 (Rhode Island, " +
            "Vermont), or Section 508 cited without a specific version, where whether that picks up the 2017 Revised 508 " +
            "Standards (WCAG 2.0 AA) is a legal reading this project has not settled (only Arkansas's statute is explicitly " +
            "pinned to the pre-2017 technical standards). Listed for information only; no `--standard` id exists for these.",
            referencedStandardDiffers);
        AppendJurisdictionStatusSection(md, "Confirmed, but does not apply to the kind of software Swipewalk scans",
            "A real, binding instrument was confirmed, but its own scope -- read directly -- does not reach native software or " +
            "mobile apps.",
            notApplicableToApps);
        AppendJurisdictionStatusSection(md, "Not yet mapped (primary source not yet verified)",
            "Researched, but not confirmed enough to ship: a blocked or unreadable primary source, or a disputed reading. This " +
            "never means \"no requirement\" -- only that verification isn't finished.",
            notYetMapped);
        AppendJurisdictionStatusSection(md, "No jurisdiction-specific requirement found (search, not exhaustive)",
            "Checked, with nothing jurisdiction-specific found -- most of these are a search pass, not a confirmed negative " +
            "from reading the jurisdiction's full code. The federal/international standards above (for example ADA Title II " +
            "for a US state) still apply where they do.",
            noRequirementFound);

        var storeGuidanceList = storeGuidance.ToList();
        if (storeGuidanceList.Count > 0)
        {
            md.AppendLine();
            md.AppendLine("## Store guidance");
            md.AppendLine();
            md.AppendLine(StoreGuidanceDisclaimer);
            md.AppendLine();
            md.AppendLine("| Store | Guidance | Related criteria | Checked |");
            md.AppendLine("|---|---|---|---|");
            foreach (var n in storeGuidanceList)
                md.AppendLine($"| {n.Store} | [{n.Name}]({n.Source}) -- {Cell(n.Detail)} | {string.Join(", ", n.RelatedCriteria.Select(FormatCriterionNumber))} | {n.CheckedOn} |");
        }

        md.AppendLine();
        md.AppendLine("## Criteria Swipewalk maps findings to");
        md.AppendLine();
        md.AppendLine($"| Criterion | Since | {string.Join(" | ", list.Select(s => s.Id))} |");
        md.AppendLine($"|---|---|{string.Concat(list.Select(_ => "---|"))}");
        foreach (var c in criteria)
            md.AppendLine($"| {c} | WCAG {c.Since.Display()} | {string.Join(" | ", list.Select(s => s.Includes(c) ? "yes" : "—"))} |");
        return md.ToString();
    }

    private const string StoreGuidanceDisclaimer =
        "\"Also relevant to\" / \"related to\" store guidance, never \"will pass review\" or a claim that the store itself " +
        "flagged this finding: neither Apple's App Store Review Guidelines nor Google Play's Developer Program Policy were " +
        "found to have a general accessibility-quality clause. Each note only shows on the platform it applies to (Apple's " +
        "guidance for iOS findings, Google Play's for Android findings). Apple's Accessibility Nutrition Labels are " +
        "self-declared by the developer, not verified by Apple. Google Play's pre-launch report is confirmed to use " +
        "Google's Accessibility Test Framework (the same engine Swipewalk wraps on Android), but its exact check set isn't " +
        "confirmed identical -- a note here says a finding's criterion is related to what that report checks, not that " +
        "Google Play actually flagged it. Even when the finding did come from Swipewalk's own ATF harness (shown " +
        "separately as \"Reported by\"/\"Also reported by\"), Google Play's own ATF version, check set and thresholds are " +
        "unconfirmed, so that still doesn't predict Google Play will flag it.";

    /// <summary>2.3.3 (Animation from Interactions) is WCAG Level AAA, unlike every other criterion Store
    /// guidance relates to here (all AA or A) -- flag it so the table doesn't read as if it were AA too.</summary>
    private static string FormatCriterionNumber(string number) => number == "2.3.3" ? "2.3.3 (AAA)" : number;

    private static void AppendJurisdictionStatusSection(StringBuilder md, string heading, string intro, IEnumerable<JurisdictionStatus> entries)
    {
        var list = entries.ToList();
        if (list.Count == 0)
            return;
        md.AppendLine();
        md.AppendLine($"## {heading}");
        md.AppendLine();
        md.AppendLine(intro);
        md.AppendLine();
        md.AppendLine("| Jurisdiction | Legal tier | Reason | Checked |");
        md.AppendLine("|---|---|---|---|");
        foreach (var e in list)
        {
            var jurisdiction = e.Source is { } source ? $"[{e.Jurisdiction}]({source})" : e.Jurisdiction;
            var tier = e.LegalTier?.Display() ?? "—";
            md.AppendLine($"| {jurisdiction} | {tier} | {Cell(e.Reason)} | {e.CheckedOn} |");
        }
    }

    private static string Applies(BeyondWcagClause c) => c.Applicability switch
    {
        BeyondWcagApplicability.Always => "Yes",
        BeyondWcagApplicability.Conditional => $"Conditional -- only when {c.ConditionDescription}",
        BeyondWcagApplicability.PlatformOrOrganizational => "No (platform, hardware, documentation or support services, not the app)",
        _ => throw new ArgumentOutOfRangeException(nameof(c), c.Applicability, "Unhandled BeyondWcagApplicability."),
    };

    private static string CheckMethodText(BeyondWcagClause c) => c.CheckMethod switch
    {
        BeyondWcagCheckMethod.PartlyAutomated => Cell(c.Explanation),
        BeyondWcagCheckMethod.Guided => Cell(c.GuidedSteps.Count > 0
            ? $"{c.Explanation} {string.Join(" ", c.GuidedSteps.Select((s, i) => $"{i + 1}. {s}"))}"
            : c.Explanation),
        BeyondWcagCheckMethod.NotTestable => Cell($"Not testable by Swipewalk: {c.Explanation}"),
        _ => throw new ArgumentOutOfRangeException(nameof(c), c.CheckMethod, "Unhandled BeyondWcagCheckMethod."),
    };

    /// <summary>Escapes a markdown table cell's pipe characters, so a clause's text (none currently contains
    /// one, but a future entry might) can't break the table layout.</summary>
    private static string Cell(string text) => text.Replace("|", "\\|");
}
