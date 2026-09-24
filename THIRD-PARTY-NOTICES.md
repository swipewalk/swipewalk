# Third-party notices

Swipewalk is licensed under the [Apache License 2.0](LICENSE). It uses the components below.

## Included in the desktop app and the sample app

| Component | License | Notes |
|---|---|---|
| [Open Sans](https://github.com/googlefonts/opensans) | SIL Open Font License 1.1 | Font files in `src/Swipewalk.Desktop/Resources/Fonts` and `samples/BuggyApp/Resources/Fonts`; the license is in `OFL.txt` next to them. |
| [.NET MAUI](https://github.com/dotnet/maui) (`Microsoft.Maui.Controls`) | MIT | Desktop app and sample app. |
| `Microsoft.Extensions.Logging.Debug` | MIT | Debug builds of the MAUI apps. |
| .NET runtime and libraries | MIT | Included in self-contained builds. |

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
| [JUnit 4](https://junit.org/junit4/) (`junit:junit`) | Eclipse Public License 1.0 | Transitive dependency of AndroidX Test; its own licence text (`LICENSE-junit.txt`) is bundled inside the APK unchanged. |
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
being Swipewalk's own code (Apache 2.0, this repository's own license).

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
