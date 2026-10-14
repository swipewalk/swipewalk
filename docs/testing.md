# How Swipewalk is tested

This page says what Swipewalk has been tested against, what the results were, and what has not been tested. It
is a summary of evidence recorded elsewhere in these docs, mainly the [case study](case-study.md) and
[known limitations](limitations.md); where the two differ, those pages have the detail.

Written 2026-10-04 for Swipewalk 0.5.0 and updated 2026-10-07 for 0.6.0. The scans, planted-bug results and real-app scans below were run with the versions named in the case study; the source-mapping measurement compares 0.5.0 with 0.6.

None of this is a statement that Swipewalk, or any app scanned with it, meets WCAG or any law. Automated checks
find only some accessibility issues, and manual testing with assistive technology is still required.

## What "tested" means here

- **Answer-key tests.** Swipewalk's own sample apps have bugs planted on purpose, with a written answer key of
  what a scan should and shouldn't report. An automated test scans saved captures and fails if a scan finds
  more or less than the key lists. The key is corrected when a real scan shows an assumption was wrong, not the
  other way round.
- **Real-app scans.** Swipewalk is also run on apps it wasn't written for. Scan results are published only for apps
  whose licence allows it (Microsoft's MIT-licensed .NET MAUI samples). The source-mapping measurement below names
  its apps with licence and commit; it reports how source mapping did, not the apps' accessibility findings.
- **Live device checks.** Scans, recordings and screen-reader captures run on emulators, Simulators and
  physical phones, with each device's settings restored afterwards.
- **Unit and UI tests.** Rules, mappings, reports and exports have unit tests that run automatically before
  changes are merged. The desktop app also has UI tests, driven through its accessibility interface, which are run
  on a Mac for changes to the app. That does not show a rule is right for every app; the sections below show how the rules behaved on real screens.

## Devices and versions used

| Kind | What | Used for |
|---|---|---|
| Android emulator | Android 16 (API 36), light mode | Sample apps, Microsoft's samples, TalkBack capture in five languages |
| Android phone | Google Pixel 4a, Android 13 (API 33), TalkBack 17.0.1 | Scans, large text, recording, TalkBack capture |
| iOS Simulator | iPhone 17, iOS 26.5 (and an iPhone SE-sized Simulator for one off-screen check) | Sample apps, Microsoft's samples |
| iPhone | iPhone SE, iOS 27.0 (an earlier check also ran on iOS 18.6) | Scans, recording, large text, Accessibility Inspector walk |
| Host | macOS (Apple silicon) with Xcode 27.0 and .NET 10 | All of the above |

Dates are in the case study (scans between 2026-09-22 and 2026-10-02). A rerun can differ a little on another
device or system version.

## Frameworks covered

| Framework | Tested how | Result in short |
|---|---|---|
| .NET MAUI | BuggyApp (planted bugs), four Microsoft MAUI samples | 10 of 11 planted bugs reported on Android (one missed) and 10 of 11 on iOS (the other one is not a problem there); see below |
| Native Android: Views | NativeAndroid sample, emulator and Pixel 4a | All 8 planted bug classes found; see the table below |
| Native Android: Jetpack Compose | NativeAndroid sample, emulator and Pixel 4a | Three bug classes not found and one not reproducible as planted (all in the table below) |
| Native iOS: UIKit | NativeiOS sample, Simulator and iPhone | Most bug classes found; the unlabeled icon button was not reproduced |
| Native iOS: SwiftUI | NativeiOS sample, Simulator and iPhone | Several bug classes found; the label-in-name mismatch was hidden |
| Flutter, React Native, hybrid web (Ionic, Capacitor, Cordova) | Not scanned as real apps. Fix examples exist, written from each framework's documentation; source mapping for React Native and Flutter was tried only on small made-up projects | Untested as scanned apps. Scanning reads the operating system's accessibility layer, so these apps should scan, but that has not been tried |
| Web content in a WebView | Android emulator (Chromium WebView); not tested in an iOS WebView | Verified on the emulator only; on a physical Android phone it isn't visible by default (see limitations) |

## Sample apps: planted bugs versus what was found

Counts come from [case study section 1](case-study.md#1-buggyapp-a-sample-net-maui-app-with-planted-bugs) and
[section 5](case-study.md#5-native-samples-the-same-bugs-four-ui-toolkits-no-net-maui); the case study is
recomputed before each release.

### BuggyApp (.NET MAUI), 11 planted bugs

- Android: 10 of 11 reported. **Missed: B10**, a tiny "Terms" tap link, because MAUI doesn't mark tap-gesture
  labels as clickable, so Android doesn't expose it as a target.
- iOS: 10 of 11 reported; B5 (a 20x20 icon) is not a problem there because MAUI enlarges the touch area to 44 pt.
- B11 (fixed-height text that clips when text is enlarged) is found only by the large-text rescan.
- Found without being planted: a low-contrast page title from the default MAUI template styles (both
  platforms), and, on Android, no pane title exposed to screen readers (for review).
- Scan totals on the 2026-10-02 Android run: 6 WCAG issues, 9 items for review, 4 platform advisories. The iOS
  re-scan of the saved capture: 6, 4 and 2 (5 items for review with the large-text rescan).

### Native samples, 8 bug classes in each of four toolkits

| Bug class | Views | Compose | UIKit | SwiftUI |
|---|---|---|---|---|
| Unlabeled icon button | found | found | not reproduced | not reproduced |
| Low-contrast text | found | found | found | found |
| Small touch target | found (advisory) | not found | found (advisory) | found (advisory) |
| Identifier as label | found | found | found | found |
| Label-in-name mismatch | found | not found | found | not found |
| Unlabeled field | found | found | found | found |
| Fixed text that shouldn't scale | found (large-text rescan) | not reproduced | found (Apple's audit) | found (Apple's audit) |
| Low-contrast icon | found (Google's ATF) | not found | found | found |

"Not reproduced" means the planted bug didn't behave as a bug on that platform when the raw captured tree was
read (for example, iOS gave the icon button a default name). "Not found" means the bug was present and Swipewalk
did not report it. Each "not found" and "not reproduced" cell was checked against the raw captured tree, and the answer key was
corrected where an assumption was wrong.

## Real apps (permissive licences only)

Microsoft's .NET MAUI samples from `dotnet/maui-samples` (MIT): DeveloperBalance, TipCalc, Calculator and
WeatherTwentyOne, on `Microsoft.Maui.Controls` 10.0.60. DeveloperBalance, TipCalc and Calculator were scanned
with Swipewalk 0.1.0; WeatherTwentyOne was scanned later, with Google's Accessibility Test Framework checks
(see case study section 3). Results are for those versions, on those devices.

- First-screen results for DeveloperBalance, TipCalc and Calculator are in the table in
  [case study section 2](case-study.md#2-real-apps-microsofts-net-maui-samples) (WCAG issues from 0 to 2 and
  platform advisories from 0 to 14 per app and device); WeatherTwentyOne is in
  [section 3](case-study.md#3-weathertwentyone-one-screen-two-themes-and-googles-atf).
- Found: an unnamed slider, a text field with no name, low-contrast tags, text that only changes size after an
  app restart (iOS, a known MAUI issue fixed in a later version), text that doesn't grow at all, many Android
  touch targets under 48 dp (advisories).
- WeatherTwentyOne's first screen scanned with no findings in dark mode and with five WCAG 1.4.3 contrast
  failures in light mode: the same app gave opposite results in the two themes.
- On iOS 27 the unmodified Microsoft samples crash at launch (they haven't adopted the UIScene lifecycle); the
  iOS 27 scans used copies with MAUI's scene support added. WeatherTwentyOne as published also crashes at launch
  on Android and iOS for a separate reason; the scanned copy moved one startup assignment.

Two Android apps that Swipewalk didn't write and that don't use .NET MAUI (one Jetpack Compose, one Views) were
also checked with TalkBack capture; their results aren't published.

## TalkBack and Accessibility Inspector capture

- TalkBack capture was run five times on one emulator (TalkBack 16.0.0), once each in English, Spanish, Hindi,
  Arabic and Japanese, plus a live check on the Pixel 4a. TalkBack's own words came back in each device language;
  none of the five produced a name difference for BuggyApp's correctly labeled controls. The comparison
  recognizes role words in English only (see limitations).
- The iOS Accessibility Inspector route was run on a physical iPhone across BuggyApp and the native iOS sample.
  Inspector order was not compared with the predicted order.
- VoiceOver captions (`record --voiceover-captions`) were tried once: one screen of the sample app, one iPhone
  SE, English.

## Source mapping on real apps

The measurement was run on 2026-10-04. Source mapping points a finding at the source line it likely comes from, given your project folder (`--source`, or "App source folder" in the desktop app). Only an exact match shows the finding's fix example next to the location, as an example to adapt; Swipewalk never changes your code. This section says how well it did on real apps with published source.

### How it was measured

Each app was built from source and installed on an Android 16 emulator or an iOS 26.5 Simulator (iPhone 17); one screen of one app was also scanned on a Pixel 4a (Android 13). Three to nine screens per app were reached by tapping and scanned with `swipewalk scan --source`. Every element on those screens that has a name or an identifier, plus unnamed images and buttons, was mapped, not only the ones with a finding. Each exact or likely result that did not match by identifier (64 of 201) was read at its source line; 12 to 16 candidate lists per app were sampled; and "not found" text was checked for whether it really is absent from the source. Results that matched by identifier (137 of 201 exact and likely: 129 exact, 8 likely) are very likely right, since the id is declared on that line; they were spot-checked for an id reused by a library or another layout.

### Apps measured

| App | Repository | Licence | Commit | Framework | Screens | Elements | Exact | Likely | Candidates | Not found |
|---|---|---|---|---|---|---|---|---|---|---|
| Jetsnack (Compose samples) | github.com/android/compose-samples | Apache-2.0 | 0091ef17 | Jetpack Compose | 6 | 158 | 28 | 1 | 94 | 35 |
| Now in Android | github.com/android/nowinandroid | Apache-2.0 | 5ba16edd | Jetpack Compose | 5 | 116 | 32 | 1 | 50 | 33 |
| Wikipedia Android | github.com/wikimedia/apps-android-wikipedia | Apache-2.0 | 4778ade23 | Android Views | 9 | 201 | 79 | 0 | 76 | 46 |
| Fossify Notes | github.com/FossifyOrg/Notes | GPL-3.0 | 486d3bd | Android Views | 3 | 90 | 42 | 0 | 1 | 47 |
| Wikipedia iOS | github.com/wikimedia/wikipedia-ios | MIT | 909df05 | UIKit | 9 | 143 | 5 | 8 | 103 | 27 |
| MovieSwiftUI | github.com/Dimillian/MovieSwiftUI | Apache-2.0 | 72eb4ae | SwiftUI | 5 | 204 | 4 | 1 | 71 | 128 |
| Ruddarr | github.com/ruddarr/app | MIT | 82968ff | SwiftUI | 5 | 128 | 0 | 0 | 100 | 28 |
| All | | | | | 42 | 1,040 | 190 | 11 | 495 | 344 |

"Exact" means the identifier or text was found in only one place in the code that could show it. "Likely" is a best guess (for example the same text in several places with one file standing out, or a match by size, an adjacent label or a template); check it before changing anything. "Candidates" is a short list of places that could be the source. "Not found" means nothing in the project matches, often because the text is supplied while the app runs. Each app was built at the commit shown, except that two Android apps were built at an older commit than their latest (the one shown) because only some Android SDK versions were installed.

### Before and after

The same 1,040 elements were mapped by the previous release and by this one.

| | Exact | Likely | Candidates | Not found | Confident results found wrong |
|---|---|---|---|---|---|
| Previous release (0.5.0) | 194 | 39 | 273 | 534 | 22 |
| This release (0.6) | 190 | 11 | 495 | 344 | 0 found |

The 22 wrong results were: 10 SwiftUI elements (combined names and system scroll bars) pointed at an unrelated view, 9 Android strings pointed at a unit-test file, 2 SwiftUI tab buttons pointed at another screen's text with the same words, and 1 toolbar action pointed at an unrelated dialog item. In this release, of the 201 exact or likely results, 62 matched by text, 1 by the size of an unlabeled button and 1 by storyboard markup; all 64 were read and none was wrong, except that the one storyboard match could not be confirmed either way. "0 found" is not a guarantee: the sets are small, all in English and light mode, and one reviewer judged what was right.

By framework, exact results were 121 of 291 elements for Android Views, 60 of 274 for Compose, 5 of 143 for UIKit and 4 of 332 for SwiftUI. For UIKit and SwiftUI expect a list of candidates rather than a single line, because the same words often appear in several places. In the sampled candidate lists (12 to 16 per app), the true source line was present in all but a few cases; the misses were text that comes from network data but also appears as sample data in the app's own code, and a toolbar's own "Done".

### Not measured

- React Native and Flutter on real apps: no real app could be built with the tools used, so their mapping has been tried only on made-up test data.
- A physical iPhone (the Simulator was used, since mapping reads your project folder on the computer).
- Ruddarr showed only an empty state without a Radarr or Sonarr server, so its five screens are one empty state; so its 0 exact says little about SwiftUI apps in general.
- Dark mode, languages other than English, and multi-module apps other than Now in Android.
- Text that the app receives while it runs (network data, system text, library screens) cannot be found in the source; it is the largest share of "not found".
- Two apps could not be built and were left out (one needed an Android platform that was not installed; one does not compile with the Xcode version used).

## Known misses and false positives

- Missed: BuggyApp's "Terms" link on Android (above); native samples' "not found" cells (above).
- Reported for review that is probably not a problem: a decorative divider (neither uiautomator nor XCUITest
  shows "hidden on purpose"; Google's ATF also flags it on contrast); an ATF duplicate-speakable-text finding on a
  correctly labeled field.
- Screen-reader output is predicted from the accessibility tree unless `--screen-reader` captured real evidence.
- Source-line pointing mostly gives a best guess, labelled exact, likely or candidates. It was measured on seven
  real Android and iOS apps (see "Source mapping on real apps") and, for React Native and Flutter, only on small
  made-up projects.
- Every limitation is listed with its impact in [docs/limitations.md](limitations.md).

## Not tested

- **The native command-line build:** built and run on macOS only (Apple silicon natively; the Intel build under Rosetta, not on an Intel Mac). The Windows and Linux installs get the regular .NET version, which has not been run on those systems.
- **Windows apps:** can't be scanned yet. Running the command-line tool on Windows to scan Android has not been
  tried on real Windows hardware.
- **Linux:** running the tool on Linux to scan Android has not been tried on real Linux hardware.
- **Languages:** the app screens scanned were English. TalkBack capture was checked in five languages on one
  emulator; VoiceOver captions in English only. Nothing else was checked in other languages.
- **Other frameworks as scanned apps:** Flutter, React Native, hybrid web and anything else not listed above.
- **Other devices and versions:** other phone makers' TalkBack builds, other iOS versions, tablets and foldables.
- **The desktop app and VS Code extension with assistive technology:** the 0.6 keyboard and VoiceOver fixes were checked by script (real key presses, a scripted VoiceOver), not by a person; the desktop app has known keyboard and
  VoiceOver gaps (see its [accessibility statement](accessibility-statement.md)); the VS Code extension has been
  tested on macOS only and not with a screen reader.
- **Apps that block scanning,** such as screens that block screenshots.
- **Testing with disabled people:** none yet.
