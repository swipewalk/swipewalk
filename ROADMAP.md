# Roadmap

Swipewalk is maintained part-time, so this is an order of work, not a schedule. Suggestions
are welcome: open an issue.

## Working now

### New in 0.6

- Swipewalk is now described as a scanner for any Android or iOS app, with extra help for .NET MAUI, with what has been tried stated. A new page,
  [How Swipewalk is tested](docs/testing.md), lists the devices, frameworks, sample apps and real apps used, what was found and
  missed, and what has not been tested.
- Native Android results are closer to what TalkBack speaks: a field named with `android:labelFor` is no longer reported as
  unnamed, a button named by several children gets the name TalkBack speaks, and a button cut off at an edge of a scrolling
  area (Android) is no longer measured at its visible size.
- Source-line pointing for native Android and iOS apps was measured on seven real apps with published source and made more careful:
  no confident wrong result was found afterwards, and many more elements get a list of candidates. Expect a list rather than one
  line for UIKit and SwiftUI apps. Results for React Native and Flutter were not measured on real apps.
- On a Mac, the command-line tool installs as a native program that needs no .NET to run (the .NET SDK is still needed to install
  it). Windows and Linux get the regular .NET version, untested there.
- The desktop app's keyboard and VoiceOver handling was improved and checked with real key presses and a scripted VoiceOver, not by
  a person: Tab reaches controls below the fold of a scrolling page, the law Search sheet and Laws that matter to me work
  from the keyboard and VoiceOver, and the app picker from the keyboard (with VoiceOver it was checked only by a scripted VoiceOver, which found reading-order problems still to fix), Escape and the arrow keys work in their filter boxes (Escape clears what you typed, then closes the sheet; choices already made in Laws that matter to me are kept), and History rows have an Open report button.
  Still open: keyboard focus is not put back on the button that opened a sheet. See the
  [accessibility statement](docs/accessibility-statement.md).
- The draft per-criterion export is replaced by an evidence report (`swipewalk export --format evidence`, and Export in the desktop app): one readable page showing, for each WCAG 2.2 Level A and AA criterion, what Swipewalk checked, what it found and what was not tested. It is not a conformance report.
- `swipewalk doctor` with two Android devices attached and no `--device` now names them instead of crashing.
- The NuGet package README now uses absolute links, so its images and links no longer break on nuget.org (checked once this version is published).

### New in 0.5

- `swipewalk share` and `swipewalk import` (and Share run… and File > Open… in the desktop app) send a saved run
  to someone else as one `.swipewalk` file, signed by default and checked before anything is added; `swipewalk keys` manages the
  keys you trust. Files are not encrypted, and a signature shows which key signed a file, not who the person is.
  The desktop app's History can read a folder you choose, such as one a team shares.
- A VS Code extension (a preview, on the Visual Studio Marketplace) marks each finding on the source line
  it likely comes from, read from a saved run; read-only, with no network calls or telemetry. Checked on macOS only.
- `swipewalk helpers remove` (and Remove Swipewalk helpers… on the Devices page) removes Swipewalk's own helper
  apps from a device, after asking; tried on an Android emulator and an iOS Simulator only.
- `swipewalk diagnostics` (and Help > Save Diagnostic Report…) saves a local diagnostic report for a bug report,
  made from a short local log; nothing is sent anywhere.
- The desktop app's pop-up menus are now drawn by the app itself so that Space or Return opens them with the keyboard (Return on macOS 14.4 and later; checked in UI tests, not yet by a person).
- A Search… button beside the desktop app's Law or standard menu, for finding one by typing part of its name or a place.
- results.json format 0.8 (a stable id, "who is affected" and a report link on every finding; see
  docs/results-schema.md).
- Each release carries two downloads of a made-up sample app (an Android .apk and an iOS Simulator .zip) to scan before
  trying your own app, with the full third-party notices and licence texts for what they carry.
- The Screen reader session page uses only the device you choose, checks it before changing anything and has a New session
  button after a session; a run's pictures are kept inside its own folder, and a shared or saved run can no longer point
  the app at files outside that folder.
- Scanning recovers when the Android helper on the phone was signed with a different key than the installed copy, error
  messages no longer show internal paths, and the README has install tips for an unreachable NuGet feed and for the
  PATH.

### New in 0.4.2

- The desktop app offers more of the command line's choices: a More checks section on New scan (dark/light, rotation, changing-content and web content checks, an app framework menu), iPhone signing, a check that an app is installed on the Devices page, comparing any two runs, a team triage file, and "Use my current laws and standards" when exporting.
- Help pages in the desktop app listing every automated check and known limitation.
- `swipewalk session --platform ios`, `swipewalk apps`, `swipewalk history --delete`, `swipewalk compare --app` and `--carry-triage`.
- The command line and the desktop app now offer nearly the same things (choosing another History folder in the desktop app came in 0.5), apart from a short list of exceptions that each have a stated reason (for example options meant for scripts).
- On Android, the dark/light and rotation checks no longer leave the phone's setting changed when it can't be read first.

### Earlier

- A desktop app for macOS, with more of the command line's scan, export and standards choices (an app source folder, any mapped US state or country, a Law or standard choice when exporting), a Laws and standards page with a way to suggest corrections, and a one-time welcome.
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
  a tagged PDF, or a draft per-criterion export for a person to complete (replaced in 0.6 by the evidence report).
- `swipewalk triage` marks a finding as already looked at (false positive, won't fix or accepted risk, with a
  required reason), stored apart from the run's own results.json so a rescan's raw findings are never touched;
  the desktop app carries a mark forward into the app's next run when it is clearly the same finding.
- Fix examples in the app's own framework (.NET MAUI, native Android Views, Jetpack Compose, UIKit, SwiftUI,
  Flutter, React Native, and web content in a hybrid app); apart from .NET MAUI and the generic native examples,
  they are written from each framework's documentation and haven't yet been applied in a real app of that
  framework and rescanned.
- `--source <path>` points findings at a likely source line in a local .NET MAUI, native Android, native iOS,
  React Native or Flutter project, stating exact / likely / candidates / not found for each; measured on seven real native
  Android and iOS apps in 0.6; .NET MAUI only on Swipewalk's sample app; React Native and Flutter only on small made-up projects.
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
