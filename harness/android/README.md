# Swipewalk Android instrumentation harness

A thin Gradle project that builds one instrumentation test APK, with two independent jobs:

- **`HarnessTest#capture`** (always run): reads the accessible windows through `UiAutomation`, builds an
  ATF `AccessibilityHierarchyAndroid` from `UiAutomation#getWindows()`, runs Google's [Accessibility Test
  Framework](https://github.com/google/Accessibility-Test-Framework-for-Android) (ATF) 4.1.1's checks with
  a screenshot for the contrast checks, and writes one JSON file with the extra node properties and ATF
  results, which `Swipewalk.Collectors.Android.AndroidHarness` pulls with `adb`.
- **`HarnessTest#captureScreenReader`** (opt-in, `--screen-reader`): drives real TalkBack over the app
  (see `TalkBackCollector.kt`'s own remarks for how) and writes what it actually said. Costs roughly 1-2
  seconds per element, so it's a separate `am instrument` call, never run as part of an ordinary scan.
  **`HarnessTest#restoreScreenReaderSettings`** repairs a leftover from a capture that crashed before its
  own restore ran; `AndroidHarness`/pre-flight call it before trusting the device's accessibility settings.

It never modifies the app under test, and it does not implement its own scanning logic beyond that: see
`Swipewalk.Collectors.Android.AndroidHarness` (build/install/invoke) and `Swipewalk.Core.Rules.AtfIssueRule`
/ `Swipewalk.Core.Rules.ScreenReaderCaptureRule` (WCAG mapping) for everything downstream.

Shipped copies of Swipewalk install a prebuilt copy of this APK automatically (no JDK, Gradle or network
access needed -- see CI's build step in `.github/workflows/ci.yml`/`release.yml` and how
`Swipewalk.Collectors.csproj`/the desktop app's csproj bundle it, next to `harness/ios`'s source). Building
from this source with Gradle is only the fallback `AndroidHarness` uses for a from-source checkout with
nothing built yet, or `--android-harness <path>`. This page is for anyone changing or debugging the harness
itself.

## Build

Needs a JDK (17+; Android Studio's bundled JBR at `/Applications/Android Studio.app/Contents/jbr` works, or
any JDK on `PATH`/`JAVA_HOME`) and the Android SDK (`ANDROID_HOME`/`ANDROID_SDK_ROOT`, or
`~/Library/Android/sdk`).

```bash
./gradlew :harness:assembleDebugAndroidTest
```

Produces `harness/build/outputs/apk/androidTest/debug/harness-debug-androidTest.apk`. It has no separate
"app under test" of its own -- `:harness` is a library module whose only real content is its `androidTest`
source set, installed with `adb install -r -t <apk>` as package `org.swipewalk.harness.test` (AGP's default
`<namespace>.test` for this module's `org.swipewalk.harness` namespace).

## Run

```bash
adb shell am instrument -w -e class org.swipewalk.harness.HarnessTest#capture \
  -e package <app package under test> \
  org.swipewalk.harness.test/androidx.test.runner.AndroidJUnitRunner
```

Writes `harness-result.json` into the harness test package's own external files directory
(`/sdcard/Android/data/org.swipewalk.harness.test/files/`), readable with a plain `adb shell cat` (no root
or extra permission needed for an app's own external files directory on a development device/emulator).

The screen-reader capture runs the same way, with a different class/result file:

```bash
adb shell am instrument -w -e class org.swipewalk.harness.HarnessTest#captureScreenReader \
  -e package <app package under test> \
  org.swipewalk.harness.test/androidx.test.runner.AndroidJUnitRunner
```

writes `screen-reader-result.json` (see [What's in the result](#whats-in-the-result) below). It changes
device-wide accessibility settings for the duration of the call and always restores them, including on
failure; `org.swipewalk.harness.HarnessTest#restoreScreenReaderSettings` (no `-e package` needed) repairs a
leftover from a call that didn't get to finish.

## What's in the result

- `nodes`: every accessibility node belonging to the target package, across all windows, with the extra
  properties `uiautomator dump` doesn't expose: `isImportantForAccessibility` (API 24),
  `isShowingHintText` (API 26), `isHeading`/`paneTitle` (API 28), `stateDescription` (API 30). Each entry's
  `key` matches the same node identity Swipewalk's `UiAutomatorParser` computes from a `uiautomator dump`
  (class, screen bounds, resource id, text, content description), so the two can be merged.
- `atfIssues`: ATF's `ERROR`/`WARNING` results (its `NOT_RUN`/`RESOLVED`/`SUPPRESSED`/`INFO` results are
  left out) from all 14 checks in `AccessibilityCheckPreset.LATEST` (the same set as `VERSION_4_0_CHECKS` as
  of ATF 4.1.1): `ClassNameCheck`, `ClickableSpanCheck`, `DuplicateClickableBoundsCheck`,
  `DuplicateSpeakableTextCheck`, `EditableContentDescCheck`, `ImageContrastCheck`,
  `LinkPurposeUnclearCheck`, `RedundantDescriptionCheck`, `SpeakableTextPresentCheck`, `TextContrastCheck`,
  `TextSizeCheck`, `TouchTargetSizeCheck`, `TraversalOrderCheck`, `UnexposedTextCheck`. See
  `Swipewalk.Core.Rules.AtfIssueRule` for which of these map to a WCAG 2.2 criterion.
- `screen-reader-result.json`'s `talkBackVersion`, `language` (the device's system language when the
  capture ran), `complete`/`notCompleteReason` and `items` (each an `order`, the verbatim `spokenText`
  TalkBack sent to be spoken, and a `key` in the same node-identity scheme as `nodes` above): see
  `TalkBackCollector.kt`'s own remarks, and `Swipewalk.Core.Rules.ScreenReaderCaptureRule` for how
  differences from the predicted transcript become findings.

## Files

- `AtfCollector.kt`: the tree walk and the ATF run. Kept as the only real logic in the harness.
- `TalkBackCollector.kt`: drives TalkBack, reads back exactly what it says by making
  `harness/android/ttsengine` (a separate module, see below) its default text-to-speech engine, and
  restores every accessibility setting it changes -- the other real logic in the harness.
- `HarnessTest.kt`: the JUnit entry points `am instrument` calls; wires `AtfCollector`/`TalkBackCollector`
  to instrumentation arguments and writes each result file. Nothing else.
- `../ttsengine`: a separate, real installed app (not part of this androidTest APK -- a text-to-speech
  engine has to be a normal app), built and installed alongside the harness only when `--screen-reader`
  is used. See its own `build.gradle.kts` and `TalkBackTtsEngine.kt` for why, and `SafetyTimerReceiver.kt`
  for the on-device timer that restores accessibility settings on its own if the host or the capture
  process stops responding partway through.

## Licensing

The Accessibility Test Framework is Apache 2.0, published on Google's Maven repository
(`https://dl.google.com/dl/android/maven2`, not Maven Central) rather than vendored here; see
`../../THIRD-PARTY-NOTICES.md`.
