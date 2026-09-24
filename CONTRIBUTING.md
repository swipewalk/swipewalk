# Contributing to Swipewalk

Thanks for helping. Issues and pull requests are welcome. Swipewalk is maintained part-time, so
reviews can take a few days.

## Most useful right now

- **Wrong findings.** A false positive, a missed issue or a wrong WCAG mapping, with a screenshot
  and the app's framework. Use the "Wrong or missing finding" issue form.
- **Real-device reports.** Which devices and OS versions work, and which don't.
- **Framework quirks.** How Flutter, React Native, Jetpack Compose, SwiftUI and others show up in
  the accessibility tree.

## Ground rules

- **No compliance claims.** Code, output and docs never say an app is "compliant", "certified",
  "accessible" or "passes WCAG". Say "automated checks found N issues" and note that manual testing
  is still required. Comparisons say "no longer found", never "fixed".
- **Every finding maps to a real WCAG 2.2 success criterion** (number, name, level), with a primary
  source. If you are unsure, leave the mapping out and say so in the pull request. Platform
  guidelines (Apple 44×44 pt, Android 48×48 dp) are advisories, kept apart from WCAG issues.
- **Wrap existing engines** (Apple's accessibility audit, Google's Accessibility Test Framework,
  axe-windows) rather than reimplementing them.
- **One rule per file**, with tests for a passing and a failing tree. Rules work only on the
  platform-neutral model in `src/Swipewalk.Core/Model`, so each rule is written once.
- **No real apps, people or devices in fixtures.** Use `samples/BuggyApp`, `samples/NativeAndroid`,
  `samples/NativeiOS` or made-up data. Never
  commit screenshots or accessibility trees of apps you don't own.
- When rules or standards mappings change, bump `RuleSources.RulesetVersion`.

## Keep docs current

Update docs in the same pull request as the code, not in a follow-up -- the PR template has a
"Docs" section for this, and the `docs-check` workflow flags a PR that changes user-facing code
(the CLI, the desktop app's screens, rules, report or coverage text, known limitations, the
standards mapping, or the Engine library) without touching `docs/**` or `README.md`, unless the PR
description has a line starting `Docs: none needed - <reason>`.

Three pages are generated from code and must be regenerated whenever their source changes, rather
than hand-edited (a test fails in CI if they go stale):

```bash
dotnet run --project src/Swipewalk.Cli -- limitations > docs/limitations.md   # src/Swipewalk.Core/Limitations
dotnet run --project src/Swipewalk.Cli -- standards > docs/standards.md      # src/Swipewalk.Core/Standards
dotnet run --project src/Swipewalk.Cli -- checks > docs/checks.md            # rules, EngineIssueRule, AtfIssueRule
```

`docs/user-guide.md` is hand-written but checked both ways against the CLI: every command and
option it shows must be real, and every real command and option must be shown there
(`UserGuideTests`).

## Building and testing

Requires the .NET 10 SDK. The desktop app also needs the MAUI workload and Xcode.

```bash
dotnet build
dotnet test
dotnet build src/Swipewalk.Desktop -f net10.0-maccatalyst   # desktop app (macOS)
scripts/desktop-uitests.sh                                      # desktop UI tests after UI changes
```

Regenerating `samples/NativeiOS` or `harness/ios` from `project.yml` needs [XcodeGen](https://github.com/yonaskolb/XcodeGen)
(`brew install xcodegen`) -- both ship their generated `.xcodeproj` committed, so it's only needed after editing
`project.yml` or adding/removing files. Building `samples/NativeAndroid` or `harness/android` with Gradle needs
JDK 17+ (on `PATH` or in `JAVA_HOME`; Android Studio's bundled JBR works) and the Android SDK
(`ANDROID_HOME`/`ANDROID_SDK_ROOT`, or `~/Library/Android/sdk`); see each folder's README.

## Sign your commits (DCO)

By contributing you certify the [Developer Certificate of Origin](https://developercertificate.org/):
you wrote the change or have the right to submit it under the Apache License 2.0. Add a sign-off
line to each commit with `git commit -s`.

## Conduct

Everyone taking part follows the [code of conduct](CODE_OF_CONDUCT.md).
