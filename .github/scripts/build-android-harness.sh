#!/usr/bin/env bash
# Builds the Android harness's debug test APK (ATF) and the standalone TTS-engine app's debug APK
# (TalkBack capture, opt-in with --screen-reader) with Gradle.
#
# Used by ci.yml (build-test, build-desktop) and release.yml, which all need the same prebuilt
# APKs bundled into the CLI/desktop package (see Swipewalk.Collectors.csproj).
#
# --stacktrace: the ":harness:packageDebugAndroidTest" task has twice failed intermittently in CI
# with only "A failure occurred while executing
# com.android.build.gradle.tasks.PackageAndroidArtifact$IncrementalSplitterRunnable" and no visible
# cause; a re-run passed both times. That wrapper message is a known Android Gradle Plugin pattern
# (e.g. mozilla-mobile/fenix#22916) that hides the real, usually memory-related exception --
# "OutOfMemoryError: Java heap space" or "Self-suppression not permitted" from a second failure
# while handling the first -- unless Gradle is asked for full stack traces. If it recurs,
# --stacktrace should print the real cause in the job log.
#
# Retry: rather than fail a whole CI run (or a release tag build) on what has so far always been a
# one-off, retry the build once before giving up.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../../harness/android"

export JAVA_HOME
JAVA_HOME="$(/usr/libexec/java_home -v 17)"

run_gradle() {
  ./gradlew :harness:assembleDebugAndroidTest :ttsengine:assembleDebug --no-daemon --stacktrace
}

if run_gradle; then
  exit 0
fi

echo "::warning::Android harness Gradle build failed; retrying once (known intermittent IncrementalSplitterRunnable failure -- see .github/scripts/build-android-harness.sh)." >&2
sleep 5
run_gradle
