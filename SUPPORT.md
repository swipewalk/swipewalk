# Getting help and reporting problems

Swipewalk is maintained part-time, so replies can take a few days. Swipewalk's source code is not public,
so there is no way to send a pull request or code; reports and suggestions go through the
[issue tracker](https://github.com/swipewalk/swipewalk/issues/new/choose).

## First steps

- Start with the [user guide](docs/user-guide.md) and the [known limitations](docs/limitations.md).
- Something failing? Run `swipewalk doctor --platform android|ios` to check your setup, then say what you ran,
  what you expected and what happened.

## Most useful right now

- **Bugs and crashes.** Use the "Bug" issue form and attach a diagnostic report (Help > Save Diagnostic Report… in the
  desktop app, or `swipewalk diagnostics`). Read it first: it removes serials, device names, team IDs and your user
  name by pattern, which can miss something, and keeps the ids of the apps you scanned.
- **Wrong findings.** A false positive, a missed issue or a wrong WCAG mapping, with a screenshot
  and the app's framework. Use the "Wrong or missing finding" issue form.
- **Real-device reports.** Which devices and OS versions work, and which don't.
- **Framework quirks.** How Flutter, React Native, Jetpack Compose, SwiftUI and others show up in
  the accessibility tree.

## What to include

- The Swipewalk version (`swipewalk --version`, or the version in the name of the file you downloaded), your
  computer's operating system and version and, for a scan problem, the platform, the device or emulator and its OS version.
- **No real apps, people or devices in what you attach.** Use made-up data, or apps you own and are allowed to share. Never post
  screenshots or accessibility trees of apps you don't own, or anything with personal data on screen. Issues are public.

## Suggesting a change to a law or standard mapping

If a law or standard is mapped wrongly or is missing, use "Suggest a correction" or "Suggest a law or
standard" on the desktop app's Laws and standards page, or the "Help improve Swipewalk's law mappings" issue form. Include a
link to the official text that shows the correct information. Swipewalk shows laws and standards as reference
information, never as legal advice or a verdict.

## Security problems

Please do not open a public issue for a security problem. See [SECURITY.md](SECURITY.md).

## Conduct

Everyone taking part follows the [code of conduct](CODE_OF_CONDUCT.md).
