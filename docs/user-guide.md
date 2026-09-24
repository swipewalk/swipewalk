# User guide

This guide walks you through installing Swipewalk, scanning your first screen, reading a report,
and running scans in CI. It's for developers and QA on .NET MAUI and native Android/iOS teams. It
takes about 10-15 minutes to read.

Two things to keep in mind throughout: Swipewalk's automated checks find only some accessibility
issues, and a report — even a report with no findings — never means an app is "compliant",
"accessible" or "passes WCAG". Manual testing with a screen reader and other assistive technology
is still required. See [the disclaimer](../DISCLAIMER.md) for the full terms.

## Before you start: use a test device

Use a test device. On a physical phone, the large-text check temporarily changes the phone's text
size and restores it afterwards. If a run is interrupted, the next run or `swipewalk doctor` tries
to restore it, and says how to change it back by hand if it can't. Avoid running the large-text
check on a phone someone relies on every day. What happens:

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
- Emulators and the iOS Simulator have their text size changed the same way, but it's a virtual
  device setting, not something anyone's own phone depends on day to day.
- **`scan --appearance both`** switches the device between dark and light appearance the same way
  (Android `cmd uimode night`; iOS Simulator `simctl ui appearance`) to check both, then restores
  the original appearance afterwards — the same interrupted-run recovery applies. Not supported yet
  on a physical iPhone.

See [section 2](#2-set-up-a-device) and [section 5](#5-record-mode-and-the-large-text-check) for
the full detail on each platform.

## Contents

1. [Install](#1-install)
2. [Set up a device](#2-set-up-a-device)
3. [First scan](#3-first-scan)
4. [Reading a report](#4-reading-a-report)
5. [Record mode and the large-text check](#5-record-mode-and-the-large-text-check)
6. [CI and history](#6-ci-and-history)
7. [The desktop app](#7-the-desktop-app)
8. [Troubleshooting](#8-troubleshooting)
9. [Privacy and reporting wrong findings](#9-privacy-and-reporting-wrong-findings)

## 1. Install

Swipewalk runs on macOS and needs the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet tool install -g Swipewalk
swipewalk --version
```

Swipewalk doesn't include the platform tools it scans through — install the ones for the
platform you're testing:

- **Android:** the [Android SDK platform-tools](https://developer.android.com/tools/releases/platform-tools)
  (`adb`) and either a physical device or an emulator.
- **iOS:** Xcode. Swipewalk builds a small scanning harness on the first iOS scan; it ships with
  Swipewalk, so you don't need the app's source.

If you cloned the repository instead of installing the tool, replace `swipewalk` with
`dotnet run --project src/Swipewalk.Cli --` in every command in this guide (add `--` again before
the command's own options, e.g. `dotnet run --project src/Swipewalk.Cli -- scan --platform android`).

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
   check everything else (screen unlocked, app installed). The app doesn't need to be in front:
   Swipewalk brings it forward itself, or starts it if it isn't running, right before scanning.

### iOS: Simulator or iPhone

The Simulator needs no pairing — boot it from Xcode or `xcrun simctl boot <name>` and it's ready.

For a physical iPhone:

1. Connect it and pair it with the Mac in Xcode (Window > Devices and Simulators) if you haven't
   already.
2. Turn on Developer Mode: Settings > Privacy & Security > Developer Mode, then restart the phone.
3. Apple requires the scanning harness (not your app) to be signed. Swipewalk signs it
   automatically, in this order:
   - with a development provisioning profile you already installed (no Apple ID in Xcode needed);
     use `--profile <uuid|name>` to pick a specific one, or `--harness-bundle-prefix com.company.x`
     to fit a company wildcard profile;
   - otherwise with Xcode automatic signing, which just needs a free Apple ID signed in to Xcode.
   - With several Apple developer teams available, pass `--team <id>` once and it's remembered for
     next time.
   Your app itself is never re-signed — only the harness is.
4. The first scan or recording asks for Face ID or your passcode on the phone, to allow UI
   automation. Approve it; you won't be asked again.

`swipewalk doctor --platform ios --device <udid>` checks all of this and tells you exactly what to
fix if something isn't ready.

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

## 3. First scan

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
dark mode but had 5 real contrast failures on another device in light mode). Off by default; not
supported yet on a physical iPhone, where the report says why it was skipped.

When it finishes, open `report/report.html` in a browser.

## 4. Reading a report

Each report groups findings into three kinds:

- **WCAG issues** — a possible failure of one or more WCAG 2.2 success criteria.
- **Needs review** — automated checks can't decide on their own; a person should review it against
  the cited criteria. For example, contrast ratios between 3:1 and 4.5:1 need review because text
  size can't always be read from the screenshot.
- **Platform advisories** — below a platform guideline (Apple Human Interface Guidelines, Android
  accessibility guidance) but not a WCAG failure. Touch-target guidelines are a common example: WCAG 2.5.8
  asks for 24×24 CSS pixels, while Apple recommends 44×44 pt and Android 48×48 dp — a target that
  meets WCAG but not the platform guideline shows up here, kept apart from WCAG issues. Findings
  beyond a WCAG threshold also land here: for instance, iOS's large-text check runs at about 235%
  (accessibility size AX3), well past the 200% WCAG 1.4.4 asks for, so clipping seen only above 200%
  is reported as a platform advisory against Apple's Dynamic Type guidance, not a WCAG issue.

A finding without an applicable WCAG criterion is labeled **"No WCAG criterion mapped"** rather
than guessing one.

Each screen also shows:

- **Predicted screen reader transcript and swipe order** — what a screen reader is *predicted* to
  announce, built from the accessibility tree in swipe order. This is a prediction, not a
  recording: VoiceOver can't be scripted and doesn't run in the Simulator, so the real screen
  reader was never listening. Compare it with TalkBack or VoiceOver by hand.
- **Screen reader (captured)** (Android only, with `--screen-reader`) — real evidence, next to the
  predicted transcript above: Swipewalk drives TalkBack itself over the screen's focusable elements
  and reads back exactly what it said, then reports every difference for you to check by hand
  (never as a confirmed WCAG failure by itself — the difference could be the app, or Swipewalk's own
  prediction, that's wrong). It works by temporarily making a small app Swipewalk installs
  (shown in the device's text-to-speech settings as "Swipewalk (testing only)") the device's
  default text-to-speech engine: Google's TalkBack sends its speech to whichever engine is set
  there (seen with TalkBack 16 and 17) — this gets Swipewalk the exact utterance text, entirely
  on-device. **TalkBack speaks nothing aloud for the length of the capture** — don't run this on a
  phone someone is relying on TalkBack with right now. Off by default, and adds roughly 1-2 seconds
  per focusable element to the capture (a 30-element screen about a minute) — pass `--screen-reader`
  to `scan` or `record` to turn it on. The accessibility settings this changes (which service is
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
  capture is opt-in..." limitation for what this can get wrong (it doesn't check plain text, and
  only Android is supported so far).
- **Relevance to standards** — each finding is labeled with the laws and standards (ADA Title II,
  Section 508, EN 301 549 v3.2.1/v4.1.1, UK public sector regulations) whose WCAG version and level
  include its criterion. This says a finding is **relevant to** a standard, never that the app
  **complies with** it — it's a mapping to help you prioritize, not legal advice.
- **Known limitations** — the report lists the limitations that apply to the scanned platform and
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
  tree, not just the theme setting), the report says the app may not have picked up the appearance
  change (some apps only read the theme at launch) instead of labeling findings by appearance at
  all — large-text and captured-screen-reader findings are never labeled by appearance either,
  since the second capture doesn't repeat those checks.
- **Lost navigation place (Android)** — on Android, when the very first attempt at the larger text
  size showed a different screen (typically the app's first) instead of the one being checked, the
  report also adds a platform advisory on that screen (the one where Swipewalk actually saw it
  happen, not any screen after): the person likely lost their place and has to navigate back. Not a
  WCAG issue (1.4.4 doesn't require an app to keep its navigation state across a text-size change);
  worded "probably", since the detection compares screens by title and element names, which can
  occasionally read a truncated or crowded screen as "different" when it isn't.

A report with no findings is not a statement of conformance — it means the checks that ran found
nothing to flag. Most WCAG success criteria have no automated check at all yet; see the "Automated
checks cover only part of WCAG" limitation.

Three reference pages that ship with Swipewalk describe what it checks and doesn't, and how; each is
also printable from the CLI so it's always current with the version you have installed:

- `swipewalk checks` — every automated check Swipewalk runs (its own rules, plus the checks it reads
  from Apple's accessibility audit and Google's Accessibility Test Framework), what each looks for,
  whether it's a WCAG issue, needs review, or a platform advisory, and which WCAG criteria or
  platform guideline it cites (see [docs/checks.md](checks.md)).
- `swipewalk limitations` — what Swipewalk cannot check or may get wrong (see
  [docs/limitations.md](limitations.md), also linked above).
- `swipewalk standards` — the laws and standards each finding is marked as relevant to, and the
  sources those mappings were checked against (see [docs/standards.md](standards.md)).

### WCAG 2.2 coverage

Every report includes a "WCAG 2.2 A/AA coverage" table listing all 55 WCAG 2.2 Level A and AA
success criteria — not just the ones this scan happened to flag — so a report never leaves a
criterion unaccounted for. Each row shows what this run did about that criterion, not whether the
app meets it:

- **Not tested in this run** — a check exists for this criterion, but it didn't run this time (for
  example, `--large-text` wasn't used, so the resize-text check couldn't run; or the app is Android,
  which has no platform accessibility audit engine). Shown first, since it's the gap most worth
  fixing by rescanning.
- **Partly checked by automation** — at least one rule ran on at least one scanned screen and
  reports the issues and items needing review it found (or "0 automated findings" if it found
  nothing — that means the automated part of the check ran and found nothing, not that the
  criterion is met).
- **Needs a manual check** — no Swipewalk rule checks this criterion yet; the table gives a
  concrete step for checking it with TalkBack or VoiceOver.
- **Usually out of scope for a single app** — WCAG2ICT applies this criterion to non-web software
  only across a "set of software programs" (separate programs from the same author, distributed
  together and interlinked), which it notes is rare; confirm your app isn't one before assuming it
  doesn't apply.

The table links each criterion to its source (the W3C "Understanding" page, or the WCAG2ICT
"Applying SC ... to non-web software" page) and, for record mode, shows how many of the scanned
screens each check actually ran on.

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
```

`--expect "Login,Home,Settings"` names the screens you meant to cover; any that weren't scanned are
listed in the report as missing, so you know what to test by hand.

On a physical iPhone, iOS shows its own "Automation Running" banner while Swipewalk controls the
app. By default (manual capture) Swipewalk starts automation only for each step, such as a capture
or a text-size change, and stops it right after, so the banner isn't shown while you navigate
between screens. Each capture takes a few seconds longer as a result. `--auto` checks the screen
every second or two to notice changes, so it keeps automation running and the banner stays up for
the whole recording.

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
screens captured up to the interruption; History is where you find out. Either way, screens after the
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
again and when. The report says the run was recorded across more than one session, with each
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

## 6. CI and history

For a repeatable, scriptable run, describe it once in a `swipewalk.json` file and run
`swipewalk run`. Here's a minimal example, based on
[`samples/BuggyApp/swipewalk.json`](../samples/BuggyApp/swipewalk.json):

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
  "framework": "maui",
  "largeText": true,
  "largeTextRestart": "never",     // record only; "ask"/"always"/"never" -- see section 5. Defaults to
                                    // "never" so an unattended run never blocks waiting for an answer.
  "appearance": false,             // scan only for now; true = also check the other dark/light appearance
                                    // (the CLI's --appearance both; here it's a plain boolean)
  "failOn": "wcag-issues"          // exit code 3 when WCAG issues are found; "never" to always exit 0
}
```

```bash
swipewalk run --config swipewalk.json
```

`run` installs the app if you gave it a build file, runs the pre-flight checks, scans (or
records) every target, and saves each run to history. Its exit codes are built for CI:

- **0** — the run completed; if `failOn` is `"wcag-issues"`, no WCAG issues were found (still not a
  statement of conformance — see [section 4](#4-reading-a-report)).
- **2** — a target couldn't be scanned (device not found, app not installed, and so on).
- **3** — `failOn` is `"wcag-issues"` and at least one WCAG issue was found, for the standard in
  focus if `--standard`/`"standard"` was set.

Every run (from `run`, and from `scan`/`record` unless you pass `--no-history`) is saved to a local
history — `~/Library/Application Support/Swipewalk/runs` on macOS by default, or the directory you
pass with `--history <dir>`. List saved runs with:

```bash
swipewalk history
```

To see what changed between two runs, compare them by run folder or `results.json` path:

```bash
swipewalk compare <earlier> <later>
```

This reports what's new, what's no longer found, and what's still found. "No longer found" means
the automated checks didn't report it this time — it does not mean the issue was fixed; check by
hand. Findings on screens that weren't scanned again aren't compared and are listed separately.

Every scanned screen's raw capture (accessibility tree, screenshot) is also saved, so you can
re-run the rules against it later — after updating Swipewalk, for example — without the device:
`swipewalk scan --platform android --from <capture dir> --out report` (or `--platform ios`)
re-scans a saved capture instead of a live device. Find capture directories inside a run folder in
history: `capture/` for a single scan, or `screens/01`, `screens/02`, … for a recording.

## 7. The desktop app

The desktop app (macOS, Mac Catalyst) does the same scans with a window instead of a terminal. Its
pages:

- **Dashboard** — per-app cards showing the latest run's counts and the change since the previous
  run, plus a trend of WCAG issues over recent runs.
- **New scan** — pick a platform, device, app, standard and what to scan (single screen or record),
  and start it. No device is chosen by default; Start (and Choose app…) stay disabled, with a short
  reason shown, until you pick one. Choosing a physical phone shows a notice that the large-text
  check changes the phone's own text size and restores it afterwards; it appears again next time
  unless you choose Don't Show Again, rather than just OK. While a physical phone and the
  large-text option are both selected, a short reminder of the same fact stays next to that option.
- **Devices** — readiness checks and fix hints for each connected device, the same checks
  `swipewalk doctor` runs.
- **History** — every saved run, to open or compare. A recording that's still going (here, or in
  another window) shows "In progress"; one that stopped before Finish -- including Swipewalk itself
  being closed or crashing -- shows "Ended early" with a Continue button to resume it.
- **Report** — the same HTML report you'd get from the CLI, in a window.
- **Compare** — pick two runs and see new, no longer found, not checked again, and still-found
  findings, with the same "no longer found isn't fixed" caveat as `swipewalk compare`.

Download the signed `.dmg` from the
[releases page](https://github.com/swipewalk/swipewalk/releases), or build it from source. See the
[Desktop app section of the README](../README.md#desktop-app-macos) for both (building needs the
.NET 10 SDK, the MAUI workload and Xcode).

## 8. Troubleshooting

`swipewalk doctor --platform android|ios` is the fastest way to find out what's wrong — run it
before opening an issue. It runs the same checks as `scan` and `record`, and each failure explains
how to fix it. The common ones:

- **`adb` not found or not working.** Install the Android SDK platform-tools and set
  `ANDROID_HOME`, or put `adb` on your `PATH`.
- **Device state `unauthorized`.** Unlock the phone and accept the "Allow USB debugging?" prompt
  (tick "Always allow").
- **Device state anything else (e.g. `offline`).** Reconnect the cable, or run `adb kill-server`.
- **Screen off or locked (Android).** Unlock the device and keep it on — Developer options > Stay
  awake keeps it on while charging.
- **App not installed.** Install the app on the device first; Swipewalk can't do that step for you.
  (Another app being in front isn't a failure — Swipewalk brings the app under test forward, or
  starts it if it isn't running, before scanning.)
- **`xcodebuild` not found.** Install Xcode and run `xcode-select --install`.
- **iOS harness not found.** Reinstall Swipewalk (the harness ships with it), run from a Swipewalk
  checkout, or pass `--harness <path to .xcodeproj>`.
- **Android: Google's accessibility checks (ATF) didn't run.** Swipewalk builds and installs a small
  instrumentation harness the first time it's needed, which needs a JDK (Android Studio's bundled one, or
  any JDK on `PATH`) and the Android SDK. If it can't be built or a run fails, the scan still completes
  with today's checks; the report and results.json say why. Pass `--android-harness <path>` to point at a
  working copy of `harness/android`, or see [docs/limitations.md](limitations.md) ("Google's Accessibility
  Test Framework needs a working instrumentation harness").
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
  `swipewalk doctor --platform android --screen-reader` — it restores what it can from its own
  record or the device's, and says what's left if it can't confirm everything.
- **iPhone: the large-text check can't find its way through Settings.** Swipewalk drives Settings >
  Accessibility > Display & Text Size > Larger Text through a fixed set of steps; a new iOS version
  that changes that screen can break it. Swipewalk falls back to a per-app text-size setting instead,
  or skips the large-text check for that screen with a reason — either way the rest of the scan still
  runs. Please report this so the Settings automation can be updated for the new iOS version.

Other things you might hit:

- **Apps that block screenshots.** Some apps set Android's `FLAG_SECURE` on sensitive screens
  (banking, passwords, DRM) to block screenshots. Swipewalk reports these screens and skips the
  pixel-based checks (contrast, close-ups) on them, rather than failing.
- **.NET MAUI Android debug builds won't install from a file.** `--install` accepts any signed
  `.apk`, but MAUI Debug builds that use fast deployment can't be installed that way (Swipewalk
  detects this and explains). Build and install with `dotnet build -t:Install -f net10.0-android`
  instead, or build in Release (or with `EmbedAssembliesIntoApk=true`), and scan the already
  installed app by `--package`.
- **Xcode version mismatch when building a MAUI app for iOS.** If your installed .NET for iOS
  workload pack expects an older Xcode than the one you have (for example the pack expects Xcode
  26.5 but you have Xcode 27.0), the build fails on the version check. Build with
  `-p:ValidateXcodeVersion=false` to skip it — this doesn't affect Swipewalk itself, only building
  the app you're about to scan.
- **`--result-bundle`.** An iOS option for testing Swipewalk itself: it forces the physical-device
  capture path (result-bundle attachments) even on a Simulator. Everyday scans don't need it — the
  right path is chosen for you.

## 9. Privacy and reporting wrong findings

Swipewalk runs entirely on your computer: no accounts, no analytics, no telemetry, and it makes no
network requests of its own. Scan output goes to the folder you choose and to a local run history
on your machine; nothing is sent to the Swipewalk authors or anyone else. The Android accessibility
harness (Google's Accessibility Test Framework) ships prebuilt, so it needs no network access
either — only a source checkout without a prebuilt copy, or a copy you name with
`--android-harness`, is built with Gradle instead, which does use the network (see
[PRIVACY.md](../PRIVACY.md)). See PRIVACY.md for exactly what's stored, where, and what the platform
tools (`adb`, `xcodebuild`, `devicectl`) may do on their own.

On Android, screenshots blank the status bar by default, since it can show notification text and
other personal information; pass `--keep-status-bar` if you specifically need it left in the
screenshot and are sure it won't show anything sensitive.

Found a false positive, a missed issue, or a wrong WCAG mapping? Please report it — that's the most
useful kind of contribution right now. Use the "Wrong or missing finding" issue form on the
[GitHub repository](https://github.com/swipewalk/swipewalk/issues/new/choose), with a screenshot
and the app's framework if you can.
