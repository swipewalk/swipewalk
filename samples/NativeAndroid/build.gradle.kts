// Root build file for the native Android ground-truth sample. Standalone Gradle project (not part
// of Swipewalk.slnx, same pattern as harness/android) so it builds without the MAUI workload.
// Unlike harness/android (a library-only androidTest module), this is a real application module.
plugins {
    id("com.android.application") version "8.6.0" apply false
    id("org.jetbrains.kotlin.android") version "2.0.20" apply false
    id("org.jetbrains.kotlin.plugin.compose") version "2.0.20" apply false
}
