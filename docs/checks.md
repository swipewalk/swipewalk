# Automated checks

<!-- Generated from src/Swipewalk.Core/Rules/DefaultRules.cs, RuleCatalog.cs,
     EngineIssueRule.cs and AtfIssueRule.cs.
     Regenerate: dotnet run --project src/Swipewalk.Cli -- checks > docs/checks.md -->

This is every automated check Swipewalk runs: Swipewalk's own rules, plus the checks it reads from the platform accessibility engines it wraps (Apple's accessibility audit on iOS, Google's Accessibility Test Framework on Android). Each check tests only part of the WCAG criteria it's listed against -- see [docs/limitations.md](limitations.md). A screen with no findings has not been shown to meet WCAG or any law; most WCAG 2.2 success criteria have no automated check at all.

- **WCAG issue** -- a possible failure of the cited criteria.
- **Needs review** -- automated checks can't decide; a person reviews against the cited criteria.
- **Platform advisory** -- doesn't meet a platform guideline; not a WCAG failure.

## Swipewalk's own rules

| Rule | What it checks | Kind | WCAG criteria | Platforms |
|---|---|---|---|---|
| `missing-name` | Interactive elements without an accessible name; images without a text alternative | WCAG issue (an interactive control with no accessible name); needs review (a text field whose name is ambiguous, or an image that may be decorative) | 1.1.1 Non-text Content (A); 4.1.2 Name, Role, Value (A) | Android, iOS |
| `target-size` | Touch targets below 24×24 (with the spacing exception) and below platform guidelines | WCAG issue (below 24×24 with no spacing exception); needs review (below 24×24 but nested in another target, a near-miss risk); platform advisory (not reported as a WCAG issue -- e.g. at least 24×24, or below 24×24 but meeting the spacing exception -- yet below the platform's own 44 pt / 48 dp guideline) | 2.5.8 Target Size (Minimum) (AA) | Android, iOS |
| `identifier-name` | Accessible names that look like developer identifiers (for review) | Needs review | 1.1.1 Non-text Content (A); 2.4.6 Headings and Labels (AA) | Android, iOS |
| `label-in-name` | Visible text missing from the accessible name | WCAG issue | 2.5.3 Label in Name (A) | Android, iOS |
| `text-contrast` | Text contrast measured from screenshot pixels | WCAG issue (below 3:1); needs review (3:1-4.5:1, since text size can't always be read from the screenshot; or the screenshot was blocked) | 1.4.3 Contrast (Minimum) (AA) | Android, iOS |
| `text-resize` | Partial 1.4.4 check (record mode and scan --large-text): text that does not grow at the OS text-size setting, and text that newly overlaps (1.4.4 at Android 200%; Apple Dynamic Type advisory at iOS AX3) | Needs review (text that did not grow, or overlap seen at or below 200%); platform advisory (overlap seen only above 200%, iOS AX3 only) | 1.4.4 Resize Text (AA) | Android, iOS |
| `text-resize-live` | Whether the OS text-size setting took effect while the app kept running, or only after a restart (platform advisory only, no WCAG criterion: 1.4.4 does not require live updates) | Platform advisory | No WCAG criterion mapped | Android, iOS |
| `text-resize-navigation` | Whether a system text-size change made the app show a different screen (typically its first), so the person loses their place (Android only; platform advisory only, no WCAG criterion: 1.4.4 does not require an app to keep its navigation state across the change) | Platform advisory | No WCAG criterion mapped | Android |
| `page-titled` | Whether the screen exposes a pane title to screen readers (Android only, needs the instrumentation harness; see KnownLimitations "android-atf-harness" and "android-page-title") | Needs review (no pane title found anywhere in the tree) | 2.4.2 Page Titled (A) | Android (needs the instrumentation harness) |
| `input-purpose` | Text fields whose label, hint or identifier suggests they collect one of WCAG 1.3.5's input purposes (for review; see KnownLimitations "input-purpose-heuristic") | Needs review | 1.3.5 Identify Input Purpose (AA) | Android, iOS |
| `icon-contrast` | Icon-only interactive controls' contrast against their background, measured from screenshot pixels (iOS only; Android is covered by the atf rule's ImageContrastCheck) | Needs review (below 3:1) | 1.4.11 Non-text Contrast (AA) | iOS |
| `large-text-lost-content` | Controls or text present at normal text size that are gone from the accessibility tree at the larger size, with no scrollable container that could reveal them (1.4.4 at Android 200%; Apple Dynamic Type advisory at iOS AX3) | Needs review (missing at the larger size with no scrollable container anywhere on the screen to reach it); platform advisory (loss seen only beyond 200%, iOS AX3 only) | 1.4.4 Resize Text (AA) | Android, iOS |
| `screen-reader-capture` | Differences between the predicted screen-reader transcript and what TalkBack actually said, captured by making Swipewalk's own text-to-speech engine TalkBack's default so it receives the exact spoken text (Android only for now, opt-in with --screen-reader; see KnownLimitations "android-screen-reader-capture") | Needs review (every difference from the predicted transcript is reported for a person to check, never as a confirmed WCAG failure by itself) | 4.1.2 Name, Role, Value (A); 1.1.1 Non-text Content (A) | Android (needs --screen-reader and the instrumentation harness; TalkBack) |

## Apple's accessibility audit (iOS, wrapped by the `engine` rule)

Runs Apple's own `performAccessibilityAudit` through the XCUITest harness; Apple does not document its thresholds against WCAG, so audit issues are reported for review, except `hitRegion`, which is a platform advisory against Apple's 44×44 pt guideline.

| Audit issue type | What it checks | Kind | WCAG criteria or platform guideline | Platforms |
|---|---|---|---|---|
| `contrast` | Text contrast against its background, checked against Apple's own audit threshold. | Needs review | 1.4.3 Contrast (Minimum) (AA) | iOS |
| `sufficientElementDescription` | Whether an element has an accessible description for VoiceOver. | Needs review | 4.1.2 Name, Role, Value (A) | iOS |
| `dynamicType` | Whether text supports the Dynamic Type (larger text) setting. | Needs review | 1.4.4 Resize Text (AA) | iOS |
| `textClipped` | Whether text is cut off at the text size the screen was captured at (not the large-text rescan). | Needs review | No WCAG criterion mapped | iOS |
| `trait` | Whether an element's accessibility traits (its role) match what it does. | Needs review | 4.1.2 Name, Role, Value (A) | iOS |
| `hitRegion` | Whether an element has a hit area Apple's audit considers too small (Apple's guideline is 44×44 pt; the audit's own threshold isn't documented). | Platform advisory | Apple Human Interface Guidelines: hit targets at least 44×44 pt | iOS |

## Google's Accessibility Test Framework (Android, wrapped by the `atf` rule)

Runs ATF 4.1.1's 14 checks (one of which, UnexposedTextCheck, never reports anything, and one, TextSizeCheck, has reported on some devices but not others; see below) through the Android instrumentation harness (harness/android); needs the harness to have installed and run (see "Google's Accessibility Test Framework needs the instrumentation harness to install and run" in [docs/limitations.md](limitations.md)). Most checks are reported for review, the same reasoning as the Apple audit above.

| ATF check | What it checks | Kind | WCAG criteria or platform guideline | Platforms |
|---|---|---|---|---|
| `SpeakableTextPresentCheck` | Whether an element a screen reader would focus on (for example a button, image or icon) has text for the screen reader to speak. | Needs review | 1.1.1 Non-text Content (A); 4.1.2 Name, Role, Value (A) | Android (needs the instrumentation harness) |
| `EditableContentDescCheck` | Whether an editable field has a fixed content description that could hide what was typed from a screen reader. | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs the instrumentation harness) |
| `ClassNameCheck` | Whether an element's exposed accessibility class matches a role a screen reader would recognize. | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs the instrumentation harness) |
| `TextContrastCheck` | Text color contrast against its background, estimated from the screenshot. | Needs review | 1.4.3 Contrast (Minimum) (AA) | Android (needs the instrumentation harness) |
| `ImageContrastCheck` | Icon or image contrast against its background, estimated from the screenshot. | Needs review | 1.4.11 Non-text Contrast (AA) | Android (needs the instrumentation harness) |
| `LinkPurposeUnclearCheck` | Whether a link's text is a generic phrase (for example "click here" or "learn more") that doesn't say where it leads on its own. | Needs review | 2.4.4 Link Purpose (In Context) (A) | Android (needs the instrumentation harness) |
| `TraversalOrderCheck` | Whether custom traversal-order settings (accessibilityTraversalBefore/After) form a loop or conflict. | Needs review | 1.3.2 Meaningful Sequence (A); 2.4.3 Focus Order (A) | Android (needs the instrumentation harness) |
| `TextSizeCheck` | Whether text sized in dp/px, or placed in a fixed-size container, may not grow with the system font size. Reported a finding on a physical Pixel 4a (Android 13) against a px-sized TextView (samples/NativeAndroid), but not on an Android 16 emulator scanning the exact same screen -- see docs/limitations.md. | Needs review | 1.4.4 Resize Text (AA) | Android (needs the instrumentation harness) |
| `UnexposedTextCheck` | Whether text drawn on screen is exposed to the accessibility API at all. Currently never reports anything: it needs text recognition the harness doesn't supply yet (see docs/limitations.md). | Needs review | 1.1.1 Non-text Content (A) | Android (needs the instrumentation harness) |
| `TouchTargetSizeCheck` | Touch targets below Android's own 48×48 dp guideline (stricter than WCAG's 24×24). | Platform advisory | Android accessibility guidelines: touch targets at least 48×48 dp | Android (needs the instrumentation harness) |
| `ClickableSpanCheck` | Whether a clickable span inside text is reachable by a screen reader; depends on the API level. | Needs review | No WCAG criterion mapped | Android (needs the instrumentation harness) |
| `DuplicateClickableBoundsCheck` | Whether two or more clickable elements share the same screen bounds. | Needs review | No WCAG criterion mapped | Android (needs the instrumentation harness) |
| `DuplicateSpeakableTextCheck` | Whether two or more elements have identical accessible names. | Needs review | No WCAG criterion mapped | Android (needs the instrumentation harness) |
| `RedundantDescriptionCheck` | Whether an accessible name redundantly states the element's role (for example the word "Button" inside a button's name). | Needs review | No WCAG criterion mapped | Android (needs the instrumentation harness) |
