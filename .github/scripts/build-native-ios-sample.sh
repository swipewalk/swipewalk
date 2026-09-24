#!/usr/bin/env bash
# Builds samples/NativeiOS for the iOS Simulator, as a compile check for the native (no MAUI) iOS
# sample used in docs/case-study.md and tests/Swipewalk.Core.Tests/NativeSamplesGroundTruthTests.cs.
# Used by ci.yml's build-samples job. Regenerates the Xcode project from project.yml with xcodegen
# (same as harness/ios) before building, so a change to project.yml is caught too.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../../samples/NativeiOS"

xcodegen generate

xcodebuild -project NativeiOS.xcodeproj -scheme NativeiOS \
  -destination 'generic/platform=iOS Simulator' build
