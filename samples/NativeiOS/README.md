# NativeiOS

A small native iOS app (Swift, no .NET MAUI) with deliberately planted accessibility
bugs, used as ground-truth data for Swipewalk. It exists alongside
[`samples/BuggyApp`](../BuggyApp) (a MAUI app) so Swipewalk's findings on the same
kind of screen can be compared across a MAUI app and native UIKit/SwiftUI code, and
so native-toolkit results can be shown and used as regression data.

The fictional client is "City of Exampleville", the same fictional org BuggyApp
uses. No real people, companies or data.

## Screens

The app opens on a plain root menu (`RootView`, SwiftUI) with two buttons:

- **Views screen** pushes `ViewsScreenViewController`, a screen built entirely in
  UIKit (programmatic views, no storyboard/xib).
- **SwiftUI screen** pushes `SwiftUIScreenView`, the same screen built in SwiftUI.

Both screens implement the same flow, "Pay a parking ticket" (matching
`samples/BuggyApp/MainPage.xaml`'s flow), and both plant the same 8 bug classes
below -- each done the way a developer working in that toolkit would actually make
the mistake, not copy-pasted between the two. Both set a real navigation title
("Pay a parking ticket") so screen-title checks have something to find.

To reach a screen directly for scanning, without tapping through the root menu, launch with the
`-views` or `-swiftui` argument (the app reads `ProcessInfo.processInfo.arguments`, the same style
as this repo's iOS large-text fallback):

```bash
xcrun simctl launch <device> org.swipewalk.nativeios -views
xcrun simctl launch <device> org.swipewalk.nativeios -swiftui
```

`simctl launch`/`devicectl ... process launch` on an *already-running* instance of the app does
not restart it, so a launch argument only takes effect after the app is terminated first
(`xcrun simctl terminate <device> org.swipewalk.nativeios`, or `--terminate-existing` with
`devicectl` on a physical device) -- otherwise the previous screen just stays in front.

## Building

Regenerating the Xcode project from `project.yml` (the first command below) needs
[XcodeGen](https://github.com/yonaskolb/XcodeGen) (`brew install xcodegen`). If you haven't changed `project.yml`
or added/removed files, you can skip it: the generated `NativeiOS.xcodeproj` is committed. CI regenerates it on
every build to catch `project.yml` changes.

```
cd samples/NativeiOS
xcodegen generate
xcodebuild -project NativeiOS.xcodeproj -scheme NativeiOS -destination 'generic/platform=iOS Simulator' build
```

Regenerate the `.xcodeproj` with `xcodegen generate` after editing `project.yml` or
adding/removing files under `Runner/`; both `project.yml` and the generated
`.xcodeproj` are committed, the same convention `harness/ios` uses.

For a physical device, signing needs a development team, e.g.
`xcodebuild ... -destination 'id=<device-udid>' -allowProvisioningUpdates DEVELOPMENT_TEAM=<team-id> build`
(the harness's own `--team` handling is separate; this is a plain Xcode project).

## Planted bugs

Each planted element is tagged with a comment (`// N1:`, etc.) matching the row
below; `// OK:` tags a negative control (a correctly built equivalent) that a
scan should not flag. Both `ViewsScreenViewController` and `SwiftUIScreenView`
plant all 8.

| ID | Bug | UIKit result | SwiftUI result |
|---|---|---|---|
| N1 | Icon-only button (search, SF Symbol `magnifyingglass`) with no accessible name set | **not reproduced** (symbol supplied the name "Search") | **not reproduced** (symbol supplied the name "Search") |
| N2 | Helper text at about 2.3:1 contrast against its white background | `text-contrast` (WCAG issue) | `text-contrast` (WCAG issue) |
| N3 | Icon-only button ("Clear form") drawn at 20x20 | `target-size` (platform advisory -- spacing exception) | `target-size` (platform advisory) |
| N4 | Icon-only button (email receipt) named `img_btn_email_receipt` | `identifier-name` | `identifier-name` |
| N5 | Button reads "Pay" but its accessible name is "Submit" | `label-in-name` (WCAG issue) | **not reported** -- see Framework differences |
| N6 | Text field ("Plate number") with an unassociated caption and no name of its own | `missing-name` (WCAG issue) | `missing-name` (WCAG issue) |
| N7 | Fixed point-size text ignoring the system Dynamic Type setting | `engine:dynamicType` (Apple's own audit, on a normal scan) | `engine:dynamicType` (Apple's own audit, on a normal scan) |
| N8 | Icon-only button ("Help") with a correct name but a low-contrast icon color | `icon-contrast` | `icon-contrast` |

Negative controls: a text button whose visible title matches its accessible name ("Cancel") and a
text field with a real associated name ("Ticket number") both rendered below Apple's 44pt
guideline on both screens -- a realistic near-miss, not planted. "View payment history" stayed
clean on the UIKit screen, but on SwiftUI (`.buttonStyle(.bordered)`) it also renders under 44pt
and its foreground color measured low contrast -- see Framework differences.

## Ground truth

[`ground-truth.uikit.json`](ground-truth.uikit.json) and
[`ground-truth.swiftui.json`](ground-truth.swiftui.json) follow the same schema as
`samples/BuggyApp/ground-truth.json`, narrowed to a single `"ios"` platform key
(this app has no Android build). `iosLargeText` entries need a large-text/Dynamic
Type rescan (record mode or `scan --large-text`), not run here (matching
`samples/BuggyApp`'s own note that there is no saved iOS large-text fixture yet).

**Verified 2026-09-23** against a live scan of the iPhone 17 Simulator (iOS 26.5) and a physical
iPhone (iOS 27.0). `tests/Swipewalk.Core.Tests/NativeSamplesGroundTruthTests.cs` enforces the
Simulator captures (saved as fixtures) match these files exactly; the physical-iPhone scans were
live checks, not saved as committed fixtures, and are described below and in each ground-truth
file's notes instead.

## Framework differences

Confirmed by live scans, not assumptions -- see each ground-truth file's own notes for full detail:

- **A common SF Symbol already has a name.** `N1` (search icon, no `accessibilityLabel` set
  anywhere) is captured with accessible name "Search" on both UIKit and SwiftUI, on both the
  Simulator and a physical iPhone -- Apple ships a built-in accessibility description for many SF
  Symbols, and `missing-name` correctly finds nothing to report. Android's equivalent (an
  `ImageButton`/Compose `Icon` with no `contentDescription`) has no such default; the identical
  mistake in `samples/NativeAndroid` is correctly flagged there. New limitation:
  `ios-sf-symbol-default-label`.
- **SwiftUI's `.accessibilityLabel()` replaces the visible text in the tree; UIKit's doesn't.**
  `N5` is detected on UIKit (`label-in-name`, "Pay" vs "Submit") because UIKit keeps the button's
  title as a separate child node alongside its `accessibilityLabel`. The same mismatch built in
  SwiftUI is **not** detected: `.accessibilityLabel("Submit")` on `Button("Pay") { }` leaves no
  trace of "Pay" anywhere in the captured tree, so `label-in-name` has nothing to compare against.
  New limitation: `ios-swiftui-accessibility-label-hides-text`.
- **SwiftUI's built-in text styles support Dynamic Type without an opt-in; UIKit's don't.** Apple's
  audit names 3 elements on the UIKit screen for unsupported Dynamic Type (including the "Ticket
  number" field, whose font was never set explicitly) but only 1 on the SwiftUI screen (`N7`
  itself) -- SwiftUI's `.subheadline`/`.footnote`/`.title2` styles scale automatically; UIKit's
  `UILabel`/`UITextField` each need `adjustsFontForContentSizeCategory` set by hand.
- **A `.bordered` SwiftUI button can measure low contrast where a plain UIKit button doesn't.**
  "View payment history" is clean on UIKit but on SwiftUI its tint against the style's light gray
  background measured about 2.90:1 (`icon-contrast`) and was also flagged by Apple's own audit
  (`engine:contrast`, needs review) -- `icon-contrast` reading a text button's foreground color this
  way (not only true icons) is flagged as a possible over-broad match, not fixed, since a trivial
  fix wasn't obvious.
- **On the physical iPhone only**, "View payment history" (UIKit, a `UIButton.Configuration.gray()`
  button, otherwise clean) additionally measured a WCAG text-contrast issue (about 1.48:1, white
  text on light gray) that the Simulator did not show, and the SwiftUI screen's `N7` text measured
  against a **black** background (about 1.27:1) instead of the Simulator's white -- possibly the
  phone being in Dark Mode at the time (SwiftUI's screen never sets an explicit background, unlike
  UIKit's, which forces `.white`), though the phone's appearance setting wasn't checked and this run
  didn't change it, so this isn't confirmed. Not reproduced in the committed fixtures since it's
  device/appearance-specific.
