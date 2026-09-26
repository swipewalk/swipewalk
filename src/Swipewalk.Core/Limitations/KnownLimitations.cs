using Swipewalk.Core.Model;

namespace Swipewalk.Core.Limitations;

/// <summary>
/// Every known limitation and framework note. This is the single source for the report section,
/// results.json and docs/limitations.md (regenerate with `swipewalk limitations`). Add an entry
/// whenever a gap is found; tag it with the platforms and frameworks it applies to.
/// </summary>
public static class KnownLimitations
{
    public static IReadOnlyList<Limitation> All { get; } =
    [
        // ---- All platforms, all frameworks ----
        new()
        {
            Id = "partial-wcag-coverage",
            Area = LimitationArea.Coverage,
            Title = "Automated checks cover only part of WCAG",
            Description = "Each check tests only part of the WCAG criteria it maps to. Most WCAG 2.2 success criteria are not tested automatically at all. On Android, when the instrumentation harness ran (see \"android-atf-harness\"), Google's Accessibility Test Framework adds its own checks (14 in ATF 4.1.1, some overlapping Swipewalk's own rules); one of them, UnexposedTextCheck, needs text recognition the harness does not currently supply and never produces a finding. Another, TextSizeCheck, reported a finding on a physical Pixel 4a (Android 13) but not on an Android 16 emulator scanning the exact same px-sized text (samples/NativeAndroid, 2026-09-23); the two also differ in device type, density and screen size, so which of those differences explains it hasn't been pinned down. These are partial too.",
            Impact = "A screen with no findings has not been shown to meet WCAG or any law.",
            ManualCheck = "Test every screen with a screen reader, keyboard or switch access, and large text, against the full WCAG 2.2 A/AA list.",
        },
        new()
        {
            Id = "jurisdiction-mapping-partial",
            Area = LimitationArea.Coverage,
            Title = "US state, other-country and store guidance mapping is opt-in and covers only some jurisdictions",
            Description = "docs/standards.md lists jurisdiction standards Swipewalk researched (each checked against an official government source -- a statute, regulation, agency policy, or in a few cases an agency's own overview/FAQ page -- with a citation, legal tier and date) -- but they are not part of the default \"relevant to\" labels a finding gets; pass `--standard <id>` to add one. Several researched jurisdictions have no id at all: their own primary source wasn't confirmed (for example Maryland, Utah, Australia, India), their instrument references something other than WCAG 2.x and is shown as-is rather than translated (for example Rhode Island and Vermont's WCAG 1.0 policies, or several states' generic Section 508 citations), their own scope doesn't reach native apps (for example Ontario, Idaho, Iowa), or nothing jurisdiction-specific was found (docs/standards.md lists each reason). Apple and Google Play accessibility guidance is likewise informational (\"also relevant to\"/\"related to\"), never a claim about passing store review, and each only appears for the platform it applies to.",
            Impact = "A report's default standards table, and the CLI's list of --standard ids in its error message, do not surface every jurisdiction Swipewalk has researched.",
            ManualCheck = "Run `swipewalk standards` (or read docs/standards.md) for the full list, including jurisdictions researched but not yet mapped, and pass --standard for one that applies to you.",
            Planned = "More jurisdictions confirmed and mapped as their primary sources are read.",
        },
        new()
        {
            Id = "current-screen-only",
            Area = LimitationArea.Coverage,
            Title = "Only screens that were shown are scanned",
            Description = "scan covers the one screen visible when it runs; record mode covers the screens you navigate to. Screens, dialogs, menus, error messages, empty and loading states that were never shown are not scanned. The report lists every screen that was scanned.",
            Impact = "Issues on screens that were not scanned are missed.",
            ManualCheck = "List the app's screens and states (pass them to record --expect to see which were missed), and scan or manually test each one.",
            Planned = "Automatic navigation through apps",
        },
        new()
        {
            Id = "guided-checks-first-ten",
            Area = LimitationArea.Coverage,
            Title = "Guided step-by-step checks exist for only 10 criteria so far",
            Description = "`swipewalk guide` (and the desktop app's Guided checks page) can suggest which WCAG criteria might not apply to a screen and record a tester's Pass/Fail/Inconclusive/not-applicable answer for any of the 55, but written, numbered TalkBack/VoiceOver steps exist today only for 1.3.1, 1.3.2, 1.3.4, 1.4.1, 2.4.3, 2.5.1, 2.5.7, 3.3.1, 3.3.2 and 4.1.3. The CLI asks about those ten by default (pass --criterion for any other criterion); the desktop page asks about every applicable criterion by default. Either way, a criterion with no script yet shows the one-line \"how to check by hand\" note from the WCAG 2.2 coverage table instead of a full script.",
            Impact = "A guided-checks session covers less ground than the full manual-check list until more scripts are written.",
            ManualCheck = "Use the coverage table's one-line note for any criterion without numbered steps yet.",
            Planned = "Guided steps for the remaining criteria",
        },
        new()
        {
            Id = "predicted-screen-reader",
            Area = LimitationArea.ScreenReader,
            Title = "Screen-reader output is predicted, not recorded",
            Description = "The transcript and swipe order are predicted from the accessibility tree using the screen reader's usual rules; the real screen reader was not running. Android: pass --screen-reader to have Swipewalk drive TalkBack itself and read back exactly what it said, compared against the prediction (off by default -- costs roughly 1-2 seconds per element; see \"android-screen-reader-capture\"). iOS: pass --screen-reader to scan (not yet record) to have Swipewalk walk Xcode's Accessibility Inspector instead -- VoiceOver itself is never turned on, and this needs a person present for a one-time permission-and-setup step (see \"ios-inspector-walk-capture\"). Without --screen-reader on either platform, or with it declined or unavailable, the transcript stays predicted only.",
            Impact = "Without real evidence, wording and order can differ from what a real screen reader would say, for example with custom accessibility actions, grouping or focus changes.",
            ManualCheck = "Swipe through the screen with TalkBack, VoiceOver or Narrator and compare with the predicted transcript.",
            Planned = "Screen-reader testing",
        },
        new()
        {
            Id = "no-interaction-checks",
            Area = LimitationArea.Coverage,
            Title = "Behavior over time is not checked",
            Description = "Scans are a snapshot. Announcements of status messages, focus movement after actions, timeouts, animations and gestures are not tested.",
            Impact = "Issues under 4.1.3 Status Messages (AA), 2.2.1 Timing Adjustable and 2.2.2 Pause, Stop, Hide (A), 2.3.1 Three Flashes or Below Threshold (A), 2.5.1 Pointer Gestures and 2.5.2 Pointer Cancellation (A), 2.5.7 Dragging Movements (AA), 2.4.3 Focus Order (A) and 3.3.1 Error Identification (A) are not detected.",
            ManualCheck = "Complete key tasks with a screen reader: submit forms, trigger errors, and check that changes are announced and focus lands sensibly.",
        },
        new()
        {
            Id = "contrast-from-pixels",
            Area = LimitationArea.Rules,
            Title = "Contrast is estimated from screenshot pixels",
            Description = "Text and background colors are estimated from the screenshot inside each text element. Anti-aliasing, gradients, transparency and text over images can skew the estimate. Text size cannot be read, so values between 3:1 and 4.5:1 need review against 1.4.3 Contrast (Minimum) (AA). The same kind of pixel-based estimate is used for icon-only interactive controls on iOS (icon-contrast rule); on Android, Google's ATF ImageContrastCheck estimates icon contrast from pixels too, with its own separate method, not Swipewalk's. Focus indicators and non-icon component borders are not checked on either platform.",
            Impact = "Possible false positives or misses on text or icons over images or gradients. Focus indicators and non-icon component borders (1.4.11) are not checked.",
            ManualCheck = "Check flagged and image-backed text or icons with a color picker, and check focus indicators and input borders for 3:1 contrast.",
            Rules = ["text-contrast", "icon-contrast"],
        },
        new()
        {
            Id = "decorative-images",
            Area = LimitationArea.Rules,
            Title = "Decorative and informative images look the same",
            Description = "The accessibility trees used do not say whether an image was hidden on purpose, so every image without a text alternative is listed for review, including decorative ones.",
            Impact = "Decorative images appear under Needs review even when they are correctly hidden.",
            ManualCheck = "For each listed image, decide whether it conveys information. If it does, it needs a description.",
            Rules = ["missing-name"],
        },
        new()
        {
            Id = "images-in-named-rows",
            Area = LimitationArea.Rules,
            Title = "Images inside a named control or row are not listed",
            Description = "An image inside a control or row that screen readers read as one element with a name (for example a menu row) is not reported separately, because screen readers announce the row's name instead.",
            Impact = "An icon that conveys information not in the row's name, such as a warning or status icon, is not flagged.",
            ManualCheck = "In lists and menus, check with a screen reader that icons carrying meaning are part of what is announced.",
            Rules = ["missing-name"],
        },
        new()
        {
            Id = "identifier-heuristic",
            Area = LimitationArea.Rules,
            Title = "Identifier-like names are found by pattern",
            Description = "Names are flagged when they look like code (snake_case, prefixes like btn/img, or equal to the AutomationId). Meaningless names that look like words are not caught.",
            Impact = "Names such as \"Button 3\" or \"Image\" are not flagged; unusual real words with underscores might be.",
            ManualCheck = "Read the predicted transcript: every name should describe the element's purpose.",
            Rules = ["identifier-name"],
        },
        new()
        {
            Id = "input-purpose-heuristic",
            Area = LimitationArea.Rules,
            Title = "Input-purpose candidates are found by keyword, not by a declared hint",
            Description = "Swipewalk flags a text field as a WCAG 1.3.5 candidate only when its label, hint or identifier text matches a small, specific set of phrases (for example \"first name\", \"email\", \"street address\") -- not the bare word \"name\" alone, which is too common on non-personal fields (\"file name\", \"team name\"). Neither Android's accessibility tree (uiautomator dump or the instrumentation harness) nor iOS's XCUITest exposes whether the app actually declared an autofill/content-type hint for a field (Android autofillHints, iOS textContentType), so the check can never confirm one either way -- only guess the purpose from visible text.",
            Impact = "Personal-data fields with a generic or unmatched label (for example a bare \"Name\" field, or one named only by an icon) are not flagged. A flagged field may already correctly declare its purpose; an unflagged field may not.",
            ManualCheck = "Check every field that collects personal information -- flagged or not -- for a declared autofill/content-type hint in the app's source.",
            Rules = ["input-purpose"],
        },
        new()
        {
            Id = "target-size-exceptions",
            Area = LimitationArea.Rules,
            Title = "Only the spacing exception of 2.5.8 is evaluated automatically; the inline exception is only suggested for review",
            Description = "Small targets are checked against the 24×24 minimum and the spacing exception. The inline exception (\"the target is in a sentence or its size is otherwise constrained by the line-height of non-target text\") is never granted automatically: the tree has no concept of a sentence or paragraph, so Swipewalk cannot tell a target that is genuinely inline in running text from one that merely sits next to an unrelated text label. When a target whose whole tap area is plain text with no separate button styling (Android a clickable TextView; iOS an XCUITest \"link\" element or a button with no accessibility label of its own wrapping one matching-bounds text child) sits beside a sibling carrying non-target text on the same visual line, and the spacing exception does not already explain why there is no WCAG issue, Swipewalk reports needs-review instead of a WCAG issue, so a person confirms whether it is really inline. The equivalent-control, user-agent and essential exceptions are not evaluated at all. dp and pt are treated as CSS pixels (an approximation).",
            Impact = "A short text-styled control next to an unrelated text label (for example a checkbox's own label next to a separate link) is marked needs-review even when it is not really in a sentence with that text, so it is not counted as a confirmed WCAG issue until a person checks it. A genuine inline link the tree shows differently -- a link inside a UITextView's paragraph, or an Android ClickableSpan -- is not recognized by this check and is evaluated as if no exception applied. A tap-gesture label that a platform doesn't expose as a target at all (for example a MAUI Label with a tap gesture on Android, which uiautomator never reports as clickable -- see samples/BuggyApp/ground-truth.json's \"B10\") can't be evaluated by this rule in the first place, on that platform. A flagged target may also still be acceptable under one of the other, unevaluated exceptions.",
            ManualCheck = "For each flagged target, check whether an equivalent larger control exists on the same screen, the target is inline in text, it is a platform-default control whose size the app did not set, or it must be that size to work (essential).",
            Rules = ["target-size"],
        },
        new()
        {
            Id = "wcag-version",
            Area = LimitationArea.Coverage,
            Title = "Findings are mapped to WCAG 2.2; laws reference different WCAG versions",
            Description = "Each finding cites WCAG 2.2 criteria and is labelled with the standards whose WCAG version and level include it (for example ADA Title II references WCAG 2.1 AA, Section 508 WCAG 2.0 AA). WCAG 2.2 adds criteria that are not in 2.0 or 2.1, such as 2.5.8 Target Size (Minimum), and removes 4.1.1 Parsing. Section 508 and EN 301 549 apply WCAG to non-web software with some criteria excluded or reworded; the mapping uses version, level and the known exclusions only.",
            Impact = "\"Relevant to\" is a mapping, not a legal conclusion. Standards' own exceptions are not evaluated; requirements beyond WCAG are listed with a per-run status in the report's Beyond WCAG section (see \"beyond-wcag-clauses\"), most needing a person; 4.1.1 Parsing is not assessed.",
            ManualCheck = "Confirm which standard, WCAG version and level your contract or regulation requires (see docs/standards.md), and scan with --standard to focus the report on it.",
        },
        new()
        {
            Id = "beyond-wcag-clauses",
            Area = LimitationArea.Coverage,
            Title = "Requirements beyond WCAG are listed; few are partly checked by automation",
            Description = "Section 508 (Chapters 5 and 6) and EN 301 549 (clauses 5, 6, 7 and 11) add requirements beyond what they reference from WCAG. The report's \"Beyond WCAG\" section lists each clause with an honest status: partly checked by automation (reusing an existing signal, for example the large-text and dark/light rescans, or the accessible names Swipewalk already reads for WCAG 4.1.2 -- carrying no finding count of its own; those stay under the WCAG criterion), needing a guided check with 1-3 steps, or not testable by Swipewalk (documentation, support services or platform/OS behaviour). Clauses that only apply when the app has a specific feature (two-way voice calling, video, biometric sign-in, content authoring) are reported as \"not tested\": Swipewalk does not yet detect whether a scanned app actually has that feature, so it never assumes a conditional clause doesn't apply. An always-applicable clause whose evidence (the large-text or dark/light rescan) didn't run this time is also \"not tested\", naming what to run.",
            Impact = "A conditional clause always needs a person to first decide whether it applies at all, then check it by hand. \"Not tested\" here does not mean the clause is irrelevant.",
            ManualCheck = "For each conditional clause in the report, decide from the app's real features whether it applies, then follow its guided steps or the standard's own text.",
            Planned = "Per-feature detection (e.g. video or call controls in the tree) so an app confirmed not to have the feature is reported as not applicable instead of not tested.",
        },
        new()
        {
            Id = "rule-versions",
            Area = LimitationArea.Coverage,
            Title = "Results reflect the rule versions listed in the report",
            Description = "Rules and standards mappings are checked against specific versions of WCAG, laws and platform guidelines, listed in each report with the date the mapping was reviewed. Swipewalk does not detect changes to them automatically.",
            Impact = "After a law, standard or guideline changes, results may not reflect the new requirements until Swipewalk is updated. Reports warn when a mapping was last reviewed more than a year earlier.",
            ManualCheck = "Check the listed sources for changes before relying on results, and keep Swipewalk up to date.",
        },
        new()
        {
            Id = "device-settings",
            Area = LimitationArea.Coverage,
            Title = "Scans use the device's current settings unless told to check the alternative too",
            Description = "Each scan captures the screen as the device is. `scan --appearance both` captures the screen in the device's other dark/light appearance too and runs every check on it (Android `cmd uimode night`; iOS Simulator `simctl ui appearance`; not yet supported on a physical iPhone), restoring the device's original appearance afterward. `scan --orientation both` does the same for portrait/landscape (Android `settings put system accelerometer_rotation`/`user_rotation`; iOS Simulator through the scanning harness; not yet supported on a physical iPhone). On Android it restores the device's exact original orientation and rotation-lock state afterward; on the iOS Simulator there is no way to read the original orientation back, so it rotates back to whichever of portrait/landscape the first capture showed. Without either flag, only the device's current appearance and orientation are checked.",
            Impact = "1.4.10 Reflow is tested only partly, and on iOS only: the offscreen-unreachable rule checks the device's current orientation and text size for content pushed off-screen with no way to scroll to it, but rotation and text-zoom scenarios are not tested. Contrast is checked in both dark and light appearance only when `--appearance both` was used; otherwise only the device's current appearance is checked -- why that matters: `scan --appearance both` on Microsoft's WeatherTwentyOne sample (2026-09-23, an Android emulator and a Pixel 4a) found 5 genuine WCAG 1.4.3 contrast failures in light appearance and none in dark, on both devices, in a single run each (see docs/case-study.md). 1.3.4 Orientation is checked only when `--orientation both` was used, and even then only for review -- Swipewalk can tell whether a screen's content followed the rotation, not whether staying in one orientation is essential to it, so a flagged screen still needs a person's judgment. 1.4.4 Resize Text is tested only partly: in record mode and scan --large-text, by comparing the screen at the OS text-size setting (Android font scale 2.0, exactly the 200% 1.4.4 asks for; iOS accessibility size AX3, about 235%, beyond it), and otherwise only where Apple's audit reports it. Text that does not grow is always reported against 1.4.4 regardless of which of those two levels was tested, since it implies text can't reach 200% either way; clipping or overlap seen only on iOS at AX3 (above 200%) is reported as a platform advisory against Apple's Dynamic Type guidance instead of a WCAG finding. Other resize mechanisms are not tested.",
            ManualCheck = "Repeat key screens at 200% text size (and your platform's largest setting), in landscape, and in dark mode; also check any in-app text size controls. Pass --appearance both and --orientation both to check the alternative appearance and orientation automatically instead of by hand.",
        },
        new()
        {
            Id = "no-heading-checks",
            Area = LimitationArea.Coverage,
            Title = "Headings and structure are not checked",
            Description = "Heading roles are not reliably exposed by the collectors yet, so missing or wrong headings and reading structure are not checked. On Android, the instrumentation harness now reads Android's own heading flag (isHeading(), API 28), but no check uses it yet; headings and reading structure are still not checked on any platform.",
            Impact = "Issues under 1.3.1 Info and Relationships (A) and 2.4.6 Headings and Labels (AA) are not detected for headings.",
            ManualCheck = "With a screen reader, navigate by headings and check that section titles are announced as headings.",
        },

        new()
        {
            Id = "production-builds",
            Area = LimitationArea.Detection,
            Title = "Some production apps limit what can be scanned",
            Description = "Scanning works on release and store builds, with no source code or debug build. But apps can block screenshots (Android FLAG_SECURE on banking, password or DRM screens; the scanner reports these screens and skips pixel checks), refuse to run while automation is active (some anti-tamper protections), or draw their own UI without accessibility information (games, canvas-based UIs), leaving little to check.",
            Impact = "Contrast and close-ups are unavailable on blocked screens; protected apps may not be scannable; custom-drawn screens may show few findings even when they have many problems.",
            ManualCheck = "Test such screens by hand with the screen reader, and check contrast with a color picker on the device.",
        },

        // ---- Android ----
        new()
        {
            Id = "android-install-files",
            Area = LimitationArea.Devices,
            Platforms = [Platform.Android],
            Title = "Android App Bundles need bundletool and Java, and IDE-only debug builds can't be installed from a file",
            Description = "--install takes a signed .apk (debug or release) directly, and an Android App Bundle (.aab) via Google's bundletool: it's usually found automatically (on PATH, or bundled with the installed .NET Android SDK workload), needs a Java runtime to run, and builds a set of .apks for the connected device, signed with the standard Android debug key (~/.android/debug.keystore, created by Swipewalk with keytool if it doesn't already exist, using the same standard settings Android Studio and Gradle use) unless --keystore is given -- a debug-signed build differs from your store build, for scanning only, and installing over an app already installed with a different key fails until it's uninstalled or the matching --keystore is given. .NET MAUI Debug builds that use fast deployment still cannot run on their own, in either format (detected and explained).",
            Impact = "A .aab install needs bundletool and a Java runtime present (a clear message says how to get them if not); a MAUI Debug fast-deployment build is refused either way. The app can still be scanned once installed another way.",
            ManualCheck = "Install bundletool (brew install bundletool, or the .NET Android SDK workload already has it, pointed at with --bundletool if it isn't found automatically) and a JRE if the message says they're missing, or build MAUI apps in Release (or with EmbedAssembliesIntoApk=true).",
        },
        new()
        {
            Id = "android-uiautomator-view",
            Area = LimitationArea.Detection,
            Platforms = [Platform.Android],
            Title = "The Android tree comes from uiautomator, not TalkBack",
            Description = "Elements, names and reachability are read with uiautomator dumps. The compressed dump is used to decide what TalkBack can reach, which approximates TalkBack's own rules. When the instrumentation harness ran (see \"android-atf-harness\"), the extra properties it adds and Google's ATF checks are read from Android's accessibility framework (AccessibilityNodeInfo, through UiAutomation) directly, the same kind of node data TalkBack itself receives, rather than a uiautomator dump -- but still not the exact node set TalkBack works from (the harness can also see views marked not important for accessibility) and without TalkBack's own filtering and grouping on top, so this does not mean the scanner sees exactly what TalkBack does.",
            Impact = "TalkBack may group, skip or name some elements differently than predicted.",
            ManualCheck = "Confirm unlabeled and unreachable elements with TalkBack.",
            Planned = "More automated checks on Android; screen-reader testing",
        },
        new()
        {
            Id = "android-nonlinear-font-scaling",
            Area = LimitationArea.Rules,
            Platforms = [Platform.Android],
            Title = "Android 14+ enlarges big text less than the font scale",
            Description = "From Android 14, text that is already large grows less than the font-scale setting, by design. At font scale 2.0 a large heading can grow well under 2×.",
            Impact = "Large headings may be listed under Needs review for not growing even though the platform is working as intended.",
            ManualCheck = "For flagged large text, check at 200% text size whether it is still readable and not clipped.",
            Rules = ["text-resize"],
        },
        new()
        {
            Id = "android-font-scale-restart",
            Area = LimitationArea.Detection,
            Platforms = [Platform.Android],
            Title = "Changing the text size can restart the screen",
            Description = "Android restarts the current activity when the font scale changes unless the app handles that change. scan --large-text and record wait briefly for the app to return to the front, then skip the large-text check for that screen (and say why) if it shows a different screen, or never comes back to front, at the larger size. When the very first attempt at the larger size shows a different screen, a platform advisory (text-resize-navigation) is also added to the screen where Swipewalk actually saw it, flagging the lost place itself -- but only for that one screen, not for every screen skipped this way.",
            Impact = "Some screens may have no large-text check; app state (such as typed text) can be lost when the text size changes.",
            ManualCheck = "For screens without a large-text capture, set the system font size to 200% and check them by hand.",
            Rules = ["text-resize", "text-resize-navigation"],
        },
        new()
        {
            Id = "large-text-lost-content-heuristic",
            Area = LimitationArea.Rules,
            Platforms = [Platform.Android, Platform.iOS],
            Title = "Lost-content detection is a per-screen signal, not per-element",
            Description = "large-text-lost-content compares the normal and large-text accessibility trees for a screen and flags controls or text present at normal size that are gone at the larger size. It reports them only when the large-text tree has no scrollable container anywhere on the screen: on Android, uiautomator dump omits nodes that are entirely off-screen -- whole containers, not just their leaves (verified 2026-09-23 on a Pixel 4a: an off-screen HorizontalStackLayout was missing along with its children) -- so the rule can't reliably trace which container a specific missing element belonged to. If the screen has any scrollable region at all, missing elements are assumed reachable by scrolling there, even if they actually sat in a different, non-scrolling part of the screen. On iOS this check has not been verified on a device: XCUITest's snapshot usually still includes elements positioned past the screen edge (unlike uiautomator dump), so content pushed off-screen there may not show up as missing at all, and IsScrollable means \"is a scroll/table/collection view\" rather than \"has something to scroll right now\", which is a different signal than the Android one this was verified against.",
            Impact = "A genuine loss of content or functionality can go unreported on a screen that also has an unrelated scrollable region. It can also flag text that simply changed between the two captures (a timer, a clock, a live count, a carousel, a toast or snackbar shown in only one of the two), or an element that was renamed rather than removed.",
            ManualCheck = "On a screen with a scrollable region, also check by hand at 200% text size (Android) or your platform's largest text size whether every control present at normal size can still be reached, especially outside that scrollable area.",
            Rules = ["large-text-lost-content"],
        },
        new()
        {
            Id = "offscreen-unreachable-android-gap",
            Area = LimitationArea.Rules,
            Platforms = [Platform.Android],
            Title = "Off-screen content is not detected on Android",
            Description = "offscreen-unreachable flags controls or text positioned wholly or mostly outside the visible screen at normal text size, with no scrollable ancestor that could bring them into view. It runs on iOS only: Android's uiautomator dump does not report a node's true position when part or all of it is off-screen. AOSP's AccessibilityNodeInfoDumper writes each node's bounds through getVisibleBoundsInScreen, which clips them to the screen's visible area first, and a node that is entirely off-screen is omitted from the dump altogether (see \"large-text-lost-content-heuristic\"). Confirmed in this project's own fixture (tests/Swipewalk.Core.Tests/Fixtures/BuggyApp.Android.Pixel4a/large/uiautomator.xml): \"Pay\", cut off at the bottom of that capture, has bounds ending exactly at the screen's edge (2340 px), never beyond it. On iOS, XCUITest's snapshot usually keeps an element's real frame regardless of its position (lazily-created content, such as an unrendered table/collection cell, can still be absent), which is what this rule reads instead.",
            Impact = "Every case on Android is missed: a control positioned off-screen on Android never produces a finding from this rule, however far past the edge it actually sits.",
            ManualCheck = "On Android, check by hand whether every control shown in the app's source or design is reachable on the actual device screen, especially near the bottom of a long, non-scrolling layout.",
            Rules = ["offscreen-unreachable"],
        },
        new()
        {
            Id = "android-edittext-text",
            Area = LimitationArea.ScreenReader,
            Platforms = [Platform.Android],
            Title = "Text field contents and hints can't be told apart without the Android harness",
            Description = "On Android, a text field's typed value, its placeholder and some labels can all appear as the field's text. When the Android instrumentation harness ran for a capture, AccessibilityNodeInfo#getHintText() (API 26) recovers a placeholder even on a device/version whose uiautomator dump omits the \"hint\" attribute (seen on an Android 13 phone, not on an Android 16 emulator), so a field with a real hint gets a name and is not flagged at all. isShowingHintText() (API 26) then tells whether a field with no hint and no label is showing a placeholder (listed under Needs review: TalkBack reads the placeholder while the field is empty, but the field may have no name once something is typed) or an entered value -- which still doesn't prove the field has no name at all, since Android can also name a field through android:labelFor/getLabeledBy(), which the harness does not read yet, so that case is Needs review too. Without the harness at all, none of this is available.",
            Impact = "When the harness ran: a field with a real hint is not flagged; a field showing a placeholder that Android doesn't also expose as hint text is listed under Needs review (4.1.2); a field showing an entered value with no hint, description or (readable) label is also listed under Needs review (4.1.2), because it may still be labelled via android:labelFor. When the harness did not run, a text field whose only text is its visible text is listed under Needs review (4.1.2) for the same underlying reason, worded more generally.",
            ManualCheck = "For a field listed under Needs review, check with TalkBack that it is announced with a name (including one linked with android:labelFor), not just its content.",
            Rules = ["missing-name"],
        },
        // No Google source confirms that Play's pre-launch report runs ATF (only that it would be
        // consistent with public descriptions of both), so Swipewalk never claims that parity in public
        // text -- only ATF's own named checks (see AtfIssueRule and this entry's Impact).
        new()
        {
            Id = "android-atf-harness",
            Area = LimitationArea.Detection,
            Platforms = [Platform.Android],
            Title = "Google's Accessibility Test Framework needs the instrumentation harness to install and run",
            Description = "A small test app (harness/android) runs Google's Accessibility Test Framework (ATF) 4.1.1 against the app under test through UiAutomation, without changing it, and also reads a few extra accessibility properties uiautomator dump does not (isShowingHintText, hint text, isHeading, paneTitle, stateDescription, isImportantForAccessibility). Swipewalk ships this harness prebuilt (installed with adb; no JDK, Gradle or network access needed) and installs it automatically on first use, replacing an older install left on the device by a previous Swipewalk version. Only a from-source checkout with nothing built yet, or a copy named with --android-harness, builds it from source with Gradle instead, which needs a JDK and the Android SDK. If it can't be built (the from-source/--android-harness path only) or installed, or a run fails (a device policy, a flaky instrumentation run), the scan continues without it and says so.",
            Impact = "ATF's 14 checks (ClassName, ClickableSpan, DuplicateClickableBounds, DuplicateSpeakableText, EditableContentDesc, ImageContrast, LinkPurposeUnclear, RedundantDescription, SpeakableTextPresent, TextContrast, TextSize, TouchTargetSize, TraversalOrder, UnexposedText -- though UnexposedTextCheck needs text recognition the harness does not supply and never produces a finding, and TextSizeCheck reported on one device but not another scanning the same screen, for a reason not yet pinned down, see \"partial-wcag-coverage\") and the extra properties above are unavailable for that scan; the report and results.json say Google's checks did not run and why, and the WCAG coverage section shows the affected criteria as not tested rather than silently reporting nothing.",
            ManualCheck = "If Google's checks keep being skipped, the report and results.json give the reason for each screen; from a source checkout without a prebuilt copy, install a JDK (or Android Studio) and the Android SDK, or pass --android-harness <path> to a working copy of harness/android; otherwise test by hand with TalkBack.",
            Rules = ["atf"],
        },
        new()
        {
            Id = "android-page-title",
            Area = LimitationArea.Rules,
            Platforms = [Platform.Android],
            Title = "Page-title check needs the harness, and can't tell \"no title\" from \"can't read it\" on Android 8.0/8.1",
            Description = "The page-titled check reads only AccessibilityNodeInfo#getPaneTitle() (API 28), which the instrumentation harness only reports when it actually ran for that capture (see \"android-atf-harness\"); when it didn't run, the check doesn't run either rather than assume there's no title. It does not read the separate window title (AccessibilityWindowInfo#getTitle(), API 24), which TalkBack also announces and which the check has no way to credit -- confirmed on samples/BuggyApp's MainPage, which sets a MAUI Page.Title shown in its NavigationPage toolbar and has two on-screen headings, yet still has no pane title, so the check fires there. On Android 8.0/8.1 (API 26-27: at or above the harness's own minSdk 26, but below getPaneTitle's API 28), the harness runs but can never read a pane title on any screen, so the check reports \"no pane title exposed\" for every screen on those two OS versions regardless of whether one is actually set. Not implemented on iOS: XCUITest exposes a \"navigationBar\" element (used elsewhere only to crop screenshots), but nothing has verified that its presence or title reliably matches WCAG2ICT's \"software titled\" concept across screen types (tab-bar-only screens, sheets, screens without a navigation bar by design).",
            Impact = "No 2.4.2 check runs at all when the harness didn't run. A screen whose title is only a window title (not a pane title) is flagged even though a screen reader may announce that window title. On Android 8.0/8.1 specifically, every screen is flagged for review even when it has a title. No automated 2.4.2 check on iOS.",
            ManualCheck = "Check each screen has a title describing its purpose, announced by the screen reader on screen change, on every platform and OS version.",
            Rules = ["page-titled"],
        },
        new()
        {
            Id = "android-labelfor",
            Area = LimitationArea.Detection,
            Platforms = [Platform.Android],
            Title = "android:labelFor / getLabeledBy() associations aren't read",
            Description = "Android lets a visible label name a field it isn't the content of, via android:labelFor (XML) or View#setLabelFor()/getLabeledBy() -- TalkBack is documented to use this to announce the label together with the field. Confirmed 2026-09-23 with samples/NativeAndroid's Views screen: a field labelled this way produces an uiautomator dump node identical to a genuinely unlabelled one (empty content-desc, empty hint); uiautomator dump's XML has no attribute for the relationship at all, and the instrumentation harness does not currently read getLabeledBy() either. Not tested with TalkBack directly.",
            Impact = "missing-name reports a field labelled only via android:labelFor exactly like an unlabelled one -- a false positive if TalkBack does announce the association as documented.",
            ManualCheck = "For a text field flagged by missing-name, check whether its visible label is wired up with android:labelFor before treating it as unlabelled; confirm with TalkBack that the label is (or isn't) announced.",
            Rules = ["missing-name"],
        },
        new()
        {
            Id = "android-compose-merged-name",
            Area = LimitationArea.Rules,
            Platforms = [Platform.Android],
            Title = "A name set inside a Jetpack Compose button can land on a different uiautomator node than its clickable one",
            Description = "Verified 2026-09-23 with samples/NativeAndroid's Compose screen: for several IconButtons/Buttons whose accessible name is set with contentDescription on a child Icon() or with Modifier.semantics { contentDescription = ... } on the button itself, uiautomator dump's clickable=\"true\", focusable=\"true\" node reports an empty content-desc, and the name appears only on a separate, non-clickable child node (confirmed in the raw dump; bounds and the name are both present, just on a different node). One IconButton on the same screen, built the same way but with contentDescription = null, correctly showed up as an unlabeled clickable button, and a TextField's label = { ... } parameter correctly named the field -- so this isn't every Compose control, and the exact condition that causes the split hasn't been pinned down. Partly addressed 2026-09-23 for the unambiguous case: when exactly one non-focusable descendant carries a name and nothing else in the subtree is independently focusable or clickable, UiAutomatorParser.TryMergeSingleDescendantName gives the clickable node that name as its own Label, approximating how a screen reader is expected to read the merged node together (matching the existing ScreenReaderPredictor.AccessibleName fallback, but now also seen by rules that read AccessibilityNode.Label directly). Deliberately NOT merged: a button with SEVERAL named descendants (for example an icon's contentDescription alongside a separate visible-text child, both under one merged Button) -- guessing which text is actually announced, and in what order, risks a wrong label-in-name call. Tested with TalkBack for this several-named-descendants shape only, on one button (N5, see this limitation's Impact); not tested with TalkBack for the single-named-descendant merge case above.",
            Impact = "identifier-name now reports the clickable element's real role (e.g. \"button\" instead of \"group\") for the single-named-descendant case. missing-name's own result is unchanged -- it already found these buttons named through ScreenReaderPredictor.AccessibleName's descendant-walk fallback, so the model-level fix only makes that more robust for other rules, not a behavior change for missing-name itself. Corrected assumption: target-size was never blocked by this gap -- it already reads the clickable node's own (correct) bounds regardless of naming; a measured 48x48dp Compose IconButton not tripping target-size is IconButton's own minimum-touch-target padding (similar to samples/BuggyApp's B5, though B5 enlarges to only 44dp and still trips the platform advisory), not this naming gap. Still open: label-in-name reports nothing for a several-named-descendants button where the visible text and an overriding name are split across two different children (see samples/NativeAndroid's N5) -- a real mismatch there would be missed; identifier-name also still reports the wrong (non-clickable) role in that same several-named-descendants case, since nothing is merged there. Google's Accessibility Test Framework's ImageContrastCheck also reported nothing for a low-contrast icon built the same way, for a reason not determined. With --screen-reader, screen-reader-label-in-name (added 2026-09-25) does evaluate a several-named-descendants button like N5 -- it reads the visible text from N5's only descendant that has any (\"Pay\"; the separate \"Submit\" content-desc lives on a different, non-merged descendant and isn't part of this comparison) -- so it can report a real mismatch between a control's visible text and what TalkBack actually says, a case label-in-name structurally can't check at all -- confirmed on a real app (2026-09-30): a real TalkBack capture of N5 announced it as \"Submit. Pay. Button\" on a Pixel 4a (one utterance, TalkBack 17.0.1) and \"Submit || Pay || Button\" on the Android emulator (three separate utterances, TalkBack 16.0.0, see docs/case-study.md \"Real TalkBack capture, in five languages\"), both of which contain \"Pay\" as its own word, so this rule reports nothing for N5 itself on either device. This check runs whenever a capture reaches every focusable or interactive element it found and TalkBack said something for each (see \"android-screen-reader-capture\"). Whether Voice Access would actually activate the button by saying \"Pay\" hasn't been tested.",
            ManualCheck = "For a Compose button whose visible text and accessible name might differ (an icon plus separate text under one merged control), check with speech input (Voice Access) whether saying the visible text activates it, not only what a screen reader announces.",
            Rules = ["label-in-name", "identifier-name", "atf", "screen-reader-label-in-name"],
        },
        new()
        {
            Id = "android-screen-reader-capture",
            Area = LimitationArea.ScreenReader,
            Platforms = [Platform.Android],
            Title = "TalkBack capture is opt-in, checks only some of the screen, and silences the phone while it runs",
            Description = "With --screen-reader, the instrumentation harness turns TalkBack on, moves accessibility focus to each focusable/interactive element in Swipewalk's own tree order (not TalkBack's own swipe order -- this checks what TalkBack says about an element, not its navigation order), and reads back exactly what TalkBack said. This works by temporarily making a small app Swipewalk installs (harness/android/ttsengine, labelled \"Swipewalk (testing only)\" in the device's text-to-speech settings) the device's default text-to-speech engine: Google's TalkBack sends its speech to whichever engine that setting names, seen with both TalkBack 16 and 17, so Swipewalk's engine receives the exact utterance text and reports it back, entirely on-device (an earlier design read TalkBack's on-screen caption with an OCR library; that approach and its one non-local-only exception are gone). Swipewalk's engine does not speak the text through anything else -- TalkBack is effectively silent for the length of the capture (a pass-through to the phone's normal engine was tried and removed: it made TalkBack repeat itself in a fast, unbounded loop). Don't run this on a phone someone is relying on TalkBack with right now. Because this changes accessibility settings, every SETTING it changes (which service is enabled, touch exploration, the default text-to-speech engine) is restored on three independent paths: the capture's own restore when it finishes normally; a marker file read back at the start of the next --screen-reader run or by pre-flight, if the process was killed before its own restore ran; and a timer armed on the device itself before anything changes and cancelled on a normal finish, which restores those settings on its own if Swipewalk stops responding or is disconnected from the phone -- verified on a Pixel 4a by killing both the host command and the on-device capture process and confirming the phone put the settings back with no computer involved, within about 90 seconds when an exact alarm is allowed on the device (a locked-down device without that permission falls back to an inexact alarm, which can be several minutes later). The helper app itself is uninstalled once every changed setting reads back as restored, which removes its WRITE_SECURE_SETTINGS grant with it; only a restore that failed or could not be confirmed leaves it installed (the on-device safety timer above and `doctor`'s own leftover-repair both live inside it, so removing it early would break exactly the safety net this is about). This does mean a `record` session with --screen-reader on for several screens reinstalls and grants the app again before each one. On a physical phone, --screen-reader asks for confirmation before any of this (a non-interactive run needs --screen-reader-confirm instead); use a test device regardless. The text-to-speech routing was verified on a Pixel 4a (Android 13, TalkBack 17.0.1) and the Android emulator (TalkBack 16.0.0); an older TalkBack version was seen to split one element's name and its role word into two separate utterances rather than one combined announcement, which the comparator now recovers correctly. The comparison across languages was verified on an Android emulator with the system language set to English, Spanish, Hindi, Arabic and Japanese: TalkBack said its own role and hint words in that language in every case, but did not translate the app's own accessible names in any of them, so a localized role word alone (for example Spanish \"Botón\") is not reported as a name difference -- the trade-off is that a genuine difference could go unreported when nothing was predicted for that element to begin with, EXCEPT when the text looks like a developer identifier rather than a plausible role word. That exception catches a real problem the tree alone can't show, seen on samples/BuggyApp regardless of language: a button with an empty accessible name in the tree, which TalkBack nonetheless announced as \"btnCancelPayment\" (that button's AutomationId, planted as bug B7 -- where TalkBack itself got that text wasn't determined). Because that text looks like a developer identifier, it is still reported as a difference even on a device whose language this has no role vocabulary for. Plain text isn't captured (only focusable/interactive elements are, to keep the cost down, roughly 1-2 seconds per element); this capture counts as \"complete\" when every focusable/interactive element the walk found was reached and TalkBack said something for it, not when the whole screen (plain text included) was covered -- the report never describes this route as covering the whole screen. An element TalkBack said nothing for makes the whole screen's capture \"not complete\", with a reason naming how many (a silent element is never reported as a finding itself); if nothing comes back at all for the first couple of elements, the capture stops with a reason rather than walking the rest of the screen for nothing (a managed device or a phone maker's own TalkBack build may not honor the text-to-speech engine setting).",
            Impact = "Off by default: without --screen-reader, screen-reader evidence stays predicted only (see \"predicted-screen-reader\"), and nothing on the device changes. With it: TalkBack speaks nothing aloud for the length of the capture; a screen with many elements takes noticeably longer to scan; plain text and silently-skipped elements are never checked at all; a managed device, a locked-down TTS setting, or a TalkBack build this hasn't been verified against skips the whole capture for a screen, with a reason shown in the report, rather than reporting something unreliable; and a `record` session that checks several screens reinstalls and re-grants the helper app before each one, since it's removed again as soon as each capture's restore succeeds.",
            ManualCheck = "Swipe through plain text by hand (it isn't captured), and compare the report's captured transcript against a manual TalkBack pass if a finding looks surprising.",
            Rules = ["screen-reader-capture", "screen-reader-label-in-name"],
        },

        // ---- iOS ----
        new()
        {
            Id = "ios-large-text-physical-settings",
            Area = LimitationArea.Coverage,
            Platforms = [Platform.iOS],
            Title = "On a physical iPhone the large-text check drives the Settings app",
            Description = "On a physical iPhone, scan --large-text and record set Larger Text to AX3 (about 235%) through the Settings app, return to the app, and restore the original setting afterwards. This takes a couple of minutes per screen and needs the phone unlocked. The Settings layout can change between iOS versions; if the path isn't found, Swipewalk uses a per-app text-size launch setting instead, and otherwise skips the check with a reason.",
            Impact = "On a new iOS version the check may fall back or be skipped until Swipewalk is updated; the report says which method was used or why the check was skipped.",
            ManualCheck = "If the check was skipped, set Settings > Accessibility > Display & Text Size > Larger Text to a large size and check each screen by hand.",
        },
        new()
        {
            Id = "appearance-physical-iphone",
            Area = LimitationArea.Coverage,
            Platforms = [Platform.iOS],
            Title = "The appearance rescan doesn't support a physical iPhone yet",
            Description = "scan --appearance both works on the iOS Simulator (via `simctl ui appearance`) and on Android, real device or emulator (via `cmd uimode night`), but Swipewalk doesn't switch a physical iPhone's appearance yet, so the check is skipped there with a reason.",
            Impact = "On a physical iPhone, only the device's current appearance is checked; a contrast failure that only shows up in the other appearance is missed unless the phone is switched by hand and scanned again.",
            ManualCheck = "On a physical iPhone, switch Settings > Display & Brightness between Light and Dark by hand and scan again in each.",
            Planned = "Switching appearance on a physical iPhone",
        },
        new()
        {
            Id = "orientation-physical-iphone",
            Area = LimitationArea.Coverage,
            Platforms = [Platform.iOS],
            Title = "The orientation rescan doesn't support a physical iPhone yet",
            Description = "scan --orientation both works on the iOS Simulator (via the scanning harness's XCUIDevice.shared.orientation -- there is no `simctl` equivalent) and on Android, real device or emulator (via `settings put system accelerometer_rotation`/`user_rotation`), but Swipewalk doesn't rotate a physical iPhone yet, so the check is skipped there with a reason.",
            Impact = "On a physical iPhone, only the device's current orientation is checked; a screen restricted to one orientation is missed unless the phone is rotated by hand (with rotation lock off) and scanned again.",
            ManualCheck = "On a physical iPhone, turn off rotation lock, rotate the device by hand, and scan again to check whether the screen followed.",
            Planned = "Rotating a physical iPhone",
        },
        new()
        {
            Id = "auto-update-detection-heuristic",
            Area = LimitationArea.Coverage,
            Title = "The auto-updating-content check is a heuristic (it can miss or over-flag content), and only runs in scan",
            Description = "scan --auto-update-content takes a few further captures of a screen a few seconds apart, with no input, and reports content that changed across two consecutive intervals (not just one) for review against WCAG 2.2.2 Pause, Stop, Hide. It compares accessibility trees, not screenshot pixels, so it never mistakes a blinking text-input caret for a change, and a loading spinner or other one-off transition that settles within the first interval is never reported. Requiring two consecutive intervals is a heuristic, not a guarantee: it does not distinguish sustained change from two unrelated, near-instantaneous transitions that happen to land just before and just after the middle capture (for example a screen that loads in two visible steps), and it compares any element that changed in each window, not necessarily the same element both times -- either case can be reported the same as genuine sustained content. Going the other way, it can also miss real auto-updating content: a carousel or ticker whose cycle length happens to closely match --auto-update-interval can look unchanged at every capture, and a single slow change spanning more than one interval (rather than a settled one-off change) looks the same as a settled one here. It also can't tell whether the changing content is shown alongside other content (WCAG 2.2.2 only applies when it is -- content that is the only thing on the screen, like a preloader, is exempt) or whether a pause/stop/hide control already exists elsewhere on the screen, so every finding needs both checked by hand. It never touches the device, unlike the appearance and orientation rescans, so it works on a physical device too -- but it is scan only for now; record and the desktop app don't offer it yet. Verified on a live scan against a planted auto-advancing bug (Android emulator and a physical Pixel: detected 2 of 3 runs, correctly not reported on the 1 run where the content's own 2-second cycle happened to alias with the 3-second capture interval; iOS Simulator: the check itself runs and completes with no false positive on static content, not yet verified detecting a planted auto-updating bug there).",
            Impact = "A screen with no auto-updating-content finding has not been shown to have no moving, blinking, scrolling or auto-updating content; a finding is not proof the content is sustained, shown alongside other content, or missing a control; and the check isn't available at all outside scan.",
            ManualCheck = "Watch each screen for at least 10-15 seconds with no input and check any moving, blinking, scrolling or auto-updating content for a way to pause, stop or hide it.",
            Planned = "Wiring the check into record and the desktop app",
        },
        new()
        {
            Id = "ios-overlays-during-recording",
            Area = LimitationArea.Detection,
            Platforms = [Platform.iOS],
            Title = "iOS overlays over the app may be captured while recording",
            Description = "Record mode captures only while the app under test is in front, but iOS can report the app as in front while Notification Center or Control Center is pulled down over it.",
            Impact = "A screenshot taken at that moment could include notifications.",
            ManualCheck = "Don't open Notification Center or Control Center while recording, and review screenshots before sharing reports.",
        },
        new()
        {
            Id = "ios-install-unsigned-ipa",
            Area = LimitationArea.Devices,
            Platforms = [Platform.iOS],
            Title = "A device .ipa must be signed for that device to be installed",
            Description = "Any installed app can be scanned, whoever signed it. But iOS only installs an .ipa file signed for the device: a development or ad-hoc build whose profile includes it, or an enterprise build. App Store .ipa files cannot be sideloaded, and Swipewalk does not re-sign apps.",
            Impact = "Builds signed for other devices, or App Store builds from a file, cannot be installed and scanned from the file.",
            ManualCheck = "Install the app through TestFlight, the App Store or your MDM and scan it by --bundle-id, or scan a Simulator build.",
            Planned = "Installing builds signed for other devices",
        },
        new()
        {
            Id = "ios-physical-devices",
            Area = LimitationArea.Devices,
            Platforms = [Platform.iOS],
            Title = "Physical iPhones: slower recording, some checks unverified",
            Description = "scan and record work on physical iPhones and iPads (tested on an iPhone with iOS 18.6). The harness is signed with a fitting installed development profile (no Apple ID needed) or Xcode automatic signing, and exchanges files through devicectl. Each check of the screen takes several seconds, so new screens are noticed with a delay. The first run asks for Face ID or the passcode to allow UI automation. Manual signing with a hand-installed profile, and the pause when another app comes to the front during recording, are covered by tests but not yet confirmed on hardware. Bringing the app to the front (or starting it) when a scan or recording begins was verified on the Simulator only.",
            Impact = "Recording on a physical device notices screen changes a few seconds late. If the in-front check does not work on a device, other apps are still not captured unless a new screen of the target app appears while they are in front.",
            ManualCheck = "Wait for each screen to be reported before moving on, don't switch apps while recording, and review screenshots before sharing reports.",
        },
        new()
        {
            Id = "ios-accessibility-element",
            Area = LimitationArea.Detection,
            Platforms = [Platform.iOS],
            Title = "XCUITest doesn't expose isAccessibilityElement or traits",
            Description = "Whether VoiceOver can reach an element is approximated from its type and label. Traits such as header and adjustable are not visible to the scanner through XCUITest. With --screen-reader's Accessibility Inspector route (see \"ios-inspector-walk-capture\"), traits ARE visible for the elements it walks -- but only for a small, deliberately conservative subset of predicted roles (button, link, image, heading, slider) that this is confident the Inspector's trait vocabulary can confirm or deny; a role Swipewalk never predicted in the first place (for example a heading with no visible sign of being one) is still not found even then.",
            Impact = "Reachability can be predicted wrongly for custom views, so missing heading and control roles (1.3.1, 4.1.2) are not detected except where Apple's audit reports them, or where the Inspector route flags one of the small set of roles above as unconfirmed for review. On a screen with an Accessibility Inspector capture, 1.3.1's row in the WCAG coverage lists the elements the Inspector reported with the Header trait -- this is evidence for the manual check (VoiceOver itself is not turned on for it, and it cannot show text that looks like a heading but isn't exposed as one), never an automated finding or a status change.",
            ManualCheck = "Swipe through with VoiceOver and listen for the element type and state of each control.",
        },
        new()
        {
            Id = "ios-geometric-order",
            Area = LimitationArea.ScreenReader,
            Platforms = [Platform.iOS],
            Title = "VoiceOver order is predicted from positions",
            Description = "The predicted swipe order sorts elements top to bottom and left to right. Custom accessibilityElements ordering set by the app is not visible. --screen-reader's Accessibility Inspector route captures the Inspector's own navigation order (Apple's tool for previewing what VoiceOver would read, not VoiceOver itself running) -- but an order difference from it isn't reported, on any screen: confirmed on a real device (2026-09-25) that the walk starts wherever the person clicked to set it up, not the top of the screen, and its order is circular, so an unanchored walk would look rotated relative to the predicted order in a way this can't tell apart from a genuine difference (see \"ios-inspector-walk-capture\").",
            Impact = "The predicted order may differ from VoiceOver's where the app customizes it; the Inspector route doesn't yet confirm or deny this either way.",
            ManualCheck = "Swipe through with VoiceOver and check the order is logical.",
        },
        new()
        {
            Id = "ios-inspector-walk-capture",
            Area = LimitationArea.ScreenReader,
            Platforms = [Platform.iOS],
            Title = "The Accessibility Inspector route is opt-in, needs a person present, and covers scan only for now",
            Description = "With --screen-reader on iOS, `scan` (not yet `record`) walks Xcode's Accessibility Inspector's Inspection menu (\"Move to Next/Previous Item\") over the macOS Accessibility API, reading its panel's label, value, traits, identifier, hint and class for each element the Inspector reaches. VoiceOver itself is never turned on -- this reports the Inspector's own view of the same accessibility properties VoiceOver would read, not real recorded speech. It needs the macOS Accessibility permission (System Settings > Privacy & Security > Accessibility) for whichever app is responsible for the process running Swipewalk -- which lets that app operate other apps on the Mac, not only the Inspector, though Swipewalk itself only ever uses it for the Inspector -- asked for before the Inspector is used; if it's missing or declined, the run falls back to the predicted-only report with a clear reason recorded, never silently. It also needs a one-time manual step, per Inspector session, that has no Accessibility-API surface at all: opening Accessibility Inspector, choosing the target device in its own toolbar, and clicking the first element on the app's screen so the walk starts from the top -- both asked for directly by the prompt; if that click lands on the app's window or background rather than a real element, the walk is detected as implausible (every item empty, or far fewer items than the screen's predicted stops) and marked incomplete with a reason to click an element and scan again, rather than being reported as a clean, complete walk of nothing (found on a real device, 2026-09-25, samples/NativeiOS). \"Per Inspector session\" was observed to hold across one screen change, on a physical iPhone (2026-09-25, samples/NativeiOS): after the one setup click, the person opened a different screen in the app with no more clicking in the Inspector at all, and the next scan's walk followed onto that new screen and captured it completely (17 elements) -- one data point, not a guarantee for every app or every screen change; if a walk comes back empty or short after navigating, click an element in the Inspector and scan again. Because the Inspector reports no frame/geometry field, a captured element is matched to the scanned tree by its accessibility identifier first, then by its accessible name (disambiguating same-named elements by their native class where possible), then by position alone as a last resort -- recorded per item in results.json as Exact, Likely, Weak or unmatched, so a Weak or unmatched item is a hint to check by hand, not confirmed evidence of anything. The Inspector's own placeholder text for an unset field (\"None\", or \"Empty string\" for an empty text value) is normalized to nothing before matching or comparing, so it is never mistaken for a real name (found on a real device, 2026-09-25, samples/BuggyApp) -- this does mean a genuine label or value that happens to be exactly that text is also treated as unset. A UIToolbar's own container and a scroll view's built-in scroll-position indicator each get a real XCUITest accessibility label (\"Toolbar\", \"Vertical scroll bar, 1 page\") -- on the same physical-iPhone capture, the Accessibility Inspector's own Next/Previous Item walk never stopped on either one (VoiceOver itself was not run, and it may still reach a scroll bar's indicator by touch even though this walk did not), so both are excluded from the predicted transcript entirely (a fix in the tree parser, not just this comparison), not only from this route's findings. Order differences from this route are not reported at all (see \"ios-geometric-order\"): confirmed on a real device that the walk starts wherever the person clicked, not the top of the screen, and the Inspector's navigation order is circular, so an unanchored walk looks rotated relative to the predicted order in a way this can't tell apart from a genuine difference.",
            Impact = "Off by default, and only wired into scan for now -- record mode still reports predicted-only screen-reader evidence on iOS, the same as before this route existed. Needs a person present for the one-time device pick and element click, so it can't run unattended (a non-interactive run, or a declined/missing permission, falls back to the predicted-only report); clicking the wrong thing (the app's window instead of an element) is caught and reported as incomplete rather than a false clean pass. A Weak-confidence or unmatched item is a hint to check by hand, not a confirmed name or role difference; only Exact and Likely matches are used for name and role comparisons. No order (1.3.2, 2.4.3) differences are reported from this route at all. A real label or value of exactly \"None\" or \"Empty string\" would be read as unset.",
            ManualCheck = "Swipe through with VoiceOver directly for anything the Inspector route was declined for, only partly captured, or matched with Weak confidence -- and for swipe order generally, since this route doesn't check it at all.",
            Planned = "Wiring the Inspector route into record mode; a way to anchor the walk's starting point so order differences can be reported.",
            Rules = ["screen-reader-capture"],
        },
        new()
        {
            Id = "ios-apple-audit",
            Area = LimitationArea.Rules,
            Platforms = [Platform.iOS],
            Title = "Apple audit results are listed for review",
            Description = "Issues from Apple's accessibility audit are included, but Apple does not document its thresholds against WCAG, so audit-only issues are marked Needs review.",
            Impact = "Some audit items may not be WCAG issues; some WCAG issues are outside the audit's scope.",
            ManualCheck = "Confirm each Apple audit item by hand before reporting it as a WCAG issue.",
            Rules = ["engine"],
        },
        new()
        {
            Id = "ios-sf-symbol-default-label",
            Kind = LimitationKind.FrameworkNote,
            Area = LimitationArea.Rules,
            Platforms = [Platform.iOS],
            Title = "A UIButton or SwiftUI Button built from a common SF Symbol may already have a name",
            Description = "Verified 2026-09-23 with samples/NativeiOS: a UIButton/SwiftUI Button whose only content is UIImage(systemName: \"magnifyingglass\") (no accessibilityLabel set anywhere) was captured with the accessible name \"Search\" on both the Simulator and a physical iPhone. Apple appears to supply default accessibility descriptions for at least some SF Symbols, and UIKit/SwiftUI expose that description as the control's name when nothing else is set -- observed here for magnifyingglass only, not surveyed across other symbols. Android's equivalent (an ImageButton or Compose Icon with no contentDescription) has no such default -- confirmed on the same sample's Android screens, where the identical mistake correctly produces a missing-name finding.",
            Impact = "An icon-only button with no explicit accessible name produces no missing-name finding when its SF Symbol supplies a default one. That default describes the symbol (for example \"Search\" for a magnifying glass), not necessarily what the control does here -- a magnifying-glass icon used to zoom rather than search would get the same default name, and Swipewalk has no way to tell it's wrong for the context.",
            ManualCheck = "For an icon-only control, check with VoiceOver that the announced name actually describes what the control does here, not just that a name exists.",
            Rules = ["missing-name"],
        },
        new()
        {
            Id = "ios-swiftui-accessibility-label-hides-text",
            Area = LimitationArea.Rules,
            Platforms = [Platform.iOS],
            Title = "SwiftUI's .accessibilityLabel() replaces a button's visible text in the captured tree",
            Description = "Verified 2026-09-23 with samples/NativeiOS: a SwiftUI Button(\"Pay\") { }.accessibilityLabel(\"Submit\") is captured with accessible name \"Submit\" and no trace of the visible text \"Pay\" anywhere in the tree. The equivalent UIKit button (a title \"Pay\" plus accessibilityLabel = \"Submit\") keeps \"Pay\" as a separate child node alongside the button's own \"Submit\" label, which is what label-in-name compares against.",
            Impact = "label-in-name cannot detect a label-in-name mismatch (WCAG 2.5.3) on a SwiftUI control whose visible text and accessible name were set this way: there is nothing left in the tree to compare the accessible name to, so the rule reports nothing, even though a sighted speech-input user sees one word and a screen reader announces another.",
            ManualCheck = "For a SwiftUI button, compare its visible text against what VoiceOver announces directly, rather than relying on label-in-name to catch a mismatch.",
            Rules = ["label-in-name"],
        },

        // ---- Windows ----
        new()
        {
            Id = "windows-not-supported",
            Area = LimitationArea.Devices,
            Platforms = [Platform.Windows],
            Title = "Windows apps can't be scanned yet",
            Description = "The Windows collector (UI Automation with axe-windows) is not implemented.",
            Impact = "No automated results for Windows.",
            ManualCheck = "Test with Narrator and keyboard only, or use Accessibility Insights for Windows.",
            Planned = "Windows support",
        },

        // ---- .NET MAUI ----
        new()
        {
            Id = "maui-android-tap-gesture",
            Area = LimitationArea.Detection,
            Platforms = [Platform.Android],
            Frameworks = [AppFramework.Maui],
            Title = "MAUI tap gestures on labels are invisible on Android",
            Description = "A MAUI Label (or other view) with a TapGestureRecognizer is not marked clickable in Android's accessibility tree, so the scanner doesn't see it as a control.",
            Impact = "Target size and naming checks are skipped for such elements. Their button role is not exposed (4.1.2 Name, Role, Value), and TalkBack users may not be able to activate them.",
            ManualCheck = "With TalkBack on, double-tap every tappable text or image and check it works. Prefer Button or ImageButton; SemanticProperties.Description adds a name but not a button role or activation.",
        },
        new()
        {
            Id = "maui-ios-detection",
            Area = LimitationArea.FixExamples,
            Platforms = [Platform.iOS],
            Frameworks = [AppFramework.Unknown],
            Title = "MAUI-specific advice on a physical iPhone needs the MAUI option",
            Description = "Scanning and findings work for any app, however it was installed; framework detection only affects MAUI-specific advice. Swipewalk recognizes .NET MAUI and its Microsoft.Maui.Controls version by reading the app's files, which it can do on the iOS Simulator and on a physical iPhone when Swipewalk installed the app itself (--install <.app/.ipa>), but not for an app already on a physical iPhone (installed through Xcode, TestFlight, the App Store or an MDM). For those, choose \"App built with .NET MAUI (fix examples in XAML)\" in the desktop app, or pass --framework maui, to get MAUI fix examples. The Microsoft.Maui.Controls version stays unknown either way, so version-specific hints (for example about live text-size updates, fixed in 10.0.100) ask you to check it yourself.",
            Impact = "Without the MAUI option or flag, fix examples for a MAUI app on a physical iPhone Swipewalk didn't install are in Swift rather than MAUI XAML, the report can't show a Microsoft.Maui.Controls version or apply version-specific advice, and text-size findings leave out their .NET MAUI likely causes. Which findings are raised, and their WCAG mapping, is unaffected.",
            ManualCheck = "If the app uses .NET MAUI, tick \"App built with .NET MAUI (fix examples in XAML)\" in the desktop app or pass --framework maui, and check the app's Microsoft.Maui.Controls version yourself against 10.0.100 (see \"How .NET MAUI apps apply text-size changes\").",
        },
        new()
        {
            Id = "maui-automationid-not-announced",
            Kind = LimitationKind.FrameworkNote,
            Area = LimitationArea.ScreenReader,
            Frameworks = [AppFramework.Maui],
            Title = "MAUI AutomationId is never announced",
            Description = "AutomationId becomes the Android resource id and the iOS accessibilityIdentifier. Screen readers do not read it, so a control with only an AutomationId has no name.",
            Impact = "Controls that rely on AutomationId are reported as missing a name.",
            ManualCheck = "Use SemanticProperties.Description for the spoken name and keep AutomationId for tests.",
        },
        new()
        {
            Id = "maui-ios-text-size-restart",
            Kind = LimitationKind.FrameworkNote,
            Area = LimitationArea.Detection,
            Frameworks = [AppFramework.Maui],
            Title = "How .NET MAUI apps apply text-size changes",
            Description = "On iOS, Microsoft.Maui.Controls before 10.0.100 does not update text while the app keeps running after the system text size changes, only after the app is terminated and relaunched; this was fixed in 10.0.100 by dotnet/maui#34445 (merged 2026-06-23). Swipewalk reads the Microsoft.Maui.Controls version on the iOS Simulator, and on a physical iPhone when the app is installed with --install <.app/.ipa>. Otherwise the version is unknown and the report says so. The older issues describing the same symptom (dotnet/maui#16625, #11832) are still open as of 2026-09-22 but predate the fix. In our tests (Microsoft.Maui.Controls 10.0.60 and 10.0.101, iOS 26.5 Simulator), Shell tab bar titles did not grow with the text size in either version, live or after a restart; the text-resize check reports that as text that did not scale, which is expected on both tested versions, not a Swipewalk error. On Android, the default MainActivity has no FontScale in ConfigurationChanges, so a font-scale change restarts the activity (returning to its first page) instead of updating text in place; adding ConfigChanges.FontScale avoids the restart, but then text keeps its old size until the app is relaunched unless the app re-applies fonts itself. Separately, `dotnet new maui` with SDK 10.0.300 resolved Microsoft.Maui.Controls 10.0.20 (before the fix) by default, so a new project needs `<MauiVersion>` set explicitly to get 10.0.100 or later.",
            Impact = "People who change text size mid-task see the old size until they restart the app (iOS, and Android when FontScale is handled) or lose their place (Android's default behavior). Shell tab titles may be listed as not resizing even in apps on a fixed MAUI version. record and scan --large-text report the restart-only case as one screen-level platform advisory once the restart is confirmed to show the same screen.",
            ManualCheck = "Change the text size while the app is running and check whether it takes effect without a restart; if not, relaunch and check text at the new size for clipping. Check the app's Microsoft.Maui.Controls version against 10.0.100.",
            Rules = ["text-resize-live"],
        },
        new()
        {
            Id = "maui-imagebutton-touch-area",
            Kind = LimitationKind.FrameworkNote,
            Area = LimitationArea.Rules,
            Frameworks = [AppFramework.Maui],
            Title = "MAUI may enlarge small ImageButtons at runtime",
            Description = "An ImageButton declared 20×20 was measured 44×44 on screen on both Android and iOS, because the button is enlarged at runtime. The scanner uses the measured size, not the declared one.",
            Impact = "Sizes in results can differ from WidthRequest/HeightRequest in XAML. The scanner uses the accessibility frame, which is usually but not always the area that responds to touch.",
            ManualCheck = "Where the XAML size is below 24×24, tap near the edge of the reported area to confirm the enlarged area actually responds.",
            Rules = ["target-size"],
        },

        // ---- Other frameworks ----
        new()
        {
            Id = "other-framework-fix-examples",
            Area = LimitationArea.FixExamples,
            Frameworks = [AppFramework.Unknown],
            Title = "Fix examples are for MAUI and native APIs only",
            Description = "Scanning should work for any framework that exposes its UI to the operating system's accessibility layer, but only .NET MAUI and native apps have been tested. Fix examples exist only for .NET MAUI and native Android/iOS; Flutter, React Native, Jetpack Compose and SwiftUI get native or generic advice.",
            Impact = "The code example may not match the app's framework.",
            ManualCheck = "Apply the same fix with the framework's accessibility API (for example Semantics in Flutter, accessibilityLabel in React Native).",
            Planned = "More app frameworks",
        },
    ];

    public static IEnumerable<Limitation> For(Platform platform, AppFramework framework) =>
        All.Where(l => l.AppliesTo(platform, framework));
}
