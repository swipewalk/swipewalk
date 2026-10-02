# Third-party notices

Swipewalk 0.4.2 and later is licensed under the [Swipewalk License](LICENSE); versions 0.1.0 to 0.4.1 were
released under the Apache License 2.0. The components below are included in it or used by it and keep their
own licences, which apply to them instead of the Swipewalk License.

## Included in the desktop app and the sample app

| Component | License | Notes |
|---|---|---|
| [Open Sans](https://github.com/googlefonts/opensans) | SIL Open Font License 1.1 | Font files in `src/Swipewalk.Desktop/Resources/Fonts` and `samples/BuggyApp/Resources/Fonts`; the license is in `OFL.txt` next to them. |
| [.NET MAUI](https://github.com/dotnet/maui) (`Microsoft.Maui.Controls`) | MIT | Desktop app and sample app. |
| `Microsoft.Extensions.Logging.Debug` | MIT | Debug builds of the MAUI apps. |
| .NET runtime and libraries | MIT | Included in self-contained builds. |

## Included in PDF export (`Swipewalk.Core`, used by the CLI's `swipewalk export --format pdf` and the desktop app's Export dialog)

| Component | License | Notes |
|---|---|---|
| [PDFsharp](https://github.com/empira/PDFsharp) (`PdfSharp`) | MIT | Builds the tagged, PDF/UA-1 PDF `swipewalk export --format pdf` writes (`src/Swipewalk.Core/Export/Pdf`). |
| [Open Sans](https://github.com/googlefonts/opensans) | SIL Open Font License 1.1 | Its own copy of the font files in `src/Swipewalk.Core/Resources/Fonts` (embedded in every generated PDF, so the export needs nothing installed on the host), separate from the desktop app's copy above. |
| [Noto Sans Devanagari, Noto Sans Arabic, Noto Sans JP](https://github.com/notofonts) | SIL Open Font License 1.1 | Embedded fallback fonts for non-Latin report text (Devanagari, Arabic, common-use Japanese -- not Chinese, except characters Chinese shares with Japanese); NotoSansJP-Subset-Regular.ttf is a subset built with fontTools, see `src/Swipewalk.Core/Resources/Fonts/OFL.txt` and `docs/limitations.md`. |

## Included in source mapping (`Swipewalk.Core`, used by `--source` for .NET MAUI apps)

| Component | License | Notes |
|---|---|---|
| [Roslyn](https://github.com/dotnet/roslyn) (`Microsoft.CodeAnalysis.CSharp` and the libraries it needs) | MIT | Reads your app's C# code-behind files locally to find the line a finding likely comes from. |

## Included in the Android instrumentation harness (`harness/android`)

Compiled into `harness-debug-androidTest.apk`, which Swipewalk ships prebuilt inside the NuGet package
and the desktop app (see `Swipewalk.Collectors.csproj`, `Swipewalk.Desktop.csproj` and
`scripts/mac-release.sh`) and installs on the device with `adb`. The table below is every distinct
licence among the harness's compiled dependencies (direct and transitive); run `./gradlew
:harness:dependencies --configuration debugAndroidTestRuntimeClasspath` in `harness/android` for the
exact full list, including every individual AndroidX/Kotlin artifact version.

| Component | License | Notes |
|---|---|---|
| [Accessibility Test Framework for Android](https://github.com/google/Accessibility-Test-Framework-for-Android) (`accessibility-test-framework`) | Apache 2.0 | Runs Google's ATF checks against the app under test; see `harness/android/harness/build.gradle.kts` and `docs/limitations.md` ("android-atf-harness"). |
| AndroidX Test (`androidx.test:runner`/`rules`/`monitor`/`core`, `androidx.test.ext:junit`, `androidx.test.uiautomator:uiautomator`, `androidx.test.espresso:espresso-core`, `androidx.test.services:storage`), the AndroidX support libraries they and ATF pull in (`androidx.core`, `androidx.appcompat`, `androidx.fragment`, `androidx.lifecycle`, `androidx.recyclerview` and others), Google Material Components (`com.google.android.material`), Guava (`com.google.guava`), `javax.inject`, `com.google.errorprone:error_prone_annotations`, `com.squareup:javawriter`, and the Kotlin standard library and coroutines (`org.jetbrains.kotlin`, `org.jetbrains.kotlinx`) | Apache 2.0 | Compiled into the harness APK as transitive dependencies of ATF, AndroidX Test and Espresso. |
| [JUnit 4](https://junit.org/junit4/) (`junit:junit`) | Eclipse Public License 1.0 | Source code: <https://github.com/junit-team/junit4>. Transitive dependency of AndroidX Test; its own licence text (`LICENSE-junit.txt`) is bundled inside the APK unchanged. |
| [Hamcrest](http://hamcrest.org/) (`org.hamcrest:hamcrest-core`/`hamcrest-library`/`hamcrest-integration`) | BSD 3-Clause | Transitive dependency of JUnit and Espresso. |
| Protocol Buffers Java Lite runtime (`com.google.protobuf:protobuf-javalite`) | BSD 3-Clause | Transitive dependency of the Accessibility Test Framework. |
| [jsoup](https://jsoup.org/) | MIT | Transitive dependency of the Accessibility Test Framework. |
| [Checker Framework](https://checkerframework.org/) qualifiers (`org.checkerframework:checker-qual`/`checker-compat-qual`) | MIT | Transitive dependency of Guava. |
| [FindBugs](https://findbugs.sourceforge.net/) `jsr305` (`com.google.code.findbugs:jsr305`) | BSD 3-Clause | Transitive dependency of Espresso. |

## Included in the standalone TTS-engine app (`harness/android/ttsengine`), only installed when `--screen-reader` is used

A small separate app (not the androidTest APK above -- a real text-to-speech engine has to be a normal
installed app; see `harness/android/ttsengine/build.gradle.kts`) that TalkBack is pointed at during a
`--screen-reader` capture so Swipewalk gets the exact text of every utterance, entirely on-device and
locally. It has no dependencies beyond the Android SDK itself, so it adds nothing to this page beyond
being Swipewalk's own code (covered by the Swipewalk License).

## Included for the Android web-content audit (`third-party/axe-core`), only run when `--web-audit` is used

| Component | License | Notes |
|---|---|---|
| [axe-core](https://github.com/dequelabs/axe-core) 4.10.3 (`axe.min.js`) | MPL 2.0 | Vendored unmodified (see `third-party/axe-core/README.md`); injected into a WebView's own page over the Chrome DevTools Protocol by `scan --web-audit` (Android debug/inspectable builds only -- `src/Swipewalk.Collectors/Android/AndroidWebAudit.cs`) for a deeper audit of the WebView's DOM. Its own `LICENSE` file ships alongside it unchanged. This is a separate copy from the developer-only one below; distinct because this one ships with Swipewalk and runs against a scanned app's own WebView, while the one below only ever runs against Swipewalk's own generated reports during development. Source Code Form: https://github.com/dequelabs/axe-core/tree/v4.10.3 |

**axe-core source.** axe-core is licensed under the Mozilla Public License 2.0. The copy above is unmodified, and its
source code is available from its authors: <https://github.com/dequelabs/axe-core/tree/v4.10.3>. If Swipewalk ever
changes axe-core, the changed files stay under the MPL 2.0 and are published.

## Used only to build and test (not distributed)

| Component | License |
|---|---|
| [xUnit](https://github.com/xunit/xunit) (`xunit`, `xunit.runner.visualstudio`) | Apache 2.0 |
| `Microsoft.NET.Test.Sdk` | MIT |
| [coverlet](https://github.com/coverlet-coverage/coverlet) (`coverlet.collector`) | MIT |
| [axe-core](https://github.com/dequelabs/axe-core) (`tools/report-axe`, developer check of the HTML reports) | MPL 2.0 |
| [Puppeteer](https://github.com/puppeteer/puppeteer) (`puppeteer-core`, same tool) | Apache 2.0 |

## Tools Swipewalk runs but does not include

Swipewalk calls these tools on your computer; install them yourself under their own terms.

- Android SDK Platform-Tools (`adb`), under the Android Software Development Kit License Agreement.
- Xcode and its command-line tools (`xcodebuild`, `xcrun simctl`, `devicectl`), under Apple's Xcode
  and Apple SDKs Agreement.
- Apple's accessibility audit (`XCUIApplication.performAccessibilityAudit`), part of XCTest.
