package org.swipewalk.harness.ttsengine

import android.media.AudioFormat
import android.speech.tts.SynthesisCallback
import android.speech.tts.SynthesisRequest
import android.speech.tts.TextToSpeech
import android.speech.tts.TextToSpeechService
import android.util.Log

/**
 * A text-to-speech engine whose only job is to be TalkBack's speech target during a --screen-reader
 * capture, so Swipewalk gets the exact text of every utterance TalkBack asks it to speak -- no OCR, no
 * on-device caption reading, no third-party dependency (see docs/limitations.md,
 * "android-screen-reader-capture", for the OCR-based approach this replaced and why). Verified this
 * works on TalkBack 16 and 17: once `tts_default_synth` names
 * this app (set by `Swipewalk.Collectors.Android.AndroidHarness`), TalkBack routes every announcement's
 * text to [onSynthesizeText] instead of speaking it through the normal engine, with the text arriving
 * 40-100ms after the accessibility-focus action that triggered it. `harness/android/harness`'s
 * `TalkBackCollector.kt` reads back what was said from this app's logcat output (tag [Tag]) -- simpler
 * and more robust than any in-process IPC for a thin, test-only tool like this.
 *
 * Accepts every language unconditionally ([onIsLanguageAvailable]/[onLoadLanguage] never say no):
 * TalkBack itself decides what to say and in what language, so refusing one here would only make
 * TalkBack fall back to a different engine mid-capture rather than actually change what's spoken.
 *
 * A person may be relying on TalkBack for real while this capture runs; while it does, their phone is
 * silent through this engine (this is why the settings-restore path is on three independent timers --
 * see `TalkBackCollector.kt`'s remarks -- rather than only "restore when the capture finishes"). Real
 * pass-through speech (forwarding each utterance to the engine that was active before the capture, so
 * the phone keeps speaking real audio throughout) was tried and measured unreliable: on a Pixel 4a it
 * caused TalkBack to re-announce the same utterance in a fast, unbounded repeat loop (a single
 * BuggyApp screen produced items over 200KB of repeated text before this was found and pass-through was
 * removed) -- almost certainly because binding a second `TextToSpeech` client, even to an explicit
 * engine package, still interacts with the `tts_default_synth` routing this capture already changed.
 * Not shipped; [OriginalEngine] is kept (harmless, unused) in case a safer approach is found later.
 */
class TalkBackTtsEngine : TextToSpeechService() {
    companion object {
        const val Tag = "SwipewalkTtsEngine"
    }

    override fun onIsLanguageAvailable(lang: String?, country: String?, variant: String?): Int =
        TextToSpeech.LANG_AVAILABLE

    override fun onGetLanguage(): Array<String> = arrayOf("eng", "USA", "")

    override fun onLoadLanguage(lang: String?, country: String?, variant: String?): Int =
        TextToSpeech.LANG_AVAILABLE

    override fun onStop() {
        Log.i(Tag, "onStop")
    }

    override fun onSynthesizeText(request: SynthesisRequest?, callback: SynthesisCallback?) {
        val text = request?.charSequenceText?.toString() ?: request?.text ?: ""
        Log.i(Tag, "UTTERANCE: [$text]")

        if (callback == null) return
        val sampleRate = 8000
        callback.start(sampleRate, AudioFormat.ENCODING_PCM_16BIT, 1)
        // ~50ms of silence: enough that TalkBack sees "speech" happened (and moves on to the next
        // queued utterance promptly), short enough not to add per-element delay of its own.
        val silence = ByteArray((sampleRate * 0.05).toInt() * 2)
        callback.audioAvailable(silence, 0, silence.size)
        callback.done()
    }
}
