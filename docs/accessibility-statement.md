# Accessibility statement for Swipewalk

Swipewalk helps teams find accessibility issues, so we want people with disabilities to be able to use it too. This statement
describes what we test Swipewalk against, how, and the issues we know about. It covers the Swipewalk desktop
app for macOS and the HTML reports it produces. It is not a statement of conformance: automated and developer
testing cannot show that software meets every requirement, and some checks listed below still need to be done by
people who use assistive technology every day.

Last reviewed: 2026-09-24, on macOS 26.6 (desktop app and report format 0.2).

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
- Every page is reachable from the keyboard: ⌘1 Dashboard, ⌘2 or ⌘N New scan, ⌘3 Devices, ⌘4 History. Help >
  Keyboard Shortcuts lists them.
- Text can be enlarged to 200% with ⌘= (Zoom menu), including the embedded report, with the exceptions listed
  below. In our developer checks at 200% the layout reflows without losing content.
- Automated tests check that the app's text colors reach 4.5:1 and text-field outlines 3:1 against their
  backgrounds in light and dark mode. The main button's default macOS color did not, so it was changed.
- Reports have headings, landmarks, text alternatives for the screenshots and color samples, and a predicted
  screen-reader transcript that is labelled as predicted.
- Each row on the History page has one combined accessible name (app, date, platform, mode, status such as
  "recording in progress" or "ended early", screen and finding counts, and standard), so VoiceOver announces the
  whole row at once instead of separate, disconnected pieces of text. Its Delete button, and its Continue button
  on a row that ended early, are still separately reachable and separately named (for example "Delete the run
  of … from …"), not hidden behind the row's combined name.
- The notice shown when a physical phone is selected (large-text checks change the phone's own text size) is a
  native system alert rather than a page in the app. On a Mac with VoiceOver, the notice's title and body were
  read, and a real Escape key closed it.
- The Guided checks page (walks a run's screens with step-by-step WCAG questions) sets an accessible name via
  `SemanticProperties` on every interactive control -- the screen and assistive-technology pickers, each
  criterion's result choice, the evidence/reason/note/tester fields and the Save button -- and marks each
  criterion's title as a heading. The evidence and reason fields' accessible names repeat "required for pass"/
  "required" (their placeholder text alone would not be heard once a screen reader announces the field's own
  Description). It has not yet been checked with VoiceOver (see Known issues); one known gap that check would
  likely find: the Save button doesn't say why it's disabled while it is.

## Known issues

| Issue | WCAG | Workaround | Plan |
|---|---|---|---|
| The text inside native Mac controls (the values of menu buttons, button titles such as "Choose…", checkbox labels and sidebar items) stays at the system size when the in-app text size is increased. The labels next to them do scale. | 1.4.4 Resize Text (AA) | Use macOS Zoom (System Settings > Accessibility > Zoom); this magnifies the screen and does not fix the issue. | Check whether these controls follow the macOS text size setting; investigate drawing them at the app's text size without losing their native accessibility. |
| On the Mac, the menu buttons are reported as buttons (with their selected value and a hint that they open a menu) rather than pop-up buttons, and the checkboxes as switches. Their names, values and states are announced; only the role names differ from native Mac apps. | 4.1.2 Name, Role, Value (A) | — | Revisit when Mac Catalyst exposes pop-up button and checkbox roles. |
| Not every control has been checked for keyboard operation and a visible focus indicator, with and without macOS Full Keyboard Access (System Settings > Accessibility > Keyboard). | 2.1.1 Keyboard (A), 2.4.7 Focus Visible (AA) | Use ⌘1–⌘4 to move between pages. | Planned: a keyboard-only walkthrough of every page. |
| Contrast of control borders, checkbox and focus indicators has not been measured; only text-field outlines are tested. | 1.4.11 Non-text Contrast (AA) | — | Extend the palette test or measure from screenshots. |
| The app has not yet been tested by a person using VoiceOver, Voice Control or Switch Control for a whole task. The UI tests use the same accessibility interface, but they are not a substitute. | — (not yet tested) | — | Planned: testing with people who use assistive technology. We welcome feedback in the meantime (see below). |
| Progress messages in the log are only announced for key events, not every line. | 4.1.3 Status Messages (AA) | Review the Progress list after a scan. | Review with screen reader users. |
| On the Guided checks page, the Save button becomes enabled once a valid answer is entered, but nothing announces why it was disabled before that. | 3.3.2 Labels or Instructions (A) | Fill in the required evidence/reason field; Save enables once it's non-empty. | Announce the requirement when Save is pressed while still disabled, or state it up front. |
| The Windows version has not been built or tested. | — (not yet tested) | — | Windows collector and desktop build (roadmap). |

## Feedback

If you find an accessibility problem in Swipewalk, please open an issue in the
[Swipewalk repository](https://github.com/swipewalk/swipewalk/issues) and describe what you were trying
to do, what happened, and the assistive technology you use. We aim to reply within ten working days.
