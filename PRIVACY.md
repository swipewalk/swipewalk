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
  was an emulator, simulator or physical device. The name you gave the device ("Alex's iPhone"), its
  serial number and its device ID are not saved.

## Network access

Swipewalk itself makes no network requests. The platform tools it runs may: for example `adb`,
`xcodebuild` (which can contact Apple to sign the iOS harness for a physical device) and
`devicectl`. Their own privacy terms apply.

The Android accessibility harness (Google's Accessibility Test Framework) ships prebuilt inside
Swipewalk, so using it needs no network access either, only `adb`. Swipewalk builds it from source
with Gradle instead only when running from a source checkout with no prebuilt copy, or when you name
your own copy with `--android-harness <path>`; Gradle then downloads itself, build tools and
dependencies from Gradle's, Google's and Maven Central's servers, and their own privacy terms apply.

## Reports you share

Reports can contain whatever was on screen during the scan. See
[the disclaimer](DISCLAIMER.md#your-responsibilities) for advice on test data and reviewing reports
before sharing them.
