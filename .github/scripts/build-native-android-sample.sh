#!/usr/bin/env bash
# Builds samples/NativeAndroid's debug APK with Gradle, as a compile check for the native (no MAUI)
# Android sample used in docs/case-study.md and tests/Swipewalk.Core.Tests/NativeSamplesGroundTruthTests.cs.
# Used by ci.yml's build-samples job. Same JDK 17 requirement as harness/android; see
# .github/scripts/build-android-harness.sh for the reasoning.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../../samples/NativeAndroid"

export JAVA_HOME
JAVA_HOME="$(/usr/libexec/java_home -v 17)"

./gradlew :app:assembleDebug --no-daemon
