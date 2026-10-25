# Accessibility statement for Swipewalk

Swipewalk helps teams find accessibility issues, so we want people with disabilities to be able to use it too. This statement
describes what we test Swipewalk against, how, and the issues we know about. It covers the Swipewalk desktop
app for macOS, the HTML reports it produces, and the PDF it can export (`swipewalk export --format pdf`). It is
not a statement of conformance: automated and developer testing cannot show that software meets every
requirement, and some checks listed below still need to be done by people who use assistive technology every day.

Last reviewed: 2026-10-24 (read again for 0.6.3; the New scan page gained three check boxes under More checks (text size beyond 200%, smaller screen, keyboard navigation (Tab); all three are Android only) and a text field for the size beyond 200%, and the Export choices gained the screen-reader transcript; these were reviewed by the developers only. The Export choice was checked with a UI test and the new More checks rows only with unit tests of the page logic; none was checked by a person using VoiceOver or the keyboard); earlier: 2026-10-19 (read again for 0.6.2; no app screen changed since 0.6.1); earlier: 2026-10-13 (the Laws and standards page search box now uses the filter-box style; not yet re-tested); earlier: 2026-10-09 (added the Known issues row on VoiceOver and the Report page: one Mac, macOS 27, scripted VoiceOver and a minimal test app; a person has not checked it); earlier: 2026-10-08 (the generated reports were run through axe-core, read by hand and read with a scripted VoiceOver in Safari, with fixes; not walked with VoiceOver by a person); earlier: 2026-10-07 (read again against the 0.6.0 changes; nothing was re-tested since the checks of 2026-10-06 below); earlier: 2026-10-06 (keyboard and VoiceOver gaps from the 0.5 hands-on check re-tested with real key presses and a scripted VoiceOver, not by a person, most no longer reproduce in those checks (Escape, arrow keys and the clear button added); a person has not re-checked them yet; see What we have done and Known issues); earlier: 2026-10-03 (known keyboard and VoiceOver gaps added to Known issues, from a hands-on check of version 0.5); earlier review notes: 2026-10-01; the items added for 0.4.2 (the More checks section and iPhone signing choice on New scan, the iPhone signing choice on the Devices and Screen reader session pages, the Compare runs choices, the team triage file field, the Help pages that list the automated checks and known limitations, and the keyboard handling of checkboxes and pop-up menus) were reviewed by the developers only and checked with UI tests, not by a person using VoiceOver or the keyboard (see Known issues); the items added for 0.4.0 were reviewed on macOS 27 (desktop app 0.4.0), and the items added for 0.4.1 (the Laws and standards page, Laws that matter to me, the welcome screen, File > New Scan and the Law or standard choice when exporting) were reviewed by the developers only: none has been walked with VoiceOver by a person yet (see Known issues). The items added for 0.5 (including the Law or standard Search sheet and the diagnostic report and unexpected-close notice) were reviewed by the developers only, except that a person checked the New scan checkbox, the What to scan menu and the Search sheet with the keyboard and VoiceOver, and the Law or standard menu with the keyboard only, on 2026-10-03 (see Known issues). The PDF export section below was last checked on 2026-09-27.

## What we test against

[WCAG 2.2](https://www.w3.org/TR/WCAG22/) Level A and AA, applied to desktop software as described in
[WCAG2ICT (W3C Group Note)](https://www.w3.org/TR/wcag2ict-22/), for the desktop app; WCAG 2.2 A and AA for the HTML reports.

## How we test

Every change is checked with:

- **Desktop UI tests**, which drive the app only through the macOS accessibility
  interface, finding every control by its accessible name. They cover navigating to every page with the sidebar and
  with keyboard shortcuts, the choice controls exposing their name and value, enlarging text to 200%, the readiness
  check, error messages, and a full scan from start to report and history.
- **A contrast test for the app's palette**: every text color meets 4.5:1 and every text
  field outline 3:1 against its background, in light and dark mode.
- **Developer review of screenshots** at 100% and 200% text size, in light and dark mode.

**axe-core**, run by hand on 2026-10-08, twice: before and after the fixes (it is not part of the checks run on every change), on a scan report and an evidence report generated from the sample app (the exported single-file report is built from the same markup as the scan report). It found content outside landmarks, a label on a plain span, and scrolling code blocks that the keyboard could not reach, and wide tables that made the whole page scroll sideways at narrow widths. These were fixed, and a second run on the fixed code, in light mode, reported no violations for the scan report at 1280 px and 500 px wide (with every detail section open) and for the evidence report at 1280 px. The evidence report at 500 px was not run. It left some color contrast checks and table-header checks undecided. The color contrast was computed from the stylesheet instead, on the code before the last fixes and not repeated on the final code, and the table-header checks still need a manual review. At 500 px the laws table in the scan report is wider than the window, so it scrolls sideways inside its own labelled area, which the keyboard can reach; the page itself does not scroll sideways. Not re-run on the final code: dark mode, the screen-reader text for finding numbers and the label on code examples (both added after reading the reports with a scripted VoiceOver), and the notes shown on imported runs and on combined exports, whose position was fixed from reading the markup. axe-core finds only some kinds of issue, so the reports also need manual review.

## What we have done

- Controls use the Mac's native menu buttons, checkboxes, buttons and text fields, so their names, values and
  states are available to assistive technology. MAUI's default controls reported the wrong type of control on the
  Mac, so they were replaced.
- Screen readers are told about important progress: when an app is installed, when a check fails, each screen
  recorded, and the final result.
- Every page is reachable from the keyboard: ⌘1 Dashboard, ⌘2 or ⌘N New scan (also File > New Scan), ⌘3 Devices, ⌘4 History, ⌘5 Laws and standards, ⌘K Sharing keys (also File > Sharing Keys…), ⌘O Open a shared run file (also File > Open…). Help >
  Keyboard Shortcuts lists them.
- Key handling was added for checkboxes and pop-up menus (relevant to WCAG 2.1.1 Keyboard (A)). For checkboxes, a UI test that
  makes a checkbox the control receiving key presses through a test-only launch option found that Space turns it on or off once
  per press and Return leaves it unchanged, as on standard Mac checkboxes (2026-10-01, one Mac running macOS 27.0.1; other
  macOS versions not run). The same kind of test on the pop-up menus found that Space and Return did not open the menu of the system's own pop-up button, and none of the three ways of opening it from code that were tried helped (2026-10-01, one Mac, macOS 27.0.1). Since 0.5 the pop-up menus are buttons the app draws itself, drawn to resemble Mac pop-up menus (a grey rounded shape, an up-and-down arrow, the system's text colours in light and dark mode). The same UI test then found that Space and Return each open the menu (2026-10-01, one Mac, macOS 27.0.1). Return relies on a system feature available from macOS 14.4; on earlier versions the app does not handle Return on these menus, and Space was not tried there. A second test found that the pop-up keeps the button role, its name and the chosen item as its value, and that a click opens the menu and choosing an item changes the value. Comparing the old and new look from renders of the controls' own views, the fill, shape, text position and arrow match closely, with the fill a few shades off (one Mac, light and dark; looked at by eye, not measured against a standard). A person then tried the open menu by hand with keyboard navigation on (2026-10-03, one Mac, macOS 27): Space and Return open it, Escape closes it, arrow keys move the highlight and typing the start of a name moves to it, but Return does not choose the highlighted item (see Known issues). VoiceOver was tried on "What to scan" only (it opened the menu and choosing read the name and value). Not yet checked: VoiceOver on the other pop-up menus, the contrast of the text and shape of the redrawn control (not measured; these system colours are not covered by the palette test), Full Keyboard Access, a visible focus indicator on the redrawn button, and other macOS versions.
- The one-time welcome screen has a heading, four short points, a Get started button and an About these
  mappings button; Return and Escape close it, and Help > Welcome to Swipewalk opens it again. Its VoiceOver
  reading has not yet been checked with VoiceOver itself.
- Keyboard and VoiceOver checks with real key presses (added in 0.6; a script kept with the source code). A script sends real key presses to the app and reads which control has focus, because the UI-test framework's simulated keys passed some fixes tried during 0.5 that did nothing when real keys were pressed. Results from 2026-10-06 (one Mac, macOS 27.0.1, keyboard navigation on, the UI-test build of the app; VoiceOver was started and driven through its AppleScript interface, not by a person): on New scan, with the window short enough to hide the lower half, Tab reached all 20 enabled controls that take focus and Shift-Tab reached them backwards (0.5 reached 5 of them); in an open pop-up menu (What to scan), Return chose the highlighted item and closed the menu (the 0.5 finding was most likely because the first Down arrow only highlights the item already chosen, so Return re-chose it; nothing was changed, and a person has not re-checked it); in the Search laws and standards sheet and in Laws that matter to me, keyboard focus starts in the search field, Tab goes from the field to each law row and then to Cancel or Done, Space and Return on a focused row choose it (or tick it in Laws that matter to me), and VoiceOver starts on the search field, reads the match count, each entry's name (with ", selected" on the current choice in the Search sheet, and "checked" or "not checked" in Laws that matter to me) and the closing button in that order, and VoiceOver's activate command, sent through its AppleScript interface (the VO+Space keys themselves were not pressed), chose it; on History, each run has an Open report button that Tab reaches and Space presses. The search box in those sheets is a plain text field since 0.6: the system search field's own clear button kept Tab cycling between the field and that button once something was typed. Also checked the same way: Escape in the filter boxes (with focus in the box and text in it, Escape empties the box and the page stays open; with the box empty, or with focus on a law entry in Laws that matter to me, Escape closes the page; checked in the Search sheet and the app picker, and for the empty box and a law entry in Laws that matter to me); Up and Down arrow in a filter box highlight the first, next or previous entry while keyboard focus stays in the box, and Return chooses (or ticks) the highlighted entry (Search sheet, Laws that matter to me and the app picker); and the filter box's standard clear button, which is named "Clear text", is on screen while text is typed (checked in the Search sheet). The app picker was checked on an Android emulator, with the device chosen by keyboard: Tab goes from the filter through "Show system apps" to the app entries, and Space on an entry fills the App field. With VoiceOver started through its AppleScript interface (2026-10-07, same Mac, an Android emulator, one app entry shown after typing "android"), VoiceOver's activate command opened the picker from the Choose app… button, VoiceOver started on the filter box, then reached "Show system apps", the app entry (read as its id, "button") and Cancel, and its activate command on the entry closed the picker and filled the App field; the VO+Space keys themselves were not pressed. VoiceOver read the explanatory line ("Recently scanned apps, then apps installed on the device...") after the entries instead of before them, read an empty group once, and on moving back onto the entry spoke a general hint instead of the app's id. The filter boxes switch off autocorrect and inline suggestions: the suggestion pop-up took the first Escape before the app ever saw it. Not checked: VoiceOver on History's Open report button, a person using VoiceOver in the app picker (only the scripted check above), VoiceOver in the app picker with more than one app entry on screen, the VO+Space keys in the app picker, the other History row buttons (Delete, Continue, Remove from list) from the keyboard, the note field of the Screen reader session, the Share and Export dialogs, whether VoiceOver speaks the highlighted entry as the arrow keys move it (the app asks it to, which was not verified), how the highlight and the magnifier icon look (no person has seen them), and the arrow keys with keyboard navigation off (see the Known issues row on that setting).
- Help > Automated Checks and Help > Known Limitations open a reference page. It has no sidebar item or ⌘ shortcut. Each row is a button whose name says whether it is expanded or collapsed (Mac Catalyst has no native expanded state). A UI test on macOS 27.0.1 (2026-10-01) found all five Help menu items and that Known Limitations opens this page; the page's VoiceOver reading has not yet been checked with VoiceOver itself.
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
- History's "Open in Finder", "Change location…", the "Other people use this folder" option, "Show removed runs" and each
  row's "Remove from list" and "Add back to list" buttons have text names, and the row buttons start their
  name with that text and then name their run (for example "Remove from list: the run of … from …"). The
  questions asked after choosing a new location (move or leave, then "Do other people use this folder?") are native
  system alerts. Changing the location, removing a run and adding it back are set to announce the result. These have
  not yet been tried with VoiceOver; after Remove from list the row disappears, and where keyboard focus lands then
  has not been checked.
- Sharing runs: each row's and the Report page's "Share run…" button, the Share sheet (a screenshots switch, a triage
  switch shown only when the run has marks, a "Shared by" field, Share and Cancel), File > Open… (⌘O) and History's
  opened-file notice ("Trust this key…", "Dismiss") have text names; the row button's name starts with its visible
  text, then names the run. File > Open… (⌘O) is in the File menu so that dropping a file on History (a mouse or
  trackpad gesture) is never the only way; opening a file from the keyboard end to end, a real double-click, a drop on
  the Dock icon and dragging onto History have not been tried yet. The opened-file notice is set to be announced. The trust question
  (Cancel is its default and it shows the key's full fingerprint in groups of four), the question asked before a file that isn't signed or doesn't match its signature is opened ("Don't import" is its default; it shows the key named in the file in groups of four when there is one) and file failures are native system alerts. These two questions have not yet been tried with VoiceOver. A History row for an imported run reads out its signature line (with the key's short form); the signer's full fingerprint is shown in the row and in the report header but is not read in the row's spoken name.
  The Share sheet's "Your sharing key" caption, its "Sharing keys…" link and its "Sign this file" switch, and File > Sharing Keys… (⌘K), which opens the Sharing keys page (your key's full fingerprint in
  groups of four, a list of the keys you trust that you select and then choose "Stop trusting this key", and a field to
  trust a key by pasting its fingerprint) have text names; the list is a native list, the kind Mac apps normally let Tab reach
  when keyboard navigation is off; this has not been checked. Trusting or no longer trusting a key is set to be announced. An imported recording that
  ended early still says "Ended early" in its row and its spoken name.
  None of this has been tried with VoiceOver, and the native Save panel and the macOS share sheet
  are drawn by the system and have not been checked.
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
- Help > Save Diagnostic Report…, the "Report saved" alert (Done is its default action and Cancel-style, so Return and Escape are
  expected to close it; Show in Finder is the other choice) and the notice on the Dashboard after Swipewalk closed unexpectedly (a heading with two buttons,
  Save report… and Not now, whose names are their visible text) were reviewed by the developers only. Not yet checked:
  VoiceOver reading of the alert and the notice, a real Return/Escape press on the alert, and where keyboard focus lands
  after the notice closes. UI tests for them are written and have not yet been run (see Known issues).
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
  link-classifying logic and a full app build; not checked with VoiceOver, because VoiceOver cannot read the
  report inside the app on macOS 27 (see Known issues); use Open in browser.

## The PDF export

`swipewalk export --format pdf` (added 2026-09-27, report format 0.3) writes a tagged PDF: a real structure
tree (headings, paragraphs, lists, a table for the coverage summary, figures with alt text on every evidence
screenshot), a document title and language, and bookmarks for each screen, each root-cause group and
each finding listed individually. It targets
[PDF/UA-1](https://www.iso.org/standard/64599.html) (ISO 14289-1).

**Checked 2026-09-27** with [veraPDF](https://verapdf.org/) 1.28.2 (`verapdf --flavour ua1`), the PDF/UA validator: a report built from a saved Android scan of the sample app BuggyApp was exported
to PDF and run through veraPDF. For that export, and for a larger export of a saved multi-screen run with
screenshots included, veraPDF reported
0 failed checks against its PDF/UA-1 profile. Also checked by hand (not yet scripted): the document language and structure tree with
[pikepdf](https://pikepdf.readthedocs.io/), and that the text extracts in reading order and matches the report's
real text (not images of text) with [pdfminer.six](https://pdfminer-six.readthedocs.io/).

**Not yet re-checked with veraPDF:** after that check (also 2026-09-27), the PDF export gained a triage
section (the same open/triaged split, per-finding note and "Triaged by your team" group the HTML report
already has) and a fix for a token too wide for a line being split by character instead of truncated -- both
change the structure tree veraPDF validated above. They are covered by new unit tests (reopening with
PDFsharp, structure assertions), but not by a fresh veraPDF run; that's still owed
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
Japanese text: reopens correctly, text extracts correctly for Hindi and Japanese, and veraPDF still reported
0 failed checks against its PDF/UA-1 profile.

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
| On the Mac, the menu buttons are reported as buttons (with their selected value and a hint that they open a menu) rather than pop-up buttons, and the checkboxes as switches. Their names, values and states are announced; only the role names differ from native Mac apps. The pop-up menus were redrawn in 0.5; a UI test found them still exposed as a button with the same name and the chosen item as the value (2026-10-01, one Mac); a person opened "What to scan" with VoiceOver and heard its name and chosen value (2026-10-03, one Mac, macOS 27); the other menus were not checked with VoiceOver. | 4.1.2 Name, Role, Value (A) | — | Revisit when Mac Catalyst exposes pop-up button and checkbox roles. |
| Not every control has been checked for keyboard operation and a visible focus indicator, with and without macOS Full Keyboard Access (System Settings > Accessibility > Keyboard). What has been checked: Space turns a checkbox on and off once per press and Return leaves it unchanged (a person, with keyboard navigation on, 2026-10-03, one Mac, macOS 27); Tab and Space reach and press the buttons when the window is tall enough to show the whole page (same check; since 0.6 also in a short window, see What we have done); Space or Return opens a pop-up menu, Escape closes it, and arrow keys and typing the start of a name move through the open menu (same check). Not checked: Full Keyboard Access, a visible focus indicator on the redrawn pop-up menus, other macOS versions. Return opening a pop-up menu relies on a system feature available from macOS 14.4; on earlier versions the app does not handle Return on these menus, and Space was not tried there. With macOS Keyboard navigation and Full Keyboard Access both off, Tab stops only on text fields (checked with real key presses on 2026-10-06; see the row on that setting below). | 2.1.1 Keyboard (A), 2.4.7 Focus Visible (AA), 2.4.11 Focus Not Obscured (Minimum) (AA) (possible) | Use ⌘1–⌘5 to move between pages. | Planned: a keyboard-only walkthrough of every page. |
| After the "Search laws and standards" sheet, "Laws that matter to me" or the app picker closes, no control shows keyboard focus: a real-key check on 2026-10-06 (one Mac, macOS 27, keyboard navigation on) found focus on an unnamed group, and the next Tab went to the button that opened the sheet. Several ways of asking the system to put focus back on that button (asking the page controller to prefer it, asking the window scene's focus system to update to it) were tried with real key presses and none moved it, so the fix was not found. | 2.4.3 Focus Order (A) (possible) | Press Tab once: focus goes to the button that opened the sheet. | No fix yet. |
| The folder shown in "App source folder (optional)" is a read-only text field filled by "Choose folder…", so Tab skips it and goes to "Choose folder…" and "Clear app source folder". VoiceOver navigation should still reach the path; this was not checked. | — | — | By design; reconsider if people ask to select the text. |
| In the desktop app on macOS 27, a scripted VoiceOver check (not a person) found that VoiceOver announces the report shown inside the Report page as an "empty frame" and does not read it, and Tab does not move into the report from the buttons above it (keyboard navigation on). The same happens in a small test app built with no Swipewalk code (a button and a web view), checked on one Mac on 2026-10-08, so it appears to come from how macOS 27 handles web content in Mac Catalyst apps rather than from Swipewalk's code; other macOS versions were not checked. Several workarounds were tried; the only one that let VoiceOver read the report hid the rest of the window, so none was kept. The same report opened in Safari was read by a scripted VoiceOver, including its headings and landmarks (not checked by a person; other browsers not checked). While VoiceOver, or another accessibility tool the system reports the same way, is running, the Report page shows a sentence about this under its first button; that sentence has not yet been checked by a person using VoiceOver. | 2.1.1 Keyboard (A); 4.1.2 Name, Role, Value (A) (possible: names, roles and structure are exposed to the system, but VoiceOver does not reach them in the app) | Choose Open in browser, the first button on the Report page, to read the same report in your default web browser (the report was read in Safari with a scripted VoiceOver; using this button with VoiceOver has not yet been checked by a person). Show in Finder opens the run's folder; opening `report.html` from there opens the same file in your default browser. Or use the command-line version (`swipewalk scan --out <folder>` writes the same HTML report). If this blocks you, tell us (see [Feedback](#feedback)). | Look for a way to show the report with native controls instead of a web view. |
| With macOS keyboard navigation off (the macOS default), Tab moves only between text fields: in a real-key check on 2026-10-06 (one Mac, macOS 27, the setting switched off for the run and switched back on afterwards), on New scan Tab stopped on text fields only, never on a button, pop-up menu, checkbox or sidebar item, so the Search and Choose app buttons, the pop-up menus and the sheets could not be opened from the keyboard, and Shift-Tab stayed on the Dashboard sidebar item. This is how Mac apps behave in general with that setting off. Once a sheet is open, the filter box takes focus and the arrow keys and Return work without Tab stops on buttons, but that was not checked with the setting off, because opening a sheet needs a mouse click and the check only uses the keyboard. | 2.1.1 Keyboard (A) (possible) | Turn on System Settings > Keyboard > Keyboard navigation (or Full Keyboard Access), or use the pointer, or the command line. | No change planned: this is the standard Mac behaviour with that setting off, and the app follows it. |
| Contrast of control borders, checkbox and focus indicators, and of the redrawn pop-up menus' fill, arrow and text (drawn by the app since 0.5, in system colours), has not been measured; only text-field outlines are tested. | 1.4.11 Non-text Contrast (AA), 1.4.3 Contrast (Minimum) (AA) (possible) | — | Extend the palette test or measure from renders of the controls. |
| The app has not yet been tested by a person using VoiceOver, Voice Control or Switch Control for a whole task. The UI tests use the same accessibility interface, but they are not a substitute. | — (not yet tested) | — | Planned: testing with people who use assistive technology. We welcome feedback in the meantime (see below). |
| Progress messages in the log are only announced for key events, not every line. | 4.1.3 Status Messages (AA) | Review the Progress list after a scan. | Review with screen reader users. |
| The Report page's findings list and the Export dialog have not yet been checked with VoiceOver for a whole export task (built following the same `SemanticProperties`/native-control conventions as the rest of the app, and covered by unit tests for their non-UI logic, but not yet walked with VoiceOver on). | — (not yet tested) | Use the CLI's `swipewalk export` (the desktop app's export uses the same code) if you rely on assistive technology for this task today. | Include in the planned assistive-technology walkthrough. |
| The large-text restart question (a native action sheet shown during a scan or recording) has been driven through the app's accessibility tree by a UI test, but not checked with VoiceOver by a person. | — (not yet tested) | Use `--large-text-restart always` (restart and check) or `never` (don't check at the larger size) on the CLI to avoid the prompt if you rely on assistive technology for this today. | Include in the planned assistive-technology walkthrough. |
| Clicking an external link inside the Report page's embedded report opens it in the default browser (checked with unit tests and a full build), but this was not checked with VoiceOver, because, in a scripted VoiceOver check on macOS 27, VoiceOver could not read the report inside the app (see the VoiceOver and Report page row). | — (not yet tested) | Choose Open in browser on the Report page if you rely on assistive technology for this. | Include in the planned assistive-technology walkthrough. |
| After the window is closed and reopened while a run is going, Tab did not reach the last button in the Progress panel when a person tried it once with VoiceOver; whether Full Keyboard Access was on wasn't recorded. A fix was tried and not confirmed, so it was left out. | 2.1.1 Keyboard (A), 2.4.3 Focus Order (A) (possible) | Reach the button with VoiceOver navigation or the pointer. | Re-check with Full Keyboard Access and VoiceOver, then fix. |
| The Screen reader session page (Android and iPhone), the Laws and standards page, Laws that matter to me, the welcome screen, File > New Scan, the Law or standard menu in the Export dialogs, its Search sheet (the Search… button beside the menu on New scan and in both Export dialogs; its UI tests were run on one Mac on 2026-10-01 and passed; a person tried it with VoiceOver on 2026-10-03, and it was re-checked with real key presses and a scripted VoiceOver on 2026-10-06; see What we have done and the filter boxes row below), the version-grouped Export dialog, Help > Save Diagnostic Report… with its "Report saved" alert, the Dashboard notice after an unexpected close, the More checks section and iPhone signing choice on New scan, the Compare runs choices, the team triage file field and the Help pages for automated checks and known limitations have not been walked with VoiceOver on the Mac for a whole task by a person; UI tests drive them through the accessibility interface only. | — (not yet tested) | Use `swipewalk session`, `swipewalk export` (with `--standard` and `--my-laws`) or `swipewalk standards` (or docs/standards.md) on the command line if you rely on assistive technology for this today (`swipewalk diagnostics` saves the same diagnostic report); the welcome text is also in the README and the user guide. | Include in the planned assistive-technology walkthrough. |
| The four filter boxes (the Law or standard Search sheet, Laws that matter to me, the app picker and the search box on the Laws and standards page) are plain text fields, with a magnifier icon and the standard clear button (named "Clear text"); the Laws and standards page's box has the same icon, named clear button and autocorrect and inline suggestions switched off in this release. That box was added to the group after the checks below and has not been checked with real key presses, VoiceOver or by a person; the Escape and arrow-key behaviour below applies to the three in sheets, not to that page. Escape with text in the box empties it and Escape on an empty box (or from a list entry) closes the page without choosing anything (in Laws that matter to me, choices already made are kept); Up and Down arrow move a highlight through the entries and Return chooses it (all checked with real key presses on 2026-10-06, one Mac, macOS 27, keyboard navigation on; Escape with text was checked in the Search sheet and the app picker only). Return in the Search sheet chooses the entry when exactly one matches (checked by a person on 2026-10-03 with 0.5, and with real key presses on 2026-10-06); Return in the app picker with no match uses the typed text as the app id (UI tests only). With VoiceOver on, the Search sheet reads the number of matches as you type (2026-10-03, one Mac). A person has not yet looked at how the fields and the highlighted entry appear, including at larger text sizes, and the app picker with VoiceOver was checked only with a scripted VoiceOver (2026-10-07), not by a person; it read the explanatory line after the entries, an empty group once, and a general hint instead of the app's id when moving back onto an entry. | 4.1.2 Name, Role, Value (A), 4.1.3 Status Messages (AA), 1.4.4 Resize Text (AA) (possible) | Choose from the Law or standard menu, or use `swipewalk standards` (a list of the ids to pass to `--standard`) and `--my-laws <ids>` on the command line; in New scan you can type the app id yourself instead of using the app picker. | Check the look at larger text; check the Laws and standards page search box with real keys and VoiceOver; fix the app picker's VoiceOver reading order, the empty group and the hint on the entries, then check it with VoiceOver by hand. |
| Each sidebar icon (all six sidebar items have one; Laws and standards and Screen reader session have not been checked separately) is still exposed in the app's accessibility tree as a separate element with the icon's system name (e.g. "dashboard" next to Dashboard); several attempts to hide it at the native-view level haven't worked. The desktop UI tests, which use the same accessibility interface as assistive technology, find it. In one manual VoiceOver check (macOS, 2026-09-28) VoiceOver announced only the page names, not the icons (before the Laws and standards item was added and before Screen reader session had an icon); Voice Control and Switch Control have not been checked. | 1.1.1 Non-text Content (A), 4.1.2 Name, Role, Value (A) (possible) | In the VoiceOver check, each sidebar item was announced by its page name only. If an extra "dashboard"-style name shows up in Voice Control or Switch Control, use the page name or ⌘1–⌘5. | Find what exposes the icon's name and hide it; check with Voice Control and Switch Control. |
| On the Laws and standards page, whether a row is expanded is given in the row button's name and hint ("…, collapsed"), not as a native expanded state, because Mac Catalyst has no native expanded state for it. | 4.1.2 Name, Role, Value (A) (possible) | The state is spoken as part of the button's name; `swipewalk standards` lists the same entries as plain text. | Revisit when Mac Catalyst exposes an expanded state. |
| The Windows version has not been built or tested. | — (not yet tested) | — | Windows collector and desktop build (roadmap). |

## The VS Code extension

The Swipewalk VS Code extension (a preview) is separate from the desktop app. It uses VS Code's own
surfaces (Problems, hover, views, quick picks, commands), which supply keyboard navigation, zoom, themes and screen
reader mode, plus one web page of its own, the finding details page.

What is in place, checked by automated tests only: every item in the Findings view has a text label and a spoken
description (kind, match confidence, line); meaning is never carried by colour alone; the details page has one
top-level heading with the other headings in order, labelled sections and buttons, a language, a polite live region for status messages,
moves focus to its heading when it opens, and uses only VS Code theme colours with high-contrast and reduced-motion
support; the page carries a strict content security policy. The extension makes no network calls and sends no telemetry.

**Not yet done:** it has not been walked with NVDA, VoiceOver or VS Code's screen reader mode by a person, or with
Voice Control. Its nine integration tests, which open a real VS Code (version 1.140.0, macOS, 2026-10-01), cover the
commands, markers, the hover, Open in report, Copy ticket text and refused files, not the Swipewalk view's labels or the
details page. Until a person has walked them, treat the list above as design intent that automated tests protect, not as a
tested result. Please tell us what doesn't work.

## Feedback

If you find an accessibility problem in Swipewalk, please open an issue in the
[Swipewalk issue tracker](https://github.com/swipewalk/swipewalk/issues) and describe what you were trying
to do, what happened, and the assistive technology you use. We aim to reply within ten working days.
