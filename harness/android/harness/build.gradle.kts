// Swipewalk's Android instrumentation harness: a thin UiAutomation + Google Accessibility Test
// Framework (ATF) reader, mirroring harness/ios's XCUITest harness. It has no "app under test" of its
// own -- this module builds only an androidTest APK (self-instrumenting), installed and run against
// whatever app is already in front on the device (see src/androidTest and this folder's README.md).
plugins {
    id("com.android.library")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "org.swipewalk.harness"
    compileSdk = 35

    defaultConfig {
        // 26 = Build.VERSION_CODES.O, the oldest API isShowingHintText() needs (see AtfCollector.kt);
        // isHeading/paneTitle (28), stateDescription (30) are read only when the running device supports
        // them (Build.VERSION.SDK_INT checks in AtfCollector.kt), same as isImportantForAccessibility (24).
        minSdk = 26
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }
    testOptions {
        targetSdk = 35
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }

    // No src/main: this module ships no app code, only the androidTest APK below.
    sourceSets {
        getByName("main") {
            java.setSrcDirs(emptyList<String>())
            res.setSrcDirs(emptyList<String>())
        }
    }
}

dependencies {
    // UiAutomation access, JUnit runner/rules.
    androidTestImplementation("androidx.test:runner:1.6.2")
    androidTestImplementation("androidx.test:rules:1.6.1")
    androidTestImplementation("androidx.test.ext:junit:1.2.1")
    androidTestImplementation("androidx.test.uiautomator:uiautomator:2.3.0")

    // Google's Accessibility Test Framework (Apache 2.0), pinned to 4.1.1, verified 2026-09-23 as
    // published on Google's Maven repository (dl.google.com/dl/android/maven2), not Maven Central.
    // See NOTICE / THIRD-PARTY-NOTICES.md for its licence text and docs/limitations.md ("android-atf-harness").
    androidTestImplementation("com.google.android.apps.common.testing.accessibility.framework:accessibility-test-framework:4.1.1")

    // ATF's own Guava dependency is Maven "runtime" scope, which Gradle doesn't put on the Kotlin
    // compiler's classpath; AtfCollector.kt needs ImmutableSet's type at compile time, so it's pinned
    // here to the exact version ATF 4.1.1 already resolves at runtime (see its POM).
    androidTestImplementation("com.google.guava:guava:31.0.1-android")
}
