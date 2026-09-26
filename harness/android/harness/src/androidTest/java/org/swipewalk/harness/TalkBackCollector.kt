package org.swipewalk.harness

import android.app.UiAutomation
import android.graphics.Rect
import android.speech.tts.TextToSpeech
import android.view.accessibility.AccessibilityNodeInfo
import androidx.test.platform.app.InstrumentationRegistry
import org.json.JSONArray
import org.json.JSONObject
import java.io.File

/**
 * Drives Android's TalkBack screen reader over every focusable/interactive element of the app under
 * test, and reads back what it actually said -- real evidence to compare against Swipewalk's own
 * predicted transcript (see `Swipewalk.Core.ScreenReader.ScreenReaderCaptureComparer`). Verified by hand
 * on a Pixel 4a (Android 13, TalkBack 17.0.1) and the emulator (TalkBack 16); the surprises that shaped
 * this design:
 *
 * 1. The default `UiAutomation` SUPPRESSES every other accessibility service while connected, so TalkBack
 *    never actually runs unless the connection is opened with [UiAutomation.FLAG_DONT_SUPPRESS_ACCESSIBILITY_SERVICES]
 *    (see [captureAsync]'s caller, `HarnessTest`).
 * 2. TalkBack routes its speech through whichever engine `Settings.Secure.tts_default_synth` names.
 *    Pointing that at `harness/android/ttsengine`'s `TalkBackTtsEngine` (a small separate app -- a TTS
 *    engine has to be a real installed app, not something that can live only in this androidTest APK)
 *    gets Swipewalk the EXACT text of every utterance, entirely on-device, with no OCR and no
 *    third-party dependency: an earlier design read TalkBack's on-screen caption with Google's ML Kit
 *    OCR, which is not open source and could contact Google's servers -- see docs/limitations.md,
 *    "android-screen-reader-capture", for that history.
 * 3. Utterances arrive 40-100ms after the [AccessibilityNodeInfo.ACTION_ACCESSIBILITY_FOCUS] that
 *    triggers them (read back via logcat -- see [readUtterances]); an older TalkBack version can split
 *    one element's announcement into several short utterances, which this joins together (see
 *    [Item.spokenText] and `Swipewalk.Core.ScreenReader.ScreenReaderCaptureComparer`'s handling of the
 *    join separator). If nothing arrives at all for an element within [PerElementTimeoutMs], that
 *    element gets no item (see [Missing]); if that happens for the first couple of elements on a
 *    screen, TalkBack most likely isn't routing through Swipewalk's engine at all (a phone-maker's own
 *    TalkBack, a managed device with the TTS setting locked, ...) and the whole capture stops rather
 *    than silently returning 40 empty elements -- see [RefusalThreshold].
 *
 * Because a person may be relying on TalkBack for real while this runs, every accessibility setting
 * this changes is restored through three independent paths, in order of preference: this method's own
 * `finally` block (the normal path, every time); [restoreFromMarkerIfPresent] (checked at the start of
 * the next capture, or on demand via `HarnessTest#restoreScreenReaderSettings`, from the device-side
 * marker this writes before any change); and harness/android/ttsengine's `SafetyTimerReceiver`, an
 * on-device alarm armed before any change and disarmed once this method's own restore succeeds, which
 * fires on its own (needing no adb/host connection at all) if neither of the other two gets to run --
 * see that class's remarks. `Swipewalk.Collectors.Android.AndroidHarness`/`ScreenReaderSettingsRestore`
 * keep a fourth copy on the host machine for pre-flight to use even if the device-side marker is lost.
 */
internal object TalkBackCollector {
    const val TalkBackPackage = "com.google.android.marvin.talkback"
    private const val TalkBackService = "$TalkBackPackage/com.google.android.marvin.talkback.TalkBackService"

    const val TtsEnginePackage = "org.swipewalk.harness.ttsengine"
    private const val TtsEngineComponent = "$TtsEnginePackage/.SafetyTimerReceiver"
    private const val UtteranceTag = "SwipewalkTtsEngine"

    /** Element cap per screen: keeps a busy screen's capture bounded (see this type's remarks on cost). */
    private const val MaxElements = 40

    /** How long to wait for at least one utterance after focusing an element before giving up on it
     * (measured on a Pixel 4a: a typical utterance arrives in 40-100ms; this is generous headroom for a
     * slower device or a momentary TalkBack hiccup -- see [RefusalThreshold] for when repeated timeouts
     * mean something more serious than one slow element). */
    private const val PerElementTimeoutMs = 1000L

    /** Once at least one utterance has arrived for an element, how much longer to wait for more of the
     * SAME element's announcement (an older TalkBack can split one announcement into several utterances
     * a Swipewalk-measured ~30-75ms apart on a Pixel 4a) before moving on. */
    private const val JoinWindowMs = 200L

    /** If this many of the FIRST elements on a screen time out completely (see [PerElementTimeoutMs]),
     * TalkBack is most likely not routing speech through Swipewalk's engine at all (rather than just
     * having a slow or quiet first element), so the whole capture stops with a plain reason instead of
     * silently walking the rest of the screen with nothing to show for it. */
    private const val RefusalThreshold = 2

    /** Joins multiple utterances captured for one element (see this type's remarks, point 3). Exposed so
     * the C# comparator can split it back apart -- see `ScreenReaderCaptureComparer`'s remarks on why a
     * joined multi-part utterance needs per-part parsing, not one flat string. */
    const val UtteranceJoinSeparator = " || "

    /** File the marker lives in, inside the harness test package's own external files dir -- see
     * [RestoreState] and [restoreFromMarkerIfPresent]/`HarnessTest#restoreScreenReaderSettings`. */
    const val RestoreMarkerFile = "screen-reader-restore-state.json"

    /** The accessibility settings this collector can change, and their values immediately before it
     * changes them -- written to [RestoreMarkerFile] BEFORE any change, so a crash or a killed process
     * leaves behind exactly what's needed to put the device back. [Null] stands in for "the setting was
     * unset", since an empty string can't round-trip through `settings put`/`settings delete`
     * unambiguously. */
    data class RestoreState(
        val enabledServices: String,
        val accessibilityEnabled: String,
        val touchExploration: String,
        val ttsDefaultSynth: String,
    ) {
        fun toJson(): String = JSONObject()
            .put("enabledServices", enabledServices)
            .put("accessibilityEnabled", accessibilityEnabled)
            .put("touchExploration", touchExploration)
            .put("ttsDefaultSynth", ttsDefaultSynth)
            .toString()

        companion object {
            const val Null = "\u0000null\u0000"
            fun fromJson(text: String): RestoreState {
                val j = JSONObject(text)
                return RestoreState(
                    j.getString("enabledServices"), j.getString("accessibilityEnabled"),
                    j.getString("touchExploration"), j.getString("ttsDefaultSynth"))
            }
        }
    }

    /** One capture's outcome; see `AndroidHarness.HarnessResult`-style parsing on the C# side. [Missing]
     * lists elements this walked but got no utterance for, by their [Item.key] scheme, in walk order --
     * kept separate from [Item] (which requires real spoken text) so the C# side can tell "TalkBack said
     * nothing about this element" from "this element wasn't walked at all" (see this type's remarks). */
    data class Item(val order: Int, val spokenText: String, val key: String)
    /** [language] is the device's system language (BCP-47, e.g. "es-ES") at capture time -- not a
     * per-utterance value, since it can't change mid-capture. The C# comparator uses it to tell whether
     * `Swipewalk.Core.ScreenReader.RoleWords`' English vocabulary can be trusted to recognize a role word
     * in [Item.spokenText]: on a non-English device, an unrecognized segment might be a role/hint word in
     * that language rather than a real accessible name, so a predicted-empty name is not compared against
     * it (see the comparator's remarks). Null only when it could not be read at all. */
    data class Result(
        val talkBackVersion: String?, val items: List<Item>, val missingCount: Int,
        val complete: Boolean, val notCompleteReason: String?, val language: String? = null)

    /**
     * Runs a full capture for [packageName], which must already be the foreground app. Always restores
     * every accessibility setting it changed before returning -- see this type's remarks. Never throws
     * for an ordinary "TalkBack isn't installed"/"engine not routing" situation: those come back as
     * [Result.notCompleteReason] with an empty item list, the same best-effort contract
     * `Swipewalk.Collectors.Android.AndroidHarness.RunAsync` already has for the ATF harness.
     */
    fun captureAsync(automation: UiAutomation, packageName: String): Result {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        // Read once, up front: the device's language governs every utterance in this capture (see
        // [Result.language]'s remarks), and reading it never fails the way TalkBack/engine checks can.
        val language = java.util.Locale.getDefault().toLanguageTag()
        val dir = instrumentation.context.getExternalFilesDir(null)
            ?: return Result(null, emptyList(), 0, false, "no external files directory available on this device", language)
        val markerFile = File(dir, RestoreMarkerFile)

        if (!isTalkBackInstalled(automation))
            return Result(null, emptyList(), 0, false,
                "Google's TalkBack (Android Accessibility Suite) is not installed on this device (other TalkBack builds, such as a phone maker's own, aren't supported yet)", language)
        if (!isTtsEngineInstalled(automation))
            return Result(null, emptyList(), 0, false,
                "Swipewalk's TalkBack-capture TTS engine (harness/android/ttsengine) is not installed on this device", language)

        // Without this, automation.windows comes back empty and every window/node lookup below silently
        // finds nothing.
        AtfCollector.ensureRetrievesInteractiveWindows(automation)

        // Restore any leftover from an earlier run that crashed before its own finally block ran, before
        // touching anything new -- otherwise this capture's own "original state" snapshot below would
        // capture the LEFTOVER (already-changed) state as if it were the real original.
        restoreFromMarkerIfPresent(automation, markerFile)

        val original = RestoreState(
            enabledServices = readSetting(automation, "enabled_accessibility_services") ?: RestoreState.Null,
            accessibilityEnabled = readSetting(automation, "accessibility_enabled") ?: "0",
            touchExploration = readSetting(automation, "touch_exploration_enabled") ?: "0",
            ttsDefaultSynth = readSetting(automation, "tts_default_synth") ?: RestoreState.Null,
        )
        // The engine in place before this capture, recorded only so the safety timer's own restore path
        // has it too (see `SafetyTimerReceiver`/`OriginalEngine`); Swipewalk's engine no longer speaks
        // through it (an earlier pass-through design was tried and removed -- see
        // `TalkBackTtsEngine`'s remarks) but the value is still resolved and kept in case a safer
        // pass-through approach is found later.
        val originalEngine = resolveDefaultEngine(instrumentation)

        markerFile.writeText(original.toJson()) // written BEFORE any change -- see this type's remarks
        armSafetyTimer(automation, original, originalEngine)

        var notCompleteReason: String? = null
        val items = mutableListOf<Item>()
        var missingCount = 0
        var talkBackVersion: String? = null

        try {
            enableTalkBack(automation, original.enabledServices)
            talkBackVersion = talkBackVersionName(automation)
            enableTtsEngine(automation, original.ttsDefaultSynth)

            sh(automation, "monkey -p $packageName -c android.intent.category.LAUNCHER 1")
            Thread.sleep(1500)
            val root = foregroundRoot(automation, packageName)
            if (root == null) {
                notCompleteReason = "$packageName did not come back to the front after enabling TalkBack"
                return Result(talkBackVersion, emptyList(), 0, false, notCompleteReason, language)
            }

            val candidates = mutableListOf<AccessibilityNodeInfo>()
            collectStops(root, candidates)
            val walked = candidates.take(MaxElements)
            val capped = candidates.size > MaxElements
            // This walk only ever covers focusable/interactive elements (see collectStops), never plain
            // informational text -- ScreenReaderPredictor.Predict also stops at plain named text, which
            // this capture deliberately skips to keep the per-element cost down. That's a property of the
            // SCOPE this route captures (Swipewalk.Core.Model.ScreenReaderCaptureScope.FocusableElementsOnly,
            // set on the C# side in AndroidCollector.LoadScreenReaderCapture -- not carried through this
            // JSON, since it never varies per capture), not a reason any ONE capture is incomplete, so it
            // no longer disqualifies `complete` below on its own: `complete` now means "every
            // focusable/interactive element this walk found was actually reached and said something",
            // which is genuinely sometimes true. ScreenReaderCaptureComparer's own Missing-diff check still
            // never fires for this scope at all, complete or not (see ScreenReaderCaptureScope's remarks):
            // this harness has no way today to say WHICH exact elements it walked, only how many items it
            // captured, so treating an un-walked predicted stop as a real gap would risk a false 4.1.2
            // citation from a node-identity mismatch, not genuine evidence.

            // Warm-up: focus the root itself first so the window-entry announcement (TalkBack speaks the
            // app/window name whenever a window with accessibility focus changes) is out of the way
            // before the first real element -- otherwise the first element's utterance can be preceded
            // or replaced by that unrelated one.
            readUtterances(automation) { root.performAction(AccessibilityNodeInfo.ACTION_ACCESSIBILITY_FOCUS) }

            var order = 0
            var consecutiveTimeouts = 0
            for (node in walked) {
                val utterances = readUtterances(automation) { node.performAction(AccessibilityNodeInfo.ACTION_ACCESSIBILITY_FOCUS) }
                if (utterances.isEmpty()) {
                    missingCount++
                    consecutiveTimeouts++
                    if (order == 0 && consecutiveTimeouts >= RefusalThreshold) {
                        notCompleteReason = "TalkBack did not say anything through Swipewalk's engine for the first $RefusalThreshold elements " +
                            "(a managed device or a phone maker's own TalkBack build may not honor the text-to-speech engine setting)"
                        return Result(talkBackVersion, items, missingCount, false, notCompleteReason, language)
                    }
                    continue
                }
                consecutiveTimeouts = 0
                order++
                items.add(Item(order, utterances.joinToString(UtteranceJoinSeparator), nodeKey(node)))
            }
            // complete requires BOTH: the cap wasn't hit (every focusable/interactive element found was
            // actually attempted) AND TalkBack said something for every one of them (missingCount == 0) --
            // deliberately simple/strict rather than tolerating a few silent elements: a single timeout is
            // common enough (this study saw one caption lag ~0.6 s) that a softer threshold risks quietly
            // treating a capture with real gaps as trustworthy for the 2.5.3 real-evidence check that
            // depends on `complete`. The cost is that one silent element turns that check off for this
            // whole screen, not just for the element that stayed silent.
            val reason = when {
                capped -> "screen has more focusable/interactive elements (${candidates.size}) than the capture limit ($MaxElements), and plain informational text was not captured either"
                missingCount > 0 -> "TalkBack said nothing for $missingCount of ${walked.size} element(s) it focused -- it may have been briefly unavailable during this capture"
                else -> null
            }
            notCompleteReason = reason
            return Result(talkBackVersion, items, missingCount, complete = reason == null, notCompleteReason, language)
        } catch (e: Exception) {
            return Result(talkBackVersion, items, missingCount, false, "the TalkBack capture failed: ${e.message ?: e.javaClass.simpleName}", language)
        } finally {
            // ALWAYS attempt to restore, however this method is leaving -- normal return, an exception
            // above, or the instrumentation process being killed partway (in which case this finally
            // block itself may not get to run; that's exactly what the marker file,
            // restoreFromMarkerIfPresent, and the on-device safety timer armed above are for). Only
            // delete the marker and disarm the timer once every setting is CONFIRMED back (see
            // restoreSettings' remarks) -- an unconfirmed "the calls didn't throw" leaves both in place,
            // for the next --screen-reader run's leftover repair, `doctor`, or the timer itself to finish
            // the job; this is the C# side's own contract too (AndroidHarness.HasLeftoverScreenReaderSettingsAsync).
            val restored = try { restoreSettings(automation, original) } catch (_: Exception) { false }
            if (restored) {
                markerFile.delete()
                disarmSafetyTimer(automation)
            }
            // Best effort: leave the target app back in front rather than wherever the walk left off, so
            // the next capture in this run (scan or record) doesn't have to fail once just to notice.
            try { sh(automation, "monkey -p $packageName -c android.intent.category.LAUNCHER 1") } catch (_: Exception) { }
        }
    }

    /**
     * Focuses [action] with logcat cleared just before, then collects every [UtteranceTag] utterance
     * that shows up: waits up to [PerElementTimeoutMs] for the first one, then [JoinWindowMs] more for
     * any further parts of the SAME announcement (an older TalkBack can split one announcement into
     * several -- see this type's remarks, point 3). Returns them in arrival order, empty if none arrived
     * at all within the timeout.
     */
    private fun readUtterances(automation: UiAutomation, action: () -> Boolean): List<String> {
        sh(automation, "logcat -c")
        val ok = action()
        if (!ok) return emptyList() // not accessibility-focusable right now (e.g. went off-screen mid-walk)

        val deadline = System.currentTimeMillis() + PerElementTimeoutMs
        var sawFirst = false
        var joinDeadline = 0L
        while (true) {
            val now = System.currentTimeMillis()
            val utterances = dumpUtterances(automation)
            if (utterances.isNotEmpty() && !sawFirst) {
                sawFirst = true
                joinDeadline = now + JoinWindowMs
            }
            if (sawFirst && now >= joinDeadline) return dumpUtterances(automation)
            if (!sawFirst && now >= deadline) return emptyList()
            Thread.sleep(30)
        }
    }

    private fun dumpUtterances(automation: UiAutomation): List<String> =
        sh(automation, "logcat -d -s $UtteranceTag:I")
            .lineSequence()
            .mapNotNull { line -> Regex("UTTERANCE: \\[(.*)]$").find(line)?.groupValues?.get(1) }
            .toList()

    /** Called before any real work, and from `HarnessTest#restoreScreenReaderSettings` (a tiny standalone
     * entry point the C# side calls before a capture, and can call on its own after a crash -- see
     * `Swipewalk.Collectors.Android.AndroidHarness`): if a marker is on disk, an earlier run changed
     * settings and never got to restore them, so restore from it now, before anything else happens. */
    fun restoreFromMarkerIfPresent(automation: UiAutomation, markerFile: File) {
        if (!markerFile.exists()) return
        // A marker whose JSON didn't parse is never deleted here: leaving it for a future attempt (or a
        // person to look at) is safer than discarding the one record of what to put back.
        val state = runCatching { RestoreState.fromJson(markerFile.readText()) }.getOrNull() ?: return
        // Only delete the marker and disarm the timer once every setting is CONFIRMED back to its
        // original value (read back below) -- not just because restoreSettings ran without throwing.
        // Each individual `settings put`/`delete` call can silently fail (device busy, a transient
        // permission hiccup) while the overall call still returns normally, and this marker/timer pair is
        // exactly the safety net for "something didn't actually restore" -- deleting it on unconfirmed
        // success would defeat it.
        val restored = try { restoreSettings(automation, state) } catch (_: Exception) { false }
        if (restored) {
            markerFile.delete()
            disarmSafetyTimer(automation)
        }
    }

    /** Puts every setting back and reads each one back afterward to confirm it actually took --
     * returns true only if all four are confirmed. See [restoreFromMarkerIfPresent]'s remarks for why a
     * plain "the calls didn't throw" isn't good enough here. */
    private fun restoreSettings(automation: UiAutomation, original: RestoreState): Boolean {
        val a = putOrDeleteSetting(automation, "enabled_accessibility_services", original.enabledServices)
        val b = putOrDeleteSetting(automation, "accessibility_enabled", original.accessibilityEnabled)
        val c = putOrDeleteSetting(automation, "touch_exploration_enabled", original.touchExploration)
        val d = putOrDeleteSetting(automation, "tts_default_synth", original.ttsDefaultSynth)
        return a && b && c && d
    }

    /** Sets (or deletes) one setting, then reads it back to confirm -- returns whether the read-back
     * value actually matches [value], not just whether the `settings put`/`delete` command itself threw. */
    private fun putOrDeleteSetting(automation: UiAutomation, key: String, value: String): Boolean {
        return try {
            if (value == RestoreState.Null) sh(automation, "settings delete secure $key")
            else sh(automation, "settings put secure $key $value")
            (readSetting(automation, key) ?: RestoreState.Null) == value
        } catch (_: Exception) {
            false
        }
    }

    private fun readSetting(automation: UiAutomation, key: String): String? {
        val value = sh(automation, "settings get secure $key").trim()
        return if (value == "null" || value.isEmpty()) null else value
    }

    private fun isTalkBackInstalled(automation: UiAutomation): Boolean =
        sh(automation, "pm list packages $TalkBackPackage").contains("package:$TalkBackPackage")

    private fun isTtsEngineInstalled(automation: UiAutomation): Boolean =
        sh(automation, "pm list packages $TtsEnginePackage").contains("package:$TtsEnginePackage")

    /** Merges Swipewalk's TalkBack service into whatever's already enabled (never drops another enabled
     * accessibility service the person may be relying on), and waits for it to bind. */
    private fun enableTalkBack(automation: UiAutomation, originalEnabledServices: String) {
        val already = if (originalEnabledServices == RestoreState.Null) "" else originalEnabledServices
        val merged = if (already.split(':').contains(TalkBackService)) already
            else if (already.isEmpty()) TalkBackService else "$already:$TalkBackService"
        sh(automation, "settings put secure enabled_accessibility_services $merged")
        sh(automation, "settings put secure accessibility_enabled 1")
        Thread.sleep(2000) // observed bind time on a Pixel 4a; generous margin for a slower device
    }

    private fun enableTtsEngine(automation: UiAutomation, @Suppress("UNUSED_PARAMETER") originalTtsDefaultSynth: String) {
        sh(automation, "settings put secure tts_default_synth $TtsEnginePackage")
        Thread.sleep(500) // observed switch time on a Pixel 4a
    }

    private fun talkBackVersionName(automation: UiAutomation): String? {
        val dump = sh(automation, "dumpsys package $TalkBackPackage")
        return Regex("versionName=([\\w.]+)").find(dump)?.groupValues?.get(1)
    }

    /** The TTS engine actually in effect before this capture changes it -- resolved via a throwaway
     * client rather than read straight from `tts_default_synth` (which can be null/unset, meaning "the
     * system picks one"). Not used to speak through any more (a pass-through design that needed this was
     * tried and removed -- see `TalkBackTtsEngine`'s remarks); kept only because `SafetyTimerReceiver`
     * still records it (`OriginalEngine`, itself unused for now) in case a safer pass-through approach is
     * found later. Null if it can't be resolved for any reason. */
    private fun resolveDefaultEngine(instrumentation: android.app.Instrumentation): String? {
        var result: String? = null
        val latch = java.util.concurrent.CountDownLatch(1)
        var client: TextToSpeech? = null
        client = TextToSpeech(instrumentation.targetContext) { _ ->
            result = client?.defaultEngine
            latch.countDown()
        }
        latch.await(2, java.util.concurrent.TimeUnit.SECONDS)
        client.shutdown()
        return result
    }

    /** Arms harness/android/ttsengine's on-device safety timer (see this type's remarks) before any
     * setting is actually changed -- best effort: if the ttsengine app can't be reached for any reason,
     * the capture still proceeds (the marker file and this method's own `finally` restore are still in
     * place), just without the on-device backstop for this one capture. */
    private fun armSafetyTimer(automation: UiAutomation, original: RestoreState, originalEngine: String?) {
        val extras = StringBuilder()
        fun extra(key: String, value: String) { extras.append(" --es $key ").append(shellQuote(value)) }
        fun flag(key: String, value: Boolean) { extras.append(" --ez $key ").append(value) }
        flag("enabledServicesNull", original.enabledServices == RestoreState.Null)
        extra("enabledServices", if (original.enabledServices == RestoreState.Null) "" else original.enabledServices)
        extra("accessibilityEnabled", original.accessibilityEnabled)
        extra("touchExploration", original.touchExploration)
        flag("ttsDefaultSynthNull", original.ttsDefaultSynth == RestoreState.Null)
        extra("ttsDefaultSynth", if (original.ttsDefaultSynth == RestoreState.Null) "" else original.ttsDefaultSynth)
        if (originalEngine != null) extra("originalEngine", originalEngine)
        try {
            sh(automation, "am broadcast -a org.swipewalk.harness.ttsengine.ACTION_ARM -n $TtsEngineComponent$extras")
        } catch (_: Exception) { /* best effort: see this method's own remarks */ }
    }

    private fun disarmSafetyTimer(automation: UiAutomation) {
        try { sh(automation, "am broadcast -a org.swipewalk.harness.ttsengine.ACTION_DISARM -n $TtsEngineComponent") }
        catch (_: Exception) { /* best effort */ }
    }

    private fun shellQuote(value: String): String = "'" + value.replace("'", "'\\''") + "'"

    /** Waits (briefly retrying) for [packageName]'s window to appear and returns its root, or null. */
    private fun foregroundRoot(automation: UiAutomation, packageName: String): AccessibilityNodeInfo? {
        repeat(15) {
            val window = automation.windows.firstOrNull { it.root?.packageName?.toString() == packageName }
            if (window?.root != null) return window.root
            Thread.sleep(200)
        }
        return null
    }

    /** Narrower than `Swipewalk.Core.ScreenReader.ScreenReaderPredictor`'s own stops: the predictor also
     * stops at plain informational text with a name, which this walk deliberately skips to keep the
     * per-element cost down -- a fixed scope for this whole route
     * (`Swipewalk.Core.Model.ScreenReaderCaptureScope.FocusableElementsOnly`, set on the C# side), not
     * something `complete` on the result below reflects any more. */
    private fun collectStops(node: AccessibilityNodeInfo, out: MutableList<AccessibilityNodeInfo>) {
        if (node.isImportantForAccessibility && node.isVisibleToUser &&
            (node.isClickable || node.isLongClickable || node.isCheckable || node.isFocusable || node.isEditable))
            out.add(node)
        for (i in 0 until node.childCount) {
            val c = node.getChild(i) ?: continue
            collectStops(c, out)
        }
    }

    /** Same node-identity scheme as `AtfCollector.nodeKey` (class, screen bounds, resource id, text,
     * content description), so the C# side can match a captured item to the same node identity it already
     * uses to merge ATF's extra properties -- see `Swipewalk.Collectors.Android.AndroidHarness`. */
    private fun nodeKey(node: AccessibilityNodeInfo): String {
        val bounds = Rect()
        node.getBoundsInScreen(bounds)
        val boundsText = "[${bounds.left},${bounds.top}][${bounds.right},${bounds.bottom}]"
        return listOf(
            node.className?.toString() ?: "", boundsText, node.viewIdResourceName ?: "",
            node.text?.toString() ?: "", node.contentDescription?.toString() ?: "",
        ).joinToString("|")
    }

    private fun sh(automation: UiAutomation, cmd: String): String {
        val pfd = automation.executeShellCommand(cmd)
        return java.io.FileInputStream(pfd.fileDescriptor).bufferedReader().use { it.readText() }
    }

    fun toJson(result: Result): JSONObject {
        val items = JSONArray()
        for (item in result.items)
            items.put(JSONObject().put("order", item.order).put("spokenText", item.spokenText).put("key", item.key))
        val json = JSONObject()
            .put("items", items)
            .put("missingCount", result.missingCount)
            .put("complete", result.complete)
        result.talkBackVersion?.let { json.put("talkBackVersion", it) }
        result.notCompleteReason?.let { json.put("notCompleteReason", it) }
        result.language?.let { json.put("language", it) }
        return json
    }
}
