package org.swipewalk.harness.ttsengine

import android.app.AlarmManager
import android.app.PendingIntent
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.provider.Settings
import android.util.Log

/**
 * The phone-side half of the --screen-reader capture's safety net: restores TalkBack and its
 * accessibility settings even if both the host machine and the instrumentation test process that
 * started the capture die before they get to restore anything themselves -- the primary restore path
 * (`Swipewalk.Collectors.Android.AndroidHarness`/harness/android's `TalkBackCollector.kt`, which runs
 * with adb shell privileges) is unaffected by this and is what runs in the ordinary case; this is only
 * the backstop for when it can't.
 *
 * Sequence, driven by `am broadcast` from the instrumentation test (shell-privileged, so it can target
 * this non-exported receiver -- see AndroidManifest.xml's remarks):
 * 1. [ActionArm], with the device's accessibility settings from immediately before the capture changes
 *    them: persisted to [Prefs] on this app's own storage (so a later, separately-started alarm receiver
 *    can read them even if this process has since died) and an inexact alarm scheduled [SafetyDelayMs]
 *    out via [ActionRestoreNow] -- inexact ([AlarmManager.setAndAllowWhileIdle]) so no special permission
 *    is needed on any Android version, at the cost of a possibly slightly later fire time, which is fine
 *    for a safety net rather than a precise timer.
 * 2. [ActionDisarm] (sent right after the primary restore path succeeds) cancels the pending alarm, so
 *    it never fires on the ordinary, successful path.
 * 3. If nothing cancels it, [ActionRestoreNow] fires (from the OS, via the same receiver) and restores
 *    every setting from [Prefs] directly with [Settings.Secure.putString]/`delete` -- this needs
 *    WRITE_SECURE_SETTINGS, which this app declares and the C# side grants once via `adb shell pm grant`
 *    (never auto-granted otherwise; see AndroidManifest.xml and AndroidHarness.cs).
 */
class SafetyTimerReceiver : BroadcastReceiver() {
    companion object {
        private const val Tag = "SwipewalkSafetyTimer"
        const val ActionArm = "org.swipewalk.harness.ttsengine.ACTION_ARM"
        const val ActionDisarm = "org.swipewalk.harness.ttsengine.ACTION_DISARM"
        const val ActionRestoreNow = "org.swipewalk.harness.ttsengine.ACTION_RESTORE_NOW"

        const val ExtraEnabledServices = "enabledServices"
        const val ExtraEnabledServicesNull = "enabledServicesNull"
        const val ExtraAccessibilityEnabled = "accessibilityEnabled"
        const val ExtraTouchExploration = "touchExploration"
        const val ExtraTtsDefaultSynth = "ttsDefaultSynth"
        const val ExtraTtsDefaultSynthNull = "ttsDefaultSynthNull"
        /** The concrete engine to speak through for pass-through (see [OriginalEngine]) -- separate
         * from [ExtraTtsDefaultSynth], which is the raw setting value to restore and can be null/unset. */
        const val ExtraOriginalEngine = "originalEngine"

        /** How long after arming the safety net fires if nothing disarms it first -- generous enough
         * that a normal capture (even a slow one, or one covering the harness's element cap) always
         * finishes and disarms first, short enough that a killed run doesn't leave TalkBack changed for
         * long. Not exact (see class remarks), so treat as "about" this long. */
        private const val SafetyDelayMs = 90_000L

        private const val Prefs = "safety_timer"

        private fun alarmIntent(context: Context): PendingIntent {
            val intent = Intent(context, SafetyTimerReceiver::class.java).setAction(ActionRestoreNow)
            return PendingIntent.getBroadcast(context, 0, intent, PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
        }
    }

    override fun onReceive(context: Context, intent: Intent) {
        when (intent.action) {
            ActionArm -> arm(context, intent)
            ActionDisarm -> disarm(context)
            ActionRestoreNow -> restore(context)
        }
    }

    private fun arm(context: Context, intent: Intent) {
        context.getSharedPreferences(Prefs, Context.MODE_PRIVATE).edit()
            .putString(ExtraEnabledServices, intent.getStringExtra(ExtraEnabledServices) ?: "")
            .putBoolean(ExtraEnabledServicesNull, intent.getBooleanExtra(ExtraEnabledServicesNull, false))
            .putString(ExtraAccessibilityEnabled, intent.getStringExtra(ExtraAccessibilityEnabled) ?: "0")
            .putString(ExtraTouchExploration, intent.getStringExtra(ExtraTouchExploration) ?: "0")
            .putString(ExtraTtsDefaultSynth, intent.getStringExtra(ExtraTtsDefaultSynth) ?: "")
            .putBoolean(ExtraTtsDefaultSynthNull, intent.getBooleanExtra(ExtraTtsDefaultSynthNull, false))
            .apply()
        OriginalEngine.write(context, intent.getStringExtra(ExtraOriginalEngine))

        val alarmManager = context.getSystemService(Context.ALARM_SERVICE) as AlarmManager
        // setExactAndAllowWhileIdle, not the inexact setAndAllowWhileIdle this started with: measured on
        // a Pixel 4a that a freshly-installed, rarely-used app (no launcher activity, so the OS never
        // sees it "opened") lands in App Standby Bucket NEVER, which delayed an inexact alarm by well
        // over two minutes past its nominal delay even with the app deviceidle-whitelisted and the
        // screen on -- Doze/whitelisting only affects Doze itself, not App Standby Bucket throttling,
        // which exact alarms are exempt from. Needs SCHEDULE_EXACT_ALARM (granted once via `adb shell
        // appops set ... SCHEDULE_EXACT_ALARM allow`, the same way WRITE_SECURE_SETTINGS is granted --
        // see AndroidHarness.cs); falls back to the inexact call if that wasn't granted for any reason,
        // which is still better than not arming the safety net at all.
        try {
            alarmManager.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, System.currentTimeMillis() + SafetyDelayMs, alarmIntent(context))
        } catch (e: SecurityException) {
            Log.i(Tag, "exact alarm not permitted, falling back to inexact: ${e.message}")
            alarmManager.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, System.currentTimeMillis() + SafetyDelayMs, alarmIntent(context))
        }
        Log.i(Tag, "armed: will restore in ${SafetyDelayMs}ms unless disarmed first")
    }

    private fun disarm(context: Context) {
        val alarmManager = context.getSystemService(Context.ALARM_SERVICE) as AlarmManager
        alarmManager.cancel(alarmIntent(context))
        Log.i(Tag, "disarmed")
    }

    /** Best effort throughout: this is already the last resort, so a failure here has nothing further
     * to fall back to except leaving the device as it is. */
    private fun restore(context: Context) {
        Log.i(Tag, "firing: restoring accessibility settings the primary path never got to")
        val prefs = context.getSharedPreferences(Prefs, Context.MODE_PRIVATE)
        try {
            if (prefs.getBoolean(ExtraEnabledServicesNull, false))
                Settings.Secure.putString(context.contentResolver, "enabled_accessibility_services", null)
            else
                Settings.Secure.putString(context.contentResolver, "enabled_accessibility_services", prefs.getString(ExtraEnabledServices, ""))
            Settings.Secure.putString(context.contentResolver, "accessibility_enabled", prefs.getString(ExtraAccessibilityEnabled, "0"))
            Settings.Secure.putString(context.contentResolver, "touch_exploration_enabled", prefs.getString(ExtraTouchExploration, "0"))
            if (prefs.getBoolean(ExtraTtsDefaultSynthNull, false))
                Settings.Secure.putString(context.contentResolver, "tts_default_synth", null)
            else
                Settings.Secure.putString(context.contentResolver, "tts_default_synth", prefs.getString(ExtraTtsDefaultSynth, ""))
            Log.i(Tag, "restore complete")
        } catch (e: SecurityException) {
            // WRITE_SECURE_SETTINGS wasn't granted (see AndroidManifest.xml/AndroidHarness.cs) --
            // nothing more this receiver can do; the device is left as it was.
            Log.e(Tag, "restore failed: WRITE_SECURE_SETTINGS not granted", e)
        }
    }
}
