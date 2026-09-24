# NativeAndroid

A small native Android app (Kotlin, Gradle, no .NET MAUI) with the same set of deliberate
accessibility bugs planted twice: once built the classic Android View system way, once built
with Jetpack Compose. It exists so Swipewalk's results on native Android toolkits can be shown
and regression-tested, the same way `samples/BuggyApp` (a .NET MAUI app) is used today. It is a
standalone Gradle project, not part of `Swipewalk.slnx` -- the same pattern as `harness/android`
in this repo.

Package: `org.swipewalk.nativeandroid`. Fictional client: "City of Exampleville" (the same
fictional organization BuggyApp uses -- nothing here is a real person, company or data).

## Building

```bash
cd samples/NativeAndroid
JAVA_HOME="$(/usr/libexec/java_home -v 17)" ./gradlew :app:assembleDebug
```

The debug APK lands at `app/build/outputs/apk/debug/app-debug.apk`.

## The two screens

Both screens implement the same "Pay a parking ticket" flow (a resident enters a plate/ticket
number and pays or saves for later), and both plant the same 8 bug classes -- done the idiomatic
way for that toolkit, not copy-pasted. Each is a separate, directly launchable activity so it can
be scanned on its own without navigating through the picker screen first:

```bash
adb shell am force-stop org.swipewalk.nativeandroid
adb shell am start -n org.swipewalk.nativeandroid/.ViewsActivity
adb shell am start -n org.swipewalk.nativeandroid/.ComposeActivity
```

(`am force-stop` first avoids a stale top activity: `am start` on an activity that's already the
task's top instance redelivers the intent instead of actually switching screens, which can leave
a scan pointed at the wrong screen -- seen while verifying this app.)

`MainActivity` is only a picker between the two (two buttons); it is not itself part of the
ground truth.

- **ViewsActivity** -- classic Android View system, XML layout
  (`app/src/main/res/layout/activity_views.xml`, `ViewsActivity.kt`).
- **ComposeActivity** -- Jetpack Compose (`ComposeActivity.kt`).

## Ground truth

`ground-truth.views.json` and `ground-truth.compose.json` list the findings each screen produces,
in the same schema `samples/BuggyApp/ground-truth.json` uses, simplified to a single `"android"`
key (this app has no iOS build). Rule ids follow [docs/checks.md](../../docs/checks.md).

**Verified 2026-09-23** against live scans of an Android emulator (Android 16, API 36) and a
physical Pixel 4a (Android 13, API 33), both with Google's Accessibility Test Framework via the
Android instrumentation harness. `tests/Swipewalk.Core.Tests/NativeSamplesGroundTruthTests.cs`
enforces the emulator captures (saved as fixtures) match these files exactly; the Pixel 4a scans
were live checks, not saved as committed fixtures, and are described below and in each ground-truth
file's notes instead.

## Planted bug classes

Every bug is tagged in the source with a comment (`// N1:` etc. in Kotlin/XML) matching the id
below. Elements tagged `// OK:` are negative controls -- built correctly, included so a rule
firing on them would itself be a false positive worth investigating. A few negative controls
still trip a real platform advisory honestly (for example, an icon button sized just under
Android's 48dp guideline even though it's correctly labelled) -- those are marked `planted: false`
in the ground-truth files, the same convention BuggyApp uses for its `T1`/`T2` entries.

| id | Description | Views result | Compose result |
|---|---|---|---|
| N1 | Icon-only button with no accessible name at all | `missing-name` (WCAG issue) | `missing-name` (WCAG issue) |
| N2 | Helper text at about 2.3:1 contrast (`#AAAAAA` on white) | `text-contrast` (WCAG issue) | `text-contrast` (WCAG issue) |
| N3 | Icon button with a correct name but drawn at 20dp | `target-size` (platform advisory) | **not reported** -- see Framework differences (unrelated to the naming gap) |
| N4 | Accessible name copied from a developer identifier (`img_btn_email_receipt`) | `identifier-name`, role `button` | `identifier-name`, role `button` -- see Framework differences |
| N5 | Visible text "Pay" but accessible name "Submit" | `label-in-name` (WCAG issue) | **not reported** -- see Framework differences |
| N6 | Text field with no label, hint or content description, and an unassociated caption | `missing-name` + `target-size` | `missing-name` |
| N7 | Text sized in a unit meant to ignore the system font-size setting (Views: `px`; Compose: a `dp`-to-`sp` conversion) | `text-resize`, confirmed on the large-text rescan | **did not reproduce** -- see Framework differences |
| N8 | Icon-only control with a correct name and normal size, but a low-contrast icon tint | ATF `ImageContrastCheck` | **not reported** -- see Framework differences (unrelated to the naming gap) |

Negative controls: `OK1` ("View payment history", generously sized) stayed clean on the emulator,
but showed a `target-size` platform advisory on the Pixel 4a's Compose screen (184x41 dp, a
rendering/density difference between the two devices, not fully explained). `OK2` (a correctly
labelled 44dp icon button, a realistic near-miss under Android's 48dp guideline) triggers
`target-size` on the Views screen; ATF also measured low contrast on this same icon and on N4's
(both use an unmodified platform stock drawable) -- found without being planted. `OK3` (a text
field with a visible label wired up via `android:labelFor`) is **not** clean on the Views screen --
see Framework differences below.

## Framework differences

Confirmed by live scans, not assumptions -- see each ground-truth file's own notes for full detail:

- **`android:labelFor` isn't visible to `uiautomator dump`.** The Views screen's `OK3` field (a
  visible label wired up the standard Android way) gets exactly the same `missing-name` and
  `target-size` findings as `N6`'s genuinely unlabelled field, because uiautomator's XML has no
  attribute for the labelFor/`getLabeledBy()` relationship, and the instrumentation harness doesn't
  read it either. TalkBack is documented to announce the labelFor association; not tested with
  TalkBack here. New limitation: `android-labelfor`.
- **A name set inside a Compose button doesn't always reach the clickable node uiautomator
  reports.** For `N3`, `N5` and `N8`, the accessible name (set via `contentDescription` on a child
  `Icon()`, or via `Modifier.semantics {}` on the button itself) ends up on a *separate* node
  reported `clickable="false"` in the raw dump, while the actual clickable/focusable node has an
  empty content-desc. Addressed for the unambiguous case (`N3`, `N4`, `N8`, `OK1`, `OK2`): when
  exactly one non-focusable descendant carries a name and nothing else in the subtree is
  independently focusable or clickable, the parser now gives the clickable node that name as its
  own `Label`
  (`UiAutomatorParser.TryMergeSingleDescendantName`). `missing-name`'s own output is unchanged (it
  already found these buttons named through the existing `ScreenReaderPredictor.AccessibleName`
  descendant-walk fallback); `identifier-name` (which reads `Label` directly and scans every named
  node, not just clickable ones) is what actually changes, now reporting `N4`'s role as `button`
  instead of `group`. Left unmerged, deliberately: `N5`, where the icon's `contentDescription`
  ("Submit") and the visible text ("Pay") are two *different* children of the same merged Button --
  TalkBack was seen to announce both ("Submit || Pay || Button", see docs/case-study.md section 6),
  but that doesn't show which name speech input matches on, so `label-in-name` still can't check
  that mismatch, and `identifier-name` would still report the wrong role for a developer-identifier
  name built the same way. `target-size` was never blocked by this gap at all:
  it reads the clickable node's own (correct) bounds regardless of naming, and `N3`/`OK2` simply
  measure exactly 48dp on the emulator (Compose's `IconButton` padding its touch target back up
  regardless of a smaller `Modifier.size`), similar to `samples/BuggyApp`'s `B5` (there, MAUI
  enlarges the target to only 44dp, which still trips the platform advisory). Limitation
  `android-compose-merged-name` narrowed to the still-open `label-in-name`/`identifier-name` gap.
- **`N7`'s Compose version doesn't reproduce.** `with(LocalDensity.current) { 14.dp.toSp() }` was
  assumed to bypass the system font-scale setting; a live large-text rescan showed the text's
  bounds grow exactly in proportion to the 200% scale (63dp to 126dp), the same as an ordinary
  `14.sp` literal. `Dp.toSp()` only affects how the *number* is derived, not whether Compose
  applies the current font scale when it lays the text out. There is currently no working
  "fixed text size" bug planted on the Compose screen.
- **Compose's `TextField(label = { ... })` correctly names the field**, unlike the Views screen's
  `android:labelFor` -- a positive difference in the opposite direction from the point above.
- **`page-titled` behaved inconsistently across devices.** It fired on both emulator screens
  and the Pixel 4a's Compose screen, but not the Pixel 4a's Views screen (which uses AppCompat's
  `supportActionBar` rather than Compose's `TopAppBar`). Not fully explained in the time available.
- **`TextSizeCheck` (Google's Accessibility Test Framework) fired on the Pixel 4a but not the
  emulator**, scanning the exact same `px`-sized text (`N7` on the Views screen) -- see
  `docs/limitations.md`'s "Automated checks cover only part of WCAG". Previously documented as
  "has not reported anything in testing"; this is the first confirmed sighting.
