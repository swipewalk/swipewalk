# Roadmap

Swipewalk is maintained part-time, so this is an order of work, not a schedule. Suggestions
are welcome: open an issue.

## Working now (0.4)

- Scan the screen shown now on Android (emulator or USB device) and iOS (Simulator or iPhone).
- Record mode: use the app, and each new screen is scanned, also at a large system text size (Android and iOS,
  including physical devices — a physical iPhone's is driven through its own Settings app). Screens that weren't
  scanned are listed as a manual task. If checking a screen at the larger size needs the app restarted, Swipewalk
  asks first rather than restarting on its own. A recording that stops early (an error, a cancellation, or
  Swipewalk itself closing) is saved with what it captured and can be resumed with `record --continue`.
- `scan --appearance both` checks the screen again in the device's other dark/light appearance, and
  `scan --orientation both` rotates it and flags the screen for review (WCAG 1.3.4 Orientation) when its
  content doesn't visibly follow (scan only for now). `--auto-update-content` takes a few further captures a
  few seconds apart and flags content that changes on its own, for review against WCAG 2.2.2 Pause, Stop, Hide.
- Rules for missing names, identifier-like names (for review), visible text missing from the name, touch target
  size, text contrast measured from screenshot pixels, text that doesn't grow at large text sizes, controls or
  text that disappear at large text sizes or sit off-screen with no scrollable area to reach them (for review),
  whether Android screens expose a pane title, candidate personal-data input fields with no confirmed
  autofill/content-type hint, and icon contrast on iOS. Apple's accessibility audit runs on iOS, and Google's
  Accessibility Test Framework runs on Android (through a small instrumentation harness that ships with
  Swipewalk); both engines' findings are included.
- Real TalkBack capture on Android (`--screen-reader`, on by default in the desktop app's New scan page): drives
  TalkBack itself over a screen's focusable elements and reports differences from the predicted transcript,
  running locally with no cloud service. Tested on an Android emulator with the system language set to English,
  Spanish, Hindi, Arabic and Japanese in turn. On iOS (`scan` and `record`), `--screen-reader` instead walks
  Xcode's Accessibility Inspector on your Mac and records the properties VoiceOver uses (label, value, traits)
  for each element -- this is not VoiceOver's own speech, and it needs the macOS Accessibility permission and
  a setup step once per scan or recording session.
- WCAG 2.2 mapping and "relevant to" labels for ADA Title II, Section 508, EN 301 549 and the UK public sector
  regulations, each standard's own clauses beyond WCAG, and mapped US states and other countries too,
  in every report: each finding mapped to WCAG names the ones it is relevant to and how many more, with a grouped list, and a
  laws table lists them all; `--standard <id>` shows only that one (`swipewalk standards`), and `--my-laws <ids>` lists the ones you choose first without hiding the rest. Shown as name, WCAG version and level,
  scope and official source only, never legal detail.
- Reports open on a "What to fix" view (findings and fix guidance) with a "Full audit detail" view alongside it
  (the full WCAG/beyond-WCAG coverage and limitations); findings that likely share one root cause are
  grouped, every finding states who it affects, and every report records the scanned app's own version.
- `swipewalk export` turns a saved run into a Markdown ticket per finding, a CSV, a self-contained HTML report,
  a tagged PDF, or a draft Accessibility Conformance Report for a person to review.
- `swipewalk triage` marks a finding as already looked at (false positive, won't fix or accepted risk, with a
  required reason), stored apart from the run's own results.json so a rescan's raw findings are never touched;
  the desktop app carries a mark forward into the app's next run when it is clearly the same finding.
- Fix examples in the app's own framework (.NET MAUI, native Android Views, Jetpack Compose, UIKit, SwiftUI,
  Flutter, React Native, and web content in a hybrid app); apart from .NET MAUI and the generic native examples,
  they are written from each framework's documentation and haven't yet been applied in a real app of that
  framework and rescanned.
- `--source <path>` points findings at a likely source line in a local .NET MAUI, native Android, native iOS,
  React Native or Flutter project, stating exact / likely / candidates / not found for each; tested only on
  Swipewalk's sample apps and small made-up projects.
- Web content inside apps is scanned on the Android emulator (verified there only; not by default on a physical
  phone); `scan --web-audit` (Android debug builds) adds axe-core.
- `swipewalk session` and the desktop app's Screen reader session page: use TalkBack yourself while Swipewalk
  records which controls it reached, what it said and your notes (Android); evidence to review, never a pass or
  fail.
- `record --voiceover-captions` and the desktop app's iPhone option: a first look at reading VoiceOver's captions
  on a physical iPhone while you swipe (tried on one iPhone, one screen, English).
- Saved runs grouped by app and version, and one report combined from scans you choose (`swipewalk export --app
  ... --app-version ... --run ...`, or History in the desktop app).
- The desktop app launches on macOS 27, and closing its window leaves a running scan, recording or session going.
- Installing an Android App Bundle (`.aab`) directly, via Google's bundletool.
- HTML and JSON reports with a predicted screen-reader transcript, swipe order, and (with `--screen-reader`)
  captured screen-reader evidence next to the prediction.
- Run history, comparing runs, and CI exit codes (`swipewalk run`).
- A desktop app for macOS, with more of the command line's scan, export and standards choices (an app source folder, any mapped US state or country, a Law or standard choice when exporting), a Laws and standards page with a way to suggest corrections, and a one-time welcome.
- Findings come from automated checks only; manual testing with assistive technology is still required.

## Next

Broad areas we're working towards, in no fixed order:

- Real VoiceOver evidence on iOS
- Guided manual testing
- Wider WCAG coverage
- Automatic navigation through apps
- Windows support
- More app frameworks

## Ways to help now

- Try it on your devices and apps, and report what works and what doesn't.
- Report wrong or missing findings, and wrong WCAG mappings, with the "Wrong or missing finding" form.
- Share how your framework's controls appear in the accessibility tree.
