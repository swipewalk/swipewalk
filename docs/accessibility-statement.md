# Accessibility statement for Swipewalk

Swipewalk helps teams find accessibility issues, so we want people with disabilities to be able to use it too. This statement
describes what we test Swipewalk against, how, and the issues we know about. It covers the Swipewalk desktop
app for macOS, the HTML reports it produces, and the PDF it can export (`swipewalk export --format pdf`). It is
not a statement of conformance: automated and developer testing cannot show that software meets every
requirement, and some checks listed below still need to be done by people who use assistive technology every day.

Last reviewed: 2026-10-01; the items added for 0.4.0 were reviewed on macOS 27 (desktop app 0.4.0), and the items added for 0.4.1 (the Laws and standards page, Laws that matter to me, the welcome screen, File > New Scan and the Law or standard choice when exporting) were reviewed by the developers only: none has been walked with VoiceOver by a person yet (see Known issues). The PDF export section below was last checked on 2026-09-27.

## What we test against

[WCAG 2.2](https://www.w3.org/TR/WCAG22/) Level A and AA, applied to desktop software as described in
[WCAG2ICT (W3C Group Note)](https://www.w3.org/TR/wcag2ict-22/), for the desktop app; WCAG 2.2 A and AA for the HTML reports.

## How we test

Every change is checked with:

- **Desktop UI tests** (`scripts/desktop-uitests.sh`), which drive the app only through the macOS accessibility
  interface, finding every control by its accessible name. They cover navigating to every page with the sidebar and
  with keyboard shortcuts, the choice controls exposing their name and value, enlarging text to 200%, the readiness
  check, error messages, and a full scan from start to report and history.
- **axe-core** (`scripts/report-axe.sh`) on generated reports in light and dark mode: no violations of the WCAG
  2.0–2.2 A/AA and best-practice rules it covers. axe-core finds only some kinds of issue, so the reports also need
  manual review.
- **A contrast test for the app's palette** (`DesktopPaletteTests`): every text color meets 4.5:1 and every text
  field outline 3:1 against its background, in light and dark mode.
- **Developer review of screenshots** at 100% and 200% text size, in light and dark mode.

## What we have done

- Controls use the Mac's native menu buttons, checkboxes, buttons and text fields, so their names, values and
  states are available to assistive technology. MAUI's default controls reported the wrong type of control on the
  Mac, so they were replaced.
- Screen readers are told about important progress: when an app is installed, when a check fails, each screen
  recorded, and the final result.
- Every page is reachable from the keyboard: ⌘1 Dashboard, ⌘2 or ⌘N New scan (also File > New Scan), ⌘3 Devices, ⌘4 History, ⌘5 Laws and standards. Help >
  Keyboard Shortcuts lists them.
- The one-time welcome screen has a heading, four short points, a Get started button and an About these
  mappings button; Return and Escape close it, and Help > Welcome to Swipewalk opens it again. Its VoiceOver
  reading has not yet been checked with VoiceOver itself.
- Text can be enlarged to 200% with ⌘= (Text Size menu), including the embedded report, with the exceptions listed
  below. In our developer checks at 200% the layout reflows without losing content.
- Automated tests check that the app's text colors reach 4.5:1 and text-field outlines 3:1 against their
  backgrounds in light and dark mode. The main button's default macOS color did not, so it was changed.
- Secondary buttons (for example "Choose app…") looked disabled even when enabled, because only the main
  button had explicit colors; every secondary button now gets an explicit outline and text color, with a
  visibly different, muted look once actually disabled.
- Reports have headings, landmarks, text alternatives for the screenshots and color samples, and a predicted
  screen-reader transcript that is labelled as predicted.
- Each row on the History page has one combined accessible name (app, date, platform, mode, status such as
  "recording in progress" or "ended early", screen and finding counts, and standard), so VoiceOver announces the
  whole row at once instead of separate, disconnected pieces of text. Its Delete button, and its Continue button
  on a row that ended early, are still separately reachable and separately named (for example "Delete the run
  of … from …"), not hidden behind the row's combined name.
- Delete on a History row is drawn in red, and its confirmation is a native system alert that names the run and
  makes Cancel the default button. The Delete button's text and background colours are in the app's automated
  palette contrast test (4.5:1 for text); the alert itself, including its red Delete button, is drawn by the
  system. The alert has not yet been tried with VoiceOver or a real Return/Escape key press, and how the red
  button looks on screen has not yet been checked.
- The notice shown when a physical phone is selected (large-text checks change the phone's own text size) is a
  native system alert rather than a page in the app. On a Mac with VoiceOver, the notice's title and body were
  read, and a real Escape key closed it.
- The Report page's findings list (checkboxes for selecting findings, and each finding's Copy as ticket/Save
  ticket… buttons) and the Export dialog (format choice, "leave out screenshots" checkbox) set an accessible name
  and, where the visible label alone would be ambiguous with several similar controls on the page (for example
  several "Copy as ticket" buttons, one per finding), a more specific one via `SemanticProperties`. Saving,
  copying and exporting announce their result (success or failure) with `SemanticScreenReader.Announce`. Export
  destinations use the system's own save and folder panels rather than an in-app one, so their own accessibility
  is macOS's, not Swipewalk's.
- While a scan or recording is running (New scan), each step of a single-screen scan ("Scanning… step 3 of 6:
  checking at large text") is announced, not just shown; record mode's own "don't touch the phone" line is
  announced only while a screen is actually being captured, not held on the whole time. Every control the run
  locks (device/app/build fields, options, starting a second run) keeps its own accessible hint and adds why
  it's locked, instead of just going silent when disabled. Cancel is never locked and stays reachable by
  keyboard the whole time; nothing about the busy state traps focus. This has not yet been checked with
  VoiceOver on hardware (see Known issues).
- Quitting while a scan or recording is running (⌘Q or the app menu's Quit item) shows a native system alert,
  built the same way as the physical-phone notice above (whose title and body VoiceOver read in full on a Mac).
  Cancel is the alert's default action, so pressing Return or Escape is expected to cancel rather than quit,
  matching Apple's own convention for that button style; the same confirmation appears whether it's reached by
  the keyboard shortcut or by clicking the menu item (checked with an accessibility-driven UI test either way).
  This alert's own VoiceOver reading, and a real Return/Escape press against it, have not yet been checked by
  hand. The non-modal banner naming a device Swipewalk still owes a restore to (Devices and New scan pages) gives
  each device its own heading, so it can be found in a screen reader's headings list, and its "Restore now"
  button has its own accessible name naming the device; neither the banner's own container nor its per-device
  rows carry a description that would hide those from assistive technology. None of this has yet been checked
  with VoiceOver on hardware (see Known issues).
- Closing the window while a scan, recording or screen reader session runs leaves it running, and reopening the
  window from the Dock shows the run still going, with its form read-only, in the same size and position. This uses the window setup macOS 27 requires (0.3.0 could not launch on macOS 27). A person checked
  the reopened window once with VoiceOver on macOS 27: VoiceOver started at the window's heading, the status text
  and buttons were reachable and the run's form was restored. One gap was found and is not fixed (see Known
  issues): Tab did not reach the last button in the Progress panel.
- The Screen reader session page has an Android choice (TalkBack, recorded while you use it yourself) and an
  iPhone (VoiceOver) choice, both set up with the same native controls as the rest of the app: a device and app
  picker, a confirmation checkbox, Start and Stop session, Add note, and, for the iPhone, the steps to read
  captions ("Scan this screen now", "Start reading captions", "I've finished swiping", "I've turned VoiceOver
  off"). A session's summary counts what was recorded and has an Open report button. History rows for a
  session name the kind of run and any screen reader evidence in their accessible name. The page's text and
  buttons were checked by UI tests through the accessibility interface; the whole page has not yet been walked
  with VoiceOver on the Mac (see Known issues).
- The Export dialog groups saved runs by app and version with a checkbox for each scan you choose (none ticked to
  start), and asks which capture to use for a screen two ticked scans both captured. Not yet checked with
  VoiceOver for a whole export task (see Known issues).
- The Triage page (a separate page opened over the report: Status, a required Reason, optional Marked by and
  App version fields, and Save/Clear/Cancel buttons) uses the same native controls and
  `SemanticProperties.Description` conventions as the rest of the app (the Status field is the native
  `SelectButton`, not MAUI's Picker), and disables Save until a status and a reason are both filled in. Not
  yet checked with VoiceOver on hardware (see Known issues).
- New scan's "Read Accessibility Inspector evidence (properties VoiceOver uses)" option (iOS only) shows the
  same kind of native system alert -- the macOS Accessibility permission explanation, the permission-missing
  alert (with a button that opens System Settings' Accessibility pane directly) when it isn't granted, and
  the Inspector setup step -- as the physical-phone notice above. This has not yet been checked with
  VoiceOver on hardware (see Known issues).
- The large-text restart question (`Check "<screen name>" at the larger size?`, asked during a scan or
  recording once a screen shows the larger size needs a restart, with "Check anyway" / "Don't check this
  screen" buttons, plus "Always check" / "Never check" while recording) is a plain MAUI action sheet
  (`DisplayActionSheetAsync`, backed by a native `UIAlertController` on Mac Catalyst, the same underlying
  control as the other native alerts above). A UI test drives it to a real decision through the app's
  accessibility tree on a live Android recording, so its buttons are exposed with their names in the
  accessibility tree; its own VoiceOver reading has not yet been checked by hand (see Known issues).
- Clicking an external link (http, https or mailto) inside the Report page's embedded report (a W3C
  Understanding page, ada.gov, a standards source, an archive.org copy) opens it in the system's default
  browser instead of navigating the report's `WebView` away from the report, so the report stays open and
  the Report page's own Back button still returns where you came from. Checked with unit tests on the
  link-classifying logic and a full app build; not yet checked with VoiceOver on hardware, since the report
  itself sits in a `WebView` our own UI tests already can't reliably query (see Known issues).

## The PDF export

`swipewalk export --format pdf` (added 2026-09-27, report format 0.3) writes a tagged PDF: a real structure
tree (headings, paragraphs, lists, a table for the coverage summary, figures with alt text on every evidence
screenshot), a document title and language, and bookmarks for each screen, each root-cause group and
each finding listed individually. It targets
[PDF/UA-1](https://www.iso.org/standard/64599.html) (ISO 14289-1).

**Checked 2026-09-27** with [veraPDF](https://verapdf.org/) 1.28.2 (`verapdf --flavour ua1`), the open-source
PDF/UA validator: a report built from a saved Android scan of the sample app BuggyApp was exported
to PDF and run through veraPDF. That export, and a larger export of a saved multi-screen run with
screenshots included, both got "PDF file is compliant with Validation Profile requirements" from veraPDF with
0 failed checks. Also checked by hand (not yet scripted): the document language and structure tree with
[pikepdf](https://pikepdf.readthedocs.io/), and that the text extracts in reading order and matches the report's
real text (not images of text) with [pdfminer.six](https://pdfminer-six.readthedocs.io/).

**Not yet re-checked with veraPDF:** after that check (also 2026-09-27), the PDF export gained a triage
section (the same open/triaged split, per-finding note and "Triaged by your team" group the HTML report
already has) and a fix for a token too wide for a line being split by character instead of truncated -- both
change the structure tree veraPDF validated above. They are covered by new unit tests (reopening with
PDFsharp, structure assertions), but not by a fresh `verify-pdf-export.sh` + veraPDF run; that's still owed
before relying on this section's PDF/UA-1 claim for triaged exports or a report with a very long token.

What it covers for non-Latin text: findings can contain real app text and captured screen-reader speech in any
script, so the PDF embeds Open Sans (the same typeface the rest of the report uses) plus three more fonts —
Noto Sans Devanagari, Noto Sans Arabic, and a subset of Noto Sans JP covering kana and the ~2,225 Jōyō/Jinmeiyō
(common-use Japanese) kanji, kept small on purpose (see `docs/limitations.md`). This covers common-use Japanese,
not Chinese: findings carry real app text and captured speech, so a scan of a Chinese-language app can contain
Chinese text -- a Chinese-specific character outside the bundled Japanese subset (most of them; only characters
Chinese shares with Japanese are covered) shows as `?` in the PDF, not the real character. A character in none of the bundled fonts
is replaced with `?` and counted in a note at the end of the PDF, rather than silently rendered as a
missing-glyph box or, worse, PDFsharp's own invisible ".notdef" glyph (which an earlier version of this export
hit on a real character in a real report — caught by exporting a real saved run and checking it with veraPDF, not
by a unit test with only ASCII text, and fixed by reading the bundled fonts' real character coverage instead of
guessing which parts of a Unicode block they cover). Verified with a fixture containing Hindi, Arabic and
Japanese text: reopens correctly, text extracts correctly for Hindi and Japanese, and still passes veraPDF's
PDF/UA-1 checks.

Arabic is drawn with its glyphs reordered right-to-left for a sighted reader (PDFsharp does not perform Arabic
letter shaping/joining, so each letter is drawn in its isolated form, not its normal joined form) — but a screen
reader gets the correct, original reading order via a PDF `/ActualText` override, verified by reading the PDF's
own structure tree back after export. This is disclosed as a known simplification, not a full text-shaping
implementation:

| Issue | Effect | Plan |
|---|---|---|
| Arabic letters are drawn in isolated form, not joined as normal Arabic typesetting does. | The text is legible but doesn't look like normal Arabic script. | Investigate a lightweight Arabic shaping table if this comes up in practice. |
| Right-to-left reordering only fully works for a run of pure Arabic text; a line mixing Arabic with Latin words or digits doesn't reorder those relative to each other. | A mixed-language line may display in a confusing order for a sighted reader (screen readers still get the correct text via `/ActualText`). | Implement the parts of the Unicode Bidirectional Algorithm (UAX #9) this needs, if demand justifies it. |
| A character outside the bundled fonts' coverage is shown as `?`. This includes most emoji, any script other than Latin/Cyrillic/Greek punctuation, Devanagari or Arabic, and -- plainly -- most Chinese-specific characters: the bundled CJK font is a common-use *Japanese* subset, and only covers Chinese characters it happens to share with Japanese. | The exact text isn't visible in the PDF; for Chinese specifically, most non-Japanese-shared text renders this way. | The HTML report and results.json from the same run always have the exact text; noted at the end of the PDF when this happens. |
| Page footers show "Page N", not "Page N of TOTAL" (see `PdfLayout.DrawPageNumberFooter`'s remarks): PDFsharp's PDF/UA mode only allows drawing on the single most-recently-added page, so there's no way to go back and add the total once it's known. | Slightly less informative page numbers. | Revisit if PDFsharp adds a way to finalize page numbering after the fact. |

## Known issues

| Issue | WCAG | Workaround | Plan |
|---|---|---|---|
| The text inside native Mac controls (the values of menu buttons, button titles such as "Choose…", checkbox labels and sidebar items) stays at the system size when the in-app text size is increased. The labels next to them do scale. | 1.4.4 Resize Text (AA) | Use macOS Zoom (System Settings > Accessibility > Zoom); this magnifies the screen and does not fix the issue. | Check whether these controls follow the macOS text size setting; investigate drawing them at the app's text size without losing their native accessibility. |
| On the Mac, the menu buttons are reported as buttons (with their selected value and a hint that they open a menu) rather than pop-up buttons, and the checkboxes as switches. Their names, values and states are announced; only the role names differ from native Mac apps. | 4.1.2 Name, Role, Value (A) | — | Revisit when Mac Catalyst exposes pop-up button and checkbox roles. |
| Not every control has been checked for keyboard operation and a visible focus indicator, with and without macOS Full Keyboard Access (System Settings > Accessibility > Keyboard). | 2.1.1 Keyboard (A), 2.4.7 Focus Visible (AA) | Use ⌘1–⌘5 to move between pages. | Planned: a keyboard-only walkthrough of every page. |
| Contrast of control borders, checkbox and focus indicators has not been measured; only text-field outlines are tested. | 1.4.11 Non-text Contrast (AA) | — | Extend the palette test or measure from screenshots. |
| The app has not yet been tested by a person using VoiceOver, Voice Control or Switch Control for a whole task. The UI tests use the same accessibility interface, but they are not a substitute. | — (not yet tested) | — | Planned: testing with people who use assistive technology. We welcome feedback in the meantime (see below). |
| Progress messages in the log are only announced for key events, not every line. | 4.1.3 Status Messages (AA) | Review the Progress list after a scan. | Review with screen reader users. |
| The Report page's findings list and the Export dialog have not yet been checked with VoiceOver for a whole export task (built following the same `SemanticProperties`/native-control conventions as the rest of the app, and covered by `Swipewalk.Desktop.Tests` unit tests for their non-UI logic, but not yet walked with VoiceOver on). | — (not yet tested) | Use the CLI's `swipewalk export` (the desktop app's export uses the same code) if you rely on assistive technology for this task today. | Include in the planned assistive-technology walkthrough. |
| The large-text restart question (a native action sheet shown during a scan or recording) has been driven through the app's accessibility tree by a UI test, but not checked with VoiceOver by a person. | — (not yet tested) | Use `--large-text-restart always` (restart and check) or `never` (don't check at the larger size) on the CLI to avoid the prompt if you rely on assistive technology for this today. | Include in the planned assistive-technology walkthrough. |
| Clicking an external link inside the Report page's embedded report opens it in the default browser (checked with unit tests and a full build), but this has not been checked with VoiceOver on hardware -- the report itself sits in a `WebView` our UI tests already can't reliably query. | — (not yet tested) | Open `report.html` directly in a browser if you rely on assistive technology for this. | Include in the planned assistive-technology walkthrough. |
| After the window is closed and reopened while a run is going, Tab did not reach the last button in the Progress panel when a person tried it once with VoiceOver; whether Full Keyboard Access was on wasn't recorded. A fix was tried and not confirmed, so it was left out. | 2.1.1 Keyboard (A), 2.4.3 Focus Order (A) (possible) | Reach the button with VoiceOver navigation or the pointer. | Re-check with Full Keyboard Access and VoiceOver, then fix. |
| The Screen reader session page (Android and iPhone), the Laws and standards page, Laws that matter to me, the welcome screen, File > New Scan, the Law or standard menu in the Export dialogs and the version-grouped Export dialog have not been walked with VoiceOver on the Mac for a whole task by a person; UI tests drive them through the accessibility interface only. | — (not yet tested) | Use `swipewalk session`, `swipewalk export` (with `--standard` and `--my-laws`) or `swipewalk standards` (or docs/standards.md) on the command line if you rely on assistive technology for this today; the welcome text is also in the README and the user guide. | Include in the planned assistive-technology walkthrough. |
| Each sidebar icon (all six sidebar items have one; Laws and standards and Screen reader session have not been checked separately) is still exposed in the app's accessibility tree as a separate element with the icon's system name (e.g. "dashboard" next to Dashboard); several attempts to hide it at the native-view level haven't worked. The desktop UI tests, which use the same accessibility interface as assistive technology, find it. In one manual VoiceOver check (macOS, 2026-09-28) VoiceOver announced only the page names, not the icons (before the Laws and standards item was added and before Screen reader session had an icon); Voice Control and Switch Control have not been checked. | 1.1.1 Non-text Content (A), 4.1.2 Name, Role, Value (A) (possible) | In the VoiceOver check, each sidebar item was announced by its page name only. If an extra "dashboard"-style name shows up in Voice Control or Switch Control, use the page name or ⌘1–⌘5. | Find what exposes the icon's name and hide it; check with Voice Control and Switch Control. |
| On the Laws and standards page, whether a row is expanded is given in the row button's name and hint ("…, collapsed"), not as a native expanded state, because Mac Catalyst has no native expanded state for it. | 4.1.2 Name, Role, Value (A) (possible) | The state is spoken as part of the button's name; `swipewalk standards` lists the same entries as plain text. | Revisit when Mac Catalyst exposes an expanded state. |
| With macOS keyboard navigation turned on (System Settings > Keyboard), a person using version 0.4.0 found that Tab reaches only the controls visible in the window (it does not move to controls below the fold or scroll to them), and that Space does not change checkboxes or open pop-up menus. A fix is planned; Full Keyboard Access (System Settings > Accessibility > Keyboard) has not been checked separately. The "Laws that matter to me" list, new in 0.4.1, is covered by a UI test that presses Tab, the arrow keys and Space, but has not been tried by a person with keyboard navigation on. | 2.1.1 Keyboard (A), 2.4.3 Focus Order (A) (possible) | Use VoiceOver navigation or the pointer for controls Tab does not reach, or the command line, which covers most of the same tasks. | Fix, then re-check by hand with keyboard navigation on. |
| The Windows version has not been built or tested. | — (not yet tested) | — | Windows collector and desktop build (roadmap). |

## Feedback

If you find an accessibility problem in Swipewalk, please open an issue in the
[Swipewalk repository](https://github.com/swipewalk/swipewalk/issues) and describe what you were trying
to do, what happened, and the assistive technology you use. We aim to reply within ten working days.
