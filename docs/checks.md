# Automated checks

<!-- Generated from src/Swipewalk.Core/Rules/DefaultRules.cs, RuleCatalog.cs,
     EngineIssueRule.cs, AtfIssueRule.cs and AxeIssueRule.cs.
     Regenerate: dotnet run --project src/Swipewalk.Cli -- checks > docs/checks.md -->

This is every automated check Swipewalk runs: Swipewalk's own rules, plus the checks it reads from the platform accessibility engines it wraps (Apple's accessibility audit on iOS, Google's Accessibility Test Framework on Android, and, with `--web-audit`, axe-core inside Android WebViews). Each check tests only part of the WCAG criteria it's listed against -- see [docs/limitations.md](limitations.md). A screen with no findings has not been shown to meet WCAG or any law; most WCAG 2.2 success criteria have no automated check at all.

- **WCAG issue** -- a possible failure of the cited criteria.
- **Needs review** -- automated checks can't decide; a person reviews against the cited criteria.
- **Platform advisory** -- doesn't meet a platform guideline; not a WCAG failure.

## Swipewalk's own rules

| Rule | What it checks | Kind | WCAG criteria | Platforms |
|---|---|---|---|---|
| `missing-name` | Interactive elements without an accessible name; images without a text alternative | WCAG issue (an interactive control with no accessible name); needs review (a text field whose name is ambiguous, an image that may be decorative, or an Android WebView image or control whose accessible name looks like an image file name, which could be real text or Chromium's own fallback for an unlabelled image) | 1.1.1 Non-text Content (A); 4.1.2 Name, Role, Value (A) | Android, iOS |
| `target-size` | Touch targets below 24×24 (with the spacing exception, and a needs-review flag when it looks inline in text) and below platform guidelines | Every finding states the target's measured size against each relevant threshold (WCAG's 24×24, plus Apple's 28×28 pt minimum and 44×44 pt default on iOS, or Android's 48×48 dp) as "at or above"/"below", never a verdict. WCAG issue (below 24×24 with no spacing exception, and not shaped like an inline text link); needs review (below 24×24 but nested in another target, a near-miss risk; or below 24×24, no spacing exception, and shaped like an inline text link next to other text -- WCAG 2.5.8's inline exception may apply, check by hand); platform advisory, no WCAG criterion (at least 24×24 but below a platform guideline, or below 24×24 but meeting the spacing exception -- on iOS split into two tiers: at or above Apple's 28×28 pt minimum but below its 44×44 pt default, or below the 28×28 pt minimum itself) | 2.5.8 Target Size (Minimum) (AA) | Android, iOS |
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
| `offscreen-unreachable` | Interactive controls or text present in the accessibility tree at normal text size but positioned wholly or mostly outside the visible screen, with no scrollable ancestor that could bring them into view (iOS only; see KnownLimitations "offscreen-unreachable-android-gap") | Needs review (wholly or mostly off-screen at normal text size with no scrollable ancestor, on a screen at least as large as WCAG 1.4.10's own 320×256 reference size); platform advisory (same finding on a smaller screen) | 1.4.10 Reflow (AA) | iOS |
| `orientation-restricted` | Whether the screen's content visibly follows the device being rotated between portrait and landscape (scan --orientation both; for review, since a single orientation can be essential to a screen) | Needs review (the screen still looked the same shape after the device was rotated -- always for review, never a confirmed failure, since a single orientation can be essential to a screen) | 1.3.4 Orientation (AA) | Android, iOS |
| `auto-updating-content` | Whether the screen changed on its own, with no input, in any gap between a few further captures spaced unevenly apart (scan --auto-update-content; for review, since the content may not be shown alongside anything else, a control to pause/stop/hide it may exist somewhere on the screen, or the update may be essential to an activity) | Needs review (content changed on its own in at least one gap between captures -- always for review, never a confirmed failure, since a pause/stop/hide control may exist, or the update may be essential to the screen) | 2.2.2 Pause, Stop, Hide (A) | Android, iOS |
| `screen-reader-capture` | Differences between the predicted screen-reader transcript and real evidence, opt-in with --screen-reader: on Android, what TalkBack actually said, captured by making Swipewalk's own text-to-speech engine TalkBack's default so it receives the exact spoken text (see KnownLimitations "android-screen-reader-capture"); on iOS (scan and record), what Xcode's Accessibility Inspector reports while walking the screen over the macOS Accessibility API -- VoiceOver itself is never turned on (see KnownLimitations "ios-inspector-walk-capture") | Needs review (every difference from the predicted transcript is reported for a person to check, never as a confirmed WCAG failure by itself) | 4.1.2 Name, Role, Value (A); 1.1.1 Non-text Content (A) | Android (TalkBack; needs --screen-reader and the instrumentation harness); iOS (Xcode's Accessibility Inspector; needs --screen-reader and a person present) |
| `screen-reader-label-in-name` | WCAG 2.5.3 Label in Name checked against what TalkBack actually said, for an interactive control with visible text (its own, or its only descendant's -- see KnownLimitations "android-compose-merged-name"): reported for review when the spoken name doesn't contain that text (Android only for now, opt-in with --screen-reader; see KnownLimitations "android-screen-reader-capture") | Needs review (a real screen-reader capture is evidence of the accessible name a speech-input user relies on, not proof of it) | 2.5.3 Label in Name (A) | Android (needs --screen-reader and the instrumentation harness; TalkBack) |

## Apple's accessibility audit (iOS, wrapped by the `engine` rule)

Runs Apple's own `performAccessibilityAudit` through the XCUITest harness; Apple does not document its thresholds against WCAG, so audit issues are reported for review, except `hitRegion`, which is a platform advisory against Apple's control-size guidance (a default of 44×44 pt and a stated minimum of 28×28 pt).

| Audit issue type | What it checks | Kind | WCAG criteria or platform guideline | Platforms |
|---|---|---|---|---|
| `contrast` | Text contrast against its background, checked against Apple's own audit threshold. | Needs review | 1.4.3 Contrast (Minimum) (AA) | iOS |
| `sufficientElementDescription` | Whether an element has an accessible description for VoiceOver. | Needs review | 4.1.2 Name, Role, Value (A) | iOS |
| `dynamicType` | Whether text supports the Dynamic Type (larger text) setting. | Needs review | 1.4.4 Resize Text (AA) | iOS |
| `textClipped` | Whether text is cut off at the text size the screen was captured at (not the large-text rescan). | Needs review | No WCAG criterion mapped | iOS |
| `trait` | Whether an element's accessibility traits (its role) match what it does. | Needs review | 4.1.2 Name, Role, Value (A) | iOS |
| `hitRegion` | Whether an element has a hit area Apple's audit considers too small (Apple names a default control size of 44×44 pt and a stated minimum of 28×28 pt; the audit's own threshold isn't documented, and it doesn't report the element's measured size, so Swipewalk can't say which of the two this is closer to). | Platform advisory | Apple Human Interface Guidelines: default control size 44×44 pt (minimum 28×28 pt) | iOS |

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

## axe-core's web-content audit (Android, wrapped by the `axe` rule)

Opt-in with `scan --web-audit` (Android debug/inspectable builds only): runs axe-core (Deque's open-source engine, vendored unmodified under third-party/axe-core) directly inside a WebView's own page, over the Chrome DevTools Protocol, for a deeper audit of the WebView's DOM than the native accessibility tree alone gives, restricted to axe-core's own WCAG 2.0/2.1/2.2 A and AA rules. Every one of axe-core 4.10.3's rules is listed below for reference, but the "Runs" column says which ones this restriction actually lets fire: no (best-practice or experimental rule, a rule axe-core itself marks deprecated -- which it never runs by default, whatever its tags -- or one only tagged WCAG AAA) for most of them, including five rules (`css-orientation-lock`, `label-content-name-mismatch`, `p-as-heading`, `table-fake-caption`, `td-has-header`) that DO carry a real, correctly-mapped WCAG tag below but are also tagged "experimental" by axe-core itself, which excludes them from this restriction the same way; see "The axe-core web-content audit (--web-audit) needs a debug build, and can't always place a finding on the report's screenshot" in [docs/limitations.md](limitations.md).

| axe-core rule | What it checks | Runs under --web-audit | Kind | WCAG criteria | Platforms |
|---|---|---|---|---|---|
| `accesskeys` | Ensure every accesskey attribute value is unique. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `area-alt` | Ensure `<area>` elements of image maps have alternative text. | Yes | Needs review | 2.4.4 Link Purpose (In Context) (A); 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-allowed-attr` | Ensure an element's role supports its ARIA attributes. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-allowed-role` | Ensure role attribute has an appropriate value for the element. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `aria-braille-equivalent` | Ensure aria-braillelabel and aria-brailleroledescription have a non-braille equivalent. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-command-name` | Ensure every ARIA button, link and menuitem has an accessible name. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-conditional-attr` | Ensure ARIA attributes are used as described in the specification of the element's role. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-deprecated-role` | Ensure elements do not use deprecated roles. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-dialog-name` | Ensure every ARIA dialog and alertdialog node has an accessible name. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `aria-hidden-body` | Ensure aria-hidden="true" is not present on the document body. | Yes | Needs review | 1.3.1 Info and Relationships (A); 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-hidden-focus` | Ensure aria-hidden elements are not focusable nor contain focusable elements. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-input-field-name` | Ensure every ARIA input field has an accessible name. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-meter-name` | Ensure every ARIA meter node has an accessible name. | Yes | Needs review | 1.1.1 Non-text Content (A) | Android (needs a debug/inspectable build) |
| `aria-progressbar-name` | Ensure every ARIA progressbar node has an accessible name. | Yes | Needs review | 1.1.1 Non-text Content (A) | Android (needs a debug/inspectable build) |
| `aria-prohibited-attr` | Ensure ARIA attributes are not prohibited for an element's role. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-required-attr` | Ensure elements with ARIA roles have all required ARIA attributes. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-required-children` | Ensure elements with an ARIA role that require child roles contain them. | Yes | Needs review | 1.3.1 Info and Relationships (A) | Android (needs a debug/inspectable build) |
| `aria-required-parent` | Ensure elements with an ARIA role that require parent roles are contained by them. | Yes | Needs review | 1.3.1 Info and Relationships (A) | Android (needs a debug/inspectable build) |
| `aria-roledescription` | Ensure aria-roledescription is only used on elements with an implicit or explicit role. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `aria-roles` | Ensure all elements with a role attribute use a valid value. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-text` | Ensure role="text" is used on elements with no focusable descendants. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `aria-toggle-field-name` | Ensure every ARIA toggle field has an accessible name. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-tooltip-name` | Ensure every ARIA tooltip node has an accessible name. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-treeitem-name` | Ensure every ARIA treeitem node has an accessible name. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `aria-valid-attr-value` | Ensure all ARIA attributes have valid values. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `aria-valid-attr` | Ensure attributes that begin with aria- are valid ARIA attributes. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `audio-caption` | Ensure `<audio>` elements have captions. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `autocomplete-valid` | Ensure the autocomplete attribute is correct and suitable for the form field. | Yes | Needs review | 1.3.5 Identify Input Purpose (AA) | Android (needs a debug/inspectable build) |
| `avoid-inline-spacing` | Ensure that text spacing set through style attributes can be adjusted with custom stylesheets. | Yes | Needs review | 1.4.12 Text Spacing (AA) | Android (needs a debug/inspectable build) |
| `blink` | Ensure `<blink>` elements are not used. | Yes | Needs review | 2.2.2 Pause, Stop, Hide (A) | Android (needs a debug/inspectable build) |
| `button-name` | Ensure buttons have discernible text. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `bypass` | Ensure each page has at least one mechanism for a user to bypass navigation and jump straight to the content. | Yes | Needs review | 2.4.1 Bypass Blocks (A) | Android (needs a debug/inspectable build) |
| `color-contrast-enhanced` | Ensure the contrast between foreground and background colors meets WCAG 2 AAA enhanced contrast ratio thresholds. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `color-contrast` | Ensure the contrast between foreground and background colors meets WCAG 2 AA minimum contrast ratio thresholds. | Yes | Needs review | 1.4.3 Contrast (Minimum) (AA) | Android (needs a debug/inspectable build) |
| `css-orientation-lock` | Ensure content is not locked to any specific display orientation, and the content is operable in all display orientations. | No | Needs review | 1.3.4 Orientation (AA) | Android (needs a debug/inspectable build) |
| `definition-list` | Ensure `<dl>` elements are structured correctly. | Yes | Needs review | 1.3.1 Info and Relationships (A) | Android (needs a debug/inspectable build) |
| `dlitem` | Ensure `<dt>` and `<dd>` elements are contained by a `<dl>`. | Yes | Needs review | 1.3.1 Info and Relationships (A) | Android (needs a debug/inspectable build) |
| `document-title` | Ensure each HTML document contains a non-empty `<title>` element. | Yes | Needs review | 2.4.2 Page Titled (A) | Android (needs a debug/inspectable build) |
| `duplicate-id-active` | Ensure every id attribute value of active elements is unique. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `duplicate-id-aria` | Ensure every id attribute value used in ARIA and in labels is unique. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `duplicate-id` | Ensure every id attribute value is unique. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `empty-heading` | Ensure headings have discernible text. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `empty-table-header` | Ensure table headers have discernible text. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `focus-order-semantics` | Ensure elements in the focus order have a role appropriate for interactive content. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `form-field-multiple-labels` | Ensure form field does not have multiple label elements. | Yes | Needs review | 3.3.2 Labels or Instructions (A) | Android (needs a debug/inspectable build) |
| `frame-focusable-content` | Ensure `<frame>` and `<iframe>` elements with focusable content do not have tabindex=-1. | Yes | Needs review | 2.1.1 Keyboard (A) | Android (needs a debug/inspectable build) |
| `frame-tested` | Ensure `<iframe>` and `<frame>` elements contain the axe-core script. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `frame-title-unique` | Ensure `<iframe>` and `<frame>` elements contain a unique title attribute. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `frame-title` | Ensure `<iframe>` and `<frame>` elements have an accessible name. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `heading-order` | Ensure the order of headings is semantically correct. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `hidden-content` | Informs users about hidden content. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `html-has-lang` | Ensure every HTML document has a lang attribute. | Yes | Needs review | 3.1.1 Language of Page (A) | Android (needs a debug/inspectable build) |
| `html-lang-valid` | Ensure the lang attribute of the `<html>` element has a valid value. | Yes | Needs review | 3.1.1 Language of Page (A) | Android (needs a debug/inspectable build) |
| `html-xml-lang-mismatch` | Ensure that HTML elements with both valid lang and xml:lang attributes agree on the base language of the page. | Yes | Needs review | 3.1.1 Language of Page (A) | Android (needs a debug/inspectable build) |
| `identical-links-same-purpose` | Ensure that links with the same accessible name serve a similar purpose. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `image-alt` | Ensure `<img>` elements have alternative text or a role of none or presentation. | Yes | Needs review | 1.1.1 Non-text Content (A) | Android (needs a debug/inspectable build) |
| `image-redundant-alt` | Ensure image alternative is not repeated as text. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `input-button-name` | Ensure input buttons have discernible text. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `input-image-alt` | Ensure `<input type="image">` elements have alternative text. | Yes | Needs review | 1.1.1 Non-text Content (A); 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `label-content-name-mismatch` | Ensure that elements labelled through their content must have their visible text as part of their accessible name. | No | Needs review | 2.5.3 Label in Name (A) | Android (needs a debug/inspectable build) |
| `label-title-only` | Ensure that every form element has a visible label and is not solely labeled using hidden labels, or the title or aria-describedby attributes. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `label` | Ensure every form element has a label. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `landmark-banner-is-top-level` | Ensure the banner landmark is at top level. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `landmark-complementary-is-top-level` | Ensure the complementary landmark or aside is at top level. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `landmark-contentinfo-is-top-level` | Ensure the contentinfo landmark is at top level. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `landmark-main-is-top-level` | Ensure the main landmark is at top level. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `landmark-no-duplicate-banner` | Ensure the document has at most one banner landmark. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `landmark-no-duplicate-contentinfo` | Ensure the document has at most one contentinfo landmark. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `landmark-no-duplicate-main` | Ensure the document has at most one main landmark. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `landmark-one-main` | Ensure the document has a main landmark. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `landmark-unique` | Ensure landmarks are unique. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `link-in-text-block` | Ensure links are distinguished from surrounding text in a way that does not rely on color. | Yes | Needs review | 1.4.1 Use of Color (A) | Android (needs a debug/inspectable build) |
| `link-name` | Ensure links have discernible text. | Yes | Needs review | 2.4.4 Link Purpose (In Context) (A); 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `list` | Ensure that lists are structured correctly. | Yes | Needs review | 1.3.1 Info and Relationships (A) | Android (needs a debug/inspectable build) |
| `listitem` | Ensure `<li>` elements are used semantically. | Yes | Needs review | 1.3.1 Info and Relationships (A) | Android (needs a debug/inspectable build) |
| `marquee` | Ensure `<marquee>` elements are not used. | Yes | Needs review | 2.2.2 Pause, Stop, Hide (A) | Android (needs a debug/inspectable build) |
| `meta-refresh-no-exceptions` | Ensure `<meta http-equiv="refresh">` is not used for delayed refresh. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `meta-refresh` | Ensure `<meta http-equiv="refresh">` is not used for delayed refresh. | Yes | Needs review | 2.2.1 Timing Adjustable (A) | Android (needs a debug/inspectable build) |
| `meta-viewport-large` | Ensure `<meta name="viewport">` can scale a significant amount. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `meta-viewport` | Ensure `<meta name="viewport">` does not disable text scaling and zooming. | Yes | Needs review | 1.4.4 Resize Text (AA) | Android (needs a debug/inspectable build) |
| `nested-interactive` | Ensure interactive controls are not nested as they are not always announced by screen readers or can cause focus problems for assistive technologies. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `no-autoplay-audio` | Ensure `<video>` or `<audio>` elements do not autoplay audio for more than 3 seconds without a control mechanism to stop or mute the audio. | Yes | Needs review | 1.4.2 Audio Control (A) | Android (needs a debug/inspectable build) |
| `object-alt` | Ensure `<object>` elements have alternative text. | Yes | Needs review | 1.1.1 Non-text Content (A) | Android (needs a debug/inspectable build) |
| `p-as-heading` | Ensure bold, italic text and font-size is not used to style `<p>` elements as a heading. | No | Needs review | 1.3.1 Info and Relationships (A) | Android (needs a debug/inspectable build) |
| `page-has-heading-one` | Ensure that the page, or at least one of its frames contains a level-one heading. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `presentation-role-conflict` | Elements marked as presentational should not have global ARIA or tabindex to ensure all screen readers ignore them. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `region` | Ensure all page content is contained by landmarks. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `role-img-alt` | Ensure [role="img"] elements have alternative text. | Yes | Needs review | 1.1.1 Non-text Content (A) | Android (needs a debug/inspectable build) |
| `scope-attr-valid` | Ensure the scope attribute is used correctly on tables. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `scrollable-region-focusable` | Ensure elements that have scrollable content are accessible by keyboard. | Yes | Needs review | 2.1.1 Keyboard (A) | Android (needs a debug/inspectable build) |
| `select-name` | Ensure select element has an accessible name. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `server-side-image-map` | Ensure that server-side image maps are not used. | Yes | Needs review | 2.1.1 Keyboard (A) | Android (needs a debug/inspectable build) |
| `skip-link` | Ensure all skip links have a focusable target. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `summary-name` | Ensure summary elements have discernible text. | Yes | Needs review | 4.1.2 Name, Role, Value (A) | Android (needs a debug/inspectable build) |
| `svg-img-alt` | Ensure `<svg>` elements with an img, graphics-document or graphics-symbol role have an accessible text. | Yes | Needs review | 1.1.1 Non-text Content (A) | Android (needs a debug/inspectable build) |
| `tabindex` | Ensure tabindex attribute values are not greater than 0. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `table-duplicate-name` | Ensure the `<caption>` element does not contain the same text as the summary attribute. | No | Needs review | No WCAG criterion mapped | Android (needs a debug/inspectable build) |
| `table-fake-caption` | Ensure that tables with a caption use the `<caption>` element. | No | Needs review | 1.3.1 Info and Relationships (A) | Android (needs a debug/inspectable build) |
| `target-size` | Ensure touch targets have sufficient size and space. | Yes | Needs review | 2.5.8 Target Size (Minimum) (AA) | Android (needs a debug/inspectable build) |
| `td-has-header` | Ensure that each non-empty data cell in a `<table>` larger than 3 by 3  has one or more table headers. | No | Needs review | 1.3.1 Info and Relationships (A) | Android (needs a debug/inspectable build) |
| `td-headers-attr` | Ensure that each cell in a table that uses the headers attribute refers only to other cells in that table. | Yes | Needs review | 1.3.1 Info and Relationships (A) | Android (needs a debug/inspectable build) |
| `th-has-data-cells` | Ensure that `<th>` elements and elements with role=columnheader/rowheader have data cells they describe. | Yes | Needs review | 1.3.1 Info and Relationships (A) | Android (needs a debug/inspectable build) |
| `valid-lang` | Ensure lang attributes have valid values. | Yes | Needs review | 3.1.2 Language of Parts (AA) | Android (needs a debug/inspectable build) |
| `video-caption` | Ensure `<video>` elements have captions. | Yes | Needs review | 1.2.2 Captions (Prerecorded) (A) | Android (needs a debug/inspectable build) |
