// Root build file for the Swipewalk Android harness. Deliberately minimal: this Gradle project exists
// only to build the instrumentation test APK in :harness and the small standalone TTS-engine app in
// :ttsengine (see README.md in this folder).
plugins {
    id("com.android.library") version "8.6.0" apply false
    id("com.android.application") version "8.6.0" apply false
    id("org.jetbrains.kotlin.android") version "2.0.20" apply false
}
