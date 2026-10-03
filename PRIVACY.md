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
- **Sharing keys.** The first time you share a signed run, Swipewalk creates a signing key: on a Mac `~/Library/Application Support/Swipewalk/signing-key.p8`, on Linux `~/.config/swipewalk/signing-key.p8` (or under `$XDG_CONFIG_HOME/swipewalk`), a file only your user can read and not encrypted (anyone who can read your user files or backups can sign as you). On Windows it is designed to be protected for your account with DPAPI (not yet tried on Windows). The keys you trust are in `trusted-keys.json` in the same folder. Runs you remove from the list in a shared History location are recorded in `hidden-runs.json` next to the default History folder. Delete these files to remove them; a deleted signing key means people have to trust a new one.
- **Helper apps on the device.** Scanning installs a few small apps of Swipewalk's own on the device (on Android a
  scanning helper, and for a TalkBack capture a text-to-speech helper that is normally removed again afterwards and stays only if Swipewalk can't confirm the settings were put back; on iOS
  up to two runner apps). They stay after a scan so the next one is faster. While a scan runs they write the
  screen's accessibility data, which includes the text on screen, into their own storage on the device, and
  those files can stay there until the helpers are removed. `swipewalk helpers remove`, or Remove Swipewalk helpers… on the
  desktop app's Devices page, removes them; the user guide lists each one
  ([What Swipewalk installs on a device](docs/user-guide.md#what-swipewalk-installs-on-a-device)).

## Diagnostic log

Swipewalk keeps a short log of what it did, so a failure or an unexpected close can be explained afterwards. It is a
plain text file on your computer and is never sent anywhere.

- **Where:** `~/Library/Logs/Swipewalk` on macOS, `%LOCALAPPDATA%\Swipewalk\Logs` on Windows,
  `~/.local/state/swipewalk/logs` (or `$XDG_STATE_HOME/swipewalk/logs`) on Linux. One file a day (more if a day's log passes 4 MB); the command line
  and the desktop app write to the same files. Files more than a week old are deleted, and older files are removed to
  keep the folder near 10 MB (checked when Swipewalk starts and once a day). Delete the folder to remove the log. The environment variable
  `SWIPEWALK_LOG_DIR` names another folder for it (the command line and `swipewalk diagnostics` then use that folder; the
  desktop app opened from Finder doesn't see it).
- **What it holds:** Swipewalk's version, what each scan or recording was asked to do (platform, app id, framework,
  which options were on) and how it ended (screens, counts, time), pre-flight results, warnings, errors with their
  stack traces, and each progress message with anything in double quotes (the name of a screen) replaced by "…".
  It is designed not to hold text from the screens you scan (labels, values, what TalkBack or VoiceOver said), and it
  never holds screenshots or the contents of `results.json`. Error messages are kept as the tools gave them. A normal
  run records the standard detail only, with no commands except where an error message names the command that failed;
  setting `SWIPEWALK_LOG=debug` in a terminal also records the tool commands Swipewalk runs (adb, xcrun, xcodebuild and
  others), with their arguments, exit codes and how long each took, with the removal below applied (the VoiceOver
  captions capture, used by `--voiceover-captions` and the iPhone VoiceOver session, is not yet included; an app opened from Finder doesn't see a shell
  variable, so it records the standard detail).
- **What is removed before a line is written:** device serial numbers and IDs, the names people gave their devices
  (the model is kept), Apple team IDs and signing identities, your user name, your computer's name, and home folders
  such as `/Users/<name>`, `C:\Users\<name>` and `/home/<name>`.
- **What is kept:** the package or bundle id of each app you scan, the names of app files and folders you give
  Swipewalk (your home folder shows as `~`), the error messages of the tools it runs, the versions of Swipewalk, your
  system and those tools, and times. These are in the log and in the report.
- **The report:** `swipewalk diagnostics`, or Help > Save Diagnostic Report… in the desktop app, copies the versions, the
  last pre-flight results Swipewalk logged (it does not run them again), the last five runs and the last 24 hours of the
  log (at most 2,000 lines) into one text file you choose where to save, and passes it through the same removal once more. Swipewalk never
  uploads it: you read it, and you decide whether to attach it to a bug report. Reports usually end up on a public
  GitHub issue, so read it first. The removal works by pattern and can miss something.
- **After an unexpected close** the desktop app keeps a small marker file in `~/.config/swipewalk/running` while it
  runs, removed when it exits normally. A marker left behind makes the Dashboard offer a diagnostic report once;
  nothing happens unless you choose it.

## Network access

Swipewalk itself makes no network requests. The platform tools it runs may: for example `adb`,
`xcodebuild` (which can contact Apple to sign the iOS harness for a physical device) and
`devicectl`. Their own privacy terms apply.

The Android accessibility harness (Google's Accessibility Test Framework) ships prebuilt inside
Swipewalk, so using it needs no network access either, only `adb`. Only a harness build named with
`--android-harness` (an option for Swipewalk's maintainers; normal use never needs it) is built with Gradle instead,
and Gradle then downloads itself, build tools and
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

A shared run file (`swipewalk share`, or Share run… in the desktop app) is a plain zip, not encrypted. It holds the run's results (all the text Swipewalk read on each screen), the screenshots unless left out, the triage marks unless left out (with the reason entered with each; the names entered with them are left out), the optional "Shared by" text, and, for a run scanned with `--source`, file names and line numbers. Home folders, device ids, Apple team names and ids and similar details are replaced with placeholders in the text the file holds, by pattern, which can't be complete: read the report before you send it. See [Sharing a run](docs/user-guide.md#13-sharing-a-run).
