# Security policy

## Reporting a vulnerability

Please do not open a public issue for security problems. Report them privately through
[GitHub private vulnerability reporting](https://github.com/swipewalk/swipewalk/security/advisories/new).

Include what you found, how to reproduce it and what an attacker could do with it. We aim to reply
within 7 days. Swipewalk is maintained part-time, so fixes may take longer; we will keep you
updated and credit you in the release notes unless you prefer not to be named.

## Scope

Swipewalk runs local tools (`adb`, `xcodebuild`, `devicectl`) and reads files from scanned apps
and devices. Issues of particular interest:

- command or argument injection through app IDs, device names, file paths or `swipewalk.json`;
- script injection into the HTML report from text or labels shown by a scanned app;
- personal data leaking into reports despite the status-bar blanking;
- the iOS harness or its signing handling exposing credentials.

## Supported versions

Only the latest release receives security fixes.
