package org.swipewalk.harness

import android.app.UiAutomation
import android.view.accessibility.AccessibilityWindowInfo
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.json.JSONObject
import org.junit.Test
import org.junit.runner.RunWith
import java.io.File

/**
 * Entry points run with `adb shell am instrument -w -e class org.swipewalk.harness.HarnessTest#<method>
 * -e package <app package> org.swipewalk.harness.test/androidx.test.runner.AndroidJUnitRunner`
 * (see harness/android/README.md). [capture] writes [ResultFile]; [captureScreenReader] writes
 * [ScreenReaderResultFile]. Both go into this test APK's own external files directory, which Swipewalk
 * pulls with `adb shell cat` -- no root, no extra permissions, the same external-files-dir path any
 * adb-connected test tooling uses (Appium, Firebase Test Lab).
 *
 * Thin by design: this class only wires [AtfCollector]/[TalkBackCollector] to `am instrument` and writes
 * their output.
 */
@RunWith(AndroidJUnit4::class)
class HarnessTest {
    companion object {
        /** File name written under this test package's external files dir (see class doc). */
        const val ResultFile = "harness-result.json"

        /** File name [captureScreenReader] writes its result to (see [TalkBackCollector.toJson]). */
        const val ScreenReaderResultFile = "screen-reader-result.json"
    }

    @Test
    fun capture() {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val packageName = InstrumentationRegistry.getArguments().getString("package")
            ?: throw IllegalArgumentException("Pass -e package <app package under test>.")

        val automation = instrumentation.uiAutomation
        AtfCollector.ensureRetrievesInteractiveWindows(automation)
        val windows = windowsWithRetry(automation)

        val output = JSONObject()
        output.put("nodes", AtfCollector.collectNodes(windows, packageName))
        output.put("atfIssues", AtfCollector.runAtf(automation, windows))

        val dir = instrumentation.context.getExternalFilesDir(null)
            ?: throw IllegalStateException("No external files directory available on this device.")
        File(dir, ResultFile).writeText(output.toString())
    }

    /**
     * Drives TalkBack over [packageName] and writes [ScreenReaderResultFile] (see
     * [TalkBackCollector.captureAsync] for the whole flow, including the accessibility-settings
     * restore that always runs before this method returns). Opt-in and separate from [capture]: it
     * costs roughly 1-2 seconds per element, so it only runs when
     * `Swipewalk.Collectors.Android.AndroidHarness.RunScreenReaderCaptureAsync` is asked for
     * (`--screen-reader`), never as part of an ordinary scan.
     */
    @Test
    fun captureScreenReader() {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val packageName = InstrumentationRegistry.getArguments().getString("package")
            ?: throw IllegalArgumentException("Pass -e package <app package under test>.")
        // Default UiAutomation SUPPRESSES every other accessibility service (including TalkBack) while
        // connected -- see TalkBackCollector's remarks for how this was found.
        val automation = instrumentation.getUiAutomation(android.app.UiAutomation.FLAG_DONT_SUPPRESS_ACCESSIBILITY_SERVICES)
        val result = TalkBackCollector.captureAsync(automation, packageName)

        val dir = instrumentation.context.getExternalFilesDir(null)
            ?: throw IllegalStateException("No external files directory available on this device.")
        File(dir, ScreenReaderResultFile).writeText(TalkBackCollector.toJson(result).toString())
    }

    /**
     * Repairs a leftover accessibility-settings change from a [captureScreenReader] run that crashed or
     * was killed before its own restore ran (mirrors the iOS large-text TextSizeRestore safeguard) --
     * called by `AndroidHarness`/pre-flight before trusting the device's accessibility settings. A no-op,
     * successfully, when nothing is pending.
     */
    @Test
    fun restoreScreenReaderSettings() {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val automation = instrumentation.getUiAutomation(android.app.UiAutomation.FLAG_DONT_SUPPRESS_ACCESSIBILITY_SERVICES)
        val dir = instrumentation.context.getExternalFilesDir(null)
            ?: throw IllegalStateException("No external files directory available on this device.")
        TalkBackCollector.restoreFromMarkerIfPresent(automation, File(dir, TalkBackCollector.RestoreMarkerFile))
    }

    /**
     * `UiAutomation#getWindows()` can return windows right after `adb shell am start` with none of them
     * marked active yet -- reproduced: calling this immediately after starting an activity got a
     * non-empty window list every time (system windows, mostly), but
     * `AccessibilityHierarchyAndroid.newBuilder(windows, context).build()` still threw a
     * NullPointerException ("No active windows detected"), because its own window-list path
     * (`AccessibilityHierarchyAndroid$BuilderAndroid.buildHierarchyFromWindowList`, decompiled from ATF
     * 4.1.1) requires exactly one `AccessibilityWindowInfo.isActive()` window, not merely a non-empty list
     * or a window with a root. So retry until one is active, rather than let a momentary timing gap
     * surface as "the harness failed" for a package that really is in front. [AtfCollector.runAtf]'s own
     * guard is the last-resort fallback if this still finds nothing after retrying.
     */
    private fun windowsWithRetry(automation: UiAutomation): List<AccessibilityWindowInfo> {
        repeat(15) {
            val windows = automation.windows
            if (windows.any { it.isActive }) {
                return windows
            }
            Thread.sleep(200)
        }
        return automation.windows
    }
}
