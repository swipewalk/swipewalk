// Swipewalk's TalkBack-capture TTS engine: a standalone Android app (not an androidTest APK -- a real
// text-to-speech engine has to be a normal installed app for the system's TTS engine picker
// (PackageManager#queryIntentServices on android.intent.action.TTS_SERVICE) and Settings.Secure's
// tts_default_synth to recognize it) that TalkBack is pointed at for the duration of a --screen-reader
// capture, to receive TalkBack's exact utterance text -- see TalkBackTtsEngine.kt's remarks. Installed
// and driven by Swipewalk.Collectors.Android.AndroidHarness, exactly like the harness/android:harness
// module's androidTest APK.
plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "org.swipewalk.harness.ttsengine"
    compileSdk = 35

    defaultConfig {
        applicationId = "org.swipewalk.harness.ttsengine"
        minSdk = 26
        targetSdk = 35
        versionCode = 1
        versionName = "1.0"
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }

    buildTypes {
        debug {
            isMinifyEnabled = false
        }
    }
}
