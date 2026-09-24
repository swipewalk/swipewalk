package org.swipewalk.harness.ttsengine

import android.content.Context

/**
 * The concrete TTS engine package that was actually active before a capture changed
 * `tts_default_synth` -- distinct from the raw original setting value [SafetyTimerReceiver] restores
 * (which can be unset/null, meaning "whatever the system picks"): [TalkBackTtsEngine.passThrough] needs
 * an actual package to speak through, so `AndroidHarness.cs` resolves the effective default (via
 * `TextToSpeech.getDefaultEngine()`, read by harness/android's `TalkBackCollector.kt` before arming)
 * and this stores that resolved value, written at the same time as [SafetyTimerReceiver.ActionArm]'s
 * other extras.
 */
internal object OriginalEngine {
    private const val Prefs = "original_engine"
    private const val Key = "package"

    fun write(context: Context, enginePackage: String?) {
        context.getSharedPreferences(Prefs, Context.MODE_PRIVATE).edit().putString(Key, enginePackage).apply()
    }

    fun read(context: Context): String? = context.getSharedPreferences(Prefs, Context.MODE_PRIVATE).getString(Key, null)
}
