# User guide

This guide walks you through installing Swipewalk, scanning your first screen, reading a report,
and running scans in CI. It's for developers and QA on .NET MAUI and native Android/iOS teams. It
takes about 10-15 minutes to read.

Two things to keep in mind throughout: Swipewalk's automated checks find only some accessibility
issues, and a report — even a report with no findings — never means an app is "compliant",
"accessible" or "passes WCAG". Manual testing with a screen reader and other assistive technology
is still required. See [the disclaimer](../DISCLAIMER.md) for the full terms.

## Before you start: use a test device

Use a test device and test data, not real personal information. On a physical phone, several checks temporarily change device settings and
restore them afterwards. If a run is interrupted, the next run or `swipewalk doctor` restores what
it can, and says how to change anything back by hand if it can't. Avoid running these checks on a
phone someone relies on every day. What happens:

- **The large-text check** (`scan --large-text`, and `record` by default) changes the phone's
  system text size to check how the app's text resizes, then changes it back. On a physical
  Android phone this takes a couple of seconds. On a physical iPhone, Swipewalk drives the
  Settings app itself (Accessibility > Display & Text Size) to turn on Larger Accessibility Sizes
  and set the slider to the largest size, then returns it to how it was — this takes a couple of
  minutes per screen and needs the phone to stay unlocked throughout. Swipewalk prints a notice
  before doing this the first time.
- **If a run is interrupted** (Ctrl+C at the wrong moment, the phone locks, a crash) before the
  setting is restored, the next `scan`, `record` or `doctor` run on that device notices and
  restores it for you. On Android this is a single setting and needs no extra setup; on a physical
  iPhone it needs a working signing setup, and if that fails `doctor` says so and gives the exact
  Settings steps to change it back by hand.
- **If a physical iPhone's cable comes unplugged mid-scan or mid-recording**, the underlying
  automation just waits, with no output, while it's gone. Swipewalk watches the connection in the
  background and, if the iPhone still looks disconnected after about 15 seconds, prints "The
  iPhone looks disconnected. Reconnect it, or press Ctrl-C to stop (then run `swipewalk doctor` to
  restore its settings)."; once it's back, it prints "The iPhone is connected again." In a test
  with a one-shot `scan`, reconnecting let the run carry on by itself with no further action
  needed. Resuming during a recording hasn't been tested the same way, and either way a
  disconnection that outlasts the run's own time limit still ends it — Ctrl-C and `swipewalk
  doctor` are always the fallback.
- Emulators and the iOS Simulator have their text size changed the same way, but it's a virtual
  device setting, not something anyone's own phone depends on day to day.
- **`scan --appearance both`** switches the device between dark and light appearance the same way
  (Android `cmd uimode night`; iOS Simulator `simctl ui appearance`; a physical iPhone through
  Settings > Appearance) to check both, then restores the original appearance afterwards
  — the same interrupted-run recovery applies. On a physical iPhone left on Automatic (day/night
  scheduling), there's no fixed "current" appearance to switch from, so the check is skipped there
  with a reason.
- **`scan --orientation both`** rotates the device between portrait and landscape the same way
  (Android `settings put system accelerometer_rotation`/`user_rotation`; iOS through the scanning
  harness, the same call for the Simulator and a physical iPhone) to check whether the screen's
  content follows. On Android it restores the device's exact original orientation and
  rotation-lock state afterwards; on iOS there's no way to read the original orientation back, so
  it rotates back to whichever of portrait/landscape the first capture showed — the same
  interrupted-run recovery applies either way. On a physical iPhone, there's no API to read
  Control Center's rotation lock state; a test on iOS 27.0 found the interface still rotated with
  the lock on, but that isn't confirmed on every iOS version — if a screen looks restricted, check
  with the lock off before concluding it's a real restriction.
- **On Android, `--screen-reader` (TalkBack capture; off by default on the command line, on by
  default in the desktop app's "Listen with TalkBack" option)** turns TalkBack on and
  temporarily changes the phone's accessibility settings (which service is enabled, touch
  exploration, and the default text-to-speech engine), by installing a small helper app as the
  device's text-to-speech engine so it can read back exactly what TalkBack says. TalkBack itself
  speaks nothing aloud for the length of the capture. On a physical phone, Swipewalk asks you to
  confirm first (the desktop app asks once per Mac, not before every capture; a non-interactive CLI
  run needs `--screen-reader-confirm` instead of the prompt); an emulator doesn't need to ask.
  The settings are restored once the capture finishes, and a safety timer set on the phone itself
  restores them on its own if Swipewalk stops responding or loses touch with the phone — usually
  within about 90 seconds, longer on a device that doesn't allow exact alarms, and never more than
  about 10 minutes even if the capture keeps re-arming it. The helper app is uninstalled once the
  restore is confirmed, and left in place only if it couldn't be. Like the checks above, a leftover
  from an interrupted capture is restored by the next plain `swipewalk doctor` or scan/record —
  `--screen-reader` isn't needed for that — and by the desktop app's pending-restore banner and
  Devices-page Check; starting another TalkBack capture also restores it first.
- **`scan --auto-update-content`** takes a few further captures of the same screen, spaced
  unevenly with no input (each gap deliberately longer than the last), and reports any gap where
  content (a carousel, ticker, timer or auto-advancing banner) changed on its own — for review
  against WCAG 2.2.2 Pause, Stop, Hide. It never changes the device, so unlike `--appearance
  both`/`--orientation both` it works on a physical phone too; use `--auto-update-interval
  <seconds>` to change how long it waits before the first extra capture (default 3s, on top of
  however long the capture itself takes — later gaps target about 1.6x the one before).

See [section 2](#2-set-up-a-device) and [section 5](#5-record-mode-and-the-large-text-check) for
the full detail on each platform.

## Contents

1. [Install](#1-install)
2. [Set up a device](#2-set-up-a-device)
3. [First scan](#3-first-scan)
   - [Try Swipewalk on the sample app](#try-swipewalk-on-the-sample-app)
4. [Reading a report](#4-reading-a-report)
5. [Record mode and the large-text check](#5-record-mode-and-the-large-text-check)
6. [CI and history](#6-ci-and-history)
7. [The desktop app](#7-the-desktop-app)
8. [Troubleshooting](#8-troubleshooting)
9. [Privacy and reporting wrong findings](#9-privacy-and-reporting-wrong-findings)
10. [Exporting findings](#10-exporting-findings)
11. [Triage: mark a finding as already looked at](#11-triage-mark-a-finding-as-already-looked-at)
12. [Finding the likely source line (.NET MAUI, native Android, native iOS, React Native, Flutter)](#12-finding-the-likely-source-line-net-maui-native-android-native-ios-react-native-flutter)
13. [Sharing a run](#13-sharing-a-run)
14. [Seeing findings in VS Code](#14-seeing-findings-in-vs-code)

## 1. Install

The command-line tool needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) and runs on
macOS, Windows and Linux (the desktop app is macOS only for now — see
[section 7](#7-the-desktop-app)), but which apps you can scan depends on which computer you run it
from:

| Your computer | Apps you can scan |
|---|---|
| macOS | iOS and Android |
| Windows | Android (Windows apps: planned, not yet available) |
| Linux | Android |

iOS scanning needs Xcode, which only runs on a Mac. Windows and Linux support for Android is new
in 0.4: Swipewalk looks for the Android SDK and `adb` in each OS's own well-known default
location, as it does on macOS, but this hasn't been verified on a real Windows or Linux machine
yet. `swipewalk doctor --platform android` tells you exactly where it looked if it can't find
`adb`.

```bash
dotnet tool install -g Swipewalk
swipewalk --version
```

If the install fails because your NuGet configuration lists a private feed that can't be reached or
needs a sign-in, add `--ignore-failed-sources` to the same command.

If the terminal then says `swipewalk` is not found, add .NET's tool folder to your PATH. On macOS
with the default zsh, run this once, then open a new terminal window:

```bash
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.zshrc
```

On Linux with bash, add the line `export PATH="$PATH:$HOME/.dotnet/tools"` to `~/.bashrc` instead.

Swipewalk doesn't include the platform tools it scans through — install the ones for the
platform you're testing:

- **Android:** the [Android SDK platform-tools](https://developer.android.com/tools/releases/platform-tools)
  (`adb`) and either a physical device or an emulator.
- **iOS (macOS only):** Xcode. Swipewalk builds a small scanning harness on the first iOS scan; it
  ships with Swipewalk, so you don't need the app's source.

Once the tools are in place, run `swipewalk doctor --platform android` (or `ios`) — it tells you
what's still missing.

## 2. Set up a device

Use a test device rather than your personal one where you can: some checks (the large-text check,
described below) change device settings temporarily, and reports can contain whatever was on
screen during the scan.

### Android: emulator or phone

An emulator works with no setup beyond the Android SDK. For a physical phone:

1. Enable Developer options (tap the build number seven times in Settings > About phone) and turn
   on USB debugging.
2. Connect the phone over USB and accept the "Allow USB debugging?" prompt on the phone — tick
   "Always allow" so you don't need to repeat this.
3. Run `swipewalk devices` to confirm it's listed, then `swipewalk doctor --platform android` to
   check everything else (screen unlocked, app installed). `swipewalk apps --platform android` lists the
   apps installed on it, so you can copy the package name for `--package`. The app doesn't need to be in front:
   Swipewalk brings it forward itself, or starts it if it isn't running, right before scanning.

### iOS: Simulator or iPhone

The Simulator needs no pairing — boot it from Xcode or `xcrun simctl boot <name>` and it's ready.

For a physical iPhone:

1. Connect it and pair it with the Mac in Xcode (Window > Devices and Simulators) if you haven't
   already.
2. Turn on Developer Mode: Settings > Privacy & Security > Developer Mode, then restart the phone.
3. Apple requires the scanning harness (not your app) to be signed. Swipewalk picks the Apple
   developer team automatically when it can: with only one active Apple Development certificate in
   your keychain, that team is used; with several, pass `--team <id>` once — running `swipewalk scan` from a terminal remembers it for next time (`record` and `session --platform ios` sign with the team you give them too, but only
   for that recording or session). `swipewalk run` (unattended, driven by swipewalk.json), and any command whose
   input is redirected (a script or CI), also sign with the given team, but never replace your
   remembered one. It then signs the harness itself, in this order:
   - with a development provisioning profile you already installed (no Apple ID in Xcode needed);
     use `--profile <uuid|name>` to pick a specific one, or `--harness-bundle-prefix com.company.x`
     to fit a company wildcard profile;
   - otherwise with Xcode automatic signing, which just needs a free Apple ID signed in to Xcode.
   Your app itself is never re-signed — only the harness is.

   In the desktop app this is **iPhone signing** on New scan, shown only while a physical iPhone is
   chosen. **Automatic** (the default) does what the command line does without `--team`; or pick a
   development team from your keychain by name (the menu never shows team ids; a few readiness and error messages still include one). A team you pick for a single-screen scan is
   remembered the same way `swipewalk scan --team` remembers one, shared with the command line (for a recording it is used for that recording only, as with `record`);
   Automatic never changes the remembered team. **More signing options…** holds `--profile` (a
   provisioning profile's name or id) and `--harness-bundle-prefix`. The Devices page shows the same
   team menu for Check while a physical iPhone is listed (team only; a Check never remembers it). The desktop signing choices have not yet been tried on a physical iPhone; they pass the same values as `--team`, `--profile` and `--harness-bundle-prefix`. The iPhone option of the Screen reader session page shows the same menu (team only; the counterpart of `session --platform ios --team`) once a physical iPhone is chosen. A team picked on the session page is used for that session only. With several teams and none chosen or remembered, Start and Check stop and ask you to choose one; with none at all, the readiness checks report the missing certificate and how to fix it.
4. The first scan or recording asks for Face ID or your passcode on the phone, to allow UI
   automation. Approve it; you won't be asked again.

`swipewalk doctor --platform ios --device <udid>` checks all of this and tells you exactly what to
fix if something isn't ready. If the app check can't get a straight answer from the phone (for
example, right after it's been reconnected and is still settling), `doctor` says "couldn't check
whether ... is installed" instead of guessing "not installed" — run it again in a few seconds.

**iOS 27 note:** iOS 27 stops apps at launch if they haven't adopted the UIScene lifecycle, which
some .NET MAUI apps haven't yet. If your app crashes immediately when you try to scan it on iOS 27
(Simulator or device) but runs fine on earlier versions, this is the likely cause — check your
app's scene support before blaming Swipewalk.

The large-text check (see [section 5](#5-record-mode-and-the-large-text-check)) changes the
device's text size and restores it afterwards. On the Simulator this is quick and just changes a
virtual device setting. On a physical iPhone, Swipewalk drives the Settings app itself — opening
Settings, turning on Larger Accessibility Sizes, setting Larger Text to AX3, then returning to your
app — and restores your original setting when it's done (`doctor` or the next scan restores it too,
if a scan was interrupted partway through). It prints a notice before doing this the first time, so
use a test device where you can. Expect it to take a couple of minutes per screen on an iPhone, and
keep the phone unlocked for the whole scan or recording, not just at the start.

### What Swipewalk installs on a device

Scanning needs a few small helper apps of Swipewalk's own on the device. They are not your app and are never
signed over it. Most stay installed after a scan, so the next one starts faster, which is one more reason to
use a test device. Nothing else is installed, apart from a build of your own app that you ask Swipewalk to install with `--install`.

| Device | Helper | Why it is there | When it is installed and removed |
|---|---|---|---|
| Android phone or emulator | Scanning helper, package `org.swipewalk.harness.test` | Reads the accessibility tree and runs Google's Accessibility Test Framework checks on the app in front, and moves TalkBack's focus for a TalkBack capture or session. | Installed by the first scan, recording or session (or by a settings repair, such as `swipewalk doctor`), and replaced when Swipewalk updates it. Stays until you remove it. |
| Android phone or emulator | Text-to-speech helper, package `org.swipewalk.harness.ttsengine`, listed as "Swipewalk (testing only)" in the text-to-speech settings | Only for `--screen-reader` and the screen reader session: it receives what TalkBack says. | Installed for a TalkBack capture and removed again once every changed setting reads back as restored. It is left only when that could not be confirmed. |
| iPhone | Scanning helper app, bundle id `org.swipewalk.harness.runner`, and its test runner app, `org.swipewalk.harness.uitests.xctrunner` | Run Apple's accessibility audit and read the screen; on a physical iPhone they also drive the Settings app for the large-text check. | Installed by the first scan or recording (signed with your Apple development team) and stay until you remove them. If you signed them with `--harness-bundle-prefix`, the ids start with your prefix instead. |
| iOS Simulator | The test runner app, with the standard id (in testing the scanning helper app was not found installed on the Simulator; it is removed too if present) | The same, apart from the Settings app. | The same. |

On Android, if the device holds a copy of a helper from another Swipewalk build, signed with a different key, Android will not update it. Swipewalk then removes that copy (it is Swipewalk's own, not your data), installs its own and says so in the scan's output. It does not remove the text-to-speech helper while the phone still uses it as its speech engine; the message says how to put the setting back. If a copy cannot be removed, the checks that need it are skipped (Google's checks and the screen reader capture for the scanning helper, the screen reader capture for the speech helper) and the report says why. Tried on an Android emulator only, not yet on a physical phone.

Swipewalk also keeps a few small files on your computer (the remembered team and records used to put a device's settings back in `~/.config/swipewalk`, and, once you share a run, a signing key and your trusted keys); see [PRIVACY.md](../PRIVACY.md).

To remove the helpers from a device:

- **Desktop app:** on the Devices page, choose **Remove Swipewalk helpers…** on the device's row (available while the device shows no problem). It asks first,
  saying exactly which apps it will remove, then lists what it removed. For an iPhone you signed with a company
  wildcard profile, enter the prefix you used in **Helper bundle id prefix for removal** first.
- **Command line:** `swipewalk helpers remove --platform android|ios [--device <id>] [--harness-bundle-prefix <prefix>] [--yes]`.
  It prints what it will remove and asks, unless you add `--yes` (a script has no one to ask and needs it).
  `--harness-bundle-prefix` applies to a physical iPhone only.

What happens when you remove them:

- On Android, any TalkBack or text-to-speech setting an interrupted scan left changed is put back first, the same
  way `swipewalk doctor` does, and the scanning helper is stopped first if it was left running by an interrupted
  scan or session. If putting the settings back cannot be confirmed, or the device's text-to-speech engine is still
  set to Swipewalk's helper with no record of the engine it replaced, or Swipewalk cannot read which engine is set,
  or the scanning helper is running with nothing interrupted to repair, nothing is removed and you are told why.
  Fix the setting by hand (TalkBack in Settings > Accessibility, and Text-to-speech output, under Settings >
  Accessibility or Settings > System > Languages on most phones; the place varies by Android version) or run
  `swipewalk doctor --platform android`, then try again.
- Only the helpers in the table are removed. The app you scan, your other apps and your data are left alone, and so
  are the runs saved on your computer.
- A device that is busy, because a scan, recording or screen reader session is running on this computer, is refused
  with the same message a second scan gets. A scan or session running from another computer cannot be seen from here;
  a scanning helper left running with nothing to repair makes the removal stop, as above, but make sure none is
  running first.
- The next scan installs the helpers again; the first scan afterwards takes a little longer, and on an iPhone it
  asks for Face ID or the passcode again if the phone needs it.

To remove them by hand instead: on Android, `adb -s <serial> uninstall org.swipewalk.harness.test` (and
`org.swipewalk.harness.ttsengine` if it is there; first check the text-to-speech engine is your usual one); on an
iPhone, `xcrun devicectl device uninstall app --device <udid> <bundle id>` for each id; on the Simulator,
`xcrun simctl uninstall <udid> <bundle id>`.

This was checked on an Android emulator and an iOS Simulator. Removing the helpers from a physical Android phone and a
physical iPhone is built the same way but has not been tried on those devices yet.

## 3. First scan

### Try Swipewalk on the sample app

If you want to see a scan before pointing Swipewalk at your own app, each
[release](https://github.com/swipewalk/swipewalk/releases) has two downloads of the sample app, BuggyApp:
`Swipewalk-SampleApp-<version>-android.apk` and `Swipewalk-SampleApp-<version>-ios-simulator.zip`. Its
Android package name and iOS bundle id are both `org.swipewalk.buggyapp`. It is a made-up app for a
fictional "City of Exampleville" with accessibility bugs planted on purpose, so a scan of it is expected
to report issues. Its source code is not published for versions from 0.4.2 on.

Download it only from the releases page, and install it only on an emulator, a Simulator or a test phone. The
key that signs the Android build is not secret, so the signature does not show where a copy came from.

- **Android emulator** (a test phone also works): start the emulator, then
  `adb install <path to the .apk>`, and open "BuggyApp" on the device.
- **iOS Simulator** (the download is built for the Simulator only and can't be installed on a physical
  iPhone): unzip the download, boot a Simulator, then run
  `xcrun simctl install booted <path to BuggyApp.app>` and
  `xcrun simctl launch booted org.swipewalk.buggyapp`.

Then scan it like any other app, with the first screen ("Pay a parking ticket") in front:

```bash
swipewalk scan --platform android --package org.swipewalk.buggyapp --out report
swipewalk scan --platform ios --bundle-id org.swipewalk.buggyapp --out report
```

In the desktop app, choose the emulator or Simulator on New scan, then choose `org.swipewalk.buggyapp` as
the app. Add `--large-text` to the commands to include the large-text check too. In the desktop app, the New scan
option "Also check with large system text" does this, and it is on by default.

The planted bugs, and which of them each platform reports, are listed in
[the case study](case-study.md#1-buggyapp-a-sample-net-maui-app-with-planted-bugs). The numbers depend on
the device, the screen height, the system version and the text size, so expect them to be close to the
case study's, not identical. The case study's counts include the large-text check. For example, the last
button on the screen, "Help center", can fall below the screen when the text is enlarged to 200% (it did on
a 1080×2424 emulator), and then the Android scan with the large-text check adds an item for review about content that may be lost at
the larger size.

The sample builds are covered by the [Swipewalk License](../LICENSE), like the rest of the release. Third-party
parts keep their own licences; see [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md). The downloads carry the notices: inside the app, and
beside it in the iOS Simulator zip.

### Scan your own app

Start with a readiness check, then scan:

```bash
swipewalk doctor --platform android      # or --platform ios --bundle-id <id>
swipewalk scan --platform android --out report
```

For iOS, point at the app's bundle identifier:

```bash
swipewalk scan --platform ios --bundle-id com.example.app --out report
```

`scan` checks whatever screen is currently shown on the device or Simulator — open the screen you
want to test first. Swipewalk runs the same pre-flight checks `doctor` does before scanning, and
stops with an explanation if something isn't ready (use `--skip-checks` to skip them, e.g. if
you've already confirmed everything yourself). Pass `--screen "Login"` to name the screen in the
report (default: "Screen 1") — useful when you scan several screens one at a time and want each
report to say which screen it is.

Pass `--appearance both` to also capture and check the screen in the device's other dark/light
appearance — a contrast failure that only shows up in one theme is easy to miss otherwise (see
[docs/case-study.md](case-study.md): the same app's first screen scanned clean on one device in
dark mode but had 5 real contrast failures on another device in light mode). Off by default; on a
physical iPhone this drives Settings > Appearance, and is skipped with a reason if the
iPhone is left on Automatic (day/night scheduling).

Pass `--orientation both` to also rotate the device to the screen's other orientation (portrait ↔
landscape) and check whether the content actually follows — WCAG 1.3.4 Orientation. When it does
rotate, every check runs on that capture too, the same as `--appearance both`. When it doesn't,
the report flags it for review, not as a confirmed failure: WCAG allows a single orientation when
it's essential to a screen (its own examples: a piano keyboard, a bank cheque deposit, slides meant
for a projector or TV, or VR content) — and on a physical iPhone, there's no API to read Control
Center's rotation lock state; a test on iOS 27.0 found the interface still rotated with the lock on,
but that isn't confirmed on every iOS version, so if a screen doesn't visibly rotate, check with
the lock off before concluding it's restricted. Off by default.

Pass `--auto-update-content` to take a few further captures of the screen, spaced unevenly with no
input (each gap deliberately longer than the last — see below), and report ANY gap between captures
where content changed on its own — WCAG 2.2.2 Pause, Stop, Hide. A single observed change is
reported as exactly that: a fact, not a stronger claim — it's real evidence something changed with
no input, but it does not say the content then "settled" (a later capture looking the same again
only shows those two samples matched; content that cycles can look unchanged whenever a capture
lands on the same point in its cycle) or that the change was a one-off "once" event (it may also be
ongoing). Wording scales with what was actually seen: one changed gap says so, and names whether
later captures looked the same again (which doesn't show it stopped), or whether it was the last
capture taken (so nothing after it was observed); two or more changed gaps are worded more
strongly, naming how many of the gaps changed; a value that returns to one seen in an earlier
capture (cycling) is called out as stronger evidence still, since a coincidental exact repeat is
less likely than two unrelated one-off changes. This also means a genuine one-off transition (a
spinner finishing, content arriving from the network, a toast disappearing) can now produce a
finding from a single changed gap, with nothing to show it repeats — this is a heuristic, not a
guarantee either way (see [docs/limitations.md](limitations.md) "auto-update-detection-heuristic"
for what it can still miss or over-flag). This is always for review, never a confirmed failure:
automated checks can tell content changed, not whether anything else is shown alongside it (WCAG
2.2.2 only applies to content shown alongside other content — a preloader that's the only thing on
the page is exempt), whether a pause/stop/hide control exists elsewhere on the screen, or whether
the update is essential to an activity. Off by default — each extra capture costs a full wait (never
reduced by how long the capture itself takes) plus the capture, fast on Android, much slower on iOS
(a full XCUITest harness capture); the report states the real time elapsed, not an assumed one. Use
`--auto-update-interval <seconds>` to change the wait before the FIRST extra capture (default 3s);
later waits target about 1.6x the one before, by default 3 extra captures (4 in total, 3 gaps) —
this was raised from 2 extra captures after a real, evenly-spaced capture sequence sampled a promo
banner that changes message every 2 seconds (3 messages, repeating every 6 seconds) at a near-exact
multiple of its own cycle and reported nothing; a growing, uneven wait makes that less likely.
Unlike `--appearance both`/`--orientation both`, it never changes the device, so it works on a
physical phone too — but, like them, it's scan only for now; record and the desktop app don't offer
it yet.

When it finishes, open `report/report.html` in a browser.

## 4. Reading a report

The header at the top states the screen count, when the report was generated, the Swipewalk
version, and the scanned app's own version — read from the device at capture time (Android's
versionName/versionCode, iOS's CFBundleShortVersionString/CFBundleVersion; Swipewalk doesn't take it
from you or from the build files); shown as "not available" when it couldn't be read, which never
fails the scan. If a recording spans more than one build (for example `record --continue` after a
fix), the header says the version differs by screen and lists each one instead of picking just one.
History's list also shows the version for a run where it's known.

The HTML report opens in one of two views, switchable at the top ("Report view"); the choice is
remembered (in the browser's local storage and the page's URL, when available) so the next report
you open picks up where you left off:

- **What to fix** (the default) — the summary counts, the "Laws and standards" table (shown in both
  views), a one-line reminder that every WCAG 2.2
  criterion still needs a person to check it (how many were only partly checked by automation, have
  no automated check, weren't tested this run, or are usually out of scope for a single app), a
  "Likely same root cause" section (see below) when this run has any, then every screen's findings
  with fix advice — including, for a screen with a real TalkBack or Accessibility Inspector capture
  (`--screen-reader`), a one-line pointer to it in "Full audit detail"; it's evidence to review, not a
  result by itself. For a run with no captured screens (see section 5), the headline says nothing was
  checked instead of showing an issue count. This is the view for someone fixing issues.
- **Full audit detail** — everything else too: the full WCAG 2.2 and Beyond WCAG coverage tables
  (every one of the 55 criteria, each with how to check it by hand), known limitations, the list of rules by check, and the captured and predicted screen-reader tabs.
  This is the view for someone preparing a formal accessibility review.

The switch itself needs no JavaScript (it's a plain pair of radio buttons; a link into a collapsed
section, or opening the report at the `#view-audit` link, reveals "Full audit detail" material even
before any script runs). Every "Full audit detail" table is also collapsed behind a one-line summary
with its counts (for example "WCAG 2.2 coverage: 13 partly checked, 35 need a manual check, 4
usually out of scope, 3 not tested in this run") — click it to expand. Inside those tables, rows
Swipewalk has nothing further to add for (for example "Usually out of scope for a single app", or a
Beyond WCAG clause that doesn't apply to the scanned screens or that Swipewalk can't test at all, such
as one binding the OS/hardware vendor or a support-service requirement) sit behind a "Show N more"
toggle, so the rows that matter are the ones you see first — this never means the underlying
requirement is settled, only that this scan has nothing more to say about it. Nothing is ever removed
from the report or from results.json — both views, and every row, are always in the page; the switch
and the toggles only change what's expanded.

**Likely same root cause.** Several findings often come from one underlying cause — the same
low-contrast color pair used on a dozen labels across several screens, or the same touch-target size
repeated on a set of icon buttons. When Swipewalk can tell this from the capture alone (no source
code involved), "What to fix" shows a section near the top, **"Likely same root cause"**, with one
row per shared cause: "Likely same cause: N findings across M screens", the shared fix advice, and an
expandable list of every instance (which screen, which element) linking to its own evidence further
down the page. It's worded as "likely", not certain — one change may fix several of the listed
findings, but check every instance: some may still need their own change. Grouping is deliberately
conservative: only a few checks have a strong enough, specific signal that the SAME fix genuinely
applies to every instance (matching measured colors for a contrast finding, matching role and
measured size for a touch-target finding, or a matching developer-identifier naming pattern such as
`btnItem1`/`btnItem2`), and two findings on a different platform, of a different kind, or mapped to
different WCAG criteria are never merged even if that signal happens to match. Missing-name findings
(an unlabeled control with no accessible name) are never grouped by size alone, even when several
share a role and size: two unrelated icon buttons — say Search and Settings — can easily be the same
size on the same platform while needing two entirely different names, so grouping them would wrongly
suggest "fix it once". When in doubt, Swipewalk leaves findings separate rather than risk hiding a
distinct problem. Grouping never changes a finding's own WCAG mapping, kind or fix guidance, and it
only affects "What to fix": **"Full audit detail" still lists every finding individually**, with its
own complete evidence, exactly as before. The CSV and Markdown-ticket exports (below) also carry
this: a CSV row gets a `GroupId` column, and a grouped finding's Markdown ticket names the other
findings likely sharing its cause. The PDF export shows the same "Likely same root cause" section.
Because it has no "Full audit detail" view, a grouped finding appears there only as an instance
(screen, finding number, element), without its own message or close-up; the HTML report and
results.json keep both.

If your team [triages](#11-triage-mark-a-finding-as-already-looked-at) every finding in a group, the
group's own row disappears from "Likely same root cause" — each member already shows, individually
and marked, in its own screen's "Triaged by your team" section, so the group row would only repeat
something already handled. Triage only some of a group's findings and the row stays, with an added
note: "Likely same cause: 3 findings across 2 screens (1 of 3 triaged by your team)".

Every finding row — grouped or on its own — also states **who is affected**, in plain language,
under a visible **"Who's affected:"** label, before the WCAG and platform-guideline chips (for
example "People using a screen reader hear nothing useful for this image" or "People with low
vision, or using the app in bright light, may not be able to read this text against its
background"). This is a factual statement about the effect of the issue, never a severity ranking —
findings are never reordered or scored by how many people they might affect.

Each report groups findings into three kinds:

- **WCAG issues** — a possible failure of one or more WCAG 2.2 success criteria.
- **Needs review** — automated checks can't decide on their own; a person should review it against
  the cited criteria. For example, contrast ratios between 3:1 and 4.5:1 need review because text
  size can't always be read from the screenshot. A small touch target lands here instead of as a
  WCAG issue when WCAG 2.5.8's spacing exception doesn't already explain it, but the target looks like
  a plain-text link (no separate button styling) sitting next to other text on the same line: Swipewalk
  can't tell from the accessibility tree whether it's genuinely part of a sentence or run of text (WCAG
  2.5.8's inline exception), so it's flagged for a person to check rather than counted as a WCAG issue
  or waved through — see docs/limitations.md for what this check can and can't tell apart.
- **Platform advisories** — below a platform guideline (Apple Human Interface Guidelines, Android
  accessibility guidance) but not a WCAG failure. Touch-target guidelines are a common example: WCAG 2.5.8
  asks for 24×24 CSS pixels; Apple's Human Interface Guidelines state a default iOS/iPadOS control size
  of 44×44 pt and a separate, smaller stated minimum of 28×28 pt, so a small target can be at or above
  Apple's minimum but still below its default; Android states a single 48×48 dp guideline. A target at
  or above WCAG's 24×24 but below a platform guideline shows up here, kept apart from WCAG issues. Findings
  beyond a WCAG threshold also land here: for instance, iOS's large-text check runs at about 235%
  (accessibility size AX3), well past the 200% WCAG 1.4.4 asks for, so clipping seen only above 200%
  is reported as a platform advisory against Apple's Dynamic Type guidance, not a WCAG issue.
  Every touch-target finding (WCAG issue, needs review or platform advisory) states the target's
  measured size against each threshold that applies on its platform, in the platform's own unit (points
  on iOS, dp on Android) — for example, on iOS: "Measured 36×36 pt. WCAG 2.5.8 (24×24 CSS px): at or
  above. Apple's minimum control size (28×28 pt): at or above. Apple's default control size (44×44 pt):
  below." Each line is a plain "at or above"/"below"
  measurement, never a verdict like "passes" or "meets" — Swipewalk reports what it measured against
  every relevant number and leaves the judgment to you.

A finding without an applicable WCAG criterion is labeled **"No WCAG criterion mapped"** rather
than guessing one.

Also in "Full audit detail" only:

- **Predicted screen reader transcript and swipe order** — what a screen reader is *predicted* to
  announce, built from the accessibility tree in swipe order. This is a prediction, not a
  recording: VoiceOver can't be scripted and doesn't run in the Simulator, so the real screen
  reader was never listening. Compare it with TalkBack or VoiceOver by hand. On Android, when the
  instrumentation harness ran, an element with its own custom accessibility action label (for
  example a Jetpack Compose swipe-to-dismiss action) shows a separate note listing it — not folded
  into the quoted transcript, since Swipewalk doesn't know or guess the exact words a screen reader
  would say for it. Check it on the device.
- **Screen reader (captured)** (with `--screen-reader`, or `--voiceover-captions` on iOS `record` —
  see below) — real evidence next to the predicted
  transcript above; Swipewalk reports every difference for you to check by hand (never as a
  confirmed WCAG failure by itself — the difference could be the app, or Swipewalk's own prediction,
  that's wrong). On iOS, for `scan` and `record`: Swipewalk walks Xcode's Accessibility Inspector on
  your Mac over the macOS Accessibility API instead of turning VoiceOver on — the Inspector reports
  the same accessibility properties (label, value, traits, identifier, hint, class) VoiceOver would
  read, for each element it walks. This is interactive only, and takes extra time per element (a
  large screen can take a minute or more): it asks to use the macOS Accessibility permission (System
  Settings > Privacy & Security > Accessibility) for whichever app is running Swipewalk — this
  permission lets that app, and anything it runs, operate other apps on your Mac; Swipewalk itself
  uses it only to step through the Inspector, and you can turn it off in System Settings any time,
  including right after the run — then for a one-time step per Inspector session it can't do itself:
  opening Accessibility Inspector, choosing your device in its target menu, and clicking the first
  element (for example its title) on the app's screen, so the walk starts from the top. Observed once
  on a physical iPhone, across one screen change: after that click, the Inspector followed the app
  onto a different screen with no more clicking needed, and the next scan captured that new screen in
  full — one data point, not a guarantee for every app; if a walk comes back empty or short after
  navigating, click an element in the Inspector and scan again. If that click landed on the
  app's window or background instead of an element, Swipewalk notices (every captured item comes
  back empty, or far fewer than expected) and reports the screen as not covered, with a prompt to
  click an element and scan again, rather than a false clean pass. Declining, or a non-interactive
  run, falls back to the predicted transcript, and the report records why. When recording, this
  one-time setup step is only asked once per recording session, not once per screen (continuing a
  recording later — `record --continue` — asks again): Swipewalk says so up front, and every later
  "Scan this screen now" reuses the answer silently; if a later screen's own walk comes back short or
  incomplete, that one screen is reported as not covered, with why (usually a prompt to click an
  element in the Inspector and scan again) — declining once doesn't ask again for the rest of that
  session.
  Because the Inspector reports no on-screen position for each element, a
  captured item is matched to the scanned tree by its identifier, then its accessible name, then
  position alone as a last resort — recorded per item in results.json as Exact/Likely/Weak/unmatched,
  so a Weak or unmatched item is a hint to check by hand, not confirmed evidence. Swipe order
  differences aren't reported from this route at all: the walk starts wherever you clicked, not
  necessarily the top of the screen, and the Inspector's own order is circular, so an unanchored walk
  can't be told apart from a real order difference. See the "Accessibility Inspector route..."
  limitation for what this still doesn't cover.
  On Android, with `--screen-reader`: Swipewalk drives TalkBack itself over the screen's focusable
  elements and reads back exactly what it said. It works by temporarily making a small app Swipewalk installs
  (shown in the device's text-to-speech settings as "Swipewalk (testing only)") the device's
  default text-to-speech engine: Google's TalkBack sends its speech to whichever engine is set
  there (seen with TalkBack 16 and 17) — this gets Swipewalk the exact utterance text, entirely
  on-device. **TalkBack speaks nothing aloud for the length of the capture** — don't run this on a
  phone someone is relying on TalkBack with right now. Off by default in the CLI, and adds roughly
  1-2 seconds per focusable element to the capture (a 30-element screen about a minute) — pass `--screen-reader`
  to `scan` or `record` to turn it on (the desktop app's "Listen with TalkBack" option, on by default
  for Android — see [section 7](#7-the-desktop-app)). The accessibility settings this changes (which service is
  enabled, touch exploration, the default text-to-speech engine) are restored three ways:
  automatically when the capture finishes, from a marker file read back at the start of the next
  `--screen-reader` run or by pre-flight if the process was killed first, and by a timer armed on
  the device itself that restores those settings on its own if Swipewalk stops responding or is
  disconnected from the phone. Once a capture's restore is confirmed (every changed setting read
  back), the helper app is uninstalled, which removes the permission Swipewalk granted it. It's
  only left installed when a restore failed or is still pending (the
  safety timer and `doctor`'s own repair both live inside it, so it stays until they've had their
  chance). This means a `record` session that checks several screens reinstalls the app before each
  one. On a physical phone, `--screen-reader` asks you to confirm before changing anything (a
  non-interactive run needs `--screen-reader-confirm` instead of the prompt) — use a test device. The comparison across languages was verified on an
  Android emulator with the system language set to English, Spanish, Hindi, Arabic and Japanese:
  TalkBack said its own role and hint words (for example "Button") in that language every time, but
  did not translate the app's own accessible names in any of them, and the comparison is built to
  match. Needs TalkBack (Android Accessibility Suite) installed on the device. See the "TalkBack
  capture is opt-in..." limitation for what this can get wrong (it doesn't check plain text). When
  this capture completed and matched a control with more than weak confidence, Swipewalk also checks
  WCAG 2.5.3 Label in Name against it: for a control with visible text (its own, or its only
  descendant's), it checks whether the name TalkBack actually said contains that text, and reports
  it for review — never as a confirmed failure by itself, since this shows what TalkBack said, not
  whether speech-input software such as Voice Access would match on it — when it doesn't. This can
  catch a control whose visible text sits only on a child node (a shape the tree-only Label in Name
  check can't see at all) when TalkBack's announcement leaves that text out. Controls that Label in
  Name already reports from the tree (their own text and a different name) aren't reported a second
  time. A real capture of a Jetpack Compose button in NativeAndroid (visible text and an
  overriding name split across two children) found TalkBack's announcement included the visible text
  alongside the overriding name, so nothing was reported there.
- **Screen reader (captured), a person's own VoiceOver session** (with `--voiceover-captions` on
  `record`, or the desktop app's Screen reader session page with the iPhone option; iOS, physical iPhone only) — **tried on a real device on only one screen so far, in
  English.** Unlike `--screen-reader`'s Accessibility Inspector route above, VoiceOver itself IS
  turned on here, but by you, not Swipewalk: you turn on VoiceOver and its Caption Panel (Settings >
  Accessibility > VoiceOver > Caption Panel) yourself and swipe through the screen, while Swipewalk
  polls screenshots of the phone over the cable (no XCUITest session, no extra permission) and reads
  the on-screen caption text with on-device text recognition. Nothing is spoken by Swipewalk itself,
  and nothing leaves your Mac. Per screen you scan while recording, it asks whether to capture (needs
  `--device <udid>` for a physical iPhone — VoiceOver doesn't run in the Simulator); once you say
  yes, swipe through the whole screen with VoiceOver, in one pass from the first element to the last,
  and press Enter when you're done, or wait for the 90-second limit. Each caption is matched to a
  scanned element by a best-effort detection of VoiceOver's own on-screen cursor rectangle, then by
  the caption text naming exactly one element, the same shape TalkBack's and the Inspector's evidence
  use — checked against synthetic test images, and reading a real Caption Panel was tried once on a
  real device (an iPhone SE, BuggyApp's first screen, English); the cursor-rectangle detection itself
  is still unconfirmed against a real VoiceOver cursor. Unlike TalkBack's and the Inspector's
  evidence, a swipe-order difference from this route IS reported — but only reflects a real difference
  if you swiped the whole screen in one pass, start to finish; swiping back and forth, or starting
  somewhere other than the first element, can make the order look different without that being a real
  problem, and the finding says so. Vision's on-device text recognition
  supports a fixed set of languages, not necessarily every language a Caption Panel could show. See
  the "VoiceOver-captions capture..." limitation before relying on this for anything beyond the one
  screen it's been tried against.
- **How the captured tab is laid out** — the "Screen reader (captured)" tab leads with the captured
  items themselves, numbered 1..N in the order shown, each with what was captured (TalkBack's spoken
  text; the Accessibility Inspector's label, value and traits; the VoiceOver caption text Swipewalk
  managed to read). For the Accessibility Inspector,
  since its own walk order is circular and starts wherever you clicked (see above), the report
  rotates this list, for readability only, to start at whichever confidently matched item is matched
  to the element closest to the top of the screen (using Swipewalk's own tree, since the Inspector
  reports no on-screen position of its own), and says so in one line — this is not a claim about
  where VoiceOver itself would start, which isn't captured. With no confidently matched item to start
  from, it says that too and leaves the order as captured. TalkBack's list and the VoiceOver-captions
  list are never rotated — they're shown in the order captured (TalkBack always starts at the first
  focusable element; VoiceOver-captions shows the order the person actually swiped, which reflects
  VoiceOver's navigation order only if they swiped the whole screen in one pass). A secondary
  "Differences from Swipewalk's prediction" section, collapsed by default, drops one kind of
  difference that isn't a real mismatch for any route — an item the capture couldn't match to any
  element at all (already shown, greyed out, in the list above) — and additionally drops a
  stop-number order difference from TalkBack or the Accessibility Inspector specifically: neither of
  those two routes' own capture order is a real navigation order someone actually swiped through:
  TalkBack's is Swipewalk driving TalkBack's focus to each element itself, in the tree's own
  walk order; the Inspector's is circular and unanchored, so its order difference would just report
  where the rotation happened to land. A VoiceOver-captions order difference IS kept and shown here
  as a finding for review (see the VoiceOver-captions bullet above for when it's meaningful). The
  screenshot's swipe-order overlay gets a second
  toggle, "Captured order (TalkBack)", "Captured order (Xcode's Accessibility Inspector)" or "Captured
  order (VoiceOver)" —
  deliberately not called a "swipe order" for TalkBack or the Inspector, since it's this tab's own
  display order there, not a confirmed screen-reader order — numbered from this same (rotated for the
  Inspector only) list, alongside the existing
  predicted-order toggle; an item that couldn't be matched to an element on screen is still listed in
  the tab but isn't drawn on the screenshot.
- **Evidence for WCAG coverage, inside the captured tab** — a screen with `--screen-reader` evidence
  (the VoiceOver-captions route, `--voiceover-captions`, adds no line here yet — see its own bullet
  above)
  also carries an "Evidence for WCAG coverage" section, collapsed by default, at the bottom of its
  "Screen reader (captured)" tab: a "Captured evidence (TalkBack)" or "Captured evidence (Xcode's
  Accessibility Inspector)" line for 4.1.2 Name, Role, Value, 1.1.1 Non-text Content and (TalkBack
  only) 2.5.3 Label in Name, naming the tool and how many elements it reached, compared and found to
  differ. It's never "passed": a count of zero always reads as "0 … flagged for review", the same as
  the automated-findings wording it sits next to — never as a clean result. When one of those
  differences lands on the same element as a finding from a different, tree-only check, it says so
  ("… also reported under 4.1.2; the two may disagree, so check both") instead of leaving two unlinked
  findings. On iOS, 1.3.1 Info and Relationships gets a different kind of line: the Accessibility
  Inspector's own Header trait, on any element it walked, listed by name — this is evidence for the
  existing *manual* check, never an automated finding or a status change, since it can only show what
  the Inspector called a heading, not text that looks like a heading but isn't exposed as one; a
  person still has to look. 2.4.3 Focus Order and 1.3.2 Meaningful Sequence get no line from this
  "Evidence for WCAG coverage" section for any route. For TalkBack and the Accessibility Inspector
  that's because neither one's own "order" is a real navigation order — for TalkBack, Swipewalk
  moves its focus to each element itself in the tree's own walk order, not TalkBack's own swipe order;
  for the Accessibility Inspector, the walk starts wherever you clicked, not necessarily the top of the
  screen, and its order is circular (see the "Accessibility Inspector route..." limitation in
  [docs/limitations.md](limitations.md)). A VoiceOver-captions session's order differences are reported
  a different way instead — as findings for review under these two criteria, with the one-pass caveat
  (see the VoiceOver-captions bullet above) — not as a sentence in this section.
- **Web content in apps (WebViews)** — a screen built with a WebView (an in-app browser view showing
  HTML, as Ionic/Capacitor/Cordova, Blazor Hybrid and plenty of native apps' help/checkout/map screens
  do) is scanned the same way as the rest of the screen: its DOM mirrors into the accessibility tree
  Swipewalk already reads, so every rule (missing-name, target-size, text-contrast and the rest) runs
  on it with no separate step. On iOS, WKWebView content has been seen in the tree Swipewalk reads with
  no extra step, but this has not yet been checked against a WebView test page. On Android, a WebView's content
  only mirrors into the tree once a real accessibility service is registered — on the emulator,
  Swipewalk does this for you automatically (briefly registering TalkBack, restored right after)
  whenever a screen's WebView would otherwise show up empty. On a physical phone this is not done
  automatically, and passing `--screen-reader` does not currently help with it either (that option's own
  TalkBack registration is for its separate transcript capture, which happens after the tree Swipewalk's
  rules read has already been captured) — accept that WebView content on a real Android phone isn't
  scanned by default (see "WebView content needs TalkBack registered, done automatically only on an
  emulator" in [docs/limitations.md](limitations.md)) and test it by hand with TalkBack instead. Pass
  `--web-audit` (Android, debug builds only; `webAudit` in a swipewalk.json run) for a deeper audit of a
  WebView's own DOM: Swipewalk runs axe-core (Deque's engine, bundled — nothing to
  install) directly inside the page over the Chrome DevTools Protocol, entirely on the device, and adds
  its findings the same way Google's Accessibility Test Framework's are added — marked for review, with
  duplicates of Swipewalk's own findings merged rather than shown twice. It's skipped, with a reason,
  when the app isn't a debug/inspectable build or the screen has no WebView. See "The axe-core
  web-content audit (--web-audit) needs a debug build, and can't always place a finding on the report's
  screenshot" in [docs/limitations.md](limitations.md) for what's still not covered (for example a
  general match from axe-core's own flagged element back to a specific spot in the report isn't always
  possible on a busy page — the finding then names the flagged HTML instead). iOS's own WebView content
  can't be audited this deeper way yet: on the Simulator no inspectable WebView target could be reached
  from Swipewalk even with isInspectable set, for a reason not yet established (a physical iPhone was
  not tried; see "The axe-core web-content audit (--web-audit)
  is Android only; not available on iOS yet" in [docs/limitations.md](limitations.md)).
- **Laws and standards** — a "Laws and standards" table near the top of the report (in both views,
  collapsed behind a one-line summary) lists every law and standard the report refers to, grouped as
  US federal, US states, EU and member states, UK and other countries: its name, what it references
  (WCAG version and level), one short line on who it applies to, links to the official source and the
  version-specific WCAG spec, an archived copy of the source (when one exists), the date it was
  checked and how many of this run's findings fall within its WCAG version and level. "All" means
  the five default standards (ADA Title II, Section 508, EN 301 549 v3.2.1 and v4.1.1, UK public
  sector regulations) plus every mapped US state and country in [docs/standards.md](standards.md).
  The table opens with one short line: this is reference information, not legal advice.
  Each issue then carries one line that names the first few, for example "Relevant to ADA
  Title II, Section 508, EN 301 549 and 37 more", and opens (a plain expandable section, reachable
  by keyboard and screen reader) to the full list with a link to each official source and, in words,
  whether it is within that law or standard's WCAG basis; a small "About these mappings" link goes to
  the table. Some criteria fall within fewer of them: a criterion added in WCAG 2.2, such as 2.5.8
  Target Size (Minimum), says so on the issue, for example "Relevant to EN 301 549 v4, UK public
  sector, New York and 3 more that reference WCAG 2.2; not to the 34 that reference an earlier
  version", and the expanded list shows which are and aren't. "Relevant" means the finding's WCAG
  criterion is within the WCAG version and level the law or standard references; it is never a
  statement that the app complies with, or violates, it. Pass `--standard <id>` (a default standard's id, or a US state or country's id from
  docs/standards.md) to a scan or recording, or to `swipewalk export` (below), to show that one
  only, everywhere: the table has just its row, each issue says whether it is relevant to it, and the
  headline counts narrow to it. There is no picker inside the report itself; choose when you scan or
  export. To see the laws you care about first without narrowing anything, choose **laws that matter
  to you** (`--my-laws <ids>`, "myLaws" in swipewalk.json, or the desktop app's **Laws that matter to
  me…** on New scan or the Laws and standards page): each issue then reads, for example, "Relevant to ADA Title II, Texas and N
  more" (N is every other law it is relevant to; a chosen law the issue is outside of is named too, for
  example "; outside the WCAG basis of Texas (it references an earlier WCAG version)"), the opened list starts with your laws, and the laws table lists them first with every other
  law behind "Other laws and standards". Nothing is hidden, only ordered and folded; with none chosen
  (the default) reports look as described above, and a specific `--standard` still means that one only. results.json's own `relevantStandards` field on each finding still lists only the five
  default standards, unchanged; relevance to the others is worked out from the finding's WCAG
  criteria each time a report or export is made. Some researched jurisdictions
  aren't shipped as a mapped standard at all — each is listed as "not mapped" with one short reason:
  it doesn't reference WCAG 2.x, it applies to websites only, it isn't in force yet, its official text
  isn't confirmed yet, or no requirement was found; docs/standards.md lists each one and why. A
  finding can also carry a small "also relevant to"/"related to" note for Apple's or Google Play's own
  accessibility guidance, shown only on the platform it applies to (never a claim about passing store
  review, or that the store itself flagged this specific finding).
- **Known limitations** — in "Full audit detail", collapsed behind a one-line count, the report
  lists the limitations that apply to the scanned platform and
  framework (from [docs/limitations.md](limitations.md)), each with what to check by hand instead.
  Read these; they explain gaps like "headings aren't checked yet" or "contrast is measured from
  screenshot pixels, so anti-aliasing and overlays can affect it". Print this page yourself with
  `swipewalk limitations` (`--platform`/`--framework` narrow it to what applies to your app).
- **How the large-text check was done** — under the enlarged screenshot, the report notes the
  method: "system setting" (the platform's own text-size setting, Android font scale or, on iOS, the
  Settings app) or, on a physical iPhone where that couldn't be driven, "per-app launch setting"; and
  whether it took effect while the app kept running or only "after a restart" (see
  [section 5](#5-record-mode-and-the-large-text-check)).
- **Large-text: why a screen wasn't captured** — if a screen's large-text check didn't run, the
  report says why: the app showed a different screen at the larger size, the app never came back to
  the front at the larger size, or — on a physical iPhone only — the scanning harness couldn't be
  signed to change the phone's text size, or driving Settings failed and the per-app fallback also
  failed.
- **The other appearance** (with `--appearance both`) — the report shows the screenshot from the
  other dark/light appearance alongside the normal one, and labels each finding "Found in both
  appearances" or "Only in dark/light appearance" so a theme-only issue isn't confused with one
  that always shows. The kind and WCAG mapping a finding gets doesn't depend on which appearance it
  came from — a contrast failure is a WCAG issue in either theme, since a user can pick either one;
  a finding that measures as "needs review" in one appearance and a clear failure in the other is
  kept as two separate, correctly-labeled findings rather than one that hides the worse result. If
  the second capture looks the same as the first (checked from the screenshot and the accessibility
  tree, not just the theme setting), the report says so instead of labeling findings by appearance
  at all — an app may force one theme regardless of the system setting, use fixed colors instead of
  theme-aware ones, or only read the theme at launch, and a scan from the outside can't tell which.
  Large-text and captured-screen-reader findings are never labeled by appearance either, since the
  second capture doesn't repeat those checks.
- **The other orientation** (with `--orientation both`) — when the device rotated (checked from the
  screenshot's shape, or, without a usable screenshot, the accessibility tree's, not just the
  rotation setting), the report shows the screenshot from the other orientation alongside the
  normal one and labels each finding "Found in both orientations" or "Only in portrait/landscape",
  the same as appearance. When it did *not* rotate, the report instead adds one "Needs review"
  finding for the screen, citing WCAG 1.3.4 Orientation: check by hand whether a single orientation
  is essential here (WCAG's own examples: a piano keyboard, a bank cheque deposit, slides meant for
  a projector or TV, or VR content), or whether the screen is simply locked to one orientation with
  no reason to be. On a physical iPhone, that same finding adds one sentence naming Control
  Center's rotation lock: the rotation is a simulated sensor event, and Swipewalk has no way to
  read the lock's own state; one test found the interface still rotated with the lock on (iOS
  27.0), but that isn't confirmed on every iOS version (see [docs/limitations.md](limitations.md)).
  If either capture
  lacks usable evidence (no screenshot and no usable bounds), the check is reported as not done
  rather than guessed. Large-text and captured-screen-reader findings are never
  labeled by orientation either, since the second capture doesn't repeat those checks.
- **Content that changed on its own** (with `--auto-update-content`) — when ANY gap between
  captures showed a change, the report adds one "Needs review" finding for the screen, citing WCAG
  2.2.2 Pause, Stop, Hide, naming what changed (a few examples, not everything that did), which
  captures and how far apart they really were, alongside a before/after screenshot. The wording
  states only what was observed: a single changed gap is worded as a single observed change (and
  says whether later captures looked the same again, which doesn't show it stopped, or whether that
  was the last capture taken, so nothing after it was observed — it never claims the content
  "settled" or that the change was a one-off "once" event, since neither is known from that alone);
  two or more changed gaps are worded more strongly, naming how many of the gaps changed; a value
  returning to one seen earlier (cycling) is called out separately as stronger evidence. Check by
  hand whether anything else is shown alongside the changing content (WCAG 2.2.2 only applies then),
  whether a pause/stop/hide control exists somewhere on the screen, and whether the update is
  essential to an activity — though the W3C Understanding document's own examples of essential
  content (an explanatory animation, a stock ticker) still ship with pause/restart buttons, so
  essential alone doesn't rule out needing a control.
- **Lost navigation place (Android)** — on Android, when the very first attempt at the larger text
  size showed a different screen (typically the app's first) instead of the one being checked, the
  report also adds a platform advisory on that screen (the one where Swipewalk actually saw it
  happen, not any screen after): the person likely lost their place and has to navigate back. Not a
  WCAG issue (1.4.4 doesn't require an app to keep its navigation state across a text-size change);
  worded "probably", since the detection compares screens by title and element names, which can
  occasionally read a truncated or crowded screen as "different" when it isn't.
- **Content outside the screen at normal text size (iOS)** — every iOS scan checks whether
  interactive controls or text sit wholly or mostly outside the visible screen at the app's normal
  (not enlarged) text size, with none of the containers holding them reporting itself as scrollable.
  This catches a layout that doesn't fit on a small enough device even before any text is enlarged —
  first noticed by eye on a physical iPhone SE, on a screen whose content has no ScrollView. Xcode's
  Accessibility Inspector usually still lists an element positioned past the
  screen edge, and a screen reader may still reach it by swiping, so a screen-reader-only check can
  miss that a sighted or touch user cannot reach it at all. Reported for review against WCAG 1.4.10
  Reflow when the screen is at least as large as 1.4.10's own 320×256 reference size (every phone
  screen is), or as a platform advisory on a smaller screen; either way, never as a confirmed
  failure — it can't rule out the element being reachable another way. Not yet run on Android:
  Android's own accessibility dump clips a node's reported position to the visible screen, so there
  is nothing off-screen left to measure there (see the "Off-screen content is not detected on
  Android" limitation).

A report with no findings is not a statement of conformance — it means the checks that ran found
nothing to flag. Most WCAG success criteria have no automated check at all yet; see the "Automated
checks cover only part of WCAG" limitation.

Three reference pages that ship with Swipewalk describe what it checks and doesn't, and how; each is
also printable from the CLI so it's always current with the version you have installed. In the desktop
app, **Help > Automated Checks** and **Help > Known Limitations** show the first two in a window (on macOS 27 the system replaces the Help menu and Swipewalk adds these items to it; a UI test on macOS 27.0.1 found them), from the
same lists (the limitations can be filtered by platform and app framework, as `--platform` and
`--framework` do):

- `swipewalk checks` — every automated check Swipewalk runs (its own rules, plus the checks it reads
  from Apple's accessibility audit and Google's Accessibility Test Framework), what each looks for,
  whether it's a WCAG issue, needs review, or a platform advisory, and which WCAG criteria or
  platform guideline it cites (see [docs/checks.md](checks.md)).
- `swipewalk limitations` — what Swipewalk cannot check or may get wrong (see
  [docs/limitations.md](limitations.md), also linked above).
- `swipewalk standards` — the laws and standards each finding is marked as relevant to, and the
  sources those mappings were checked against (see [docs/standards.md](standards.md)).

### Framework detection

Each screen's heading names the app's own platform and, when Swipewalk could tell, the UI framework
it's built with — for example "Pixel 4a · Jetpack Compose" or "iPhone 16 · likely Flutter". A detection
Swipewalk isn't fully confident in is always spelled "likely" right there in the heading, not just in
finer print below — so the single most visible place a framework name appears never reads as certain
for a generic bucket, a webview-filling guess, or a signal not yet checked against a real device
capture. Underneath, when there's evidence to show, a line reads "Framework: X (detected from ...)" —
what the detection rests on (a class name, a bundle file, an AutomationId, or a count of native
elements), so you can judge it yourself rather than just take Swipewalk's word for it. Detected
frameworks: .NET MAUI, native Android Views, Jetpack Compose (JetBrains' Compose Multiplatform reports
the same way — Swipewalk can't tell it apart from plain Compose), Flutter, React Native, and a WebView
filling virtually the whole screen (for example Ionic, Capacitor or Cordova; a native or .NET MAUI
Blazor Hybrid app showing one full-screen web page looks the same this way, so they all share one
"Hybrid web" label). A screen mixing two — commonly generic Android Views alongside a Jetpack Compose
region on the same screen — names the larger one first and the other as "(and ...)".

Detection never changes what's scanned or how a finding is judged; it only selects fix examples,
framework notes, (for .NET MAUI specifically) which likely cause a text-size finding names (see
[docs/limitations.md](limitations.md)), and, with `--source` (section 12), which project-source mapper
runs — a MAUI screen (on either platform) is matched against XAML/C#, an Android screen detected as native
Views/Jetpack Compose or not detected at all is matched against native Android source, an iOS screen
detected as UIKit or SwiftUI, or not detected at all, is matched against native iOS source, and a React
Native or Flutter screen, on either platform, is matched against its own source regardless of what else
was detected there (a hybrid-web screen matches none of them). Checks with fix guidance give an example
or likely causes written for .NET MAUI, native Android Views, Jetpack Compose, UIKit, SwiftUI, Flutter,
React Native and a WebView-filling hybrid-web app, shown once a screen's framework is named — by detection
(Confirmed, or Likely and labelled "if this app uses X") or by `--framework`; a screen whose framework
stays Unknown still gets the platform's combined native example, and so does a check that has no example for the named framework; either way it is labelled "Generic example". Each is written from official
documentation (the framework's own, web standards for hybrid-web content, or the library a fix
relies on); the examples for Android Views, Jetpack Compose, UIKit, SwiftUI, Flutter, React Native
and hybrid web have not yet been applied in a real app of that framework and rescanned; see
[docs/limitations.md](limitations.md). Since UIKit/SwiftUI can't be told apart on iOS (see below), pass
`--framework uikit` or `--framework swiftui` yourself if you want the report and fix examples to name the
right one; `--source` mapping itself mostly doesn't need that distinction — it runs for either, though the
merged-SwiftUI-group step (section 12) is skipped on a screen marked UIKit. Real gaps, not guesses: the
tree Swipewalk
captures through Apple's XCUITest has no class-name field at all — only a coarse element type, an
identifier and a label — so UIKit and SwiftUI cannot be told apart on iOS, and Flutter/React Native
there are detected only from the installed app bundle, on the Simulator (best-effort: it can fail to
read the bundle and stay Unknown) or on a physical iPhone only when you installed the app yourself with
`--install`; those two iOS bundle signals, and Flutter/React Native's own Android class names, haven't
yet been checked against a real Flutter/React Native build in this project's own testing, so they show
as "likely", not confirmed (see [docs/limitations.md](limitations.md) "framework-detection-partial" for
the full list of gaps). Pass `--framework <name>` when you know the app's framework and want its own
advice, or to override a wrong detection: `maui`, `androidviews`, `jetpackcompose`, `uikit`, `swiftui`,
`flutter`, `reactnative` or `hybridweb` (in the desktop app, the App framework menu under More checks on New scan does the same).
results.json carries the same detail as `framework`, `secondaryFramework`, `frameworkConfidence` and
`frameworkEvidence` on every screen, for anything built on top of it.

### WCAG 2.2 coverage

Every report includes a "WCAG 2.2 A/AA coverage" table listing all 55 WCAG 2.2 Level A and AA
success criteria — not just the ones this scan happened to flag — so a report never leaves a
criterion unaccounted for. It lives in the "Full audit detail" view (see above), collapsed behind a
one-line count summary, with "Usually out of scope for a single app" rows (below) split into their
own "Show N more" toggle, since WCAG2ICT's own note is a "usually", not a certainty — confirming that
is still one thing worth doing, just not something this scan can decide for you. Each row shows what
this run did about that criterion, not whether the app meets it:

- **Not tested in this run** — a check exists for this criterion and could have run on the
  platform(s) this run scanned, but didn't run this time (for example, `--orientation both` wasn't
  used, so the orientation rescan for 1.3.4 Orientation couldn't run; or an Android run's
  instrumentation harness didn't produce a result, so the page-titled check couldn't run). Shown
  first, since it's the gap most worth fixing by rescanning.
- **Partly checked by automation** — at least one rule ran on at least one scanned screen and
  reports the issues and items needing review it found (or "0 automated findings" if it found
  nothing — that means the automated part of the check ran and found nothing, not that the
  criterion is met).
- **Needs a manual check** — no Swipewalk rule checks this criterion on the platform(s) this
  run scanned, so the table gives a concrete step for checking it with TalkBack or VoiceOver. This
  covers criteria no rule maps to at all, and criteria whose only automated check runs on a
  different platform (for example, on an iOS run, 2.4.2 Page Titled, whose only check needs the
  Android instrumentation harness; on an Android run, 1.4.10 Reflow, whose only check runs on iOS).
  The status never lists such a check as a gap in this run; the how-to text may still say what it
  does on the other platform.
- **Usually out of scope for a single app** — WCAG2ICT applies this criterion to non-web software
  only across a "set of software programs" (separate programs from the same author, distributed
  together and interlinked), which it notes is rare; confirm your app isn't one before assuming it
  doesn't apply.

The table links each criterion to its source (the W3C "Understanding" page, or the WCAG2ICT
"Applying SC ... to non-web software" page) and, for record mode, shows how many of the scanned
screens each check actually ran on.

### Beyond WCAG

Section 508 and EN 301 549 also add requirements that go beyond what they reference from WCAG (for
example assistive-technology interoperability, following the platform's user preferences, or
requirements that only apply to apps with video or voice calling). Every report includes a "Beyond
WCAG" section — also in "Full audit detail", collapsed behind a one-line summary of how many clauses
need a check versus don't apply or aren't testable by Swipewalk — with one table per standard that
has any such clauses, listing each one with a status in the same vocabulary as the WCAG 2.2 coverage
table above. Within each standard's table, "Not testable by Swipewalk" and "Not applicable here"
clauses are split into their own "Show N more" table, the same way the WCAG 2.2 coverage table splits
off "Usually out of scope":

- **Partly checked by automation** — an existing Swipewalk signal (for example the large-text or
  dark/light rescan, or the accessible names already read for WCAG 4.1.2) covers part of it. This
  status carries no finding count of its own — any findings stay under the WCAG criterion they were
  found against.
- **Needs a guided check** — 1-3 plain steps for a person to follow.
- **Not testable by Swipewalk** — documentation, support services or platform/OS or hardware
  behaviour, out of scope for a running-app scan.
- **Not tested** — either the clause only applies when the app has a specific feature (voice calling,
  video, biometric sign-in, content authoring) and Swipewalk does not yet detect whether the scanned
  app has it, so it never assumes the clause doesn't apply; or it's an always-applicable clause whose
  evidence (the large-text or dark/light rescan) didn't run on any scanned screen this time, in which
  case the report names what to run (`--large-text` or `--appearance both`).

Pass `--standard <id>` to narrow the report's "Beyond WCAG" section (and the rest of the report) to
one standard; the section is simply absent when that standard has no beyond-WCAG clauses (ADA Title
II, the UK regulations). Without `--standard`, every standard that has beyond-WCAG clauses is shown.
See [docs/standards.md](standards.md) for the full clause list.

## 5. Record mode and the large-text check

`scan` only checks the one screen visible when you run it. `record` watches while you use the app;
by default nothing is captured until you ask for it — press Enter (or, in the desktop app, "Scan
this screen now") to scan the screen you're currently on, and `q` ("Finish") to stop and write the
report. Pass `--auto` to also scan every new screen automatically as you navigate, the way earlier
versions always did:

```bash
swipewalk record --platform android --expect "Login,Home,Settings" --out report
swipewalk record --platform ios --bundle-id com.example.app --out report
swipewalk record --platform android --auto --out report   # scan every new screen automatically too
swipewalk record --platform ios --bundle-id com.example.app --device <udid> --voiceover-captions --out report
                                            # physical iPhone only -- see "Screen reader (captured), a
                                            # person's own VoiceOver session" above; tried on one real screen only, in English
```

`--expect "Login,Home,Settings"` names the screens you meant to cover; any that weren't scanned are
listed in the report as missing, so you know what to test by hand.

On a physical iPhone, iOS shows its own "Automation Running" banner while Swipewalk controls the
app. By default (manual capture) Swipewalk starts automation only for each step, such as a capture
or a text-size change, and stops it right after, so the banner isn't shown while you navigate
between screens. Each capture takes a few seconds longer as a result. `--auto` checks the screen
every second or two to notice changes, so it keeps automation running and the banner stays up for
the whole recording. `--auto` never captures a screen while the platform's own loading spinner or
progress bar is showing, however stable the rest of the screen looks, and says so once if it's still
showing after about 30 seconds -- any progress bar counts, even one that just shows a fixed amount (a
step indicator, a storage bar), so press "Scan this screen now" (Enter) on a screen like that. A screen
whose content keeps rewriting itself once loaded (a rotating banner, a clock showing seconds) is still
captured -- once its controls have stayed the same for about 6 seconds -- rather than never being
captured at all; the report notes when a capture happened this way. A screen that shows it's loading
only as plain text, a skeleton layout, or a custom-drawn spinner (not the platform's own progress-bar
control) isn't recognized as loading, so press "Scan this screen now" (Enter) once it has actually
finished, rather than relying on it being caught automatically.

If a recording stops before you choose Finish (an error, a cancellation, a disconnect, or Swipewalk
itself crashing), the screens captured so far are kept and saved as they're captured -- not just at
the end -- so the run always shows up in History with whatever it got to. For an error or a
cancellation, Swipewalk gets to say so right away: the report and the run say it ended early, and
`record` exits with 2. If Swipewalk itself stops running mid-recording (it's force-quit, it crashes,
or the computer loses power), it can't write that note at the time. Instead, a run is marked
"recording in progress" while it's being recorded (written before the first screen, and again after
each one). The next time History, `swipewalk history` or `swipewalk record --continue` reads it and
finds the Swipewalk process that was recording it is gone, it lists the run as ended early and offers
to continue it. The run's own report doesn't say it ended early in this case -- it just shows the
screens captured up to the interruption; History is where you find out. If it stops before capturing
any screen at all (for example the iOS test harness couldn't be installed), the report's headline says
"No screens were captured in this run, so nothing was checked" instead of an issue count, with no
summary tiles; the run is still saved to History as ended early (not "in progress"), and the desktop
app stays on New scan rather than opening that report. Either way, screens after the
interruption were not scanned; scan or test them manually, or resume the recording:

```bash
swipewalk record --continue <run>   # <run> is a run folder or id, e.g. from `swipewalk history`
```

A run still being recorded right now (here, or in another window) shows in `swipewalk history` as
"recording in progress" and can't be continued until that recording stops.

This appends new screens to the same run instead of starting a new one -- `--platform` and the
package/bundle id come from the run itself, so you don't need to pass them again unless you want to
override one (a different `--device`, for example); `--out` is ignored, since it always writes back
into that run's own folder. If Swipewalk recognises a screen you scan as one already in the run
(matched by title and most of its elements -- not a guaranteed exact match: two different screens
that happen to share a title and most labels could match too), the newer capture replaces the
earlier one there and then -- only within this run; it never touches or merges with any other saved
run -- so the screen's findings aren't counted twice, and the report still notes that it was scanned
again and when. A rescan keeps the earlier screen-reader evidence unless it brings its own: if the newer capture has none of its own (captions, TalkBack speech or an Accessibility Inspector walk), the earlier evidence is kept for that screen in its own "Screen reader (earlier capture)" tab under "Full audit detail", with when it was captured. It is listed as it was and is not compared with the newer capture's findings, since the screen may have changed; if the newer capture has its own, that one replaces the earlier. The report says the run was recorded across more than one session, with each
session's start and end times. Continuing isn't offered for a run that finished normally (you chose
Finish, or `q` on the CLI): record a new one instead. If an earlier session's resume state wasn't
saved (an older run from before this existed, or a damaged file), continuing starts fresh on the
large-text-restart behaviour and says so, and that session's screens won't be recognised if you
rescan them.

`record` also captures each screen a second time at the platform's large system text size, by
default (pass `--large-text false` to skip it; `scan` doesn't do this unless you pass
`--large-text`). This partly checks WCAG 1.4.4 Resize Text (a manual check at 200% is still
needed): Android is set to font scale 2.0, exactly the 200% 1.4.4 asks for; iOS is set to
accessibility size AX3 (about 235%), deliberately past 200% — problems seen only above 200% are
reported as platform advisories against Apple's Dynamic Type guidance, not WCAG issues, and text
that doesn't grow at all stays a WCAG 1.4.4 item (needs review) either way, since it implies the
text can't reach 200% regardless of which level was tested.

While the large-text check runs, Swipewalk changes the device's text-size setting, captures the
screen, and restores the original size afterwards. On emulators and the iOS Simulator this is a
virtual device setting; on a physical Android phone it changes the phone's real text size for a few
seconds. On a physical iPhone, Swipewalk opens the Settings app, turns on Larger
Accessibility Sizes and sets Larger Text to AX3, comes back to your app, captures the screen, and
restores the original Settings state afterwards — printing a notice first, since this changes a
setting on your own phone; use a test device where you can. It takes a couple of minutes per screen
on an iPhone, and the phone needs to stay unlocked throughout. If driving Settings itself fails (for
example because a new iOS version changed where the setting lives), Swipewalk falls back to a
per-app text-size launch setting instead, which only affects the app under test; if that fails too,
it skips the large-text check for that screen with a reason. Either way the scan itself never fails
because of the large-text check.

If a scan is interrupted mid-check (e.g. Ctrl+C at the wrong moment, or the iPhone locks), the next
`scan`, `record` or `doctor` run on that device notices its text size (or, on a physical iPhone, its
Settings state) was left changed and restores it for you.

On iOS and Android, if the text doesn't visibly change size while the app keeps running, checking
further needs the app restarted. Text that only grows after a restart is reported as a platform
advisory rather than a WCAG issue, because WCAG 1.4.4 doesn't require the resize to apply without
restarting the app (Apple's Dynamic Type guidance on iOS; Android's documentation on handling
configuration changes on Android) — for .NET MAUI apps on iOS the finding cites the known issue
fixed in Microsoft.Maui.Controls 10.0.100 (dotnet/maui#34445). Text that never grows at the
system's larger sizes can be a WCAG 1.4.4 issue instead; the automated check reports it and a
person confirms it.

App developers can make this smoother for people using the app. On iOS, .NET MAUI 10.0.100 and
later applies Dynamic Type to standard controls while the app runs, so no restart is needed. On
Android the activity still restarts on a text-size change: leave `ConfigChanges.FontScale` out of
MainActivity (declaring it keeps the same activity, but .NET MAUI then does not re-apply font
sizes) and restore the page that was showing when the activity restarts. In our test with a
NavigationPage app this kept the same screen after a text-size change, with a brief flash of the
first page; Shell apps were not tested, and only the page is restored, not typed text, scroll
position or the pages below it. The fix advice for text that only changes size after a restart
shows the shape of this pattern and its limits.

`scan` checks one screen, with nobody there to navigate anywhere, so it asks a shorter question than
`record`'s: "The text didn't change size while the app kept running; checking it at the larger size
means restarting the app. Check "Login" at the larger size? Swipewalk will restart the app and try
again. If the text grows after the restart, the report explains how the app's developers can avoid
this step." Choosing "check anyway" restarts the app and re-captures the same screen on its own;
choosing "don't check" records "you chose not to check this screen at the larger size". If the
restarted app comes back on a different screen, `scan` can't recover on its own (there's nobody to
navigate back) — Swipewalk's console/log output points you at `record` instead, which lets you
navigate back after the restart. Running interactively (a terminal, or the desktop app) asks by
default; a non-interactive `scan` (redirected input, or an unattended `swipewalk run`) keeps the
automatic restart it always did, unless you set `"largeTextRestart": "never"` (then the report says
the scan was set not to check that screen) — pass `--large-text-restart always` or
`--large-text-restart never` to decide up front instead of being asked.

`record` never restarts the app on its own — that would interrupt your navigation and lose your
place, since nothing here can navigate for you. The first time a screen shows that checking the
larger size needs a restart (the text just doesn't change while the app keeps running, or — on some
Android apps — the app goes back to its first screen the moment the setting changes), Swipewalk asks
whether to check it anyway, right after that live attempt. From then on it already knows the cost,
so every later screen is asked *before* the size is touched at all, e.g. "On an earlier screen, this
app went back to its first screen when the text size changed. Check "Settings" at the larger size?
You'll need to navigate back twice. If the text grows after the restart, the report explains how the
app's developers can avoid this step." (or, when text just doesn't change: "On an earlier screen, the
text didn't change size while the app kept
running; checking this one at the larger size means restarting the app."). The prompt itself only
states what was observed, never a diagnosis. Checking anyway takes two extra navigations: once back
to the screen once the size is enlarged (nothing is captured until you press "Scan this screen now"
again), and once more to wherever you scan next once the size is restored afterward. If you choose
not to check a screen, the report says "you chose not to check this screen at the larger size".
"Always check" or "never check" applies the answer to the rest of the recording, and can be changed
again at any time — press `l` in the CLI, or use the picker next to "Scan this screen now" in the
desktop app — it's never a one-way door. Pass `--large-text-restart always` or `--large-text-restart
never` to decide up front instead of being asked; a non-interactive run (redirected input, or
`swipewalk run` unless you set `"largeTextRestart": "ask"` in `swipewalk.json`) defaults to `never`,
so instead of "you chose", the report says the recording was set not to check those screens. Either
way, the report and results.json list, per screen, whether it was checked at the larger size and, if
not, why — and the WCAG 2.2 coverage section only counts 1.4.4 as checked on the screens it actually
ran on.

`record`'s "check anyway" (on Android, the iOS Simulator, or a physical iPhone) always reads the
device's current text size before changing anything, so it can be put back exactly afterward. If that
read itself fails (for example, on a physical iPhone, the Settings app's layout changed in a new iOS
version), nothing is changed and the report says "could not read this device's current text size, so
nothing was changed" for that one screen, and the recording continues. If a later step in the same
check fails — applying the larger size, or the relaunch that follows it — the device may already be
partly changed; Swipewalk puts the original size back as a best effort before reporting "the check at
the larger text size could not be set up, so this screen wasn't checked at the larger size". On
Android and the Simulator, a best-effort restore that itself fails is reported too; on a physical
iPhone it isn't yet, but the device keeps a record of its original size either way, and the next
`swipewalk doctor` or scan puts it back. Scan mode's own physical-iPhone flow doesn't show either
message: if it can't drive Settings, it falls back to a per-app launch setting instead (see the
large-text method note above).

If any screens weren't checked when you press Finish, `record` offers to keep recording instead of
writing the report right away — an opt-in, declinable offer; saying yes just clears Finish so you
can navigate back to those screens yourself and scan them again, nothing is captured automatically.
Saying no (or a non-interactive run) writes the report as usual.

### Live screen-reader session (`swipewalk session`)

Unlike `scan`/`record` (Swipewalk driving the screen reader itself for a bounded, silent walk),
`swipewalk session` lets you use TalkBack on the phone exactly as you normally would, for as long as
you like, while Swipewalk passively records what happens: which controls TalkBack's focus reached, in
what order, what TalkBack said, and what you activated. `swipewalk session --platform android` is this live session; `swipewalk session --platform ios` and the desktop app's iPhone (VoiceOver) option are a different, per-screen design (see [iPhone VoiceOver session](#iphone-voiceover-session-swipewalk-session---platform-ios) below).

```
swipewalk session --platform android --package <app package> [--device <id>] [--history <dir>]
                  [--app-name <name>] [--app-version <v>] [--confirm]
```

`--package` is required (the app to record against, already in front on the device); `--device` picks
the Android device or emulator when more than one is connected; `--app-name`/`--app-version` label the
saved run the same way they label an ACR export (see [section 10](#10-exporting-findings)); `--history
<dir>` saves the run elsewhere instead of your real History (useful for testing). On a physical phone,
Swipewalk asks to confirm before installing its helper app and changing accessibility settings for the
session, the same way `scan --screen-reader`/`record --screen-reader` do — `--confirm` skips the
prompt for a non-interactive run. The session uses only the device you name (or the only one connected): with several
connected and no `--device`, it stops and asks you to name one. Before it changes anything it checks that device (still
connected, Google's TalkBack installed, the app installed); if the device can't run a session, for example an emulator image
without TalkBack, it says which device and why, changes nothing and does not try another device. If the helper doesn't confirm within 30 seconds that the session started, Swipewalk stops it, asks the device to put its settings back and says so, rather than showing an empty session. While the app is in front, everything TalkBack says is recorded,
including a notification it reads out, text-field contents and typed characters — use test data, never real passwords
or personal details.

Once the session starts, type a command and press Enter:

- `n` or `next`, `p` or `previous` — move TalkBack's focus in Swipewalk's own order (not TalkBack's
  swipe order), the same convenience buttons the desktop app's Screen reader session page offers.
  `a` or `activate` — presses the focused control directly. The report always keeps a move made this
  way apart from your own gesture.
- `note <text>` — saves a note, automatically capturing the current screen, the last-focused control
  and the last few things that happened; you only type the observation.
- `sound` — plays a short test phrase on the phone and asks whether you heard it (during a session Swipewalk plays
  TalkBack's speech through the phone's media volume, so a low media volume means silence). Answer no and it says to turn up the
  phone's media volume. This is only a check: it never blocks anything, and Swipewalk records what
  TalkBack says whether or not you can hear it.
- `stop` (or closing stdin, Ctrl-D) — ends the session, puts the phone's accessibility settings back
  and asks the phone to remove the helper app, prints a one-line recap (screens visited, controls
  reached and not reached, notes, evidence items to review — counts of what was recorded, not a result;
  the same counts also appear in the status line printed after each command),
  and saves the session to History as its own kind of run,
  alongside your scans and recordings. Its report is laid out like a scan's report: a short
  "Screen reader session (recorded live)" header (who, on what device, for how long, whether it ended
  early, and any notes not tied to a screen), then every screen the session visited, drawn the same way as a scanned screen, with the same
  "What to fix" and "Full audit detail" views: the screenshot with numbered outlines, the findings with who
  is affected, the WCAG criteria and fix advice, and — under "Screen reader (captured)" in Full audit
  detail — what TalkBack said for each control beside what Swipewalk predicted, and which controls were
  not reached. Any evidence found where what TalkBack said differs from what Swipewalk predicted or
  doesn't include the control's visible text is one of that screen's findings. Your notes appear on the
  screen you added them on. It's its own kind of run, never merged into an existing scan or recording of
  the same app — at most the session's first screen (the one already open when the session starts) gets a
  matched capture, so only that screen can have Swipewalk's own automated checks and a predicted transcript to
  compare against; a screen you navigate to later in the session is shown the same way but says plainly
  that the automated checks did not run on it, and has no 4.1.2/1.1.1 comparison (2.5.3 Label in Name
  evidence is not limited to the first screen).

If you leave the app, Swipewalk stops recording until you return to it or type `stop`, and says so the
next time it prints status.

### iPhone VoiceOver session (`swipewalk session --platform ios`)

```
swipewalk session --platform ios --bundle-id <id> [--device <udid>] [--history <dir>]
                  [--app-name <name>] [--app-version <v>] [--team <id>] [--confirm]
```

The command-line version of the desktop app's iPhone (VoiceOver) option, using the same code. It has not itself been run on a device yet; the desktop option is what has been tried. It needs a physical iPhone
connected by cable (VoiceOver doesn't run in the Simulator); `--device` picks the iPhone when more than one is connected.
Swipewalk runs the same pre-flight as a scan and asks you to confirm that it will read VoiceOver's captions, including
anything you type (`--confirm` answers for a non-interactive run). The app name and version default to the bundle id and the
version read from the iPhone, as in the desktop app. `--team <id>` names the Apple development team that signs the scanning
helper, as for `scan` ([Setting up an iPhone](#ios-simulator-or-iphone)); it is used for that session only and is not remembered. Swipewalk doesn't turn VoiceOver on or drive it: you do. It isn't the
Android session's shape: nothing follows VoiceOver between screens or lists controls you didn't reach. For each screen you
choose, the terminal shows the step you're on and the commands that work at it (press Enter after each):

- `scan` — scan the screen that is showing now, with VoiceOver off (it may show iOS's "Automation Running" banner and ask for Face ID or your passcode, usually only the first time).
- `start` or `skip` — read VoiceOver's captions for that screen, once you have turned on VoiceOver and its Caption Panel, or don't.
- `done` — you've finished swiping through the screen in one pass; Swipewalk stops reading (it stops by itself after 10 minutes).
- `off` — you've turned VoiceOver off again, so the next scan isn't disturbed.
- `note <text>` — saves a note; `stop`, Ctrl-C or Ctrl-D ends the session and saves it to History.

The desktop buttons Scan this screen now, Start reading captions, Don't read captions for this screen, I've finished swiping and
I've turned VoiceOver off are these commands. Continuing a saved iPhone session isn't offered. The tried-so-far limits (one iPhone
SE, one sample app, English) are in `swipewalk limitations`, entry `ios-voiceover-captions-capture`.

**A picture of each screen.** For every screen state the session sees (up to three per screen), Swipewalk
takes a plain screenshot shortly after the screen is detected, and the report shows it in the screen's
own section, in the "Captured order" view of the screenshot, with a solid, numbered outline on each
control TalkBack reached (numbered in the order it first reached them in this session) and a dashed
outline on each control of that screen it did not reach. The same information is written out as lists
under "Screen reader (captured)" in Full audit detail: the controls reached, in order, and a list headed
"Not reached in this session". "Not reached in this session" only means TalkBack's focus did not land on the control while the
session was recorded; it does not show the control can't be reached, and Swipewalk's session never says
that about a control, because nothing it records establishes it. A screen that had already changed by the
time the screenshot was taken, or one you opened after leaving the app, has no picture (the lists are
always there), and outlines can be off, or the picture can show a different state, if the screen changed between
detection and the screenshot. A screen state also has no picture if the screenshot failed or it is past
the limit of three per screen (the report says so). Each screenshot costs one extra screen grab, a check
before and after that the app is in front, and blanking the status bar; how long that adds hasn't been
measured on a device. `--no-screenshots` on an export leaves the pictures out.

**Saved as it goes.** A session is listed in History from the moment it starts ("In progress" in the
desktop app, "(session in progress)" in `swipewalk history`) and its run is saved again as it goes: at
once for each new screen, screen state or note, and otherwise about every 2 seconds while what
TalkBack reached changes. If Swipewalk is closed, force-quit or crashes before you stop the session, or
the computer loses power, the next time History or `swipewalk history` reads it and finds that Swipewalk
process gone, it lists the run as ended early, with the command to carry it on. Its report says the
session has "no recorded end" and shows only what was saved, up to the last recorded activity; the last
few seconds before it stopped may be missing, and anything after that wasn't recorded, so test it by
hand or continue the session. The phone's accessibility settings may still be changed in that case, so use
Check on the Devices page (or `swipewalk doctor`) to put them back. After a normal stop, `swipewalk history`
shows the command to carry the session on and `swipewalk session` prints "Carry on later with: ...".

**Carrying on a session later.**

```
swipewalk session --continue <run> [--package <app package>] [--device <id>] [--history <dir>] [--confirm]
```

`<run>` is a run folder or id from `swipewalk history`, where a saved session is listed with the command
to carry it on. The app comes from the run; `--package` is accepted only if it names that same app (a different app is refused with a message). New screens are added to the
same run; a screen you open again in the new session replaces its earlier recording in that run, the same
rule as `record --continue` and within that run only (a session in a different run is never changed); your
notes are all kept. A session that ended normally can be continued too. The report says the run was
recorded across several sessions, with each one's start and end times. The device is asked for again, and
the same confirmation applies on a physical phone.

## 6. CI and history

For a repeatable, scriptable run, describe it once in a `swipewalk.json` file and run
`swipewalk run`. Here's a minimal example:

```jsonc
{
  "app": {
    "android": { "package": "com.example.app" },
    "ios": { "bundleId": "com.example.app" }
  },
  "targets": [
    { "platform": "android" },
    { "platform": "ios", "device": "booted" }
  ],
  "mode": "scan",                  // "record" to walk through screens yourself
  "standard": "ada-title-ii",      // focus counts and exit code on one standard (optional)
  "myLaws": ["ada-title-ii", "us-tx"], // list these laws first in reports (optional; same ids as --standard)
  "framework": "maui",
  "largeText": true,
  "largeTextRestart": "never",     // record only; "ask"/"always"/"never" -- see section 5. Defaults to
                                    // "never" so an unattended run never blocks waiting for an answer.
  "appearance": false,             // scan only for now; true = also check the other dark/light appearance
                                    // (the CLI's --appearance both; here it's a plain boolean)
  "orientation": false,            // scan only for now; true = also check the other orientation
                                    // (the CLI's --orientation both; here it's a plain boolean)
  "screenReaderCapture": false,    // Android only for now; true = also drive TalkBack and capture what it
                                    // says (the CLI's --screen-reader) -- see section 4
  "screenReaderConfirm": false,    // required alongside screenReaderCapture on a physical Android phone
                                    // (the CLI's --screen-reader-confirm, since swipewalk run doesn't prompt for this)
  "webAudit": false,               // Android only; true = also run axe-core against WebView content, debug
                                    // builds only (the CLI's --web-audit) -- see section 4
  "triageFile": "swipewalk-triage.json",  // see section 11; picked up automatically at this name/location
                                    // next to swipewalk.json even when this line is left out
  "failOn": "wcag-issues"          // exit code 3 when WCAG issues are found; "never" to always exit 0
}
```

```bash
swipewalk run --config swipewalk.json
```

`run` installs the app if you gave it a build file, runs the pre-flight checks, scans (or
records) every target, and saves each run to history (unless you pass `--no-history`). Its exit
codes are built for CI:

- **0** — the run completed; if `failOn` is `"wcag-issues"`, no WCAG issues were found (still not a
  statement of conformance — see [section 4](#4-reading-a-report)).
- **2** — a target couldn't be scanned (device not found, app not installed, and so on).
- **3** — `failOn` is `"wcag-issues"` and at least one WCAG issue was found, for the standard in
  focus if `--standard`/`"standard"` was set.

Every run from `run`, `scan` and `record` is saved to a local history unless you pass
`--no-history` — `~/Library/Application Support/Swipewalk/runs` on macOS by default, or the
directory you pass with `--history <dir>`. `swipewalk run --history <dir>` saves into a different
folder (useful for a test or CI history separate from your own); `swipewalk run --no-history` skips
the history altogether (the report is still written, to a timestamped folder for each target under
`"out"` in swipewalk.json, or under `swipewalk-report/` in the current directory if `"out"` isn't
set). List saved runs with:

```bash
swipewalk history
```

Runs are listed grouped by app, then by that app's own version, with a count at each level (a run's
version is whatever the device reported at capture time — Android's versionName/versionCode or iOS's
CFBundleShortVersionString/CFBundleVersion; a run saved before this was recorded, or where it couldn't
be read, is grouped under "version not recorded"). Each run line also says what kind of run it was (Scan,
Recording or Screen reader session) and adds "Screen reader evidence" when the run holds any: a scan or
recording that captured screen reader evidence (`--screen-reader`: what TalkBack said on Android, or
Accessibility Inspector readings on iOS; or `--voiceover-captions`: VoiceOver's captions), or a live
session in which TalkBack's focus reached at least one control. A run opened from a shared file also shows
"Imported" (see [Sharing a run](#13-sharing-a-run)). A run saved by an earlier version shows only its kind,
even if its results hold screen reader evidence. Nothing decides which version "counts" for you — see
[Combining several runs into one report](#combining-several-runs-into-one-report) below for choosing which
version(s) to build a report from.

`swipewalk apps --platform android|ios [--device <id>]` lists the apps installed on a device (package
names on Android, bundle ids on iOS) so you can copy one into `--package` or `--bundle-id`; add
`--include-system` to include the system apps. It is the command-line counterpart of "Choose app…" in the
desktop app. `swipewalk history --delete <run>` deletes one saved run (its folder or id, as listed above) and
nothing else; a recording still in progress is refused. The desktop app's Delete button, which asks first,
does the same.

To see what changed between two runs, compare them by run folder or `results.json` path:

```bash
swipewalk compare <earlier> <later>
swipewalk compare --app <app id>      # the newest saved run of that app against the one before it
```

`--app` takes the app id shown by `swipewalk history`; add `--platform android|ios` when the same id was scanned on both,
and `--history <dir>` for another History folder; the options can be given in any order. It compares the same two runs the desktop app's Dashboard card does: the newest scan or recording and the one before it
(screen reader sessions are left out, since a session covers only the screens you went through, and so is an earlier run that captured no screens, since it has nothing to compare against).

This reports what's new, what's no longer found, and what's still found. "No longer found" means
the automated checks didn't report it this time — it does not mean the issue was fixed; check by
hand. Findings on screens that weren't scanned again aren't compared and are listed separately. A
finding the later run's own `triage.json` marks (or `--triage <file>`, to read marks from elsewhere)
keeps its place in whichever list it's already in — it's never counted as new or no longer found just
because it's triaged — with "[Marked as … by the team: reason]" shown after it (see
[section 11](#11-triage-mark-a-finding-as-already-looked-at)).

Every scanned screen's raw capture (accessibility tree, screenshot) is also saved, so you can
re-run the rules against it later — after updating Swipewalk, for example — without the device:
`swipewalk scan --platform android --from <capture dir> --out report` (or `--platform ios`)
re-scans a saved capture instead of a live device. Find capture directories inside a run folder in
history: `capture/` for a single scan, or `screens/01`, `screens/02`, … for a recording. A run folder may also have a `pictures/` folder. It holds pictures that were captured elsewhere (a saved capture replayed with `--from`) or copied in from an older run, so the run folder holds its own pictures. It is not a capture directory.

## 7. The desktop app

The desktop app (macOS, Mac Catalyst) does the same scans with a window instead of a terminal.

The first time you open it, a short welcome explains what to expect. Swipewalk runs automated accessibility
checks on your running app; automated checks find some issues, not all, so manual testing is still needed; you
are asked to use a test device and test data; and reports show which laws and standards each finding is relevant
to, with links to their official sources so you can check them (this is not legal advice). **Get started** (or
Return, or Escape) closes it, and it doesn't open by itself again; **About these mappings** closes it and opens
the Laws and standards page. To read it again, choose **Help > Welcome to Swipewalk**. **Help > Automated Checks** lists every automated check Swipewalk runs (what `swipewalk checks`
prints), and **Help > Known Limitations** lists what it cannot check or may get wrong (what `swipewalk
limitations` prints), each row opening to its details. Use the Show menu at the top of the page to switch between the two lists. **Help > Save Diagnostic Report…**
writes the diagnostic report described in [section 8](#saving-a-diagnostic-report-for-a-bug-report) (what
`swipewalk diagnostics` does).

The pages:

- **Dashboard** — per-app cards showing the latest run's kind (Scan or Recording, plus "Screen reader
  evidence" when it holds any), counts (and its app version, when known) and
  the change since the previous run, plus a trend of WCAG issues over recent runs with the app version
  each one tested underneath its bar, so progress lines up with releases, not just dates. When an
  app's saved runs cover more than one recorded version, the card also lists each version with its own
  run count ("Versions: 1.2.0 (3 run(s)), 1.1.0 (5 run(s))") — this is only a summary; build a report from
  a chosen version in History (below) or `swipewalk export --app`/`--app-version`/`--run`
  ([section 10](#10-exporting-findings)).
  After Swipewalk closed unexpectedly, a notice at the top offers to save a diagnostic report (see
  [section 8](#saving-a-diagnostic-report-for-a-bug-report)).
- **Laws and standards** — every law and standard Swipewalk tracks, grouped as US federal, US states,
  EU and member states, UK, and other countries, with a search and a count. Each row shows the name,
  whether it is mapped (or not mapped, with a one-line reason) and what it references. For a mapped
  entry, choose the name to see the rest: which WCAG version and level it references, any criteria it
  doesn't apply to apps (non-web software), by number and name, who it applies to, links to the
  official source, an archived copy when there is one, and the WCAG version it references, and the date it was checked.
  For one that isn't mapped, the details give the reason, the date checked and the official source it
  was checked against, when there is one. Every mapped entry links to its official source so you can check
  how it applies to you; this is reference information, not legal advice. "Suggest a correction"
  opens a pre-filled public issue form on GitHub in your browser (it holds only the entry's own data and the
  Swipewalk and ruleset versions, nothing about you or your devices), and "Suggest a law or
  standard" does the same for one that isn't listed. Shortcut to this page: ⌘5. **Laws that matter to
  me…**, near the top, opens a list where you tick the laws and standards to list first in reports and
  exports (nothing is hidden, only ordered and folded; a specific law or standard chosen for a scan
  still means that one only). The command line has the same
  list as `swipewalk standards`.
- **New scan** — pick a platform, device, app, standard and what to scan (single screen or record),
  and start it. "Law or standard" lists All standards, then the main standards, US states and other
  countries under their own headings, each as a name and what it references (for example "New York
  (WCAG 2.2 AA)"); the choice is the same as `--standard <id>` on the command line, and only the
  jurisdictions Swipewalk has mapped are listed (the rest are in [docs/standards.md](standards.md)).
  The menu is quick for the usual choices; **Search…**, the button beside it
  (also beside the same choice in the export dialogs), opens a sheet for finding one by typing part of its
  name or a place, such as "Texas" or "Germany". The sheet shows the same entries under the same headings,
  with how many match; clicking an entry chooses it and closes the sheet, and Return in the
  search field chooses it when only one matches. Escape in the search field clears what you typed; Escape with the field empty, or **Cancel**, closes the sheet without changing anything. The search box is a system search field, as in **Laws that matter to me** and the app picker; nobody has yet checked how it looks; with VoiceOver it reads the match count and each entry, but has the gaps below.
  The entry that is chosen now is marked, and choosing one sets exactly what the menu would. **Known gap
  in this release:** from the keyboard, Space and Return on a list entry do not choose it, and with VoiceOver
  neither does VO+Space; after typing, Tab stays in the search field instead of reaching the list or Cancel;
  and with VoiceOver, focus starts on the first list entry and VO-arrow navigation stays in the list.
  Instead, type the name and press Return when exactly one law matches, press Escape or choose **Cancel** to
  close the sheet, or, with the pointer, choose from the **Law or standard** menu itself, which lists every mapped state and
  country. This was found in a hands-on check on 2026-10-03 (macOS 27, keyboard navigation on, VoiceOver); a
  fix is planned for a later release (see the [accessibility statement](accessibility-statement.md)). The command line takes the
  same ids as `--standard <id>`; `swipewalk standards` lists them.
  **Laws that matter to me…** (also on the Laws and standards page) opens a searchable list of the same laws and standards, grouped the
  same way, with a check mark beside each chosen law; each law's accessible name ends with "checked" or "not checked", and Escape clears the filter, or closes the list when it is empty. Choosing a law with Space or VO+Space has not been checked by hand and likely does not work, the same gap as in the Search sheet above; use the pointer, or `--my-laws <ids>` on the command line; a choice is saved as you make it (on this Mac, for every scan
  and export from the app) and **Choose none** puts everything back. It changes only which laws a
  report and its exports list first, never what they include, and a specific "Law or standard" above
  still means that one only (the line next to the button then says the choice isn't used for that scan). Each scan keeps the choice it was made with, so a later export follows
  it; an older run without one uses your current choice. An export follows the run's own choice unless you tick **Use my current laws and standards instead of the saved choice**, which the desktop export offers only when the run's saved choice differs from yours (the same as `export --my-laws` with your current laws; see [Exporting from the desktop app](#exporting-from-the-desktop-app)).
  An optional **App source folder** (Choose folder… and Clear; read on this Mac only) is the same as
  `--source` ([section 12](#12-finding-the-likely-source-line-net-maui-native-android-native-ios-react-native-flutter)):
  findings that Swipewalk can match point at a likely line in your project, for scans and
  recordings. An optional **Team triage file** (Choose file… and Clear, under **More checks** below, in both scan and record mode) is the same as `--triage <file>`
  on `scan` and `record`: findings the file already marks (for example a `swipewalk-triage.json` your
  team keeps in the app's repository) start out marked in the new run. Where a finding is marked both in that
  file and in the previous run of the same app, the file's mark is the one kept; the previous run's marks
  are carried forward only for findings the file says nothing about (see
  [section 11](#11-triage-mark-a-finding-as-already-looked-at)).

  **More checks** (a button that opens a collapsed section, closed until you ask for it) holds the
  extra checks and options the command line has, each off by default like the command line:
  "Also check in the other dark or light appearance" (`--appearance both`), "Also check the other
  orientation (portrait or landscape)" (`--orientation both`), "Also check for content that changes by
  itself" (`--auto-update-content`, with "Seconds before the first extra capture" for
  `--auto-update-interval`, default 3; a number greater than 0) and "Also check web content inside the
  app" (`--web-audit`; Android only, needs a debug build of the app). These four go through the same scan code as the command-line options. The first
  three are for a single-screen scan only, so they're hidden while you choose Record (the command line
  refuses them there too); the web content check works in both. "App framework" is a menu: Detect automatically (the default), .NET MAUI, Android Views, Jetpack
  Compose, UIKit, SwiftUI, Flutter, React Native, or web content in an app — the same choices as
  `--framework`, used for fix examples and source matching. While you choose Record, "Screens you plan to
  cover" (comma-separated, for example Login, Home) is the same as `--expect`: the report lists the ones
  you didn't scan. The optional **Team triage file** described above is in this section too, for both scans and recordings. On a physical phone, the dark/light and orientation checks change the phone's settings and put them back; a line under them says so when you tick one (the scan prints the same notice). The dark/light check is skipped, with the reason in the report, when an iPhone's appearance is set to Automatic. On Android the setting is put back as it was (on, off, sunset to sunrise, bedtime, or a custom schedule with its start and end times (the reading and restoring are checked in unit tests against sample `cmd uimode` output; restoring a schedule has not yet been tried on a phone)); if it or the rotation settings can't be read, the check is skipped with the reason. Use a test device.

  No device is chosen by default; Start (and Choose app…) stay disabled, with a short
  reason shown, until you pick one. "Scan automatically when the screen changes" only appears once
  you choose Record (it has no effect on a single-screen scan) and is off by default, so a screen
  you're still navigating through is never captured on its own — use Scan this screen now instead.
  Choosing a physical phone shows a notice that the large-text
  check changes the phone's own text size and restores it afterwards; it appears again next time
  unless you choose Don't Show Again, rather than just OK. While a physical phone and the
  large-text option are both selected, a short reminder of the same fact stays next to that option.
  For Android, "Listen with TalkBack (records what TalkBack actually says)" is on by default — the
  same real TalkBack capture as the CLI's `--screen-reader` (see [section 4](#4-reading-a-report)),
  for both a single scan and every screen you scan while recording; it's hidden for iOS. The first
  time it would change a physical phone's accessibility settings, Start shows a confirmation naming
  what changes and that TalkBack stays silent while Swipewalk listens; choosing Continue is
  remembered on this Mac so you're not asked again, and Cancel stops the run before anything changes
  (asked again next time). Use a test device.
  For iOS, "Read Accessibility Inspector evidence (properties VoiceOver uses)" (hidden for
  Android) is **off by default**. Unlike TalkBack, it needs you present every time it's used, so it
  can't run unattended. This reads real accessibility evidence from Xcode's Accessibility Inspector;
  VoiceOver itself does not run and does not speak. With it on, once the screen has been captured
  (when recording, the first screen you scan), an alert explains what the macOS Accessibility
  permission is for; if it isn't granted yet, a second alert names the app to add in System Settings
  > Privacy & Security > Accessibility and offers to open that pane directly. Once granted, that
  explanation is confirmed once on this Mac and not shown again unless the permission goes missing
  again (for example, revoked in System Settings). After that, the Inspector setup step (open
  Accessibility Inspector, choose your device, click the first element on the app's screen) is asked
  once per scan, or once per recording session at its first screen (a continued recording asks
  again) — Continue only after you've done it, since nothing here can confirm it was. The permission
  explanation and the setup step both offer "Skip Inspector Evidence" instead of "Cancel": skipping
  does not stop the scan — the run carries on and still reaches its report, with the predicted
  transcript in place of Inspector evidence for that scan, or for every screen of that recording, and
  the report says which of "declined" or "permission not granted" applied, never one ambiguous
  reason.
  Either route's capture shows in the progress log while it runs (and the CLI's console, for
  `--screen-reader`): a line when it starts ("Capturing what TalkBack says…" or "Reading Accessibility
  Inspector evidence…") and a line with how many elements it captured, or why it didn't — TalkBack
  itself goes silent for that time and the Inspector walk otherwise looks like nothing is happening, so
  without this it's easy to think the capture never ran at all. Any screen with captured evidence also
  gets a one-line pointer in the report's default "What to fix" view, to its "Screen reader (captured)"
  tab in "Full audit detail" — a pointer, never a pass or fail by itself; VoiceOver is never turned on
  for either route, so this is not recorded speech (see section 4).
  A recording that ends early before capturing a single screen (for example the iOS test harness
  couldn't be installed) doesn't open a report at all: the app stays on New scan, shows the error above
  the progress log, and says where the run was saved in History — the run is not lost, and can be
  continued from History like any other ended-early recording.
  The build-file picker (Choose…
  next to "Or install a build first") accepts an Android App Bundle (`.aab`) as well as an `.apk`;
  for Android, an "Installing an Android App Bundle (.aab)" section below it lets you point at a
  specific bundletool (optional — it's usually found automatically) and, instead of the standard
  Android debug key Swipewalk otherwise creates and signs with, a keystore and its key alias to sign
  with your own key — the same options as the CLI's `--bundletool`/`--keystore`/`--keystore-alias`
  (see [section 8](#8-troubleshooting)). The keystore's password is never typed into the app or
  saved: set the `SWIPEWALK_KEYSTORE_PASSWORD` environment variable (and `SWIPEWALK_KEY_PASSWORD` if
  the key's own password differs) before launching Swipewalk. Once a scan or recording starts, a
  progress panel next to the log shows a busy indicator and, for a single-screen scan, the current
  step ("Scanning… step 3 of 6: checking at large text") — each step is announced for a screen
  reader too. Recording shows its own "don't touch the phone" line only while a screen is actually
  being captured, not for the whole recording: the rest of the time you're expected to navigate the
  app normally. Everything that could interfere with the running scan — the platform, device and app
  fields, the build/keystore/bundletool pickers, the standard and options, and starting a second
  scan — is locked while it runs (a screen reader hears why); History, old reports and Cancel stay
  available. Cancel stops the scan or recording as soon as the current step ends and puts back any
  device setting it had changed, the same as when a scan finishes or fails normally. **Quitting while a
  scan or recording is running**: ⌘Q and the app menu's Quit item ask first — "A scan or recording is
  running. Quit now? Swipewalk will stop the scan or recording and try to put back any device settings
  it changed (for up to a minute) before quitting." Choosing Stop and quit cancels the run and waits up
  to a minute for it to finish restoring the device, then quits either way, even if the restore hasn't
  finished by then. Cancel (the default — Return is expected to act like it, not yet checked by hand on
  a real keyboard) leaves the scan or recording running. With nothing running, ⌘Q and Quit just quit,
  the same as any other app. **Closing the window** (its own red close button) doesn't quit Swipewalk at
  all, running or not — the app keeps running in the background with no window open, like many Mac apps;
  a running scan, recording or screen reader session keeps running, and clicking the Dock icon brings
  the window back with the run still going (the reopened window keeps its size and position and shows what
  the run uses, read-only). Version 0.4.0 uses the window setup macOS 27 requires, which is what makes
  this possible; 0.3.0 was stopped by macOS 27 as it launched. One known gap, seen once by a person with
  VoiceOver: after the window is reopened while a run is going, Tab did not reach the last button in the
  Progress panel (whether Full Keyboard Access was on wasn't recorded); a fix was not confirmed, so reach it with VoiceOver navigation or the pointer. **Known keyboard
  and VoiceOver gaps (found in a hands-on check on 2026-10-03, macOS 27; fixes planned for a later release):**
  with macOS keyboard navigation on, Tab and Shift-Tab move only between controls that are visible in the
  window, so make the window taller or scroll with the mouse or trackpad to reach the others; in an open
  pop-up menu (checked on **What to scan**) the arrow keys move the highlight but Return does not choose it (press Space to open the menu, type the first letters of
  the item, then click to choose); the Search sheet described above has the gaps listed there; and the same
  row-choosing gap likely affects **Laws that matter to me**, the app picker and History rows with VoiceOver (not
  checked on each; in the app picker, type the app id in the field instead; on History, the row's buttons work with the
  pointer; with VoiceOver they haven't been tried). The [accessibility statement](accessibility-statement.md) lists each with its checks. The
  Dock's own Quit, logging out and shutting down can't be asked about first — that's a Mac Catalyst
  limitation, not a choice Swipewalk makes — but for those, Swipewalk still requests a cancellation as
  the app closes, using the same restore code Cancel uses, though the app may close before that restore
  finishes. A force-quit, or Swipewalk or the Mac itself crashing, gives it no chance to do even that.
  Whichever way it happens, if a device's settings look off afterward, reconnect it and use Check on the
  Devices page (or `swipewalk doctor`) to put them back — a banner also appears automatically on the
  Devices and New scan pages naming any device Swipewalk still owes a restore to (see below).
- **Screen reader session** — start with "Phone and screen reader": an Android phone with TalkBack
  (below) or an iPhone with VoiceOver (next item). Android, a live session: pick a device and app (type the package, or
  Choose app… to pick from recently scanned and installed apps, the same as New scan; optionally an app name and an app version
  to label the run with, like the command's `--app-name` and `--app-version`, which otherwise default to the package and the
  version read from the phone), confirm that
  Swipewalk will change accessibility settings for the session, and Start. No device is chosen for you: pick one from the list, and
  the page says which device the session will run on and that it uses no other (the session never switches device). On a physical
  phone, Start asks you to confirm once more, naming the phone. Before anything is changed, Swipewalk checks that the chosen device can
  run a session (still connected, Google's TalkBack installed, the app installed); if it can't, for example an emulator image without
  TalkBack, a message names the device and why, and nothing on it is changed. Unlike a normal scan or
  recording, Swipewalk doesn't drive anything and shows no step-by-step instructions — you use
  TalkBack on the phone as you normally would, for as long as you like, while Swipewalk records what
  happens: which controls TalkBack's focus reached, in what order, what TalkBack said, and what you
  activated. The phone's screen isn't shown here. Once the session starts, "Can you hear TalkBack?"
  offers a short test phrase played on the phone: during a session Swipewalk plays TalkBack's speech through the phone's media volume, so a
  low media volume means silence. Say whether you heard it; if not, the page says "Turn up the phone's
  media volume". It's only a check — nothing waits for it and you can carry on either way. "Show the last
  thing TalkBack said" (off by default) adds a line, updated as you go, with the latest thing TalkBack
  said; it can include what's in a text field or what you type, so leave it off when that matters.
  Swipewalk installs a small helper app, turns on
  TalkBack and makes the helper the phone's speech engine for this session, then passes TalkBack's
  speech on to Google's speech engine so you can hear it (the voice may differ from your usual one); if
  you hear nothing, Swipewalk still records what TalkBack says. The speech and the sound check were heard once, by a person, on one physical Pixel phone, after raising its media volume; other phones haven't been tried. Press "Add note", type what you
  noticed and press Save (or Return) to capture your own words plus the current screen, the
  last-focused control and the last few things that happened, automatically — you only type the
  observation. Next, Previous and Activate are optional convenience buttons that move TalkBack's focus
  in Swipewalk's own order (not the order TalkBack uses when you swipe) and press the focused control
  directly (not the way a double-tap does); the report always keeps a Mac-button move separate from
  your own gesture. If you leave the app, a notice says so and Swipewalk stops recording until you
  return to it, or you press Stop session; while the app is in front, everything TalkBack says is
  recorded, including a notification it reads out, and including what's in text fields and the
  characters it echoes as you type — use test data, never real passwords or personal details. Stop
  session puts the phone's accessibility settings back and asks the phone to remove the helper app — if
  the settings restore can't be confirmed, the summary says so — then saves the session as its own run in History, alongside
  your scans and recordings, together with a normal scan of the first screen. The summary after Stop
  counts what was recorded — screens visited, controls reached and not reached, notes and evidence items to
  review (counts, not a result) — and has an Open report button and a New session button (the iPhone session's summary has both too); New session closes the summary and
  shows the setup again, with the same device and app still filled in; the confirmation has to be ticked again, and the saved
  session stays in History. Each note (in the session and in that
  summary) has Copy ticket and Save ticket… buttons: the note becomes a Markdown ticket in the same layout
  as a finding's ticket, with the screen, the last control TalkBack focused and what happened just
  before; it says plainly that it's the tester's own note, with no automated check behind it. Its report includes a
  "Screen reader session (recorded live)" header and then each screen the session visited, drawn the same
  way as a screen in a scan's report (the same "What to fix" and "Full audit detail" views): controls
  reached and not reached, what TalkBack said beside what Swipewalk predicted (under "Screen reader
  (captured)" in Full audit detail), your notes on the screen you added them on, and any evidence found where
  what TalkBack said differs from what Swipewalk predicted or doesn't include the control's visible text,
  as findings with who is affected, the WCAG criteria and fix advice (a difference from Swipewalk's
  prediction is not itself a failure — it's evidence from a live TalkBack session, not an automated
  capture, so check it by hand). It's its own kind of run, never merged into an existing scan or
  recording of the same app — at most the session's first screen (the one already open when you press
  Start) gets a matched capture, so only that screen can have Swipewalk's automated checks and a predicted
  transcript to compare against; a screen you navigate to later in the session says plainly that the
  automated checks did not run on it, and is still identified and checked for 2.5.3 Label in Name evidence,
  but has no predicted-transcript comparison. See also `swipewalk session` in
  [section 5](#5-record-mode-and-the-large-text-check) for the same live session from the command line.
  Every screen state the session sees also gets a plain screenshot (up to three per screen), which the
  report shows in that screen's section with numbered outlines on the controls TalkBack reached and dashed
  outlines on the ones it did not ("not reached in this session", never "can't be reached"), plus the same
  information as text lists; see the section 5 description of the picture for what it can and can't show.
- **Continue session** (History, on a saved screen reader session) — opens the Screen reader session page
  for that run, with its app package filled in and locked. Choose the device and confirm again, then
  Start: new screens are added to the same run, a screen you open again replaces its earlier recording in
  that run, and the report says it was recorded across several sessions, with each one's start and end
  times. It is a separate button from Continue, which resumes a recording that ended early.
- **Screen reader session, iPhone (VoiceOver)** — choose "iPhone (VoiceOver)" under "Phone and screen
  reader". Only a physical iPhone is offered, connected by cable: VoiceOver doesn't run in the
  Simulator. Choose the iPhone, enter the app's bundle id (or press "Choose app…" to pick one from recently scanned apps
  and the apps installed on the iPhone; optionally an app name and version to label the run with, which otherwise
  default to the bundle id and the version read from the iPhone), tick the confirmation and press Start
  session. This is not the Android session's shape: Swipewalk doesn't follow VoiceOver between
  screens or list controls you didn't reach, and it doesn't turn VoiceOver on or drive it — you do
  (Settings > Accessibility > VoiceOver, and VoiceOver > Caption Panel, on the iPhone). It reads
  captions one screen at a time, the same way `swipewalk record --voiceover-captions` does. While
  the session runs, keep VoiceOver off and use the app as you like; when you want a screen checked,
  press "Scan this screen now" (a normal scan of that screen, which briefly shows iOS's "Automation
  Running" banner and may ask for Face ID or your passcode, usually only the first time). Swipewalk then asks
  whether to read VoiceOver's captions for that screen: turn VoiceOver and its Caption Panel on,
  press "Start reading captions", swipe through the screen in one pass, and press "I've finished
  swiping". Swipewalk keeps reading until you do (it stops by itself after 10 minutes, and says so). Then turn VoiceOver off again and press "I've turned VoiceOver off" —
  VoiceOver running during the next automated scan is reported to disturb it. "Don't read captions
  for this screen" keeps the normal scan and doesn't read captions for that screen. If reading
  captions fails, the page says so with the reason; if it finds none, the page says so and suggests
  scanning the screen again. The session log records either outcome. If you scan the same screen again later in
  the session and that newer scan has no captions, the captions read earlier are kept for the screen
  (see the record-mode rescan rule above for how the report shows them); if the newer scan has
  captions, they replace the earlier ones. The session log records each step (screen scanned, captions
  started, read with a count or none found, failed with the reason, declined, VoiceOver turned off), and each note is tied to the last
  screen you scanned. The captions are read from
  screenshots of the iPhone taken over the cable and recognized on this Mac; the screenshots and caption text stay on this Mac, and
  it needs no Screen Recording or Accessibility access on the Mac. "Add note" works as in the
  Android session; "Stop session" is always available and settles whatever step the session is on
  (a pending question counts as "no", a caption reading stops where it is). The session is saved as
  its own run in History (a "session", like an Android one), and its report shows each screen's
  captions under "Screen reader (captured)" in "Full audit detail", plus a "Screen reader session
  (recorded live)" section with your notes. Like an Android session, it is left out of the Dashboard's
  per-app cards and trends (it isn't a comparable full scan) but shows in History. There are no Next, Previous or Activate buttons for
  VoiceOver. Captions are evidence to review by hand, never a pass or fail. The caption reading has
  been tried on a real iPhone once, on one screen, in English, through `record --voiceover-captions`;
  this desktop page itself has since been tried once on one iPhone SE with the sample app, in English
  (2026-09-29): choosing the app, reading captions, the "I've finished swiping" step, the on-page counts and the
  report showing the captions all worked as described. That is one device, one app and one language (see "VoiceOver-captions capture"
  in [docs/limitations.md](limitations.md)), so a caption can be missed or misread, and Swipewalk can't
  tell whether you swiped through the whole screen. The captions can include whatever VoiceOver says
  about text fields and what you type — use test data, never real passwords or personal details.
- **One device, one scan at a time** — the same device (by its adb serial or iOS UDID) can't be
  scanned, recorded or used for a screen reader session by two things at once, whether that's two
  windows of the desktop app, the desktop app and a `swipewalk scan`/`record`/`session` running in a
  terminal, or two terminals. Starting a second one gets a clear message naming who's already using
  it ("started 2 minutes ago by swipewalk scan" or "...by the Swipewalk app") and to wait or stop it
  first; a marker left by a process that crashed or was killed is detected and cleared automatically
  the next time something checks that device. If a screen reader session's computer-side process is
  killed or crashes while its phone-side helper is still running, the next `doctor`, scan or record on
  that device stops the leftover helper first, then restores the phone's accessibility settings — only
  once it's confirmed no other Swipewalk process on this computer still owns that device.
- **Devices** — readiness checks and fix hints for each connected device, the same checks
  `swipewalk doctor` runs. **App to check (optional)** takes an Android package or iOS bundle id, typed or chosen with
  Choose app… (it browses the device you last pressed Check on, or the only device), and then Check
  also says whether that app is installed (and, on Android, whether it is in front), like
  `swipewalk doctor --package` or `--bundle-id`. A device already being scanned elsewhere skips its Check with the same
  message, rather than risk both racing to change and restore its settings at once. A banner at the
  top (also on New scan) names any device Swipewalk still owes a restore to — an interrupted scan left
  its text size, orientation, appearance or (Android) screen-reader settings changed and never put it
  back (a crash, a force-quit, or one of the quit paths above that can't ask first). If that device is
  connected, the banner offers Restore now, which runs the same check as the Devices page's own Check
  button or `swipewalk doctor`; if it isn't, the banner says to connect it, then choose Restore now
  (starting a scan on it also puts it back). The banner disappears on its own once nothing is
  pending, and re-checks itself every time either page is opened. **Remove Swipewalk helpers…** on a
  device's row (the same as `swipewalk helpers remove`) asks first, saying exactly which of Swipewalk's own
  helper apps it will remove, then reports what it removed or why it refused; see
  [What Swipewalk installs on a device](#what-swipewalk-installs-on-a-device). While a physical iPhone is listed,
  **Helper bundle id prefix for removal (optional)** is for helpers signed with a company wildcard profile
  (`--harness-bundle-prefix`).
- **History** — every saved run, grouped by app and then by that app's own version, with a count at
  each level (a run's version is grouped under "version not recorded" when it wasn't known — see
  [section 6](#6-ci-and-history)); open a run's report from its own row, or use Compare runs… at the top to compare any two runs. A recording
  that's still going (here, or in another window) shows "In progress"; one that stopped before Finish
  -- including Swipewalk itself being closed or crashing -- shows "Ended early" with a Continue button
  to resume it. A screen reader session works the same way: one still running shows "In progress", and
  one whose Swipewalk process stopped before you stopped it, or that ended early for another reason (such
  as the phone's settings not being confirmed as put back), shows "Ended early" with a Continue session
  button (a session you stopped normally also has Continue session, without the label). Each row says what kind of run it was (Scan, Recording or Screen reader session) and
  shows "Screen reader evidence" when the run holds any (a scan or recording that captured screen reader
  evidence — `--screen-reader` or `--voiceover-captions` — a live session in which TalkBack's focus
  reached a control, or an iPhone session in which VoiceOver captions were read); a run saved by an earlier version shows only its kind. Each row also has an Export… button, to export that run without opening its report
  first (see [section 10](#10-exporting-findings)), and a Share run… button (see [section 13](#13-sharing-a-run)); a run opened from a file
  shows the tag Imported and the name the sender typed, if any. Delete is a red button. It asks first and names the run
  (app, date and time, platform and mode). Cancel is set as the alert's default button, so Return and Escape are
  meant to keep the run (not yet checked with a real key press). Only choosing Delete removes the run and its
  report and screenshots. In a shared History location (see [Where runs are saved](#where-runs-are-saved)) the red
  button is replaced by **Remove from list**. Each version's own header row has its own Export…
  button that combines scans you choose from that version — nothing is ticked to start, you pick which
  ones — into one report covering every screen across them, resolving any screen two ticked scans both
  captured the same way the CLI does (see below) — the desktop app's counterpart of
  `swipewalk export --app <id> --app-version <v> --run <id>...` (see [Combining several runs into one report](#combining-several-runs-into-one-report));
  shown only for an app with a known id, since there's otherwise no id to combine runs by.
- **Report** — the same HTML report you'd get from the CLI, in a window, with a native findings list
  and Export… button for turning it into tickets, a CSV, a shareable copy of the report or a PDF (see
  [section 10](#10-exporting-findings)). Each row in the findings list also has a **Triage…** button:
  choose false positive, won't fix or accepted risk, type a required reason, and optionally who and
  which app version — the same marking `swipewalk triage` does (see
  [section 11](#11-triage-mark-a-finding-as-already-looked-at)), saved into that run's own
  `triage.json` and shown in the report the moment the dialog closes. Reopening the dialog for an
  already-marked finding shows what was saved and offers Clear mark. The findings list's **Team triage
  file** (Choose file… and Clear) is the same as `swipewalk triage --triage <file>`: while a file is chosen,
  Triage… reads and saves marks in that file instead of the run's own, Export…, Export selected… and the per-finding
  tickets use it (the export page starts with the same file chosen), and the report is re-rendered from its marks, which also shows the report's own list of
  marks that match nothing in this run (the "stale" list `swipewalk triage <run>` prints). Choosing Clear
  re-renders the report from the run's own marks again. The report file keeps whichever marks were applied last. Clicking an external link inside the
  report (a W3C Understanding page, ada.gov, a standards source, an archive.org copy) opens it in your
  default browser instead of navigating the report itself away — so the report stays open and the Report
  page's own Back button still returns where you came from.
- **Compare runs** — pick any two saved runs and see new, no longer found, not checked again, and
  still-found findings, with the same "no longer found isn't fixed" caveat as `swipewalk compare` (it
  uses the same comparison). Two pickers, **Earlier run** and **Later run**, list every saved run
  grouped by app and app version, each row naming the app, version, platform, time and counts. History's
  **Compare runs…** button opens the page on the newest run and the previous run of the same app and platform; the Dashboard's
  "Compare with previous run" opens it on that card's run and its previous run. Neither starting pair
  includes a screen reader session (as the Dashboard and `swipewalk compare --app` leave them out), though
  sessions stay in both lists. You can change either side, so you can compare release 1.1 with 1.3, or two devices. Swipewalk never swaps your choice: if
  the run you picked as earlier was started after the later one, the page says so plainly. Findings are
  marked with the later run's own triage marks, or with the marks in an optional **Team triage file**
  instead (the same as `swipewalk compare --triage <file>`; the two are never merged).

Download the signed `.dmg` from the
[releases page](https://github.com/swipewalk/swipewalk/releases). See the
[Desktop app section of the README](../README.md#desktop-app-macos).

### Where runs are saved

By default the desktop app saves runs in its own folder on this computer. History shows the folder, with buttons
above the list:

- **Open in Finder** shows the folder.
- **Change location…** lets you choose another folder for all runs, for example a project's `accessibility/runs`
  folder or a shared drive. There is one location at a time. Once you have chosen a folder, Change location… also
  offers **Use the default folder**.
- **Show removed runs** and the **Other people use this folder** option appear when they apply (see below).

After you choose a folder, Swipewalk asks whether to **move** the runs already saved or **leave them where they
are**. It also reminds you that a folder synced by iCloud Drive, OneDrive or Dropbox can lock files or upload them
only partly while a scan is saving; it does this for every folder and doesn't check whether the folder is synced.

- Moving copies each run, checks the copy, and only then removes the original. Swipewalk never deletes an original
  before its copy has been checked; a copy that fails the check is removed again and the original is kept. A
  recording that is still going is never moved.
- If some runs can't be moved, the location does not change and Swipewalk says how many moved and how many stayed.
  The runs that did move are already in the new folder, so they don't show in History until you finish: choose
  Change location… and the same folder again.
- Leaving runs behind never deletes them: they just don't show in History until you switch back.
- The location can't be changed while a scan, recording or screen reader session is running.

**Shared folders and Remove from list.** Swipewalk then also asks: "Do other people use this folder?" Your answer is
stored with the location, and you can change it later with the **Other people use this folder** option on History
(shown only while a folder you chose is in use). The default folder is always private.

- **Yes (shared).** Deleting a run there would remove it for everyone who reads the folder, so rows show **Remove
  from list** instead of Delete. It hides the run on this Mac only: the files stay in the folder and other people
  still see the run. The list of hidden runs is kept per user, outside the folder. When something is hidden, the
  **Show removed runs (N)** button lists it again, marked as removed, with **Add back to list**; it then reads
  **Hide removed runs**. To get rid of a run's files, delete its folder in Finder; Swipewalk itself refuses to
  delete runs in a folder marked as shared.
- **No (private).** Delete works as usual, for example on an external drive that only you use.

The command line doesn't read this setting (so a script or CI job never writes somewhere different because of a
desktop preference). `swipewalk` keeps its own default folder, and `--history <folder>` points `run`, `scan`,
`record`, `session`, `history` and `export --app` at another one. While the app uses a different location, runs
the command line saves to its default folder don't show in the app's History unless you give the command the same
folder with `--history`.

## 8. Troubleshooting

`swipewalk doctor --platform android|ios` is the fastest way to find out what's wrong — run it
before opening an issue. It runs the same checks as `scan` and `record`, and each failure explains
how to fix it. If the device is currently being scanned or recorded elsewhere (see "One device, one
scan at a time" in [section 7](#7-the-desktop-app)), `doctor` skips the check rather than risk racing
that run's own restore, and says so. The common ones:

- **`adb` not found or not working.** Install the Android SDK platform-tools, then either set
  `ANDROID_HOME` or put `adb` on your `PATH`. Without either of those set, Swipewalk also looks in
  the Android SDK's usual default location for your computer: `~/Library/Android/sdk` on macOS
  (Android Studio's own default), `%LOCALAPPDATA%\Android\Sdk` on Windows, or `~/Android/Sdk` on
  Linux — plus, on macOS only, where Homebrew's `android-platform-tools` cask puts `adb`
  (`/opt/homebrew/bin` or `/usr/local/bin`). The failure message lists exactly where it looked.
  That's why the desktop app can find a Homebrew-only install too: apps launched from Finder don't
  inherit a Terminal's `PATH` or `ANDROID_HOME`. (Older Visual Studio for Mac/Xamarin installs kept
  the SDK at `~/Library/Developer/Xamarin/android-sdk-macosx`; Swipewalk no longer looks there, since
  Xamarin and Visual Studio for Mac are retired — set `ANDROID_HOME` to that path, or move the SDK to
  the default location above.)
- **Device state `unauthorized`.** Unlock the phone and accept the "Allow USB debugging?" prompt
  (tick "Always allow").
- **Device state anything else (e.g. `offline`).** Reconnect the cable, or run `adb kill-server`.
- **Screen off or locked (Android).** Unlock the device and keep it on — Developer options > Stay
  awake keeps it on while charging.
- **App not installed.** Install the app on the device first; Swipewalk can't do that step for you.
  (Another app being in front isn't a failure — Swipewalk brings the app under test forward, or
  starts it if it isn't running, before scanning.)
- **Scanning iOS apps needs a Mac with Xcode.** Shown on Windows or Linux — iOS scanning only works
  from a Mac; Android scanning is supported from all three, though Windows and Linux support is new
  and not yet verified on real hardware (see [section 1](#1-install)).
- **Xcode is not installed (only the Command Line Tools are).** Install Xcode from the App Store,
  then open it once. `xcode-select --install` on its own only installs the Command Line Tools, which
  is not enough to scan iOS apps.
- **The Command Line Tools are selected instead of Xcode.** Xcode is installed, but
  `xcode-select` currently points at the Command Line Tools instead. Run:
  `sudo xcode-select -s /Applications/Xcode.app/Contents/Developer`.
- **The Xcode licence has not been accepted.** Run `sudo xcodebuild -license accept`.
- **Xcode has not finished its first-launch setup.** Run `sudo xcodebuild -runFirstLaunch`.
- **No iOS Simulator runtime is installed.** A warning, not a failure — a physical iPhone can still
  be scanned. To add a Simulator runtime: Xcode > Settings > Components, or run
  `xcodebuild -downloadPlatform iOS`.
- **iOS harness not found.** Reinstall Swipewalk (the harness ships with it). `--harness <path to .xcodeproj>`
  is only for Swipewalk's maintainers, to test a harness build; you should never need it.
- **iOS: the Accessibility Inspector walk's script was not found (the message starts "Could not find").**
  Reinstall Swipewalk (the script ships with both the desktop app and the command-line tool).
- **Android: Google's accessibility checks (ATF) didn't run.** Swipewalk installs a small prebuilt
  instrumentation harness the first time it's needed. If it can't be installed or a run fails, the scan
  still completes with today's checks; the report and results.json say why (see
  [docs/limitations.md](limitations.md), "Google's Accessibility Test Framework needs the instrumentation
  harness to install and run"). `--android-harness <path>` is only for Swipewalk's maintainers, who use it to
  test a harness build with Gradle; the prebuilt copy that ships with Swipewalk needs no option.
- **Android: `--screen-reader` (TalkBack capture) "did not run: No bundled TTS-engine APK".** Reinstall Swipewalk (a prebuilt copy ships with it). `--android-harness <path>`
  is only for Swipewalk's maintainers; you should never need it.
- **No booted simulator or device chosen (iOS).** Boot a Simulator, or connect an iPhone and pass
  `--device <udid>` (see `swipewalk devices`).
- **iOS device: cannot sign the scanning harness.** You need a development provisioning profile
  installed, or Xcode signed in with an Apple ID for automatic signing; see
  [section 2](#2-set-up-a-device).
- **Developer Mode not enabled (iOS device).** On the device: Settings > Privacy & Security >
  Developer Mode, turn on and restart.
- **iOS device locked.** Unlock the device and keep it unlocked while scanning — including during
  the large-text check, which needs to drive the Settings app.
- **App not installed (iOS device or Simulator).** Install the app on the device/Simulator, or
  scan by `--install <file>` first.
- **A device's (Android phone or iPhone) text size was left enlarged.** This normally restores
  itself when the large-text check finishes; if a scan was interrupted partway through (e.g. Ctrl+C,
  a crash, or the iPhone locking), run `swipewalk doctor --platform android|ios` — it notices and
  restores it.
- **An Android phone was left with TalkBack settings changed after an interrupted `--screen-reader`
  capture.** A crash or a lost connection mid-capture can leave TalkBack turned on, its default
  text-to-speech engine changed, or the small helper app still installed. Run
  `swipewalk doctor --platform android` (or start a normal scan/record — `--screen-reader` isn't
  needed) — it restores what it can from its own record or the device's, and says what's left if it
  can't confirm everything.
- **iPhone: the large-text check can't find its way through Settings.** Swipewalk drives Settings >
  Accessibility > Display & Text Size > Larger Text through a fixed set of steps; a new iOS version
  that changes that screen can break it. Swipewalk falls back to a per-app text-size setting instead,
  or skips the large-text check for that screen with a reason — either way the rest of the scan still
  runs. Please report this so the Settings automation can be updated for the new iOS version.

Other things you might hit:

- **Apps that block screenshots.** Some apps set Android's `FLAG_SECURE` on sensitive screens
  (banking, passwords, DRM) to block screenshots. Swipewalk reports these screens and skips the
  pixel-based checks (contrast, close-ups) on them, rather than failing.
- **.NET MAUI Android debug builds won't install from a file.** `--install` accepts a signed
  `.apk` or `.aab`, but MAUI Debug builds that use fast deployment can't be installed that way
  (Swipewalk detects this and explains, in either format). Build and install with
  `dotnet build -t:Install -f net10.0-android` instead, or build in Release (or with
  `EmbedAssembliesIntoApk=true`), and scan the already installed app by `--package`.
- **Installing a `.aab` needs bundletool.** `--install` on an Android App Bundle (`.aab`) builds a
  set of `.apks` for the connected device with Google's bundletool, then installs them. bundletool
  is usually found automatically: on `PATH` (for example after `brew install bundletool`), or
  bundled with the installed .NET Android SDK workload (so most MAUI setups already have it); pass
  `--bundletool <path>` to point at a specific copy. It also needs a Java runtime (a JRE on `PATH`
  or `JAVA_HOME`); a message says how to get either one if it's missing — Swipewalk never
  downloads bundletool itself. Without `--keystore`, the generated `.apks` are signed with the
  standard Android debug key (`~/.android/debug.keystore`, the same one Android Studio and Gradle
  use). If that file doesn't already exist, Swipewalk creates it with `keytool`, using the same
  standard settings Android Studio and Gradle use — since bundletool itself won't: its own `--ks`
  help says plainly that without a keystore, "the APKs will not be signed", and it does not create
  the file itself. Fine for scanning, but different from your store build's signing key, so it's never a
  substitute for a release build. Pass `--keystore <path> --keystore-alias <alias>` to sign with
  your own key instead; set the `SWIPEWALK_KEYSTORE_PASSWORD` environment variable first
  (`SWIPEWALK_KEY_PASSWORD` too if the key's own password differs — rare in practice, since a
  PKCS12 keystore, the default format since Java 8u60, doesn't reliably support a key password
  different from the keystore's own) — a keystore password is never taken on the command line or
  put in `swipewalk.json`, only passed to bundletool through a private temporary file. Installing
  over an app that's already installed with a different signing key fails (Android requires a
  matching signature to update an app); uninstall it first (`adb uninstall <package>`), or pass the
  matching `--keystore` — the error message says so.
  `swipewalk.json` has the same options: `bundletoolPath` (top level) and, under
  `app.android`, `keystore` and `keystoreAlias` (`keystore` needs `keystoreAlias` too, and the
  `SWIPEWALK_KEYSTORE_PASSWORD` environment variable at run time — never put a password in the file).
- **Xcode version mismatch when building a MAUI app for iOS.** If your installed .NET for iOS
  workload pack expects an older Xcode than the one you have (for example the pack expects Xcode
  26.5 but you have Xcode 27.0), the build fails on the version check. Build with
  `-p:ValidateXcodeVersion=false` to skip it — this doesn't affect Swipewalk itself, only building
  the app you're about to scan.
- **`--result-bundle`.** An iOS option for testing Swipewalk itself: it forces the physical-device
  capture path (result-bundle attachments) even on a Simulator. Everyday scans don't need it — the
  right path is chosen for you.

### Saving a diagnostic report for a bug report

When a scan fails, or Swipewalk closes unexpectedly, a diagnostic report tells whoever helps what happened, without
you describing it all by hand. It is one plain text file made on your computer from Swipewalk's local log. Nothing is
sent anywhere, and no device is touched.

```
swipewalk diagnostics --out swipewalk-diagnostics.txt
```

`--out` takes a file name or a folder; without it the file goes in the current folder as
`swipewalk-diagnostics-<date>-<time>.txt`. In the desktop app choose **Help > Save Diagnostic Report…**, pick where
to save it, and Swipewalk says the file is saved, with **Show in Finder** and **Done**. If the app closed
unexpectedly last time, the Dashboard shows a short notice, "Swipewalk closed unexpectedly last time. Save a diagnostic
report?", with **Save report…** and **Not now**. It is offered on the Dashboard once for that close: either choice means you aren't asked
about it again, and nothing is saved or sent unless you choose Save report…. The Help menu item stays available.

What the report holds:

- the versions of Swipewalk, your system, `adb`, Xcode and Java (each says "not found" when it isn't there);
- the last pre-flight results Swipewalk logged, with the time they were checked. The report does not run the
  checks again (that touches your devices); to get fresh ones, run `swipewalk doctor --platform android|ios` (or
  choose Check on the Devices page) first, then save the report;
- the last five scans or recordings: platform, app id, framework, which options were on, how many screens, and the
  counts of WCAG issues, items to review and platform advisories, or why it stopped;
- the log lines from the last 24 hours (at most 2,000): what ran, warnings and errors.

It never holds screenshots or the contents of `results.json`, and Swipewalk doesn't write text from the screens you
scanned to it (labels, values, what TalkBack or VoiceOver said; a screen's name in a progress message is replaced by
"…"). Error messages are kept as the tools gave them.

Swipewalk removes device serial numbers and IDs, the names people gave their devices (the model is shown instead),
Apple team IDs, signing identities, your user name, your computer's name and your home folder, from every line before it is written
and again when the report is made. It keeps the package or bundle id of each app you scanned (for example
`com.example.app`), because a fault is hard to diagnose without it, and the names of app files and folders you gave
Swipewalk (your home folder shows as `~`) and the error messages the tools it runs give. The removal works by pattern, so it can miss
something: **read the file, and remove anything you don't want to share, before you attach it** to a bug report;
reports usually end up on a public GitHub issue.

The log lives in `~/Library/Logs/Swipewalk` on macOS, `%LOCALAPPDATA%\Swipewalk\Logs` on Windows and
`~/.local/state/swipewalk/logs` (or `$XDG_STATE_HOME/swipewalk/logs`) on Linux: one file a day (more if a day's log passes 4 MB). Files more than a week
old are deleted, and older files are removed to keep the folder near 10 MB (checked when Swipewalk starts and once a
day). The command line and the desktop app write to the same
files. Delete the folder at any time to remove the log. By default the log holds the standard detail, with no commands
recorded except where an error message names the command that failed. Setting the environment variable
`SWIPEWALK_LOG=debug` in the terminal before running a command also records the tool commands Swipewalk runs (adb, xcrun,
xcodebuild and others), with their arguments, exit codes and how long each took, with the same removal applied (the
VoiceOver captions capture, used by `--voiceover-captions` and the iPhone VoiceOver session, is not yet included); the desktop app opened from Finder doesn't
see a shell variable, so it writes the standard detail. The report says when its period includes debug lines. `SWIPEWALK_LOG_DIR` names another folder for the log (a full path), for CI and
scripts; the command line then writes there and `swipewalk diagnostics` reads from there, while the desktop app opened from
Finder keeps the folder above. If the log can't be
written (a read-only disk, say), Swipewalk carries on without it.

## 9. Privacy and reporting wrong findings

Swipewalk runs entirely on your computer: no accounts, no analytics, no telemetry, and it makes no
network requests of its own. Scan output goes to the folder you choose and to a local run history
on your machine; nothing is sent to the Swipewalk authors or anyone else. The Android accessibility
harness (Google's Accessibility Test Framework) ships prebuilt, so it needs no network access
either — only a maintainer's own harness build (`--android-harness`) uses Gradle instead, which does use the network (see
[PRIVACY.md](../PRIVACY.md)). See PRIVACY.md for exactly what's stored, where, and what the platform
tools (`adb`, `xcodebuild`, `devicectl`) may do on their own.

Results save the device's model and OS version, not its serial number or the name you gave it. When an
iPhone's model can't be read, a name that looks like an Apple model name (such as "iPhone 16 Pro") is kept;
any other shows as "iOS device". A run saved by an earlier version that holds the name you gave an iPhone is
cleaned once, in the run history, the first time it is read; copies you exported or saved with `--out` are not
changed, so delete or re-export those yourself (details in PRIVACY.md).

On Android, screenshots blank the status bar by default, since it can show notification text and
other personal information; pass `--keep-status-bar` if you specifically need it left in the
screenshot and are sure it won't show anything sensitive.

Swipewalk also keeps a short, scrubbed log of what it did on your computer, and can save it as a diagnostic report to
attach to a bug report; see [Saving a diagnostic report for a bug report](#saving-a-diagnostic-report-for-a-bug-report)
and [PRIVACY.md](../PRIVACY.md). Nothing in it is sent anywhere.

Found a false positive, a missed issue, or a wrong WCAG mapping? Please report it — that's the most
useful kind of feedback right now. Use the "Wrong or missing finding" issue form on the
[Swipewalk issue tracker](https://github.com/swipewalk/swipewalk/issues/new/choose), with a screenshot
and the app's framework if you can.

Want to help improve the list? If something about how a law or standard is mapped to WCAG looks wrong or
is missing, check the official source linked in its details (choose its name on the desktop app's **Laws and
standards** page, or see [docs/standards.md](standards.md)). Then use "Suggest a correction" there,
"Suggest a law or standard" for one that isn't listed, or the "Help improve Swipewalk's law mappings" issue form on the
[Swipewalk issue tracker](https://github.com/swipewalk/swipewalk/issues/new/choose). Include a link to the
official text that shows the correct information. The issue is public on GitHub.

## 10. Exporting findings

`swipewalk export` turns a saved run's findings into something you can hand to an issue tracker or a
colleague, without opening the HTML report:

```bash
swipewalk export <run> --format md --out tickets           # one Markdown ticket per finding, plus images/
swipewalk export <run> --format csv --out findings.csv     # one row per finding, for bulk import
swipewalk export <run> --format html --out shared.html     # the whole report as one file, for email
swipewalk export <run> --format pdf --out report.pdf       # a tagged PDF (PDF/UA-1), for printing or sharing
swipewalk export <run> --format acr --out acr-draft        # a draft Accessibility Conformance Report, for a buyer
```

`<run>` is a run folder or a `results.json` file (a run folder from `swipewalk history`, or whatever
you passed to `--out` for a one-off scan). All five formats read the same saved data; pick the one
that fits where the findings are going:

- **`--format md`** writes one self-contained Markdown file per finding — pasteable directly into a
  Jira, GitHub Issues or Azure DevOps description — plus an `images/` folder of evidence screenshots
  shared by all of them (a cropped close-up of the element and the full screen, each named after the
  finding they belong to). One file per finding, not one combined document, so each ticket stays
  small enough to paste as-is. Each ticket has: a title naming the element and the problem; whether
  it's a WCAG issue, needs review, or a platform advisory (never a pass/fail verdict); the WCAG
  criterion, level and W3C link, and a "Laws and standards" line naming the first few the finding is relevant to and how many more
  (for example "Relevant to ADA Title II, Section 508, EN 301 549 and 37 more", with a link to
  docs/standards.md), or with `--standard`
  "Relevant to: <standard>" or "Not within the WCAG basis of <standard>" — always stated as relevance,
  never as a claim that the app complies; where it was found (app id and version, platform, device, screen — the app version is
  detected from the device at capture time, Android's versionName/versionCode or iOS's
  CFBundleShortVersionString/CFBundleVersion; shown as "not available" for a run where it couldn't be
  read, an older run recorded before this existed, or an app with no version information); the element
  (role, label, node path, bounds); the evidence (the finding's own message, plus screenshots unless
  `--no-screenshots` is given); a "Likely same cause as N other findings" line, with their ids, when
  the report's "Likely same root cause" grouping (see above) puts this finding in a group; who it
  affects, in plain language; how to fix it, in the app's detected framework; an acceptance criterion
  you can turn into a checklist item; and provenance (scan date, Swipewalk version, ruleset version,
  rule id, and the finding id — see below).
- **`--format csv`** writes one row per finding with plain column headers (finding id, a `GroupId`
  column — filled in only when "Likely same root cause" grouping put this finding in a group,
  otherwise blank — title, kind, rule id, WCAG criteria, platform guideline, the ids of the laws and standards it's relevant to (all mapped ones, or just the `--standard` one), app
  id/version/platform/device/screen, element details, the finding's message, provenance, and the
  manual-testing caveat repeated on every row) for bulk import into Jira, Azure Boards or GitHub. The
  `AppVersion` column is left empty (rather than "not available") for a finding where it couldn't be
  read, since that reads cleanly to most CSV importers. A CSV importer has nowhere to put an
  attachment, so this format never includes screenshots — `--no-screenshots` has no effect on it. A
  cell whose text starts with `=`, `+`, `-`, `@`, a tab or a carriage return (a real app label could be
  any of these) is prefixed with a single quote so Excel, Google Sheets and LibreOffice Calc show it as
  plain text instead of running it as a formula (the quote stays in the value, so an importer such as
  Jira shows it too); numeric columns (like a bounding box's X/Y) are never affected by this, even when
  negative.
- **`--format html`** writes the whole report — summary, findings by screen, coverage,
  limitations and run details, exactly as the report you already saw after a scan — as one file with
  screenshots embedded and no separate assets, so it can be attached to an email or message. This is
  the same self-contained file `report.html` already is; export just lets you regenerate it (with or
  without screenshots) for a run you saved earlier. `--finding` doesn't apply to it (there's no
  single-finding shape for a whole report) and is rejected if given together with `--format html`.
- **`--format pdf`** writes the "What to fix" view of the report (the default view, findings and fix
  guidance -- not the full audit detail) plus the WCAG 2.2 coverage summary, as a single PDF: page
  breaks between screens, evidence screenshots sized to fit, page numbers, a "Likely same root cause"
  section, who each finding affects, and the same manual-testing caveat as every other export. Like
  `--format html`, it always writes the whole report, so `--finding` is rejected with it too. The PDF
  itself has a real tagged structure (headings, lists, a table for the coverage summary), a document
  title and language, alt text on every screenshot, and bookmarks for each screen, each root-cause
  group and each finding listed individually, targeting PDF/UA-1 and checked with the [veraPDF](https://verapdf.org/) checker (see `docs/accessibility-statement.md` for exactly what was
  checked and what isn't covered yet). It needs nothing installed: the PDF is built entirely by a
  bundled .NET library ([PDFsharp](https://www.pdfsharp.net/), MIT-licensed), with the same Open Sans
  typeface as the rest of the report embedded in the file, plus three more embedded fonts (Devanagari,
  Arabic, and common-use Japanese) so text in those scripts draws correctly instead of disappearing --
  this covers common-use Japanese, not Chinese: a Chinese character outside what it happens to
  share with Japanese is not covered. A character in none of the bundled fonts is shown as `?` with a
  note at the end of the PDF, rather than silently dropped. Arabic text is shown right-to-left for a
  sighted reader, but a screen reader gets the correct reading order too (see the accessibility
  statement). Works the same way on macOS, Windows and Linux.
- **`--format acr`** writes a DRAFT Accessibility Conformance Report — for a buyer evaluating the
  app, unlike the other four formats above, which are for developers — as two files in a folder
  (default `./swipewalk-acr`; the run needs at least one finding, like every other format): `openacr.yaml`, in
  the [OpenACR](https://github.com/GSA/openacr) format (checked against its `2.5-edition-wcag-2.2-en`
  WCAG catalog with OpenACR's own validator), and `acr-draft.html`, a readable copy of the same content
  clearly marked **"Draft: for human review"**. Both files carry the same statement, at the top of
  `acr-draft.html` and at the start of `openacr.yaml`'s `notes` field:

  > This is a draft for you to review and complete. Swipewalk sets a conformance level only where automated checks confirmed a WCAG issue ("Partially Supports"); every other criterion, including ones where automated checks found nothing or only flagged items for review, is marked "Not Evaluated". Whoever publishes the report is responsible for its statements.

  One row per WCAG 2.2 Level A/AA success criterion (55
  rows; Level AAA isn't evaluated and isn't listed; Section 508's and EN 301 549's own requirements
  beyond WCAG aren't included either — see the "Beyond WCAG" section, shown in the full report when it
  includes Section 508 or EN 301 549, for what Swipewalk currently tracks about a few of them). Every
  row's conformance level is decided the same way as everywhere else in Swipewalk — never a verdict of
  "compliant" or "passes". This release doesn't record manual test results (that's a separate, later
  feature), so today every row reads either **Partially Supports** (a confirmed automated WCAG issue)
  or **Not Evaluated**; the rules below for **Supports** and **Not Applicable** describe what a
  recorded manual result would do once that exists:
  - **Supports** only ever comes from a person recording a pass, with evidence, for every scanned
    screen the criterion applies to (this release doesn't record manual results, so no row reads
    Supports yet).
  - A confirmed WCAG issue (or a person recording a failure) gives **Partially Supports**, with the
    finding id(s) named in the remark; a person finishing the report may lower this to **Does Not
    Support** if the problem turns out to be widespread, but Swipewalk itself never assigns that level
    automatically (automated checks can't tell how much of the app's functionality is affected, only
    that some is).
  - Automated checks flagging something for review, without confirming a failure, give **Not
    Evaluated**, not Partially Supports (an item flagged for review isn't a confirmed issue).
  - Automated checks running and finding nothing also give **Not Evaluated**, with a remark saying so
    explicitly — a 0 count is never shown as "met" (see the coverage section above). This draft
    deliberately uses "Not Evaluated" for Level A and AA rows, not only AAA (the usual convention),
    because no person has evaluated these criteria and automated checks cover only part of each one;
    this is called out in the draft's own notes, not left for a reader to notice.
  - **Not Applicable** only ever comes from a person confirming, for every scanned screen, that the
    criterion doesn't apply, with no confirmed WCAG issue contradicting it — Swipewalk's own "usually
    not applicable" (WCAG2ICT) reasoning is a general note, not a per-app confirmation, so a criterion
    like this reads Not Evaluated by default.
  - A finding the team marked false positive is excluded from the count behind a level, but the row's
    remark says so and names it; a finding marked won't fix or accepted risk still counts (it's still a
    real, open issue), with the same "this does not mean the issue meets WCAG" caveat as everywhere
    else.

Options:

- **`--finding <id>`** exports only that finding (md/csv only); repeat it for several. Without it,
  every finding in the run is exported. An id you gave that doesn't match anything prints the list of
  ids actually in this run, so you can copy the right one; ids also appear in each exported ticket's
  Provenance section and the CSV's `FindingId` column. `--finding` has no effect with `--format html`,
  `pdf` or `acr` (there's no single-finding shape for any of them) and is rejected if given together
  with one of those formats.
- **`--no-screenshots`** leaves screenshots out (`md`: no `images/` folder; `html`/`pdf`: nothing
  embedded; `csv` and `acr` never include them regardless). There's no privacy warning here — like the
  rest of Swipewalk, a test device is assumed — this option exists only to make a smaller export.
- **`--standard <id>`** shows one law or standard only, in the html, pdf, csv and md exports: a
  default standard's id (`ada-title-ii`, `section-508`, `en-301-549`, `en-301-549-v4`,
  `uk-public-sector`) or a US state or country id from [docs/standards.md](standards.md). Which laws
  and standards each finding is relevant to is worked out again from its WCAG criteria, so the same
  saved run can be exported for a different law without scanning again. Without it, an export keeps
  what the run was scanned with: all mapped laws and standards, unless the scan itself named one. In
  the html export each issue's "Relevant to …" line opens to the full list; the pdf
  has no expandable sections, so it lists every law and standard once, grouped by region, near the
  start (with its source and date checked) and each issue names the first few and how many more and
  points there, because repeating the whole list under every issue would run to pages; a Markdown ticket
  carries the same short line with a link to docs/standards.md; the csv's relevant-standards column
  holds the ids. `--format acr` ignores `--standard`: an ACR is written per WCAG criterion, not per law.
- **`--my-laws <ids>`** lists the laws and standards that matter to you first, in the html, pdf, csv and
  md exports: comma-separated ids, the same ones `--standard` takes (for example
  `--my-laws ada-title-ii,us-tx`). Each issue names your laws it is relevant to, then "and N more"
  for the rest (in the html export, opening an issue's line shows the full list, yours first); the laws table lists yours first
  and folds the others behind "Other laws and standards"; the pdf and csv put yours first. Nothing is
  left out. Without it, an export keeps the choice the run was scanned with (`--my-laws` on `scan` or
  `record` saves it in the run), if any; with `--standard` only that one law is shown. `--format acr`
  ignores it.
- **`--app-name <name>`** (`acr` only) sets the product name shown in the report; without it, the
  scanned app's own package/bundle id is used, or "Scanned app" if even that isn't known.
- **`--author-email <email>`** (`acr` only) sets the contact email OpenACR's own format requires for
  the report's author. Without it, an obviously fake placeholder is used and the draft's notes say it
  must be filled in before the report is shared — Swipewalk never fills this in automatically from your
  git config, OS user name or any other local identity, since this file is meant to leave the machine
  that generated it.
- **`--author-name <name>`** (`acr` only) sets a contact name alongside `--author-email` (optional in
  the OpenACR format itself).

Every finding gets a stable id (`SW-` followed by a short fingerprint of the app, screen, rule,
element and kind it was found as) that stays the same across a rescan of an unchanged screen, so
exporting the same run twice — or exporting after a rescan that still finds the same issue — reuses
the same id instead of creating a duplicate ticket. Every export also repeats Swipewalk's own
caveat: found by automated checks, manual testing is still required.

A screen reader session (see [section 5](#5-record-mode-and-the-large-text-check)) is exported the way a scan
is: every screen it visited appears in the tagged PDF, the shareable report and the combined reports, and its
evidence items (where what TalkBack said differs from what Swipewalk predicted, or doesn't include the
control's visible text) appear as findings in the tickets and the CSV. Those items carry their own
caveat instead of the one above — evidence from a session a tester drove, compared by Swipewalk, not an
automated check and not a confirmed failure — and a screen the session reached but Swipewalk never scanned
says so plainly. The draft Accessibility Conformance Report is built from the automated checks only, so
it doesn't include a session's evidence items.

### Combining several runs into one report

A single scan or recording rarely covers every screen you care about, and you may have scanned the
same app version more than once (a first pass, then a follow-up after navigating further). To build one
report covering every screen across several saved runs of the same app **version**, use `--app`,
`--app-version` and `--run` instead of a run folder:

```bash
swipewalk history                                            # find the app id, version(s) and run ids
swipewalk export --format acr --app com.example.app --app-version "1.2.0 (42)" \
  --run 20260901-090000-com.example.app --run 20260902-090000-com.example.app --out acr-draft
```

**You choose exactly which scans go in — nothing is combined by default.** `--run` is required and can
be repeated; leaving it out prints every saved run of that app and version with a one-line summary of
each (date and time, device, number of screens, and its issues by kind — WCAG issues, needs review,
platform advisories — plus how many are triaged, when a triage file exists for it), so you can see what
you'd be choosing between. `--app-version` can be the literal text `"version not recorded"` to select
runs where the version wasn't recorded at all — exactly as `swipewalk history` labels that group. Leaving
out `--app-version` prints the versions actually recorded for that app id.

**This report covers only the screens in the scans you give it — Swipewalk can't tell which screens your
app has, so make sure the scans you choose cover what you need.** This is information only: Swipewalk
never blocks or warns about which scans you pick, and never compares your choice against other saved
scans to suggest something is "missing".

If two of the given runs captured the **same screen**, Swipewalk does not guess which one to use — you
choose, every time: on an interactive terminal, it asks per screen, listing each run's date and issue
counts; in a script or CI (or wherever input or output isn't a terminal), give `--use <screen>=<run-id>`
for every such screen instead (repeat it for several), or the command lists every screen that still needs
a choice, with its options, and exits without writing anything. The finished report's own text names
every run it's based on and, for any screen captured more than once,
which capture was used — and states plainly how many screens it covers, so it doesn't suggest the report
covers the whole app. It also repeats any run's own note that it ended early (a recording stopped by an error or
cancel) or has no recorded end (a screen reader session saved while it ran and never stopped), named by run. A
screen reader session's screens are included in a combined report like any scan's screens, each with what the
session recorded on it; the session's own header (who, how long, sittings, and notes not tied to a screen) is not, so open that run's own report for it.
Triage marks from every combined run's own `triage.json` are carried in too (a finding
keeps the same id across runs of an unchanged screen, so the most recent decision for it wins regardless
of which run recorded it).

All five formats (`md`, `csv`, `html`, `pdf`, `acr`) work with combined runs — each screen's screenshots
are resolved against its own originating run's folder, so there's no single shared folder problem to
work around. `--finding` and `--source` aren't available in this mode: a combined export always covers
the whole report. The desktop app has the same combined export from History's own version-header row
(see below), with a list of that version's runs to tick instead of `--run <id>`.

### Exporting from the desktop app

The desktop app exports the same five formats using the same code, from the two places that export a
whole run (Report page → Export… and History → Export…); the Findings list's per-finding export stays
CSV/tickets only, same as before (see below — a chosen subset of findings has no per-criterion shape
for an ACR). Choosing the ACR format shows three optional fields, the desktop counterparts of
`--app-name`, `--author-name` and `--author-email`, on both the single-run export and a version's own
export: **App name**, **Author name** and **Author email**. Leave one empty and the draft falls back as the
CLI's does (the app's id for a single run, or its name as shown in History for a version's export; no author name, and a placeholder contact the draft's notes say to fill
in). Nothing is taken from your Mac and nothing is remembered between exports, as with the CLI. The
draft is still a draft for you to review and complete; whoever publishes the report is responsible for its
statements.

**Laws that matter to me.** An export lists the laws saved with the run first (or yours, when the run saved none). When the
run's saved choice differs from the one you have now, the export page also shows **Use my current laws and
standards instead of the saved choice**, with a line explaining it; tick it to list your current laws first instead, the
desktop counterpart of `swipewalk export --my-laws`. A version's own combined export has the same choice when any of its scans
saved a different choice. The saved run is never changed, and the draft ACR ignores the choice, as it ignores the law or standard.

- **Report page → Export…** exports every finding in the run you're viewing: choose a format
  (report as HTML, report as PDF, CSV, tickets, or a draft Accessibility Conformance Report),
  optionally leave out screenshots, then choose where to save. A report (HTML or PDF) or CSV goes
  through the system's own save panel; tickets and the ACR draft's two files go into a folder you
  choose, under a subfolder named after the run — exporting the same run's tickets into the same
  folder again reuses that subfolder and updates any ticket whose finding id is unchanged, rather
  than creating a second copy (the same "re-exporting reuses the same id" behavior as the CLI,
  described above).
- **Law or standard (Report page → Export…, History → Export…, the Findings list's export and a version's own Export…)** — the desktop
  counterpart of `swipewalk export --standard`, in the same grouped menu as New scan, with the same **Search…** button beside it for finding an entry by name or place. It starts on the
  law or standard the run was scanned with (All standards unless the scan named one; for a version, the one
  its newest run was scanned with, the same as `swipewalk export --app`). If the run was scanned with a law
  or standard that this version no longer lists, the menu shows All standards and a line says so. Choosing another works out
  again which laws and standards each issue is relevant to from its WCAG criteria, so nothing is scanned again and the saved run is unchanged. It applies to the report (HTML or
  PDF), CSV and ticket exports. The draft Accessibility Conformance Report is written per WCAG criterion, so
  for it the choice is disabled, with the reason below it.
- **Optional team triage file (Report page → Export…, History → Export… and the Findings list's export)** — the desktop counterpart of
  `swipewalk export --triage <file>`: the marks in this file are used INSTEAD of the run's own `triage.json`
  (the two are never merged), for every format. Left empty, the run's own marks are used, as before. Marks you save from the Report page while a team file is chosen are written into that file, not into the run's own `triage.json`.
- **Optional app source folder (Report page → Export…, History → Export… and the Findings list's export)** — the same field as
  New scan's, the desktop counterpart of `swipewalk export --source`: choose the project folder and the
  export (report, CSV or tickets; not the ACR draft, which has no per-finding lines) fills in a likely
  source line for findings that don't already have one, without rescanning. The saved run itself is
  never changed. If the folder can't be read, the export stops and says so (the command line instead warns and exports without source lines). Combining several scans into one report (History's version rows) has no source folder
  field, the same as `--source` not being available with `--app`.
- **Report page → Findings list** — a native checkbox list of every finding, grouped by screen
  (shown or hidden with the Findings list button, so it never gets in the way of just reading the
  report). It exists because the report itself is a self-contained web page in a `WebView`, and giving
  each finding its own real, separately reachable actions there reliably would need a fragile bridge
  into HTML generated elsewhere — a native list avoids that. Each row has its own two actions:
  - **Copy as ticket** puts that finding's Markdown ticket on the clipboard as text. Screenshots are
    never included (there's no way to put an image file on the clipboard as Markdown); if the
    finding has one, the copied text says where the screenshot file is instead of silently leaving no
    trace it exists.
  - **Save ticket…** writes that finding's ticket file and its screenshots to a folder you choose.

  A "Select all" checkbox at the top and a "Select all on [screen name]" checkbox per screen group
  check or uncheck every finding under them in one step. With one or more findings checked, **Export
  selected…** exports only those, as a CSV or as tickets (a subset has no single-report shape, so the
  whole-report formats aren't offered here).
- **History → Export…** exports a past run without opening its report first. For a recording that's
  still in progress, this exports whatever has been saved so far (results.json is updated after every
  screen), and the Export dialog notes that the run is still recording — but the exported report, CSV
  or tickets themselves don't currently carry that note, so a file exported this way doesn't yet say
  by itself that it's a partial run.
- **History → a version's own Export…** — see [Combining several runs into one report](#combining-several-runs-into-one-report):
  every run of that app version is listed, each with a one-line summary (date, device, screens, issues
  by kind, and how many are triaged when available) — **none is ticked to start**; tick the ones you
  want, pick a format, and choose a destination folder. Never decides which runs "count" on its own,
  the same as the CLI's `--app`/`--app-version`/`--run`. If two ticked scans captured the same screen,
  an overlap section lets you choose which capture to use for it — Export stays disabled until every
  such screen has a choice.

A run with no findings still exports: automated checks found nothing, and manual testing is still
required, the same as for any other run — the exported report (HTML or PDF) shows its normal
headline stating that automated checks found 0 WCAG issues, the CSV has its column headers with no
data rows, and the ACR draft is still a full 55-row report (mostly "Not Evaluated"), never an empty
file. There's nothing to write as tickets in that case, so choosing tickets shows a note and the
Export button stays disabled, rather than writing an empty folder. A run's CLI export
(`swipewalk export`) matches this for `--format html`, `--format pdf` and `--format acr` — all three
always write the whole report, findings or not — but refuses for the per-finding formats,
`--format md` and `--format csv`, when the run has no findings (or `--finding` matched none of the
ids given): there's nothing to put in a ticket or a CSV data row.

## 11. Triage: mark a finding as already looked at

A team can mark a finding as **false positive**, **won't fix** or **accepted risk**, each with a
required reason, so the report and exports stop treating it as an open action item — without ever
saying it's fixed, and without changing the underlying scan data.

```bash
swipewalk triage <run> --finding SW-abcdef123456 --status wont-fix --reason "Third-party control; tracked upstream."
swipewalk triage <run> --finding SW-abcdef123456 --clear                    # remove the mark
swipewalk triage <run>                                                      # list this run's current marks
```

`<run>` is a run folder or `results.json` file, same as `swipewalk export`. Finding ids are the same
`SW-…` ids `swipewalk export` prints — copy one from a report, a CSV, a ticket, or from running
`swipewalk triage <run>` with no `--finding` (it also prints the ids of every finding actually in the
run if you give one that doesn't match).

- **`--status <false-positive|wont-fix|accepted-risk>`** what the team decided: **false positive** —
  this isn't really an issue; **won't fix** — a real issue, not being fixed for now; **accepted risk**
  — a real issue the team is knowingly leaving in place. For won't fix and accepted risk (both leave a
  real issue in the app), everywhere the mark is shown also states: "This does not mean the issue meets
  WCAG or any other standard -- the team has chosen not to fix it for now." Required together with
  `--reason`.
- **`--reason <text>`** why — required. Triaging a finding with no reason is refused.
- **`--by <name>`** who made the decision, as free text (a name, a username, an email); optional.
- **`--app-version <v>`** the app version this decision was made against; optional, purely
  informational — Swipewalk records the scanned app's own version per run automatically (see
  [section 4](#4-reading-a-report)), but doesn't fill this field in for you from that.
- **`--clear`** removes the current mark from `--finding`. Its history is kept, not deleted — see
  "stale" marks below.
- **`--triage <file>`** reads and writes marks in this file instead of the run's own `triage.json` —
  for example a file your team commits alongside `swipewalk.json` and shares across every run of the
  same app, rather than one run's own folder. `swipewalk run` picks up a committed
  `swipewalk-triage.json` next to `swipewalk.json` automatically (or `"triageFile"` in `swipewalk.json`
  for a different name/location); `scan`/`record` accept `--triage <file>` too, to seed a fresh run
  with it before anything is captured.

A mark is **never written into `results.json`** — it's stored in its own file, next to it, so a
rescan's raw findings are never touched. Setting or clearing a mark re-renders that run's `report.html`
immediately (no rescan needed) and keeps a full history: an earlier mark on the same finding is never
silently overwritten, only superseded by a later one.

What changes once a finding is triaged:

- **The report** moves it into a collapsed "Triaged by your team" group on its screen (in both the
  "What to fix" and "Full audit detail" views), stating who marked it, what as, why, and when — e.g.
  "Marked as false positive by the team: *(your reason)* (2026-09-26)". A screen's own finding groups
  then count only its open findings (the triaged ones moved to that screen's own "Triaged by your
  team" group — the two always add back up to the screen's total). The headline and the summary tiles
  always state the full count automated checks found first — the same number the Dashboard and the
  coverage tables show — then the open/triaged split, e.g. "Automated checks found 7 WCAG issues
  (4 open, 3 triaged by your team)". A triaged finding is never subtracted out of the total silently.
- **`swipewalk compare`** never treats a triaged finding as "new" or "no longer found" just because
  it's triaged — it stays in whichever bucket it's already in (including the new "Still found:" list,
  printed alongside "New" and "No longer found"), with the mark shown alongside it, so a teammate
  reading the comparison doesn't act on it as if nobody had looked. `compare` reads triage marks from
  the *later* run's own `triage.json` (or `--triage <file>`, to use another file instead).
- **Exports** carry the mark too: the CSV gets `TriageStatus`/`TriageReason`/`TriageBy`/`TriageDate`
  columns (and, for won't fix or accepted risk, the WCAG caveat above appended to the `Note` column —
  there's no separate column for it); a Markdown ticket gets its own "## Triage" section near the top;
  the self-contained HTML export and the PDF export both show the same open/triaged headline split and
  the same "Triaged by your team" group the in-app report does. `swipewalk export` also accepts
  `--triage <file>` to read marks from another file instead of the run's own `triage.json`. For a mark
  the desktop app carried forward from an earlier run (see below), `TriageDate` is the ORIGINAL decision
  date, not the moment it was carried, and the `Note` column adds "Triage mark carried forward from an
  earlier run of this app."
- **A stale mark** — one whose finding id no longer matches anything in the current run (the element
  changed enough that its id changed, the check didn't run this time, or the screen wasn't scanned) —
  is shown in its own short list rather than dropped silently, both in the report and in
  `swipewalk triage <run>`'s listing.

Finding ids are computed from the app, screen, rule, the element's path and role, its kind, appearance
and orientation — not from a run-specific id that changes every scan (see `swipewalk export`'s own
notes on finding ids). Rescanning an unchanged screen reproduces the same id, so a mark carries over
*when the same triage file is read for both runs* — either a project file named with `--triage <file>`
(or picked up automatically by `swipewalk run`, see above), or by continuing the same recording
(`record --continue`) into the same run folder. A brand new scan or recording started from the CLI
still begins with its own empty `triage.json` unless you pass `--triage <file>` or `--carry-triage` (see
below), so marks from an earlier, separate run don't carry over by default there. If a screen's layout changes enough to move
the element's position in the tree, its id changes too, and an older mark for it shows as stale rather
than silently applying to a different element.

### Carrying marks forward (desktop app, and `--carry-triage`)

A new scan or recording started from the **desktop app** for the same app and platform automatically
carries forward the marks that are currently set on that app's most recent previous run in History, so
a finding that can be matched with confidence doesn't need to be triaged again after a rescan. This uses
a second, more forgiving match than the finding id above (app, platform, rule, kind, appearance/
orientation and the element's own identifier — or its role and name when it has none, or its role and
position in the tree as a last resort when it has neither) — a layout change alone doesn't lose a mark;
but it never guesses, and each of the following is reported separately in the log line printed after the
scan, with a count, so you know which of your marks to check by hand:

- The screen the mark was on couldn't be confidently matched in the new run: either no screen looks like
  it (not scanned this time, or changed too much to recognize), or more than one screen matches it, so
  it couldn't be told which one the mark belonged to.
- No finding on that screen matches the mark's element in this run — most often because the issue is
  simply no longer found (fixed, or the check that reported it didn't run this time); the element itself
  may still be there, just not flagged, or it may have been renamed or removed. Nothing is carried when
  more than one element matches the same mark either, since it's then unclear which one it was about.
- A **false positive** mark is not carried if the judged element's name, role or measured value has
  changed since it was marked (for example a contrast ratio that's now different, or an element that was
  renamed) — the earlier "this isn't really an issue" judgement no longer says anything about the new
  one. Won't fix and accepted risk still carry in that case, since those are decisions to leave a real
  issue in place regardless of its exact measurement or name — but the report notes when the name, role
  or measured value has changed since the mark was made, since a triaged finding is otherwise out of
  "What to fix".
- A mark cleared in the earlier run is not carried (there's nothing current to carry), and a mark you set
  or clear directly in the CURRENT run is never overridden by an older one (this matters for
  `record --continue`, which resumes the same run).
- This never applies across a different app or platform.
- Contrast and other pixel-measured values rarely read back byte-for-byte identical between two captures,
  so a **false positive** mark on one of those findings may often not carry even when nothing really
  changed — check the log line and re-mark it if so.

Marks only move one run at a time — from the single most recent previous run into the new one. If a
screen isn't scanned in a given run, its marks stay on the earlier run they were last carried into and
are not carried forward again into a later run; open that earlier run to review them. A previous run's
marks set later, directly against a project `--triage <file>` from the CLI (`swipewalk triage --triage
<file> ...`), aren't in that run's own `triage.json` and so have nothing to carry forward — but marks a
CLI scan/record was SEEDED with at the start (`scan`/`record --triage <file>`, or `swipewalk run`'s own
`swipewalk-triage.json`, see above) are copied into that run's own `triage.json` and do carry forward
from it like any other mark.

A carried mark is written into the new run's own `triage.json`, so it behaves exactly like any other
current mark from then on — clear or re-mark it the same way (`swipewalk triage`, or the **Triage…**
button in the desktop app's Report page findings list). The report shows it distinctly from a mark made
directly against this run, for example: "Marked in the run from *(original date)*: *(status)* —
*(reason)* (marked by *(name)*). *(WCAG caveat, for won't fix/accepted risk)* Carried forward from an
earlier run of this app; you can clear or re-mark it." On the command line the same step is opt-in: add
`--carry-triage` to `scan` or `record` and, once the run is saved to History, Swipewalk carries the
previous run's marks into it with the same rules and the same messages as above (it needs History, so
not with `--no-history`, `--from` or `--continue`, and it is not part of `swipewalk run`). Without the flag a CLI run
keeps the `--triage <file>`/`swipewalk run` behaviour described above. There is currently no setting to
turn this off for one run in the desktop app.

When a scan in the desktop app is given a **Team triage file** (New scan, the same as `--triage <file>`), that
file is applied first: its marks become the new run's marks, the same as the CLI seeds them. The carry-forward
from the previous run then only fills in findings the file says nothing about (a finding the file marks or
clears is never overridden). The report lists any mark in the file that matches nothing in the new run, in its
"stale" list.

## 12. Finding the likely source line (.NET MAUI, native Android, native iOS, React Native, Flutter)

`--source <path>` points `scan`, `record` and `export` at the app's own project folder, so a finding can
show where in your own code the element likely comes from — e.g. "Source (exact match):
`Views/LoginPage.xaml:42`" — instead of only what the running app looked like. This is a 0.4 feature,
supporting **.NET MAUI**, native **Android Views** and **Jetpack Compose**, native **iOS** (storyboards/
XIBs, UIKit and SwiftUI), **React Native** and **Flutter** so far. Which mapper runs is picked
automatically from each screen's own detected framework (see [section 4](#4-reading-a-report)) — you
never need to say which kind of project `--source` points at: a screen detected as MAUI, on either
platform, uses the MAUI mapper; an Android screen detected as native Views or Jetpack Compose, or not
detected at all, uses the Android mapper; an iOS screen detected as UIKit or SwiftUI, or not detected at
all (iOS framework detection can't reliably tell UIKit and SwiftUI apart), uses the native iOS mapper; a
React Native or Flutter screen, on either platform, always uses its own mapper. A hybrid-web
(WebView-filling) screen matches none of them and is left unchanged. A MAUI app on a physical iPhone needs
`--install` or `--framework maui` for detection to work at all — otherwise its findings are sent to the
native iOS mapper instead, which usually finds nothing in a MAUI project and reports "not found" (unless
the MAUI project also happens to contain native `.swift`/`.strings` files, e.g. under `Platforms/iOS`).
Pass `--framework uikit` or `--framework swiftui` yourself when you know which one a native iOS app is and
detection left it Unknown — this mainly affects the report's framework label and fix examples, not which
mapper `--source` uses, except that the native iOS mapper's merged-SwiftUI-group step (below) is skipped
on a screen marked UIKit.

```bash
swipewalk scan --platform android --package com.example.app --source ~/src/MyMauiApp
swipewalk record --platform ios --bundle-id com.example.app --source ~/src/MyMauiApp
swipewalk scan --platform android --package com.example.app --source ~/src/MyAndroidApp  # native Views or Compose
swipewalk scan --platform ios --bundle-id com.example.app --framework uikit --source ~/src/MyNativeApp
swipewalk export <run> --format md --source ~/src/MyMauiApp   # add source locations after the fact
```

Everything under the given path is read **locally only** — nothing is uploaded, sent anywhere, or
written back to it, and no project is built, installed or run (no MSBuild, no Gradle, no `dotnet build`/
`restore`, no npm, no `pub get`). For a **.NET MAUI** project it only reads `.xaml`, `.cs` and `.resx`
files (skipping `bin`/`obj`); for a **native Android** project it only reads layout `.xml` under a
`layout`/`layout-*` folder, the default `res/values/strings.xml`, `.kt`/`.java` files, and
`build.gradle`/`build.gradle.kts` (only to read the app's `applicationId` or `namespace`, for the
identifier check below — skipping Gradle's `build`/`.gradle` output otherwise); for a **native iOS** project it reads `.storyboard`, `.xib`, `.swift`,
`.m`, `.strings` and `.xcstrings` files, skipping build output and dependency folders such as
`DerivedData`, `Pods`, `build`, `.build`, `.git`, `.swiftpm`, `xcuserdata` and `Carthage`; **React Native**
reads `.js`/`.jsx`/`.ts`/`.tsx` and every `.json` file under the folder except `package.json`,
`package-lock.json`, `tsconfig.json` and `app.json` (see the React Native "Text" step below for why this
is looser than it sounds); **Flutter** reads `.dart` and `.arb` translation files. All of these skip
common build/dependency/version-control folders (`bin`, `obj`, `node_modules`, `build`, `dist`, `.git`,
`.dart_tool`, `.pub-cache`, `.expo`, `.gradle`, `coverage`, `Pods`). Every framework's own files are always
looked for under the same `--source` folder, so you don't need to say which kind of project it is —
pointing it at the wrong folder, or one without any of these files, simply finds nothing rather than
failing the scan.

### When a match is never "exact" (native Android, iOS, React Native and Flutter)

A label found in only one place is still not proof that this element is the one, so in these cases Swipewalk
never shows a single "exact" line: it lists candidates, or at most a "likely" match with the reason. For the
first two, the element's own identifier settles it:

- a very common or very short label (for example "Done", "Cancel", "Home", or a two-letter code), which many
  unrelated controls share (the common words are recognized in English only);
- a literal in code that also appears as a localized string value (strings.xml, string catalog, translation
  file), since the element may be using either;
- on Android, an identifier found in more than one place (an `android:id` in more than one layout, written `@+id/x` or `@id/x`, or a Compose `testTag` literal used more than once) is always listed as candidates, even though it is the element's own identifier;
- on iOS, text that is also design-time text in a storyboard or XIB (a likely match, with the reason).

On iOS, a string defined as a `Type.member` accessor (for example `static let title = NSLocalizedString(...)`)
is followed to the code that uses it as `Type.member`; when no such use is found (other ways of referring to it
are not followed), the result says it points at the definition. A candidate list from a text or identifier
match shows at most 10 places and says how many matched. When the same React Native or Flutter translation key
is in several language files, the text from a file whose name marks it as English (for example `en.json`,
`app_en.arb`) is used for the key; English in a folder name (`en/translation.json`) isn't recognized. Flutter
also matches literal `tooltip:`, `semanticLabel:` and `semanticsLabel:` arguments (text with `$` interpolation
is skipped).

### How a match is made, and how sure it is (.NET MAUI)

Matching tries these, in order, stopping at the first that finds something:

1. **Identifier** — the element's own `AutomationId`, if it has one, found as a literal in XAML or
   code-behind.
2. **Text** — the element's accessible name/label, found as a literal `Text=`/`Title=`/
   `SemanticProperties.Description=` in XAML or code-behind, or via a `.resx` string resource (the
   resource's own value matches the label, and the place that *uses* that resource key is reported —
   not just the `.resx` file itself, unless no use of it could be found).
3. **Structural** — only for an element with no name at all (a `missing-name` finding): matched by its
   size (`WidthRequest`/`HeightRequest`), an adjacent `<Label>` next to an unlabeled field, or being the
   one bound, unlabeled element of its kind inside a repeating list template.

### How a match is made, and how sure it is (native Android Views and Jetpack Compose)

Matching tries MAUI's three techniques in the same order, plus a Compose-only template step, with
different source shapes underneath:

1. **Identifier** — the element's `resource-id` (from an `android:id="@+id/..."` in layout XML), or a
   Jetpack Compose `testTag(...)` — but only when the app opts into
   `Modifier.semantics { testTagsAsResourceId = true }` on that element or an ancestor, since Compose only
   reports a `testTag` as the element's resource-id (the identifier Swipewalk reads) when that's set;
   without it, a Compose element has no identifier to match at all and this step is skipped, falling
   straight through to text matching. An identifier match is never trusted when it's Android's own
   framework id (`android:id/title`, `android:id/icon`, ...) — a short id name alone (`title`, `icon`, ...)
   isn't enough, since the app and the framework can both declare a view with the exact same short name, and
   this check doesn't depend on knowing the app's own package. Separately, a match is also never trusted
   when the id's package names a genuinely different app (an embedded WebView, a second process, a
   misidentified multi-module build) — checked first against the app's own package as actually detected
   from the scan being mapped (the most reliable source: a uiautomator `resource-id` package is the app's
   actual installed `applicationId`. Verified with a real build installed on the Android emulator (not yet
   on a physical phone): a single-module sample app (NativeAndroid) given `applicationIdSuffix` on
   its debug build type, so its installed `applicationId` differed from its Gradle `namespace` — every
   app-owned `resource-id` reported (every one outside Android's own `android:` package) carried the
   suffixed `applicationId`, never the plain `namespace` the source declares), falling back to the
   project's own `applicationId` (or `namespace`) read from `build.gradle`/`build.gradle.kts` only when the
   real scanned package isn't known; that source-only fallback reads only the base `applicationId`/
   `namespace` and does not apply a build type's `applicationIdSuffix` or a product flavor's override, so
   for such a build it rejects the app's own ids and reports "not found" rather than a wrong line
   (confirmed for that single-module suffixed build; multi-module and product-flavor projects remain
   untested). This does *not* catch a library dependency's own id (for
   example one of AppCompat's or Material's): Gradle merges a library's resources into the app's own
   package at build time, so a library id is indistinguishable from the app's own at runtime, and a short
   name the app happens to share with one can still resolve "exact". When an id's name is explicitly
   cleared in layout XML (`android:contentDescription`/`android:text` set to `@null`, and nothing else on
   that element names it either) and the finding's runtime name is still non-empty — a sign the name comes
   from somewhere other than that cleared attribute, most likely Kotlin/Java code — the match is only kept
   as a fallback: text matching (step 2) is tried first, in case it lands on the exact code line; if it
   doesn't, the id match is used, but as "likely", hedging that the name may be set in code instead of
   asserting it. When the runtime name is empty instead (a `missing-name` finding), nothing overrode the
   clearing, so the id match stays "exact" as usual — the layout line really is where to add one.
2. **Text** — the element's accessible name/label, found as a literal in the source (`android:text`/
   `android:contentDescription`/`android:hint` in layout XML, or `text`/`contentDescription`/`hint`
   assignments and Compose `Text("...")` calls in Kotlin/Java), or via a `strings.xml` value — the
   resource's own value matches the label, and the place that *uses* that resource key is reported, not
   just the resource file itself, unless no use of it could be found. A layout attribute explicitly
   cleared (`android:contentDescription="@null"`) is not treated as a name: if Kotlin/Java code sets a
   literal or resource-backed name later at runtime, that assignment is indexed as its own literal and a
   finding with that runtime (overridden) label can resolve to it — not the XML's `@null` (a name built
   from computed/bound data at runtime still comes back "not found", the same as anywhere else).
3. **Compose testTag template** — tried only when steps 1 and 2 found nothing: the longest
   `testTag("prefix${...}")` template fixed prefix the element's identifier starts with (the shape a
   shared/repeated composable typically uses, for example `"card_${item.id}"` on every row of a list) —
   this can only ever be "likely" (noting the one line probably produces many runtime elements like it) or
   "candidates" (when more than one template shares that same longest prefix), never "exact". Tried after
   text matching, not alongside identifier matching, so a unique literal label can still resolve "exact"
   even when the identifier also happens to start with some template's prefix.
4. **Structural** — only for an element with no name at all (a `missing-name` finding): matched by its
   declared size (`android:layout_width`/`_height`, or a Compose `Modifier.size(...)`/`.width(...)`/
   `.height(...)`), or an adjacent label next to an unlabeled field (a preceding `<TextView>`, or the
   nearest preceding Compose `Text(...)` call before a bare `TextField(...)`).

### How a match is made, and how sure it is (native iOS)

Matching tries, in order:

1. **Identifier** — the element's own accessibility identifier, found as a literal in Swift/Objective-C
   code, or (rarely) set from Interface Builder's own Identity Inspector — like step 4, an identifier set
   from Interface Builder is never shown as "exact", even as the only match.
2. **Text in code** — the element's accessible name/label as a literal in Swift/Objective-C (including
   `NSLocalizedString` and similar macros, and SwiftUI's own `Text`/`Button`/`Label`/`Toggle`
   initializers).
3. **A `.strings`/`.xcstrings` catalog entry** — the label matches a catalog *value*, resolved back
   through its *key* to wherever that key is used in code (or, failing that, to the catalog entry
   itself). Catalog values are matched in the base language only; a scan on a device set to another
   language won't match them.
4. **Storyboard/XIB markup** — a *lower-confidence fallback only*, since a storyboard/XIB's own
   design-time text is frequently a decoy a developer left in Interface Builder for previewing layout,
   overwritten in code via the outlet's own property; a match here is never shown as "exact", even when
   it's the only place the text appears.
5. **Structural** — for an unnamed *UIKit* element with no literal anywhere (a `missing-name` finding):
   matched by its declared size or an adjacent `UILabel`, only for elements created in Swift code
   (`let x = UIButton(...)`) — elements laid out in a storyboard/XIB are not matched this way. This step
   doesn't know which screen (or even which framework) actually produced the finding it's mapping, only
   whether a matching declaration exists anywhere in the given source, so on a project with more than one
   screen it can point at a *different* screen's element (see `swipewalk limitations`,
   `ios-source-mapping-scope`, which shows this happening for real in NativeiOS). SwiftUI has no
   equivalent here at all, since its views are rarely named local variables — an unnamed SwiftUI element
   is "not found" (item 6 below needs a label to go on, so it never applies to an unnamed element).
6. **A merged SwiftUI group** — tried only for a finding whose label looks combined from several views
   (contains ", "), on a screen that's SwiftUI or has an undetected framework (never one confirmed UIKit
   or MAUI, since this technique is SwiftUI-specific): if the given source has exactly one SwiftUI
   `.accessibilityElement(children: .combine/.contain)` grouping, it's offered as "likely" the place the
   name may be assembled. This is not a proposed literal, and not necessarily this screen's own grouping
   — there may be no single line that produces the exact announced text, for example when it's built from
   several pieces plus live data. With more than one such grouping in the source, the result is "not
   found" rather than a guess between them.

### How a match is made, and how sure it is (React Native and Flutter)

Both are read lexically (pattern matching over the raw file text), never with a real TypeScript or Dart
parser — a literal built from string concatenation, a spread prop, or any other construct outside what's
listed below simply isn't indexed, not guessed at.

**React Native** matches, in order:

1. **Identifier** — the element's own `testID`, if it has one, as a literal.
2. **Templated identifier** — the identifier as the runtime value of a dynamic, per-row `testID` built
   from a template literal with a fixed prefix, e.g. `` testID={`feedItem-by-${id}`} `` — some real apps
   build per-row identifiers this way. The exact runtime value can never be predicted from source, so
   this is always reported "likely", never "exact"; a prefix shorter than 3 characters isn't indexed as
   an anchor at all (too easy to match the wrong thing).
3. **Text** — a literal `accessibilityLabel="..."` or plain JSX `<Text>...</Text>` content, or via a
   translation key: every `"key": "value"` string pair found in any `.json` file under the folder (except
   the config files named above) counts as a possible translation, at any nesting depth and by its own
   key name only — so a non-translation JSON file (mock data, a native asset manifest) can occasionally
   point at the wrong file, and a nested TypeScript i18n module or a gettext `.po` file (both real,
   seen in real apps) are out of scope and simply aren't indexed.

**Flutter** matches, in order:

1. **Identifier** — `Semantics(identifier: '...')`, as a literal.
2. **Text** — a literal `Text('...')`, `Semantics(label: '...')` or `Tooltip(message: '...')`, or a `.arb`
   translation key, read the same lexical, any-depth way as React Native's JSON above.

**A translation-key match (React Native or Flutter) is always shown as "likely", never "exact", even
when only one use site is found** — the "used from" site is any call/reference naming that key found
anywhere in the file (React Native: `t()`/`translate()`/`{id: ...}`; Flutter: a `.keyName` member access
on anything), a real match but a looser filter than a literal string match, unlike MAUI's own `.resx`
technique (which also checks the resource class name before the dot).

Neither framework has MAUI's structural fallback (size/adjacent-label/template matching for an element
with no name at all) — an unnamed, unidentified element is always reported "not found" here. A Flutter
widget `Key`/`ValueKey`/`ObjectKey` is never treated as an identifier or a name, even if its literal
value happens to match a finding: a `Key` identifies a widget to Flutter itself (and to Flutter's own
test finders such as
[`find.byKey`](https://api.flutter.dev/flutter/flutter_test/CommonFinders/byKey.html)), but it is never
passed to the platform accessibility tree, so TalkBack/VoiceOver never see it either — a match against
one is reported "not found", with a reason explaining the likely mix-up rather than a silent guess. The
same ambiguity rules as MAUI apply when the same literal/identifier appears in more than one place
(screen co-occurrence narrows it to "likely", or "candidates" when nothing stands out). Both mappers have
so far only been tested against small, hand-written fixtures, not a real React Native or Flutter app —
see `swipewalk limitations` for the full picture (`reactnative-flutter-source-mapping-scope`).

### Confidence levels

On every framework, every result carries one of four confidence levels, shown plainly next to the
location rather than folded into a single "found it" indicator:

- **Exact** — a literal or identifier found in exactly one place in the given source, *except* where the
  sections above say otherwise: a native iOS storyboard/XIB match (text or an Interface Builder
  identifier), a Compose `testTag` template, a React Native templated `testID`, a React Native/Flutter
  translation-key match, or an Android identifier whose name is cleared with `@null` while the app still
  announces one at runtime are never shown as "exact", even when they're the only match found. For an
  exact match, the "How to fix" code example for that finding is repeated next to the location, in the
  app's own framework (for example Kotlin or Android XML for Jetpack Compose and Views, Swift for UIKit or
  SwiftUI, Dart for Flutter, JavaScript for React Native, HTML, CSS or JavaScript for hybrid web, XAML or C#
  for .NET MAUI), as an example
  to *adapt* — Swipewalk has not read or changed that line itself, and never proposes an edit from a
  guess. When the framework was not identified (or this check has no example for it), the example is the
  platform's neutral one (Android XML or Kotlin, or iOS Swift in UIKit) and is labelled "Generic example fix"; it is
  never assumed to be .NET MAUI. See `other-framework-fix-examples` in `swipewalk limitations`.
- **Likely** — a best guess, not a literal match to one unambiguous spot: either the same literal was
  found in more than one place and one file stood out (by which file shares the most of the screen's
  other finding labels), or the line was found structurally (matching size, an adjacent label, or a
  template), never from the literal runtime text. Worth checking before you change anything there.
- **Candidates** — more than one line is plausible and none stood out (the same text appears in two
  files with no file standing out, the winning file still has the text on more than one line, or
  several unlabeled elements of the same kind exist); every candidate is listed, none is picked for you.
- **Not found** — no line in the given project could be identified. Common causes: the element's name
  comes entirely from bound/computed data with no literal anywhere nearby (including a value that only
  the running app computed), it's built from a third-party control or a framework-injected element (for
  example MAUI Shell's own flyout button, or an AndroidX `Toolbar`'s default "Navigate up" back-arrow
  description) that has no line in *your* project at all, a Compose app that never carries a runtime
  identifier because it doesn't set `testTagsAsResourceId`, or `--source` points at the wrong folder.

**On a real, unmodified app, expect "likely"/"candidates"/"not found" often, not rarely — especially for
MAUI and for Jetpack Compose.** A hand review of real .NET MAUI sample apps' source (not written for
scanning) found none used `AutomationId` at all; a list or grid bound to data (a very ordinary screen
shape) usually has no literal text to match, so its findings come back "not found" unless the element has
no name at all, in which case size/label/template matching gives "likely" or "candidates" instead. Native
Android Views apps usually give an `android:id` to every view their code touches (`findViewById` and view
binding both need one to reach it), so identifier matching is expected to fire more often than for MAUI or
Jetpack Compose — this hasn't been measured on a real app either, only reasoned from what compiling
against a view requires. Jetpack Compose is the opposite case — identifier matching only works at all when
the app opts into `testTagsAsResourceId`, and even then a shared tag with no per-item value still lands on
one line for many elements. The Kotlin/Java side of Android matching is careful lexical scanning of specific
patterns, not a full parser, so it's conservative by design. On native iOS, a hand review of several real,
unrelated apps found a storyboard/XIB's own design-time text is frequently a decoy — the shipped
text is assigned in code via the outlet's own property — and every SwiftUI app reviewed had at least one
label whose text comes from data at runtime, with no literal anywhere in the source to point at.
Text/resource matching is expected to handle a static screen's toolbar, settings and form fields well on
any of these frameworks — but that, and each mapper as a whole, has so far only been tested against
Swipewalk's own sample apps (`BuggyApp` for MAUI, `NativeAndroid` for Views/Compose,
`NativeiOS` for UIKit/SwiftUI), not a real third-party app; Compose `testTag` matching hasn't been
tested on a real capture at all yet (the sample doesn't set `testTagsAsResourceId`). See
`swipewalk limitations` for the full picture (`maui-source-mapping-scope`, `android-source-mapping-scope`,
`ios-source-mapping-scope`; `reactnative-flutter-source-mapping-scope` for React Native and Flutter).

`swipewalk export --source <path>` fills in a source location for a finding that doesn't already have
one from the scan — useful when a run was scanned without the source at hand and it's since been
found — without ever rewriting the run's own `results.json`.

With a source folder, a run's `results.json` also records the folder's name in `sourceRootName` (the name
only, never the full path), so a tool that reads the file can tell which folder each source line is
relative to. Each finding also carries its stable id, who it affects and its anchor in `report.html`; see the
[results.json reference](results-schema.md).

## 13. Sharing a run

A saved run can be sent to someone else as one file, so a tester can hand a developer what they found, or
a team can keep runs in their repository. The file ends in `.swipewalk` and is a plain zip with a fixed
layout. The person who opens it needs Swipewalk, but not your device or your computer.

### Share a run

```bash
swipewalk share latest                                   # the newest finished run in History
swipewalk share <run> --out ~/Desktop/                   # a run id from `swipewalk history`, or a run folder
swipewalk share <run> --no-screenshots --no-triage       # leave those out
swipewalk share <run> --shared-by "Sam (QA)"             # an optional note saying who sent it
```

`swipewalk share` prints the path of the file it wrote. The name is the app, its version and the date, for
example `Example-App-1-2-0-20260930.swipewalk`: letters, digits and dashes only, with no device name and no
user name. What goes in:

- The run's results (`results.json`) and the run's record. There is no report in the file: whoever opens it gets
  a report drawn by their own Swipewalk. For someone without Swipewalk, use the HTML export
  ([section 10](#10-exporting-findings)) instead.
- The screenshots the results refer to, unless you pass `--no-screenshots`. Screenshots show what was on the
  screen when they were taken, so look at them before you send the file; with `--no-screenshots` the findings
  are the same; the picture paths are removed from the results, and the report the other person's Swipewalk
  draws has no pictures.
- The run's triage marks ([section 11](#11-triage-mark-a-finding-as-already-looked-at)), unless you pass
  `--no-triage`. Each mark includes the reason entered with it; the name entered with a mark (the "marked by" text,
  often a person's name or email) is left out of the shared file.
- `--shared-by` is free text you type, blank by default. It is shown to the person who opens the file, always introduced as
  what the sender wrote ("Sender wrote: …"), never as a fact about who they are.
- Sharing never replaces a file that already has the chosen name: the command stops and says so, and `--overwrite`
  replaces it. (The desktop app writes the file where you choose in the system save panel, which asks before it replaces one.)

What stays out: source code (for a run scanned with `--source`, the file names and line numbers the findings
point to stay in), the app itself, device serial numbers and the device's own name (runs save the device model and the OS version; when the model isn't known, an iOS device name is kept only if it starts with "iPhone", "iPad" or "iPod" and has no apostrophe (such as "iPhone 16 Pro"; a name someone typed that follows that pattern would be kept), otherwise it shows as "iOS device" or "iOS simulator"; an Android name is the maker and model), the location
of the run's folder, and the files a recording keeps to continue later. Where a message in the results quotes
the run's folder or your home folder, that part is replaced with `.` or `~`. Every text saved in the file (in the results, the run's record and
the reasons of triage marks) also goes through the same search the diagnostic log uses: device ids, Apple team names and ids, signing identities, names such as "Alex's
iPhone" are replaced with placeholders, and the user and computer names Swipewalk knows are replaced in messages, notes, reasons and other free text (names that are part of an identifier, such as an app id like `com.alex.app`, stay as they are, because the file's other parts refer to them). That search works by pattern and can't be complete, so look at
the report before you send the file. Only real picture files that sit inside the run's own folders are included; a path that leaves the
folder, a link, a file that isn't a picture or one over the size limit is left out and the command says how many. An older run in your own History folder whose Swipewalk screenshots were kept elsewhere has them copied inside its folder first, when the folder can be written. A picture that is missing on this computer isn't in the file. The run's record and the file's
listing give times in UTC; times inside the results (for example when a recording session started) and the run
id keep the sender's local clock time. Apart from bringing an older run's pictures into its folder (see the paragraph about pictures in "Open a file someone sent you", below), sharing doesn't change the run on your computer.

The file is not encrypted: anyone who has it can read everything in it. It is signed, unless you pass
`--unsigned`, with a key that Swipewalk creates the first time you share a signed run:

- On a Mac and on Linux the key is a file only you can read (created with mode 0600, like an SSH key; administrators of the computer can still read it): on a
  Mac `~/Library/Application Support/Swipewalk/signing-key.p8` (not in the History folder, even if you moved
  History), on Linux `~/.config/swipewalk/signing-key.p8` (or under `$XDG_CONFIG_HOME/swipewalk`). The command line
  and the desktop app on the same computer use the same file, so you have one key. The file is not encrypted:
  anyone who can read your user files or your backups can sign as you. If a restore, a copy or an archive tool left the file readable by
  other users, Swipewalk sets it back to owner-only before it uses it and says so when you share; treat the key as exposed if others could read it.
  If the file is damaged or can't be read, Swipewalk refuses to
  share and says why; it never makes a new key over it. To start over, move or delete the file: you then get a new
  key and people have to trust it. On Windows the key is protected for your user account with DPAPI. The Windows
  and Linux key storage has not yet been tried on those systems.
- Setting the `SWIPEWALK_HOME` environment variable to a folder keeps the signing key (on Windows still protected with DPAPI) and the
  list of trusted keys in that folder instead. That is meant for build machines and containers.
- A signature shows that the file has not changed since it was signed and which key signed it. It does not
  show which person made the file: confirming the key's full fingerprint with you tells the other person the
  key is yours, not who used it. If you lose the key or move to a new computer, you get a new key and people
  have to trust it again.

### Open a file someone sent you

```bash
swipewalk import Example-App-1-2-0-20260930.swipewalk    # check it, then add it to History
swipewalk import file.swipewalk --dry-run                # only check it
swipewalk import file.swipewalk --yes                    # open it without being asked (see below)
```

The file is checked before anything is added to History: its manifest, its version, every file against its
listed size and SHA-256, the file names, and limits on size (the file size limit is 200 MB; `--max-size 500`
raises it to 500 MB, and nothing goes above 2048 MB), on the number of files (5,000), and on files that expand
far more than a normal run does. If anything is wrong, nothing is imported and the command says why. Nothing in the file is ever run, and the report you see is always drawn again by your copy of
Swipewalk from the file's results, never taken from the sender's report. Text from the file (the app name, the
"shared by" note, messages) is shown as plain text, with invisible and direction-changing characters removed. Before the run is added, its results are
rewritten so that they hold nothing that points outside the run's folder: a screenshot path that is absolute or climbs out of the folder is removed (so an
export of an imported run can never copy another file from your computer), the list of sources the results say they were checked against keeps only the ones this
Swipewalk knows, with this Swipewalk's own links, and the counts History shows are worked out from the results rather than taken from the sender's record.
`--dry-run` reads and checks every file in the archive, including every picture, without writing the pictures anywhere.

Pictures are read the same way for every run, whether it came from a file or from your own scans: a picture is used only if it is a real PNG file of reasonable
size inside the run's own folder (a link out of the folder is not followed; the VS Code extension also accepts JPEG). The one exception is described below. Swipewalk keeps each run's pictures there, listed relative to the folder; a scan replayed
from a saved capture with `--from` copies the capture's pictures into the run's `pictures` folder, and nothing else is ever copied in while a run is being saved or continued. An older run, or a `--from` scan made by an earlier version, may list pictures outside its folder.
The first time Swipewalk reads such a run from your own History folder (whenever it lists that History, as the desktop app's History, Dashboard and Compare and the command line's `history`, `compare` and `export --app` do, or when `swipewalk export`, `triage` or `share` or the desktop report page opens a run folder in it), if the folder can be written, it copies the pictures that look like Swipewalk screenshots
(a PNG named `screenshot.png` beside a `tree.json` or `uiautomator.xml` file, as in a capture folder) into the run's `pictures` folder, rewrites the run's `results.json` to list them relative to the folder, and sets `picturesInFolder` in its `run.json`; the originals stay where they are. A listed picture that exists but isn't one of those is removed from the run's list;
one that can't be found is left as listed, and the run is looked at again the next time Swipewalk starts (each time, only a limited number of outside paths are checked). Either way, Swipewalk no longer uses it in anything it draws or exports from then on (a report page saved earlier still shows what it showed when it was saved, until it is redrawn). When `swipewalk export` reads a run that hasn't been brought inside yet, it says how many pictures it left out; `swipewalk share` says how many it left out of the file. If the folder can't be written (a read-only volume, say), nothing is written, and those capture pictures are used where they are, until Swipewalk is closed.
Your own History folder is the default one, or, in the desktop app, a folder you chose and said isn't shared; the command line counts only the default folder (a different folder given with `--history` doesn't count). Nothing is brought in from elsewhere, and no file is written, for a run opened from a shared file,
for a folder whose `run.json` is missing, unreadable or marked as opened from a shared file, for a folder that isn't directly in your own History folder, for a History folder marked as shared in the desktop app, or for a different folder given with `--history`; such a run simply shows only the pictures inside its folder.
Because of the older-run step, a results folder you received some other way (copied or emailed, unzipped by hand, committed to a repository) and then put into your own History folder by hand, with a `run.json`,
could make Swipewalk copy a PNG picture named `screenshot.png` that sits beside a `tree.json` or `uiautomator.xml` file elsewhere on your computer (normally a screenshot from one of your own scans) into that run, and a later export or share would include it (never any other kind of file). Open received runs with `swipewalk import` instead. A link inside History or inside a run folder is not followed: History doesn't list a run reached through a linked folder, and a run's results, triage marks and pictures are read only when they are regular files inside the run's own folder, with no link on the way.
A run opened from a shared file can't be continued.

The command prints the run id, the app and version, when it was shared and what the sender wrote (if anything). It then prints one
line about the signature. Exactly one of these appears. To see it before anything is added to History, run with `--dry-run` first:

- "Signed by a key you trust."
- "Signed by a key listed in your team keys file (the file's path). You didn't trust this key yourself." The signature matched and the key is only in an `accessibility/team-keys` file, not on your own list.
- "Signed, but the key (fingerprint XXXX-XXXX) isn't trusted yet. The file matches its signature." For a key you don't trust yet, the command also
  prints its full fingerprint in groups of four, to compare with the sender before running `swipewalk keys trust`.
- "Not signed. Nothing shows which key made this file or whether it has changed since."
- "The file doesn't match its signature, so it may have been changed after it was signed." (the signature doesn't match the file's listing, the signature file is damaged, or it claims a signature version or algorithm that a file from this version of Swipewalk could not have)
- "Signed in a format this version of Swipewalk can't check. Treat it as not signed." (only when the file says a newer Swipewalk made it and its signature is in a format this version doesn't know; it can't be checked, so it counts as not signed)

For the last three (not signed, doesn't match its signature, or a format this version can't check), nothing shows that the file is unchanged since its sender made it, so the command
stops first: "Nothing shows this file is unchanged since its sender made it. … It was not imported." In a terminal it then asks "Import it anyway? The default is no."
(`y` or `yes` imports it). When no one can answer, such as in a script or a CI job, it opens nothing and exits with 4; `--yes` imports it
without asking. `--dry-run` never asks. A file signed by a key you haven't trusted yet opens without a question. The answer you give,
and how the signature checked, are saved with the run: History and the report show the signature line and the signer's full fingerprint.

Anyone can remove a signature (the file then shows "Not signed") or sign a changed file with their own key (it then
shows as not trusted), so only "Signed by a key you trust" says the file is unchanged since a key you checked signed it.
A key's fingerprint is the same every time that key signs, so anyone who receives several of your files can tell they were signed by the same key.

An imported run is someone else's scan: it is never picked automatically as the earlier run a new scan of yours is compared with, its
triage marks are never carried into your next scan, and the Dashboard's per-app cards and trends leave it out. You can still choose it by hand in Compare.

Opening the same run again changes nothing and says "A run with this id is already in History, so this file was not opened again." (what History
shows is the copy opened earlier, with that copy's own signature line). Exit code: 0 when the run was opened
(or was already there, or `--dry-run` found nothing wrong), 2 when the file is not a valid Swipewalk file (this
includes a file over the size limit), 4 when the file needs your confirmation and didn't get it (nothing was imported), and 3 when it was made with a newer Swipewalk that this one can't read; that
message names the version needed, for
example "This run needs Swipewalk 0.8.1 or newer. Update Swipewalk to open it." A file from a slightly newer
Swipewalk that this one can read opens with one calm note: "Made with a newer Swipewalk (0.9). Some details may
not show; update to see everything." A mistake in how the command was typed (for example a bad `--max-size`) exits with 1.

### In the desktop app

- **Share a run.** On a History row, or on the Report page, choose **Share run…**. A small sheet asks "Screenshots
  show what was on screen. Include them?" with a switch (on by default), shows **Include triage marks** only when
  the run has marks (on by default; switch it off to leave them out), and has a **Sign this file** switch (on by default; switch it off to share without a signature, as `--unsigned` does) and an optional **Shared by** field
  (blank by default). A line above Share gives an upper estimate of the file's size ("Up to …"); the file is
  compressed, so it is usually smaller. **Share** then asks where to save the file (the suggested name is the app, version
  and date), and afterwards offers the macOS share sheet for the saved file (Mail, Messages, AirDrop and so on).
  The sheet's last lines show **Your sharing key**: the full fingerprint of this computer's signing key in 16 groups of four (the same text `swipewalk keys show` prints), for reading to the people you share with; until you first share a signed run it says there is no key yet and that one is created then. Opening the sheet never creates the key.
  A recording that is still going can't be shared; the sheet says so. If Swipewalk had to note something while
  making the file (for example that it couldn't be signed), it shows that after you save, before the share sheet. Unless you switch **Sign this file** off, the file is signed like the command line's: the first
  time you share a signed file, Swipewalk creates a signing key and keeps it as a file only you can read in `~/Library/Application Support/Swipewalk`, the same key the command line uses, as described above.
- **Open a shared file.** Choose **File > Open…** (⌘O), double-click a `.swipewalk` file, drop one on the Dock icon,
  or drag it onto the History page. (The automated tests open a file the way a double-click does; a real
  double-click, a drop on the Dock icon, dragging onto History and opening a file from the keyboard through
  File > Open… have not been tried end to end yet; the menu item is checked to be there.) For a file that isn't signed, doesn't match its signature or has a signature this version can't check, a native
  alert asks first, "Import this file anyway?", with the reason, the app, what the sender wrote and the key's full fingerprint when the file names one; its buttons are
  **Don't import** (the default, also chosen by Escape) and **Import anyway**. History then shows a notice with what was opened, the signature line (one of the
  sentences above), any calm note about a newer Swipewalk, and, when the file is signed by a key you don't
  trust yet, **Trust this key…**. That button shows the key's full fingerprint in 16 groups of four and asks
  first; trust it only when the sender reads you the same fingerprint by another route. The opened run appears in
  History with the tag **Imported**, a line under it with the date and what the sender wrote, if anything, such as
  "Shared 2026-09-30. Sender wrote: Sam (QA)" (the sender's own text, not something Swipewalk checked), the signature line and the signer's full fingerprint. The same lines open the report (under its header); the row's spoken name includes the signature line (with the short key), and the full fingerprint is read from the report. A file that can't be opened shows one plain sentence, says nothing was imported and what to do; a file
  over the 200 MB limit offers to open it anyway (up to 2 GB), and Swipewalk still checks everything in it first. Opening a file you already
  have says "Already in History." and that this file wasn't opened again.
- **Sharing keys.** Choose **File > Sharing Keys…** (⌘K), or the **Sharing keys…** link in the Share sheet, to open a page with your sharing key's full fingerprint and the keys you trust, as `swipewalk keys show`, `list`, `trust` and `untrust` do on the command line. Select a key in the list and choose **Stop trusting this key** to remove it; to trust a key you were told about, paste all 64 digits of its fingerprint (spaces and dashes are fine; the short form is refused) and choose **Trust this key**, but only after the sender has read you the same fingerprint by a call or another route you already trust. A key trusted from the opened-file notice is saved without a name; one trusted on this page can have a name you type. Keys from a team file are trusted too, and aren't listed here because the page can't remove them: the page says when a team file is in use, and if a key you stop trusting is also in that file, it says the key is still trusted through it (`swipewalk keys untrust` says the same).
- **Team keys.** The desktop app also reads an `accessibility/team-keys` file in the History folder (it doesn't look in
  folders above it). A file signed with one of those keys shows "Signed by a key listed in your team keys file (the file's path). You didn't trust this key yourself.", a state of its own, not
  "Signed by a key you trust." Only use a team keys file in a folder whose writers you trust.
- **Imported runs.** They have no Continue button. A recording or session that ended early still says **Ended early**, in History and in the row's spoken name, and the report still says it; it just can't be continued. Each time you open an imported run's report, Swipewalk draws it again from the run's results, so an update to Swipewalk or a change to its text shows up (if that fails, it shows the copy drawn when you opened the file), and it is always drawn the safe way described above (also after a triage change). **Delete** removes only your copy in History; the file you opened
  isn't touched. In a History location marked as shared, the button is **Remove from list** instead (see
  [Where runs are saved](#where-runs-are-saved)).

### Imported runs

An imported run appears in History next to your own, grouped by app and version, with the tag "Imported". Who
sent it (if they said) and when is printed when you import it. It opens, compares and exports like any other run.
It is marked as received: it can't be continued (there is no device and no recording to go back to; a
recording that ended early still says so, in History and in the report), and
deleting it removes only your copy. Triage marks that came with it stay with that run; they aren't merged into
the marks of your other runs.

### Keys

```bash
swipewalk keys show                                      # this computer's signing key fingerprint
swipewalk keys list                                      # the keys you trust
swipewalk keys trust file.swipewalk --label "Sam"        # trust the key that signed this file
swipewalk keys trust <full fingerprint>                  # or trust a key by its full 64-digit fingerprint
swipewalk keys trust file.swipewalk --yes                # without being asked (for scripts)
swipewalk keys untrust <full fingerprint>
```

`keys trust` prints the full fingerprint and asks "Trust this key? [y/N]" (the default is no) before it adds anything. With no one to answer
(no terminal) it trusts nothing unless you pass `--yes`.

The short form `XXXX-XXXX` shown when a file is opened is only for recognising a key. Trusting is by the full
fingerprint (or by the file itself), which `swipewalk keys show` prints. Compare it with the sender by another
route, such as a call or a message, before you trust it. The desktop app signs with the same key and shows its full fingerprint, in the Share sheet and on File > Sharing Keys…. You can also
trust the key that signed a file (`swipewalk keys trust file.swipewalk`, or **Trust this key…** in the desktop app) once the sender has read you the
same full fingerprint by another route. A team can also commit a file named
`accessibility/team-keys` in its project: one full fingerprint per line, an optional label after it, `#` for
comments. `swipewalk import` and `swipewalk keys` look for it from the current folder up to the project's root (the folder that holds `.git`) and never above it; when the current folder isn't
inside a project, only the current folder itself is looked in. `swipewalk import` also takes `--team-keys <file>`.

Sharing a run you imported signs it with your own key; the file doesn't keep who sent it to you. A team can also
commit `.swipewalk` files to its repository; each person opens them with `swipewalk import`.

## 14. Seeing findings in VS Code

The Swipewalk extension for VS Code shows a saved run inside your editor: each finding that Swipewalk
matched to a source line is marked on that line, with a hover that has the problem, who is affected, the
WCAG criterion (with a link), how sure the match is and the suggested fix. It is a preview. Install it from the
[Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=swipewalk.swipewalk-vscode) (search "Swipewalk" in VS Code's Extensions view, or run
`code --install-extension swipewalk.swipewalk-vscode`). The `.vsix` is also attached to each
[release](https://github.com/swipewalk/swipewalk/releases); install it with "Extensions: Install from VSIX…".
The extension's own README, shown in VS Code's Extensions view, lists every command and setting.

What it does and doesn't do:

- **It only reads.** It reads a saved run (a `results.json` with the run's `run.json`, `triage.json` and
  `report.html` next to it, or a `.swipewalk` file) and your trusted-keys lists. It never changes your code or a run, apart from unpacking a shared file into its own storage; it never starts a scan, never contacts a
  device, makes no network calls and sends no telemetry.
- **Source lines need `--source`.** Scan with `--source <your project folder>` (section 12; on the command
  line's `scan` and `record`, or **App source folder** in the desktop app's New scan; `export --source` doesn't change the saved run, so
  the extension doesn't see those lines) so findings have lines. A run
  saved without it still opens, as a list, with no lines marked. Source lines are saved relative to that
  folder, so a run made on one computer is designed to open in a workspace on another; newer runs (results
  format 0.8) also record the folder's name, and if more than one workspace folder could be the project, or
  none matches, you are asked once and the choice is remembered.
- **Getting a run in.** Open your project folder, then run **Swipewalk: Open run…**. One list shows every run it
  can find, grouped by app and version, newest first, each labelled with where it was found (Desktop history,
  Workspace, Additional location or Opened file); nothing is chosen for you. It looks in Swipewalk's History
  folder (the default one; if you changed the location in the desktop app, set `swipewalk.historyFolder` to it), in your project's `accessibility/runs` folder (a convention for teams that commit runs, as
  `.swipewalk` files or unpacked run folders), and in any folders you list in the `swipewalk.additionalRunLocations`
  setting. A run found in more than one place is listed once: the copy with screenshots, then the most recently
  shared one. To open something the list does not show, choose "Choose a file or folder…" or run **Swipewalk:
  Open run from a file or folder…** and pick a `.swipewalk` file, a `results.json` or a run folder. While the
  `swipewalk.watchRun` setting is on (the default; a change applies the next time a run is opened), the open
  run reloads when its results.json or triage.json changes, for example a scan written to the same `--out`
  folder again, or a finding marked in the desktop app. A new scan saved to History is a new run: open it the
  same way.
- **Runs other people shared.** A `.swipewalk` file is checked before it is used: it is refused if it is larger
  than the `swipewalk.maxRunFileSizeMb` limit (200 MB by default, never more than 2 GB), unzips to far more
  than its size, has unsafe file names, or has any file that does not match the size and SHA-256 hash it
  lists. A file made by a newer Swipewalk opens with one short note when it can still be read, and is refused
  with the version you need when it cannot. It is unpacked into the extension's own storage, never into your
  project, and nothing in it is run; the unpacked copy, including any screenshots, stays in VS Code's storage for
  the extension until you run **Swipewalk: Remove opened copies of shared runs** (it asks first, and says how many
  copies and how much space; a shared run that is open is closed); opening the same file again reuses its copy and says "Already opened", and a different file with the same run id is unpacked beside the earlier copy, never over it. A shared file carries no report (one with an .html entry is refused), and the saved report of a run found outside your Desktop history is not opened from the
  extension (nor is the report of any run found outside your Desktop history); the findings list shows the same
  findings, and the full report is in the desktop app. The Run view shows one signature notice:
  "Signed by a key you trust", "Signed by a key listed in your project's team keys file", "Signed, but the key (fingerprint XXXX-XXXX) isn't trusted yet", "Not signed",
  "Signed in a format this version can't check. Treat it as not signed" or "The file didn't match its signature". For a file that isn't signed, doesn't match its signature or is signed in a format the extension
  can't check, a message asks first ("Import this file anyway?", with the key named in the file when there is one); **Don't import** (the first button), Escape, or closing the message opens nothing. A signature shows which key signed
  the file and that it has not changed since, not who the person is; check the full fingerprint with its owner,
  then trust it with `swipewalk keys trust`. Trusted keys are read from the per-user list that command keeps and
  from your project's `accessibility/team-keys` file, read only when you have trusted the folder in VS Code (Workspace Trust) and only from the top of each workspace folder (anyone who can change that file can add a key, so review
  changes to it like code); the extension only reads them. A key found only in the team file shows as its own notice, not as trusted by you. A screenshot in any run is shown only when it is a real picture file that sits inside the run's own folder; a path that points anywhere else is never shown. A run saved by an earlier version whose screenshots are still in their capture folders shows them once the desktop app or the command line has listed or opened it in your own History folder, if its folder can be written. Team marks in a shared file are shown read-only as
  "in the shared file". The extension's checks so far are unit tests, including the shared test files made
  by Swipewalk's engine; the integration tests in a real VS Code do not open shared files, and nobody has yet tried
  opening one by hand.
- **Markers say the kind of result, not how serious it is.** A possible WCAG issue at an exact line is a
  warning; everything else with a line (a likely match, an item that needs a person to review, a platform
  advisory) is information, so it is listed in the Problems panel too; nothing is an error. A *likely* match
  says "Likely match". A finding narrowed only
  to several *candidate* lines is not marked (none is known to be right); choose one from a list in the
  Findings view or on the details page. In the hover, a code example appears only for an exact match; for a guess, the details page keeps it behind "Show an example", worded as not a change for that line.
- **The Swipewalk view** has a Run section (which run, where it was found, when, for a shared file its signature notice and who shared it, and a reminder that Swipewalk only knows the
  screens in that run) and a Findings section you can group by screen, check, source file or who is affected,
  filter by kind, exact or likely match and team marks, and search by words. The details page shows the
  screenshot with the element outlined, the predicted screen reader text, the source location and the
  suggested fix, with Go to source and Copy ticket text (and Open in report for runs in your Desktop history).
- **Laws and standards.** The hover, the details page and the copied ticket text say which laws and standards a
  finding is relevant to, worked out from the list saved in the run, in the same words as the Swipewalk reports
  (for example "Relevant to ADA Title II, Section 508, EN 301 549 and N more"). The details page expands to the full
  list grouped by region with each official link, and lists the laws chosen when the run was saved ("Laws that matter
  to me" in the desktop app, or `--my-laws`) first within each region; nothing is hidden. It uses the list of laws saved
  in the run, as it was when the run was saved, so a report made later by a newer Swipewalk can show different counts. "Relevant to" means the finding's WCAG criterion is within the WCAG version and
  level the law or standard references; it is reference information, not legal advice. A run saved before the laws list
  existed shows no such line, and platform advisories and checks with no WCAG criterion have none. On the details
  page, a finding from the light and dark or portrait and landscape rescans says which it was seen in.
- **Team marks are read-only.** Marks made in Swipewalk (won't fix, false positive, accepted risk, with a
  reason) are shown, on a run saved in results format 0.8 for every marked finding, and on an older run only
  for findings that repeat across the run (the report and the desktop app show all of them); the extension
  never makes or changes one. Use the desktop app or `swipewalk triage`.
- **Remote windows.** The extension runs where your workspace runs. In a remote window, WSL or a dev
  container it reads that machine's History folder, so use **Open run from a file or folder…** on a copied file, or
  set `swipewalk.historyFolder`.
- **Limits.** Line numbers can be out of date if your source changed after the scan. The extension has been
  checked with automated tests only, on macOS: unit tests and nine integration tests in a real VS Code (version
  1.140.0, 2026-10-01; see the extension's README for what they cover). It has not been run on Windows or Linux,
  or tested with a screen reader, and no person has tried it in a VS Code window (see the
  [accessibility statement](accessibility-statement.md)).
