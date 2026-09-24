package org.swipewalk.harness

import android.accessibilityservice.AccessibilityServiceInfo
import android.app.UiAutomation
import android.graphics.Bitmap
import android.graphics.Rect
import android.os.Build
import android.view.accessibility.AccessibilityNodeInfo
import android.view.accessibility.AccessibilityWindowInfo
import com.google.android.apps.common.testing.accessibility.framework.AccessibilityCheckPreset
import com.google.android.apps.common.testing.accessibility.framework.AccessibilityCheckResult
import com.google.android.apps.common.testing.accessibility.framework.AccessibilityHierarchyCheckResult
import com.google.android.apps.common.testing.accessibility.framework.Parameters
import com.google.android.apps.common.testing.accessibility.framework.uielement.AccessibilityHierarchyAndroid
import com.google.android.apps.common.testing.accessibility.framework.utils.contrast.BitmapImage
import org.json.JSONArray
import org.json.JSONObject
import java.util.Locale

/**
 * Reads the accessibility tree and runs Google's Accessibility Test Framework (ATF) 4.1.1 over an
 * UNMODIFIED app, entirely through public UiAutomation/AccessibilityNodeInfo APIs -- no instrumentation
 * of the app under test. Kept thin per Swipewalk's harness convention: this is the only class besides
 * [HarnessTest], which just wires it to `am instrument`.
 */
internal object AtfCollector {

    /** One node's extra accessibility properties, keyed by [nodeKey] so the C# side can line it up with
     * the same node in its own `uiautomator dump` tree (see AndroidCollector.cs / UiAutomatorParser.cs). */
    private fun nodeJson(node: AccessibilityNodeInfo): JSONObject {
        val bounds = Rect()
        node.getBoundsInScreen(bounds)
        val json = JSONObject()
        json.put("key", nodeKey(node.className, bounds, node.viewIdResourceName, node.text, node.contentDescription))
        // API 24: isImportantForAccessibility(). Always present at this module's minSdk (26).
        json.put("isImportantForAccessibility", node.isImportantForAccessibility)
        // API 26: isShowingHintText() and getHintText() -- together these resolve "text field contents
        // and hints can't be told apart" (see docs/limitations.md "android-edittext-text") for any device
        // this harness runs on: isShowingHintText() says whether the visible text is currently the hint,
        // and getHintText() recovers the hint text itself on a device/version whose uiautomator dump
        // omits the "hint" attribute even though the field has one (seen on an Android 13 phone).
        json.put("isShowingHintText", node.isShowingHintText)
        putNullable(json, "hintText", node.hintText?.toString())
        if (Build.VERSION.SDK_INT >= 28) {
            json.put("isHeading", node.isHeading)
            putNullable(json, "paneTitle", node.paneTitle?.toString())
        }
        if (Build.VERSION.SDK_INT >= 30) {
            putNullable(json, "stateDescription", node.stateDescription?.toString())
        }
        return json
    }

    /** Walks every window's node tree, keeping only nodes belonging to [packageName]. */
    fun collectNodes(windows: List<AccessibilityWindowInfo>, packageName: String): JSONArray {
        val result = JSONArray()
        for (window in windows) {
            val root = window.root ?: continue
            walk(root, packageName, result)
        }
        return result
    }

    private fun walk(node: AccessibilityNodeInfo, packageName: String, out: JSONArray) {
        if (node.packageName?.toString() == packageName) {
            out.put(nodeJson(node))
        }
        for (i in 0 until node.childCount) {
            val child = node.getChild(i) ?: continue
            walk(child, packageName, out)
        }
    }

    /**
     * Builds an ATF [AccessibilityHierarchyAndroid] from the live windows (per Google's recommended
     * on-device path, [AccessibilityHierarchyAndroid.newBuilder]) and runs every check in
     * [AccessibilityCheckPreset.LATEST] (currently the same 14 checks as VERSION_4_0_CHECKS; see
     * harness/android/README.md) against it, with a screenshot supplied for the contrast checks.
     * Returns an empty result (not a crash) when no window is active: `newBuilder(...).build()` throws a
     * NullPointerException ("No active windows detected") unless exactly one window's
     * `AccessibilityWindowInfo.isActive()` is true (see [HarnessTest.windowsWithRetry]'s doc comment for
     * where this was reproduced). [HarnessTest.windowsWithRetry] already retries for the transient case
     * (right after `adb shell am start`) before calling this; this guard is the last-resort fallback if it
     * still finds nothing (e.g. a package with no matching window at all).
     */
    fun runAtf(automation: UiAutomation, windows: List<AccessibilityWindowInfo>): JSONArray {
        if (windows.none { it.isActive }) {
            return JSONArray()
        }
        val context = androidx.test.platform.app.InstrumentationRegistry.getInstrumentation().targetContext
        val hierarchy = AccessibilityHierarchyAndroid.newBuilder(windows, context).build()

        val parameters = Parameters()
        val screenshot: Bitmap? = automation.takeScreenshot()
        if (screenshot != null) {
            parameters.putScreenCapture(BitmapImage(screenshot))
        }

        val results = JSONArray()
        for (check in AccessibilityCheckPreset.getAccessibilityHierarchyChecksForPreset(AccessibilityCheckPreset.LATEST)) {
            val checkResults: List<AccessibilityHierarchyCheckResult> =
                try {
                    check.runCheckOnHierarchy(hierarchy, null, parameters)
                } catch (e: RuntimeException) {
                    // One check failing (e.g. no screenshot, or a device quirk) must not lose the rest.
                    emptyList()
                }
            for (result in checkResults) {
                // Only issues a person should look at: NOT_RUN/RESOLVED/SUPPRESSED are ATF bookkeeping,
                // not findings, and INFO is below what Swipewalk surfaces elsewhere (see AtfIssueRule.cs).
                if (result.type == AccessibilityCheckResult.AccessibilityCheckResultType.ERROR
                    || result.type == AccessibilityCheckResult.AccessibilityCheckResultType.WARNING
                ) {
                    results.put(resultJson(check.javaClass.simpleName, result))
                }
            }
        }
        return results
    }

    private fun resultJson(checkName: String, result: AccessibilityHierarchyCheckResult): JSONObject {
        val json = JSONObject()
        json.put("checkName", checkName)
        json.put("resultType", result.type.name)
        json.put("message", result.getMessage(Locale.US)?.toString() ?: "")
        val element = result.element
        if (element != null) {
            val bounds = element.boundsInScreen
            if (bounds != null) {
                json.put("bounds", "[${bounds.left},${bounds.top}][${bounds.right},${bounds.bottom}]")
            }
            putNullable(json, "resourceId", element.resourceName)
            putNullable(json, "text", element.text?.toString())
            putNullable(json, "contentDescription", element.contentDescription?.toString())
            putNullable(json, "className", element.className?.toString())
        }
        return json
    }

    /** Grants this UiAutomation the interactive-windows flag [AccessibilityHierarchyAndroid.newBuilder]
     * (and ATF's checks) need to see every window, not just the active one -- otherwise
     * `UiAutomation#getWindows()` returns an empty list. */
    fun ensureRetrievesInteractiveWindows(automation: UiAutomation) {
        val info = automation.serviceInfo ?: AccessibilityServiceInfo()
        info.flags = info.flags or AccessibilityServiceInfo.FLAG_RETRIEVE_INTERACTIVE_WINDOWS
        automation.serviceInfo = info
    }

    /** Same identity a uiautomator XML dump's node would have (see UiAutomatorParser.Key in
     * AndroidCollector's C# code): class, screen bounds, resource id, text and content description,
     * in that order, joined with "|" and using "" (never the literal string "null") for anything
     * unset -- uiautomator's dump XML always has these attributes, empty when the node has none. */
    private fun nodeKey(className: CharSequence?, bounds: Rect, resourceId: String?, text: CharSequence?, contentDescription: CharSequence?): String {
        val boundsText = "[${bounds.left},${bounds.top}][${bounds.right},${bounds.bottom}]"
        return listOf(className?.toString() ?: "", boundsText, resourceId ?: "", text?.toString() ?: "", contentDescription?.toString() ?: "")
            .joinToString("|")
    }

    private fun putNullable(json: JSONObject, key: String, value: String?) {
        if (value != null) json.put(key, value)
    }
}
