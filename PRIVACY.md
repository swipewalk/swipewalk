# Privacy

Swipewalk runs on your computer. It has no accounts, no analytics and no telemetry, and it does
not send scan results, screenshots or usage data to the Swipewalk authors or anyone else.

## What it stores, and where

- **Scan output** (`results.json`, `report.html`, screenshots and accessibility trees) is written to
  the folder you choose with `--out`, and every `run` is also saved to the local run history
  (`~/Library/Application Support/Swipewalk/runs` on macOS). Delete these folders to remove it.
- **Settings** you ask it to remember (for example an Apple development team ID for signing the iOS
  harness) are saved in your user profile on this computer (`~/.config/swipewalk`). While a large-text
  check runs, the device's original text size is kept there too, so it can be put back if the scan is
  interrupted.
- **Device details** saved in `results.json` and reports are the model, OS version and whether it
  was an emulator, simulator or physical device. The serial number and device ID are not saved, and the
  name you gave the device ("Alex's iPhone") is normally not saved either: Swipewalk saves the model when it
  can read it. When it can't, an iOS device's own name is kept only if it starts with "iPhone", "iPad" or
  "iPod" and has no apostrophe (such as the default "iPhone 16 Pro"); otherwise it shows as "iOS device" or
  "iOS simulator". A name you typed that happens to follow that pattern (for example "iPhone Alex") would be
  kept. A run saved by an earlier version could hold the name you gave an iPhone when its model wasn't known.
  The first time a newer version reads the run history (for example the History page), it replaces the name
  using the same rule (for example with "iOS device") in that run's copy in the history and redraws its
  report there (the device shown for an iPhone VoiceOver session included). The folder you chose with `--out`, and copies you already exported or sent elsewhere,
  aren't changed; delete or re-export those yourself.
- **Screen reader sessions** (desktop app and `swipewalk session`, Android) save what TalkBack said
  while the app was in front, including a notification it read out, text-field contents and typed
  characters, plus your notes, in that run's folder.

## Network access

Swipewalk itself makes no network requests. The platform tools it runs may: for example `adb`,
`xcodebuild` (which can contact Apple to sign the iOS harness for a physical device) and
`devicectl`. Their own privacy terms apply.

The Android accessibility harness (Google's Accessibility Test Framework) ships prebuilt inside
Swipewalk, so using it needs no network access either, only `adb`. Swipewalk builds it with
Gradle instead only when you point `--android-harness <path>` at a harness project folder of your own;
Gradle then downloads itself, build tools and
dependencies from Gradle's, Google's and Maven Central's servers, and their own privacy terms apply.

A live screen-reader session (desktop app and `swipewalk session`, Android) passes each TalkBack
utterance to Google's own "Speech Services by Google" app (`com.google.android.tts`) so the person
running the session can hear TalkBack while Swipewalk records the text. That text is what the
session saves (see "Screen reader sessions" above), so it can include a notification read out,
text-field contents and typed characters. Swipewalk itself sends nothing over the network. Google's
engine may, depending on the voice: Android lets a voice report that it needs a network
connection to work at all (`Voice.isNetworkConnectionRequired()`, developer.android.com, checked
2026-09-28: "Does the Voice require a network connection to work"), and on a Pixel phone the
engine's own settings (Settings > Accessibility > General > Text-to-speech output > the gear next
to its name > Install voice data) list each language as a network download and offer "Download
voices data using only Wi-Fi, this conserves data usage" (checked 2026-09-28). Whether an
already-downloaded voice sends text anywhere when it speaks isn't confirmed by anything found, so
use test data. Google's own privacy terms apply.

## Reports you share

Reports can contain whatever was on screen during the scan. See
[the disclaimer](DISCLAIMER.md#your-responsibilities) for advice on test data and reviewing reports
before sharing them.
