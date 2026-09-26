namespace Swipewalk.Core.Standards;

/// <summary>Whether a beyond-WCAG clause normally applies to a typical native mobile app under scan.</summary>
public enum BeyondWcagApplicability
{
    /// <summary>Applies to essentially every scanned app (not conditional on a feature the app may lack).</summary>
    Always,

    /// <summary>Applies only when the app has a specific feature (see <see cref="BeyondWcagClause.ConditionDescription"/>),
    /// e.g. two-way voice calling, video playback, biometric sign-in or content authoring.</summary>
    Conditional,

    /// <summary>Binds the OS platform or its hardware, a vendor's separate documentation or its support
    /// services, not the app's own UI -- never testable by scanning a running app.</summary>
    PlatformOrOrganizational,
}

/// <summary>The check method a clause would use once its applicability condition (if any) is confirmed to hold.</summary>
public enum BeyondWcagCheckMethod
{
    /// <summary>An existing Swipewalk signal covers part of the clause; never "automated" alone -- see
    /// <see cref="Coverage.BeyondWcagRunStatus.PartlyAutomated"/>. For a <see cref="BeyondWcagApplicability.Conditional"/>
    /// clause, this describes a check Swipewalk could add in future once the condition is confirmed to hold --
    /// no such check runs today (see <see cref="KnownBeyondWcagClauses.CouldBeCheckedPrefix"/>).</summary>
    PartlyAutomated,

    /// <summary>Needs a person to observe or operate the app (1-3 steps; see <see cref="BeyondWcagClause.GuidedSteps"/>).</summary>
    Guided,

    /// <summary>Out of scope for a running-app scanner (documentation content, platform/OS/hardware behaviour,
    /// or requires source/company evidence Swipewalk cannot see).</summary>
    NotTestable,
}

/// <summary>Which existing per-run signal a <see cref="BeyondWcagCheckMethod.PartlyAutomated"/>, <see
/// cref="BeyondWcagApplicability.Always"/> clause reuses. No new rule is added for these: the signal already
/// exists for a WCAG criterion or a rescan, and reporting it here under a standard's own clause number is a
/// mapping exercise. See <see cref="Coverage.BeyondWcagCoverageExtensions"/> for how each is checked.</summary>
public enum BeyondWcagEvidenceSignal
{
    None,

    /// <summary>At least one screen was scanned, so Swipewalk's missing-name, identifier-name and
    /// label-in-name rules (which always run; see <c>ScreenActivityBuilder.AlwaysRun</c>) read each
    /// <c>AccessibilityNode</c>'s role, name/label, value, state description, and whether it is enabled,
    /// interactive, focusable, scrollable or a heading -- the same signal WCAG 4.1.2 (Name, Role, Value) and
    /// 2.5.3 (Label in Name) already use. This does not cover parent-child hierarchy checks, available
    /// actions, or WCAG 1.3.1 structure: no rule reads those today.</summary>
    TreeRolesAndNames,

    /// <summary>The large-text rescan (font size, <c>--large-text</c>) or the dark/light appearance rescan
    /// (colour, <c>--appearance both</c>) ran on at least one screen this run.</summary>
    UserPreferenceRescan,
}

/// <summary>
/// One requirement a standard adds beyond what it references from WCAG, for native mobile apps. Modeled
/// separately from <see cref="Standard.BeyondWcag"/> (which stays a one-sentence summary) so every such clause
/// gets its own honest per-run status -- see <see cref="Coverage.BeyondWcagCoverageReport"/>. Never a compliance
/// verdict: no clause here is ever given a pass-or-fail verdict.
/// </summary>
/// <param name="StandardId">The <see cref="Standard.Id"/> this clause belongs to.</param>
/// <param name="ClauseNumber">The clause number(s) as the source numbers them, e.g. "502.3.1-502.3.14" for a
/// group of sub-clauses that share one applicability, check method and explanation.</param>
/// <param name="Title">The clause's title, exactly as given in the source.</param>
/// <param name="Summary">A one- or two-sentence plain-language summary of what the clause requires. Never a
/// compliance verdict.</param>
/// <param name="Applicability">Whether this normally applies to a typical scanned app.</param>
/// <param name="ConditionDescription">For <see cref="BeyondWcagApplicability.Conditional"/>: the feature that
/// makes it apply (e.g. "the app plays video with synchronized audio"). Null otherwise.</param>
/// <param name="CheckMethod">How this clause would be checked once applicable.</param>
/// <param name="Explanation">How Swipewalk could check it (for <see cref="BeyondWcagCheckMethod.PartlyAutomated"/>),
/// or why it can't be checked automatically (for <see cref="BeyondWcagCheckMethod.NotTestable"/>), or context for
/// a guided check. Never a compliance verdict. For a <see cref="BeyondWcagApplicability.Conditional"/> clause
/// whose <see cref="CheckMethod"/> is <see cref="BeyondWcagCheckMethod.PartlyAutomated"/>, this starts with
/// <see cref="KnownBeyondWcagClauses.CouldBeCheckedPrefix"/> since no such check exists yet -- it only runs once
/// the condition is confirmed, which nothing detects today.</param>
/// <param name="GuidedSteps">1-3 plain steps for a person to follow, for <see cref="BeyondWcagCheckMethod.Guided"/>
/// (and, informationally, the guided part of some conditional <see cref="BeyondWcagCheckMethod.PartlyAutomated"/>
/// clauses). Empty otherwise.</param>
/// <param name="Evidence">For <see cref="BeyondWcagApplicability.Always"/> + <see cref="BeyondWcagCheckMethod.PartlyAutomated"/>:
/// which per-run signal decides whether this clause ran this run. <see cref="BeyondWcagEvidenceSignal.None"/> otherwise.</param>
/// <param name="Source">Primary source URL, read directly (not a secondary summary).</param>
/// <param name="CheckedOn">When this entry was last checked against the source (yyyy-MM).</param>
public sealed record BeyondWcagClause(
    string StandardId,
    string ClauseNumber,
    string Title,
    string Summary,
    BeyondWcagApplicability Applicability,
    string? ConditionDescription,
    BeyondWcagCheckMethod CheckMethod,
    string Explanation,
    IReadOnlyList<string> GuidedSteps,
    BeyondWcagEvidenceSignal Evidence,
    string Source,
    string CheckedOn)
{
    public string ClauseLabel => $"{ClauseNumber} {Title}";
}

/// <summary>
/// Requirements Section 508 and EN 301 549 add beyond what they reference from WCAG, for native mobile apps.
/// Every entry's clause number, title, condition and applicability were checked directly against the primary
/// source text (not a secondary summary) on the <see cref="BeyondWcagClause.CheckedOn"/> date; see
/// <see cref="SkippedNote"/> for what was deliberately left out and why.
/// </summary>
public static class KnownBeyondWcagClauses
{
    /// <summary>Prefix for a <see cref="BeyondWcagApplicability.Conditional"/>, <see
    /// cref="BeyondWcagCheckMethod.PartlyAutomated"/> clause's <see cref="BeyondWcagClause.Explanation"/>: no
    /// such check exists in Swipewalk today (there is no detector for the triggering feature, e.g. video or
    /// call controls), so this describes a possible future check, not something that runs now.</summary>
    public const string CouldBeCheckedPrefix = "Could be partly checked in future by";

    /// <summary>
    /// What this catalog deliberately does not include, and why -- so the gap is documented rather than silent.
    /// </summary>
    public const string SkippedNote =
        "Not included: EN 301 549 v4.1.1's clause 6 (ICT with two-way voice communication) is replaced by a " +
        "renumbered \"ICT supporting real-time bidirectional communication\" (confirmed by reading the v4.1.1 " +
        "text directly: new 6.0 scenarios, 6.1 real-time bidirectional voice communication, 6.2 Real-Time Text " +
        "expanded to 6.2.1-6.2.10, and a new 6.7 Total Conversation) -- see EN 301 549 v3.2.1's clause 6 entries " +
        "for the structure this replaces, and confirm which version your contract or regulator references. " +
        "Clause 5.1 (closed functionality), other than the new 5.1.8 (catalogued below), is reworded and " +
        "retitled in places rather than restructured: 5.1.2.2 becomes \"Assistive technology and closed " +
        "functionality\" (was \"Assistive technology\"), 5.1.3.1/.6/.7 become \"Auditory output...\" (were \"Audio " +
        "output...\"/\"Speech output...\"), and 5.1.6.1 becomes \"Functionality closed to keyboards\" (was " +
        "\"Closed functionality\") -- see EN 301 549 v3.2.1's 5.1 for the prior titles. Clause 11.5.1 (closed " +
        "functionality exemption) is now informative only (no longer a requirement) in v4.1.1, so it has no " +
        "v4.1.1 entry here; see EN 301 549 v3.2.1's 11.5.1 for the prior (binding) version. Clauses 5.7 (key " +
        "repeat) and 5.8 (double-strike key acceptance) are narrowed in v4.1.1 rather than unchanged: 5.7 now " +
        "applies only where the repeat would generate multiple entries of the same alphanumeric data, and 5.8 " +
        "now applies only where ICT includes a keyboard, keypad or device-independent keyboard interface service " +
        "and already has a capability to adjust the time before the same key is accepted again -- both stay " +
        "platform/OS keyboard requirements either way, so not individually re-catalogued for v4.1.1; see EN 301 " +
        "549 v3.2.1's 5.7/5.8. Clauses 5.2, 5.4, 5.9, 11.5.2.4-11.5.2.17 and 11.6.1 differ mainly in v4.1.1's " +
        "reworded \"Where ICT includes...\" / \"Where ICT is, or includes, ...\" preconditions and are catalogued " +
        "below for v4.1.1 too, reusing v3.2.1's wording (one narrower exception: 11.5.2.10's \"text attributes\" " +
        "are limited in v4.1.1 to those \"used or available for user generated content\", noted on that entry).";

    private const string TreeRolesAndNamesExplanation =
        "Swipewalk's missing-name, identifier-name and label-in-name rules (which check every scanned screen) " +
        "already read each element's role, name and label from the accessibility tree -- the same data WCAG " +
        "4.1.2 (Name, Role, Value) and 2.5.3 (Label in Name) use. Reporting that under this clause number is a " +
        "mapping exercise, not a new check. Other parts of this clause (parent-child hierarchy, the list of " +
        "available actions, and whether assistive technology can execute or modify them) are not read by any " +
        "rule today and need a person to check with TalkBack/VoiceOver.";

    private const string UseOfAccessibilityServicesExplanation =
        "Swipewalk reads the scanned screens through the same accessibility API TalkBack/VoiceOver use; review " +
        "the captured tree in this report -- an empty or generic tree (e.g. a raw drawing surface) would be " +
        "evidence against this clause.";

    private const string UserPreferenceExplanation =
        "Swipewalk's large-text rescan (font size) and dark/light appearance rescan (colour) can each show " +
        "whether the app follows one platform user-preference setting on the scanned screens; only the " +
        "rescan(s) named above ran this run. Contrast, font type and focus-cursor following are not checked; a " +
        "person should confirm those, and any preference whose rescan didn't run.";

    private const string Section508Source = "https://www.access-board.gov/ict/";
    private const string En301549V3Source = "https://www.etsi.org/deliver/etsi_en/301500_301599/301549/03.02.01_60/en_301549v030201p.pdf";
    private const string En301549V4Source = "https://www.etsi.org/deliver/etsi_en/301500_301599/301549/04.01.01_60/en_301549v040101p.pdf";
    private const string CheckedOn = "2026-09";

    private static readonly string[] KeepsWorkingSteps =
    [
        "Turn on TalkBack (Android), VoiceOver (iOS) or a system magnifier.",
        "Use the app normally with it on.",
        "Confirm it keeps working as expected, rather than being turned off or degraded by the app.",
    ];

    private static BeyondWcagClause Always(
        string standardId, string number, string title, string summary,
        BeyondWcagCheckMethod method, string explanation, BeyondWcagEvidenceSignal evidence, string source) =>
        new(standardId, number, title, summary, BeyondWcagApplicability.Always, null, method, explanation, [], evidence, source, CheckedOn);

    private static BeyondWcagClause AlwaysGuided(
        string standardId, string number, string title, string summary, string explanation, string[] steps, string source) =>
        new(standardId, number, title, summary, BeyondWcagApplicability.Always, null, BeyondWcagCheckMethod.Guided, explanation, steps, BeyondWcagEvidenceSignal.None, source, CheckedOn);

    private static BeyondWcagClause NotTestable(
        string standardId, string number, string title, string summary, string explanation, string source) =>
        new(standardId, number, title, summary, BeyondWcagApplicability.PlatformOrOrganizational, null, BeyondWcagCheckMethod.NotTestable, explanation, [], BeyondWcagEvidenceSignal.None, source, CheckedOn);

    private static BeyondWcagClause Conditional(
        string standardId, string number, string title, string summary, string condition,
        BeyondWcagCheckMethod method, string explanation, string[] steps, string source) =>
        new(standardId, number, title, summary, BeyondWcagApplicability.Conditional, condition, method, explanation, steps, BeyondWcagEvidenceSignal.None, source, CheckedOn);

    public static IReadOnlyList<BeyondWcagClause> All { get; } =
    [
        // ---- Section 508, Chapter 5 -- Software (36 CFR Part 1194, Appendix C) ----
        AlwaysGuided("section-508", "502.1", "General",
            "Software must interoperate with assistive technology (with an exception for closed-functionality ICT).",
            "Every native app relies on the OS accessibility API to be usable with TalkBack/VoiceOver. A near-empty " +
            "or unlabeled-everywhere accessibility tree is a weak automated signal of a problem here, but full " +
            "interoperability needs a person to confirm it with a screen reader.",
            ["Turn on TalkBack (Android) or VoiceOver (iOS).",
             "Reach and operate every control on the screen using only the screen reader.",
             "Confirm each control is announced with a usable name and responds as expected."],
            Section508Source),
        Conditional("section-508", "502.2.1", "User Control of Accessibility Features",
            "Platform software must provide user control over platform features defined in platform documentation as accessibility features.",
            "the app itself is platform software that documents its own accessibility features (rare for a typical app; this targets OS/platform vendors)",
            BeyondWcagCheckMethod.Guided,
            "Not testable automatically for a typical app, since it is not platform software.",
            ["If the app documents its own accessibility settings screen, confirm each documented feature has a working control."],
            Section508Source),
        AlwaysGuided("section-508", "502.2.2", "No Disruption of Accessibility Features",
            "Software must not disrupt platform features defined in platform documentation as accessibility features.",
            "Not reliably detectable from the accessibility tree alone: disruption could be visual, behavioural or " +
            "affect a feature (like a screen reader or magnifier) the tree doesn't expose.",
            KeepsWorkingSteps, Section508Source),
        Always("section-508", "502.3.1-502.3.14", "Accessibility Services",
            "The app's UI elements must expose role, name, state, value, relationships, text and available " +
            "actions programmatically, and let assistive technology read and operate them. (502.3's own lead-in " +
            "sentence, which is a platform vendor's obligation to provide the services in the first place, is not " +
            "evaluated here; only the generic 502.3.1-502.3.14 requirements that follow are.)",
            BeyondWcagCheckMethod.PartlyAutomated, TreeRolesAndNamesExplanation, BeyondWcagEvidenceSignal.TreeRolesAndNames, Section508Source),
        NotTestable("section-508", "502.4", "Platform Accessibility Features",
            "The platform must conform to specific ANSI/HFES 200.2 requirements, including sequential entry of " +
            "multiple (chorded) keystrokes, key-delay and double-strike adjustment, visual alternatives to audio, " +
            "synchronized audio/visual events, speech output and displaying provided captions.",
            "Binds the OS vendor (Android/iOS/Windows), not the app under test -- out of scope for a per-app scan.",
            Section508Source),
        Always("section-508", "503.1", "General",
            "Applications must conform to 503.2 through 503.4 below.",
            BeyondWcagCheckMethod.NotTestable, "Umbrella clause with no independent requirement; see 503.2-503.4.2 below.",
            BeyondWcagEvidenceSignal.None, Section508Source),
        Always("section-508", "503.2", "User Preferences",
            "Apps must permit user preferences from platform settings for colour, contrast, font type, font size " +
            "and focus cursor (apps designed to be isolated from their underlying platform software, including " +
            "web apps, are excepted).",
            BeyondWcagCheckMethod.PartlyAutomated, UserPreferenceExplanation, BeyondWcagEvidenceSignal.UserPreferenceRescan, Section508Source),
        Conditional("section-508", "503.3", "Alternative User Interfaces",
            "Where an app provides an alternative user interface that functions as assistive technology, it must " +
            "use platform and other industry-standard accessibility services.",
            "the app's function is itself to act as assistive technology (rare)",
            BeyondWcagCheckMethod.Guided, "Rare for a typical app; decide from the app's features.",
            ["Confirm the app registers as a platform accessibility service rather than a custom overlay."],
            Section508Source),
        Conditional("section-508", "503.4.1", "Caption Controls",
            "Where user controls are provided for volume adjustment, the app must provide user controls for " +
            "selecting captions at the same menu level as the user controls for volume or program selection.",
            "the app displays video with synchronized audio and provides user controls for volume adjustment",
            BeyondWcagCheckMethod.PartlyAutomated,
            $"{CouldBeCheckedPrefix} looking for a media-player-like control set (play/pause plus volume) alongside " +
            "a captions/CC control at the same menu level -- a presence check only, not confirmation of true equivalence.",
            ["Confirm a captions on/off control is offered at the same menu level as the volume control.",
             "Turn captions on and confirm they display."],
            Section508Source),
        Conditional("section-508", "503.4.2", "Audio Description Controls",
            "Where user controls are provided for program selection, the app must provide user controls for " +
            "selecting audio descriptions at the same menu level as the user controls for volume or program selection.",
            "the app displays video with synchronized audio and provides user controls for program selection",
            BeyondWcagCheckMethod.PartlyAutomated,
            $"{CouldBeCheckedPrefix} looking for an audio-description toggle near program selection -- a presence " +
            "check only, not confirmation of true equivalence.",
            ["Confirm an audio-description control is offered at the same menu level as program selection.",
             "Turn it on and confirm description audio plays."],
            Section508Source),
        Conditional("section-508", "504.1-504.4", "Authoring Tools",
            "Where an app is an authoring tool, it must support creating and editing content that conforms to " +
            "WCAG 2.0 A/AA (except when used to directly edit plain-text source code), preserve accessibility " +
            "information across format conversion (and, for a tool that exports PDF, also be able to export " +
            "PDF/UA-1), prompt authors to create conforming content, and offer conforming templates -- to the " +
            "extent the destination format supports it.",
            "the app is itself an authoring/content-creation tool (e.g. a document editor, CMS or form builder)",
            BeyondWcagCheckMethod.Guided,
            "Requires judging the semantic quality of user-authored content, not the app's own tree -- not testable automatically.",
            ["If the app is a content-authoring tool, check whether a template it offers produces accessible output.",
             "Check whether it flags or helps repair accessibility problems in content the user creates."],
            Section508Source),

        // ---- Section 508, Chapter 6 -- Support Documentation and Services ----
        NotTestable("section-508", "601.1", "Scope",
            "States that Chapter 6 requirements apply where required by 508/255 scoping or referenced elsewhere in the Revised 508 Standards.",
            "A scope statement, not an independent requirement.", Section508Source),
        NotTestable("section-508", "602.1", "General",
            "Documentation supporting the use of ICT must conform to 602.",
            "About the vendor's documentation (manuals, help sites), not the app -- a running-app scan does not evaluate external documentation.",
            Section508Source),
        NotTestable("section-508", "602.2", "Accessibility and Compatibility Features",
            "Documentation must list and explain the built-in accessibility features and assistive-technology compatibility features required by Chapters 4 and 5.",
            "About the vendor's support documentation as a whole, not just any in-app help screens -- out of scope for a running-app scan; check any in-app help by hand if the app has one.",
            Section508Source),
        NotTestable("section-508", "602.3", "Electronic Support Documentation",
            "Documentation in electronic format (including web self-service support) must meet WCAG 2.0 A/AA.",
            "A website/document conformance requirement, a different scan target from a native-app scan.", Section508Source),
        NotTestable("section-508", "602.4", "Alternate Formats for Non-Electronic Support Documentation",
            "Where support documentation is only provided in non-electronic formats, alternate formats usable by people with disabilities must be provided on request.",
            "A process/policy requirement about the vendor's documentation formats, with no runtime signal.", Section508Source),
        NotTestable("section-508", "603.1", "General",
            "ICT support services (help desks, call centers, training, automated self-service) must conform to 603.",
            "Services outside the app.", Section508Source),
        NotTestable("section-508", "603.2", "Information on Accessibility and Compatibility Features",
            "Support services must include information on the accessibility and compatibility features required by 602.2.",
            "A staffing/process requirement, not app behaviour.", Section508Source),
        NotTestable("section-508", "603.3", "Accommodation of Communication Needs",
            "Support services must accommodate communication needs of people with disabilities.",
            "A staffing/process requirement, not app behaviour.", Section508Source),

        // ---- EN 301 549 v3.2.1, Clause 5 -- Generic requirements ----
        Conditional("en-301-549", "5.1", "Closed functionality",
            "A set of requirements (non-visual access, speech output, masked entry, volume, etc.) that only bind " +
            "ICT whose functionality is closed to assistive technology (e.g. a kiosk with no AT support).",
            "the app deliberately locks out assistive technology (closed functionality) rather than relying on the platform's open, AT-compatible UI toolkit",
            BeyondWcagCheckMethod.Guided,
            "Rare for a typical app; decide from the app's features.",
            ["If the app deliberately locks out assistive technology, check it against 5.1.3-5.1.7."],
            En301549V3Source),
        Conditional("en-301-549", "5.2", "Activation of accessibility features",
            "Where ICT includes documented accessibility features, it must be possible to activate the ones needed for a specific need without relying on a method that doesn't support that need.",
            "the app has its own documented accessibility feature (e.g. an in-app large-text or high-contrast toggle) rather than relying solely on the OS",
            BeyondWcagCheckMethod.Guided, "Not testable automatically.",
            ["If such a toggle exists, confirm it can be reached and activated by someone who needs it (e.g. without fine motor control or without vision), not just visually."],
            En301549V3Source),
        Conditional("en-301-549", "5.3", "Biometrics",
            "Where ICT uses biological characteristics, it must not rely on a particular biological characteristic as the only means of identification or control.",
            "the app offers biometric sign-in (Face ID, fingerprint) as a control method",
            BeyondWcagCheckMethod.Guided, "Not automatable from the accessibility tree alone; requires trying the sign-in flow.",
            ["Open the sign-in/authentication screen.",
             "Confirm another means (a non-biometric one, or under v3.2.1 a different biometric) is offered alongside it."],
            En301549V3Source),
        Conditional("en-301-549", "5.4", "Preservation of accessibility information during conversion",
            "Where ICT converts information or communication, it must preserve accessibility information to the extent the destination format supports it.",
            "the app converts or exports content (documents, media) as part of its function",
            BeyondWcagCheckMethod.Guided, "Not testable automatically.",
            ["Export or convert a piece of content.", "Compare accessibility metadata (e.g. alt text, captions) before and after conversion."],
            En301549V3Source),
        NotTestable("en-301-549", "5.5.1", "Means of operation",
            "Operable parts needing grasping, pinching or twisting need an alternative that doesn't require that.",
            "Targets physical hardware controls; a touchscreen app's on-screen buttons are usually tapped, not grasped or pinched -- a hardware design requirement, not an app-level one.",
            En301549V3Source),
        NotTestable("en-301-549", "5.5.2", "Operable parts discernibility",
            "Operable parts must be discernible without vision and without operating them (e.g. tactilely).",
            "Mainly a hardware requirement (physical buttons, screen edges); for software, on-screen discernibility overlaps Swipewalk's own WCAG 4.1.2/2.5.8 checks rather than needing an independent one.",
            En301549V3Source),
        Conditional("en-301-549", "5.6.1", "Tactile or auditory status",
            "Where a locking/toggle control's status is visually presented, the ICT must offer a way to determine that status through touch or sound as well, without operating the control.",
            "the app has a locking or toggle control whose status is shown visually",
            BeyondWcagCheckMethod.PartlyAutomated,
            $"{CouldBeCheckedPrefix} checking whether such a control's on/off state is also exposed " +
            "programmatically (so a screen reader can announce it) -- the same signal WCAG 4.1.2 already reads.",
            ["If the app has a toggle/switch, confirm its state is announced by TalkBack/VoiceOver, not shown only visually."],
            En301549V3Source),
        Conditional("en-301-549", "5.6.2", "Visual status",
            "Where a locking/toggle control's status is presented non-visually, the ICT must offer a way to determine that status visually as well, when the control is presented.",
            "the app has a locking or toggle control whose status is shown non-visually",
            BeyondWcagCheckMethod.Guided,
            "Not automatable from the tree alone: it needs someone to look at the control while it's presented.",
            ["If the app has a control whose status is announced but not shown visually, confirm a visual status indicator is also presented."],
            En301549V3Source),
        NotTestable("en-301-549", "5.7", "Key repeat",
            "If a key-repeat function can't be turned off, its delay and rate must be adjustable.",
            "Mobile soft keyboards are supplied by the OS, not the app -- binds the platform/keyboard, not app software.",
            En301549V3Source),
        NotTestable("en-301-549", "5.8", "Double-strike key acceptance",
            "Adjustable delay to avoid double key-presses being rejected.",
            "Same reasoning as 5.7: OS keyboard/input responsibility.", En301549V3Source),
        Conditional("en-301-549", "5.9", "Simultaneous user actions",
            "If an action requires simultaneous inputs (e.g. a two-finger gesture), an alternative that needs only one action at a time must exist.",
            "the app relies on a multi-touch gesture (pinch-zoom, two-finger swipe) as the only way to do something",
            BeyondWcagCheckMethod.Guided, "Not reliably automatable: the accessibility tree doesn't record gesture handlers.",
            ["Identify gestures used by the app during a walkthrough.", "Confirm a single-action alternative (e.g. zoom buttons) exists for any multi-touch-only gesture."],
            En301549V3Source),

        // ---- EN 301 549 v3.2.1, Clause 6 -- ICT with two-way voice communication (conditional: calling apps only) ----
        Conditional("en-301-549", "6.1", "Audio bandwidth for speech",
            "Where the app provides two-way voice communication, it must be able to encode and decode audio with an upper frequency limit of at least 7000 Hz for good audio quality.",
            "the app offers two-way voice communication",
            BeyondWcagCheckMethod.NotTestable, "Requires audio-signal measurement of the call path, not accessibility-tree/screenshot data.", [],
            En301549V3Source),
        Conditional("en-301-549", "6.2.1-6.2.4", "Real-Time Text (RTT) functionality",
            "Where the app provides two-way voice communication, it must also provide two-way RTT (text sent as " +
            "it is typed, so the conversation feels continuous; each unit of text -- usually a character -- must " +
            "be transmitted within 500 ms of being entered), unless that would need new input/output hardware.",
            "the app offers two-way voice communication",
            BeyondWcagCheckMethod.PartlyAutomated,
            $"{CouldBeCheckedPrefix} looking for a text-entry control alongside a call UI -- a presence check only.",
            ["Confirm text is sent live during a call (within about half a second of typing it), not only after pressing Enter.",
             "Confirm it interoperates with a standard RTT endpoint."],
            En301549V3Source),
        Conditional("en-301-549", "6.3", "Caller ID",
            "Where the app provides caller identification or similar telecommunications functions, it must be available in text form as well as being programmatically determinable, unless the functionality is closed.",
            "the app provides caller identification or similar telecommunications functions",
            BeyondWcagCheckMethod.Guided, "Not automatable from the tree alone.",
            ["Check the incoming-call screen shows caller ID as text, and that it is also exposed programmatically (e.g. to a screen reader), not only as an audio tone."],
            En301549V3Source),
        Conditional("en-301-549", "6.4", "Alternatives to voice-based services",
            "Where the app provides real-time voice communication and also voicemail, auto-attendant or interactive voice response, it must offer a way to access that information and those tasks without hearing or speech.",
            "the app offers real-time voice communication and also voicemail, an auto-attendant or interactive voice response",
            BeyondWcagCheckMethod.Guided, "Not automatable from the tree alone.",
            ["Check whether a non-audio path (text or RTT) exists for the voicemail/auto-attendant/IVR facility."],
            En301549V3Source),
        Conditional("en-301-549", "6.5.2-6.5.4", "Video communication (resolution, frame rate, synchronization)",
            "Video-calling performance requirements (resolution, frame rate, audio/video sync) to support sign-language communication.",
            "the app offers two-way voice communication that includes real-time video",
            BeyondWcagCheckMethod.NotTestable, "Video/audio quality metrics, not accessibility-tree data.", [],
            En301549V3Source),
        Conditional("en-301-549", "6.5.5-6.5.6", "Video communication (visual indicator of audio, speaker identification)",
            "A real-time visual indicator of audio activity, and -- where the app identifies speakers for voice " +
            "users -- a means of speaker identification for signing/sign-language users once the start of " +
            "signing has been indicated.",
            "the app offers two-way voice communication that includes real-time video",
            BeyondWcagCheckMethod.Guided,
            "These are visible UI features (an on/off indicator, a speaker-identification display), not quality metrics -- a person can check for them, unlike the rest of clause 6.5.",
            ["Start a video call and confirm a visual indicator shows audio activity.",
             "If the app identifies the current speaker for voice users, confirm it also identifies a signing participant once the start of signing has been indicated."],
            En301549V3Source),
        Conditional("en-301-549", "6.6", "Alternatives to video-based services",
            "Where the app provides real-time video communication and also answering machine, auto-attendant or interactive response facilities, it should offer a way to access that information and those tasks without hearing, speech or vision (a \"should\", not a \"shall\").",
            "the app offers real-time video communication and also an answering machine, auto-attendant or interactive response facility",
            BeyondWcagCheckMethod.Guided, "Not automatable from the tree alone.",
            ["Check whether a non-video, non-audio-only path exists for the facility."],
            En301549V3Source),

        // ---- EN 301 549 v3.2.1, Clause 7 -- ICT with video capabilities (conditional: video-with-audio apps only) ----
        Conditional("en-301-549", "7.1.1", "Captioning playback",
            "Where the app displays video with synchronized audio, it must have a mode to display available captions, and let the user choose to display closed captions provided as part of the content.",
            "the app displays video with synchronized audio",
            BeyondWcagCheckMethod.PartlyAutomated, $"{CouldBeCheckedPrefix} looking for a captions/CC control in the media player's UI -- a presence check only.",
            ["Turn captions on and confirm they display."], En301549V3Source),
        Conditional("en-301-549", "7.1.2", "Captioning synchronization",
            "Where the app displays captions, they must stay in sync: within 100 ms of the caption's time stamp " +
            "(recorded material) or of the caption becoming available to the player (live).",
            "the app displays captions",
            BeyondWcagCheckMethod.NotTestable, "Needs frame-accurate timing measurement, not tree/screenshot data.", [],
            En301549V3Source),
        Conditional("en-301-549", "7.1.3", "Preservation of captioning",
            "Where the app transmits, converts or records video with synchronized audio, caption data must survive so it can still be displayed.",
            "the app transmits, converts or records video with synchronized audio",
            BeyondWcagCheckMethod.NotTestable, "Requires inspecting the app's internal media pipeline.", [],
            En301549V3Source),
        Conditional("en-301-549", "7.1.4", "Captions characteristics",
            "Where the app displays captions, the user must be able to adapt their display (colour, size, etc.), unless captions are unmodifiable (e.g. bitmap-image subtitles).",
            "the app displays captions",
            BeyondWcagCheckMethod.Guided, "Not automatable from the tree alone.",
            ["Check for a captions-appearance settings screen."], En301549V3Source),
        Conditional("en-301-549", "7.1.5", "Spoken subtitles",
            "Where the app displays video with synchronized audio, it must have a mode that speaks the available " +
            "captions aloud, unless the caption text isn't programmatically determinable (e.g. bitmap captions).",
            "the app displays video with synchronized audio",
            BeyondWcagCheckMethod.Guided, "Not automatable from the tree alone.",
            ["Check for a spoken-subtitles option."], En301549V3Source),
        Conditional("en-301-549", "7.2.1", "Audio description playback",
            "The app must let the user select and play an audio-description track.",
            "the app displays video with synchronized audio",
            BeyondWcagCheckMethod.PartlyAutomated, $"{CouldBeCheckedPrefix} looking for an audio-description toggle or track-selection control -- a presence check only.",
            ["Turn it on and confirm it plays audibly."], En301549V3Source),
        Conditional("en-301-549", "7.2.2", "Audio description synchronization",
            "Where the app has a mechanism to play audio description, it must stay in sync with the video.",
            "the app has a mechanism to play audio description",
            BeyondWcagCheckMethod.NotTestable, "Timing measurement, not tree/screenshot data.", [],
            En301549V3Source),
        Conditional("en-301-549", "7.2.3", "Preservation of audio description",
            "Where the app transmits, converts or records video with synchronized audio, audio-description data must survive.",
            "the app transmits, converts or records video with synchronized audio",
            BeyondWcagCheckMethod.NotTestable, "Requires inspecting the app's internal media pipeline.", [],
            En301549V3Source),
        Conditional("en-301-549", "7.3", "User controls for captions and audio description",
            "Where the app primarily displays video with associated audio content, caption/audio-description controls must be at the same interaction level (number of steps) as the primary media controls.",
            "the app primarily displays video with associated audio content",
            BeyondWcagCheckMethod.PartlyAutomated,
            $"{CouldBeCheckedPrefix} comparing the number of taps/menu depth to reach captions vs. the primary media controls, from the captured tree -- a partial signal, not confirmation of equivalence.",
            ["Confirm the caption/audio-description control needs the same number of steps as the primary media controls (e.g. play/pause, volume)."],
            En301549V3Source),

        // ---- EN 301 549 v3.2.1, Clause 11 -- non-WCAG sub-clauses ----
        Conditional("en-301-549", "11.5.1", "Closed functionality",
            "An exemption: software whose closed functionality already conforms to clause 5.1 need not also conform to 11.5.2.",
            "the app is closed to assistive technology (see clause 5.1)",
            BeyondWcagCheckMethod.Guided, "Rare for a typical app; decide from the app's features (see clause 5.1).", [],
            En301549V3Source),
        NotTestable("en-301-549", "11.5.2.1-11.5.2.2", "Platform accessibility service support",
            "The platform (OS) must offer documented accessibility services that let apps and assistive technology interoperate.",
            "Binds the OS/platform vendor (Android, iOS), not the app under test.", En301549V3Source),
        Always("en-301-549", "11.5.2.3", "Use of accessibility services",
            "The app must use the platform's documented accessibility services, not a custom, non-interoperable mechanism.",
            BeyondWcagCheckMethod.PartlyAutomated, UseOfAccessibilityServicesExplanation, BeyondWcagEvidenceSignal.TreeRolesAndNames, En301549V3Source),
        Conditional("en-301-549", "11.5.2.4", "Assistive technology",
            "If the app itself is assistive technology, it must use the documented platform services.",
            "the app is itself assistive technology (rare)",
            BeyondWcagCheckMethod.Guided, "Rare for a typical app; decide from the app's features.",
            ["Confirm the app uses documented platform accessibility services rather than a custom mechanism."],
            En301549V3Source),
        Always("en-301-549", "11.5.2.5-11.5.2.17", "Object info, relationships, actions, focus and change notification",
            "Programmatic exposure and modifiability of UI semantics -- the same content as Section 508's 502.3.x " +
            "(object info, row/column/headers, values, label relationships, parent-child, text, list of actions, " +
            "execution of actions, focus tracking, focus modification, change notification, state/property " +
            "modification, value/text modification).",
            BeyondWcagCheckMethod.PartlyAutomated, TreeRolesAndNamesExplanation, BeyondWcagEvidenceSignal.TreeRolesAndNames, En301549V3Source),
        NotTestable("en-301-549", "11.6.1", "User control of accessibility features",
            "Where the software is a platform, it must let users control the platform's accessibility features.",
            "Binds platform software, not a typical app.", En301549V3Source),
        AlwaysGuided("en-301-549", "11.6.2", "No disruption of accessibility features",
            "The app must not disrupt documented platform accessibility features, except when the user asks it to while using the app.",
            "Not reliably detectable from the accessibility tree alone: disruption could be visual, behavioural or " +
            "affect a feature (like a screen reader or magnifier) the tree doesn't expose.",
            KeepsWorkingSteps, En301549V3Source),
        Always("en-301-549", "11.7", "User preferences",
            "The app's UI must follow the platform's user-preference settings (units of measurement, colour, " +
            "contrast, font type, font size, focus cursor) unless the user overrides them, unless the app is " +
            "isolated from the platform.",
            BeyondWcagCheckMethod.PartlyAutomated, UserPreferenceExplanation, BeyondWcagEvidenceSignal.UserPreferenceRescan, En301549V3Source),
        Conditional("en-301-549", "11.8.0-11.8.5", "Authoring tools",
            "If the app is a content-authoring tool, it must help create accessible content (accessible content " +
            "creation, preservation in transformations, repair assistance, templates).",
            "the app is itself an authoring/content-creation tool (e.g. a document editor, CMS or form builder)",
            BeyondWcagCheckMethod.Guided,
            "Requires judging the semantic quality of user-authored content, not the app's own tree -- not testable automatically.",
            ["If the app is a content-authoring tool, check whether a template it offers produces accessible output.",
             "Check whether it flags or helps repair accessibility problems in content the user creates."],
            En301549V3Source),

        // ---- EN 301 549 v4.1.1 -- confirmed differences from v3.2.1 (see SkippedNote for what's not repeated) ----
        Conditional("en-301-549-v4", "5.10.1-5.10.5", "Authoring tools",
            "v3.2.1's authoring-tool requirements (11.8.1-11.8.5, output conforming to clause 9 web content or 10 " +
            "non-web content) move to a generic clause 5.10.1-5.10.5 that applies wherever ICT is or includes " +
            "authoring-tool functionality (not just non-web software), and adds clause 11 (non-web software) as a " +
            "third accepted output target alongside clauses 9 and 10.",
            "the app is itself an authoring/content-creation tool (e.g. a document editor, CMS or form builder)",
            BeyondWcagCheckMethod.Guided,
            "Same conditional applicability and guided check as v3.2.1's 11.8.0-11.8.5, extended to cover whichever output format the tool produces.",
            ["If the app is a content-authoring tool, check whether a template it offers produces accessible output.",
             "Check whether it flags or helps repair accessibility problems in content the user creates."],
            En301549V4Source),
        NotTestable("en-301-549-v4", "5.5", "Tactilely discernible operable parts",
            "A mode of operation that allows all functionality requiring manual operation and control to be " +
            "controlled without vision using only tactilely discernible operable parts (v3.2.1's 5.5.1 and 5.5.2 merged into one clause).",
            "Hardware-oriented, same as v3.2.1's 5.5.1/5.5.2 assessment, now one clause instead of two.", En301549V4Source),
        Conditional("en-301-549-v4", "5.1.8", "Identify input purpose (closed functionality)",
            "New in v4.1.1: where the app has closed functionality and an input field collecting information " +
            "about the user is needed to use that closed functionality, and it serves a WCAG 2.2 Input Purpose, " +
            "the app must present that field's purpose audibly.",
            "the app has closed functionality (see clause 5.1) with an input field collecting information about the user for it -- distinct from the open-functionality WCAG 1.3.5 check",
            BeyondWcagCheckMethod.Guided, "Rare for a typical open app; decide from the app's features.",
            ["If the app has closed functionality with such an input field, confirm its purpose is presented audibly."],
            En301549V4Source),
        Conditional("en-301-549-v4", "5.2", "Activation of accessibility features",
            "Where ICT includes documented accessibility features, it must be possible to activate the ones needed for a specific need without relying on a method that doesn't support that need.",
            "the app has its own documented accessibility feature (e.g. an in-app large-text or high-contrast toggle) rather than relying solely on the OS",
            BeyondWcagCheckMethod.Guided, "Not testable automatically. Reads the same in substance as v3.2.1's 5.2.",
            ["If such a toggle exists, confirm it can be reached and activated by someone who needs it (e.g. without fine motor control or without vision), not just visually."],
            En301549V4Source),
        Conditional("en-301-549-v4", "5.4", "Preservation of accessibility information during conversion",
            "Where ICT converts information or communication, it must preserve accessibility information to the extent the destination format supports it.",
            "the app converts or exports content (documents, media) as part of its function",
            BeyondWcagCheckMethod.Guided, "Not testable automatically. Reads the same in substance as v3.2.1's 5.4.",
            ["Export or convert a piece of content.", "Compare accessibility metadata (e.g. alt text, captions) before and after conversion."],
            En301549V4Source),
        Conditional("en-301-549-v4", "5.9", "Simultaneous user actions",
            "If an action requires simultaneous inputs (e.g. a two-finger gesture), an alternative that needs only one action at a time must exist.",
            "the app relies on a multi-touch gesture (pinch-zoom, two-finger swipe) as the only way to do something",
            BeyondWcagCheckMethod.Guided, "Not reliably automatable: the accessibility tree doesn't record gesture handlers. Reads the same in substance as v3.2.1's 5.9.",
            ["Identify gestures used by the app during a walkthrough.", "Confirm a single-action alternative (e.g. zoom buttons) exists for any multi-touch-only gesture."],
            En301549V4Source),
        NotTestable("en-301-549-v4", "11.5.2.1", "Platform interoperability with assistive technologies",
            "The platform (OS) must offer documented accessibility services (v3.2.1's 11.5.2.1 and 11.5.2.2 merged; 11.5.2.2 voided in v4.1.1).",
            "Binds the OS/platform vendor, not the app under test.", En301549V4Source),
        Always("en-301-549-v4", "11.5.2.3", "Use of accessibility services (recommendation)",
            "The app should use the platform's documented accessibility services (downgraded from \"shall\" in v3.2.1 to \"should\" in v4.1.1).",
            BeyondWcagCheckMethod.PartlyAutomated,
            UseOfAccessibilityServicesExplanation + " Not following this is a missed recommendation under v4.1.1, not a non-conformance.",
            BeyondWcagEvidenceSignal.TreeRolesAndNames, En301549V4Source),
        Conditional("en-301-549-v4", "11.5.2.4", "Assistive technology",
            "If the app itself is assistive technology, it must use the documented platform services.",
            "the app is itself assistive technology (rare)",
            BeyondWcagCheckMethod.Guided, "Rare for a typical app; decide from the app's features. Reads the same in substance as v3.2.1's 11.5.2.4.",
            ["Confirm the app uses documented platform accessibility services rather than a custom mechanism."],
            En301549V4Source),
        Always("en-301-549-v4", "11.5.2.5-11.5.2.17", "Object info, relationships, actions, focus and change notification",
            "Programmatic exposure and modifiability of UI semantics -- the same content as Section 508's 502.3.x " +
            "(object info, row/column/headers, values, label relationships, parent-child, text, list of actions, " +
            "execution of actions, focus tracking, focus modification, change notification, state/property " +
            "modification, value/text modification). v4.1.1 narrows 11.5.2.10 (Text) so its \"text attributes\" " +
            "cover only those \"used or available for user generated content\", not all rendered text attributes.",
            BeyondWcagCheckMethod.PartlyAutomated, TreeRolesAndNamesExplanation, BeyondWcagEvidenceSignal.TreeRolesAndNames, En301549V4Source),
        NotTestable("en-301-549-v4", "11.6.1", "User control of accessibility features",
            "Where the software is a platform, it must let users control the platform's accessibility features.",
            "Binds platform software, not a typical app. Reads the same in substance as v3.2.1's 11.6.1.", En301549V4Source),
        AlwaysGuided("en-301-549-v4", "11.6.2", "No disruption of accessibility features",
            "The app must not disrupt documented platform accessibility features, except when the user asks it to while using the app.",
            "Not reliably detectable from the accessibility tree alone: disruption could be visual, behavioural or " +
            "affect a feature (like a screen reader or magnifier) the tree doesn't expose.",
            KeepsWorkingSteps, En301549V4Source),
        Always("en-301-549-v4", "11.7", "User preferences",
            "The app's UI must follow the platform's user-preference settings for documented accessibility " +
            "features (reworded from an enumerated list to an example list: colour filters, contrast, text size, " +
            "pointer size, text cursor) unless it is essential to the app's function not to follow a setting.",
            BeyondWcagCheckMethod.PartlyAutomated,
            UserPreferenceExplanation + " The v4.1.1 exception wording is looser (\"essential to the function... " +
            "not to follow\"): a deviation the app can justify should not be flagged outright. Also, the source's " +
            "own NOTE 6 only gives examples (colour filters, contrast, text size, pointer size, text cursor) of " +
            "what a platform might document as an accessibility feature -- dark/light appearance may or may not " +
            "be one on a given platform, so the dark/light rescan is suggestive evidence here, not confirmation.",
            BeyondWcagEvidenceSignal.UserPreferenceRescan, En301549V4Source),
        Conditional("en-301-549-v4", "7.1.1", "Subtitle playback",
            "The app must be able to display available subtitles for video with synchronized audio (renamed from \"Captioning playback\"; \"caption\" -> \"subtitle\" throughout clause 7 in v4.1.1).",
            "the app displays video with synchronized audio",
            BeyondWcagCheckMethod.PartlyAutomated, $"{CouldBeCheckedPrefix} looking for a subtitles/CC control in the media player's UI -- a presence check only.",
            ["Turn subtitles on and confirm they display."], En301549V4Source),
        Conditional("en-301-549-v4", "7.1.2", "Subtitle synchronization",
            "Where the app displays video with synchronized audio and displays subtitles, they must display within 100 ms of the subtitle's timecode (renamed from \"Captioning synchronization\"; v4.1.1's precondition combines both video-with-audio and displaying subtitles, where v3.2.1's 7.1.2 only needed captions to be displayed).",
            "the app displays video with synchronized audio and displays subtitles",
            BeyondWcagCheckMethod.NotTestable, "Needs frame-accurate timing measurement, not tree/screenshot data.", [],
            En301549V4Source),
        Conditional("en-301-549-v4", "7.1.3", "Preservation of subtitles",
            "Where the app transmits, converts or records video with synchronized audio, subtitle data -- including presentational data such as screen position, colours, style and fonts -- must survive so it can still be displayed (renamed from \"Preservation of captioning\"; v4.1.1 makes preserving presentational data an explicit part of the requirement itself, not just a following comment).",
            "the app transmits, converts or records video with synchronized audio",
            BeyondWcagCheckMethod.NotTestable, "Requires inspecting the app's internal media pipeline.", [],
            En301549V4Source),
        Conditional("en-301-549-v4", "7.1.4", "Subtitle characteristics",
            "Where the app displays video with synchronized audio and has control over the presentation of its subtitles, the user must be able to adapt their display, unless subtitles are unmodifiable (e.g. bitmap-image subtitles) (renamed from \"Captions characteristics\"; v4.1.1's precondition adds \"has control over the presentation\", where v3.2.1's 7.1.4 needed only that captions be displayed).",
            "the app displays video with synchronized audio and has control over the presentation of its subtitles",
            BeyondWcagCheckMethod.Guided, "Not automatable from the tree alone.",
            ["Check for a subtitles-appearance settings screen."], En301549V4Source),
        Conditional("en-301-549-v4", "7.1.5", "Spoken subtitles",
            "Where the app displays video with synchronized audio, it must have a mode to output available spoken " +
            "subtitles (v4.1.1's own NOTE 2 clarifies this means playing spoken-subtitle audio that already " +
            "exists, not generating speech from subtitle text -- a substantive change from v3.2.1's wording, " +
            "which required a spoken output \"except where the content... is not programmatically determinable\").",
            "the app displays video with synchronized audio",
            BeyondWcagCheckMethod.Guided, "Not automatable from the tree alone.",
            ["Check for a spoken-subtitles option, and confirm it plays existing spoken-subtitle audio rather than being expected to generate speech."], En301549V4Source),
        Conditional("en-301-549-v4", "7.2.1", "Audio description playback",
            "Where the app displays video with synchronized audio, it must let the user select and play an audio-description track.",
            "the app displays video with synchronized audio",
            BeyondWcagCheckMethod.PartlyAutomated, $"{CouldBeCheckedPrefix} looking for an audio-description toggle or track-selection control -- a presence check only.",
            ["Turn it on and confirm it plays audibly."], En301549V4Source),
        Conditional("en-301-549-v4", "7.2.2", "Audio description synchronization",
            "Where the app displays video with synchronized audio and has a mechanism to play audio description, it must stay in sync with the video (v4.1.1 adds \"displays video with synchronized audio\" as an explicit precondition alongside \"has a mechanism to play audio description\").",
            "the app displays video with synchronized audio and has a mechanism to play audio description",
            BeyondWcagCheckMethod.NotTestable, "Timing measurement, not tree/screenshot data.", [],
            En301549V4Source),
        Conditional("en-301-549-v4", "7.2.3", "Preservation of audio description",
            "Where the app transmits, converts or records video with synchronized audio, audio-description data must survive.",
            "the app transmits, converts or records video with synchronized audio",
            BeyondWcagCheckMethod.NotTestable, "Requires inspecting the app's internal media pipeline.", [],
            En301549V4Source),
        Conditional("en-301-549-v4", "7.3", "User control of audiovisual accessibility features",
            "Where the app primarily displays video with synchronized audio and has control over the activation " +
            "of subtitles, audio description and spoken subtitles, it must allow turning the user's choice of one " +
            "of those on or off with a single user operation at the same level as the volume control (renamed " +
            "from \"User controls for captions and audio description\"; now explicitly covers spoken subtitles as a third option).",
            "the app primarily displays video with synchronized audio and has control over the activation of subtitles, audio description and spoken subtitles",
            BeyondWcagCheckMethod.PartlyAutomated,
            $"{CouldBeCheckedPrefix} comparing the number of taps/menu depth to reach subtitles/audio description " +
            "vs. the primary play/volume controls, from the captured tree -- a partial signal, not confirmation " +
            "of a single-operation equivalence.",
            ["Confirm the subtitle/audio-description/spoken-subtitles control is a single user operation at the same level as the volume control."],
            En301549V4Source),
        Conditional("en-301-549-v4", "5.3", "Biometrics",
            "Where ICT uses biological characteristics, it must not rely on a biological characteristic as the " +
            "only means of identification or control (v4.1.1 drops \"a particular\" biological characteristic and " +
            "removes v3.2.1's note that an alternative could be a different biometric method).",
            "the app offers biometric sign-in (Face ID, fingerprint) as a control method",
            BeyondWcagCheckMethod.Guided,
            "Not automatable from the accessibility tree alone; requires trying the sign-in flow. v4.1.1 no " +
            "longer notes that a different biometric can be the alternative; a non-biometric fallback (PIN, " +
            "password) is the safer reading.",
            ["Open the sign-in/authentication screen.", "Confirm a non-biometric fallback (PIN, password) is offered alongside biometrics."],
            En301549V4Source),
        Conditional("en-301-549-v4", "5.6.1", "Tactile or auditory status",
            "Where the app has a locking/toggle control, its status must be determinable through touch or sound, " +
            "without operating the control (v4.1.1 drops v3.2.1's precondition that this only applies when status " +
            "is visually presented -- it now applies to every locking/toggle control). The source itself notes " +
            "that for graphical software controls in open functionality, conforming to WCAG 1.3.1 and 4.1.2 will " +
            "usually be enough to meet this.",
            "the app has a locking or toggle control",
            BeyondWcagCheckMethod.PartlyAutomated,
            $"{CouldBeCheckedPrefix} checking whether such a control's on/off state is exposed programmatically " +
            "(so a screen reader can announce it) -- the same signal WCAG 4.1.2 already reads, which the source " +
            "itself says will usually be enough for a graphical control in open functionality.",
            ["If the app has a toggle/switch, confirm its state is announced by TalkBack/VoiceOver."],
            En301549V4Source),
        Conditional("en-301-549-v4", "5.6.2", "Visual status",
            "Where the app has a locking/toggle control, its status must also be visually determinable when the " +
            "control is presented (v4.1.1 drops v3.2.1's precondition that this only applies when status is " +
            "presented non-visually -- it now applies to every locking/toggle control).",
            "the app has a locking or toggle control",
            BeyondWcagCheckMethod.Guided,
            "Not automatable from the tree alone: it needs someone to look at the control while it's presented.",
            ["If the app has a toggle/switch, confirm a visual status indicator is presented, not just an announced state."],
            En301549V4Source),
    ];
}
