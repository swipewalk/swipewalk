# Roadmap

Swipewalk is maintained part-time, so this is an order of work, not a schedule. Suggestions and help
are welcome: open an issue, or comment on one of the items below.

## Working now (0.2)

- Scan the screen shown now on Android (emulator or USB device) and iOS (Simulator or iPhone).
- Record mode: use the app, and each new screen is scanned, also at a large system text size (Android and iOS,
  including physical devices — a physical iPhone's is driven through its own Settings app). Screens that weren't
  scanned are listed as a manual task. If checking a screen at the larger size needs the app restarted, Swipewalk
  asks first rather than restarting on its own. A recording that stops early (an error, a cancellation, or
  Swipewalk itself closing) is saved with what it captured and can be resumed with `record --continue`.
- Rules for missing names, identifier-like names (for review), visible text missing from the name, touch target
  size, text contrast measured from screenshot pixels, text that doesn't grow at large text sizes, controls or
  text that disappear at large text sizes with no scrollable area to reach them (for review), whether Android
  screens expose a pane title, candidate personal-data input fields with no confirmed autofill/content-type
  hint, and icon contrast on iOS. Apple's accessibility audit runs on iOS, and Google's Accessibility Test
  Framework runs on Android (through a small instrumentation harness that ships with Swipewalk); both engines'
  findings are included.
- Real TalkBack capture on Android (opt-in, `--screen-reader`): drives TalkBack itself over a screen's focusable
  elements and reports differences from the predicted transcript, running locally with no cloud service. Tested
  on an Android emulator with the system language set to English, Spanish, Hindi, Arabic and Japanese in turn.
- WCAG 2.2 mapping, and "relevant to" labels for ADA Title II, Section 508, EN 301 549 and the UK public
  sector regulations.
- HTML and JSON reports with a predicted screen-reader transcript, swipe order, and (on Android with
  `--screen-reader`) captured TalkBack speech next to the prediction.
- Run history, comparing runs, and CI exit codes (`swipewalk run`).
- A desktop app for macOS.
- Findings come from automated checks only; manual testing with assistive technology is still required.

## Next

Broad areas we're working towards, in no fixed order:

- Screen-reader testing
- Guided manual testing
- Wider WCAG coverage
- Automatic navigation through apps
- Windows support
- More app frameworks

## Ways to help now

- Try it on your devices and apps, and report what works and what doesn't.
- Report wrong or missing findings, and wrong WCAG mappings, with the "Wrong or missing finding" form.
- Share how your framework's controls appear in the accessibility tree.
