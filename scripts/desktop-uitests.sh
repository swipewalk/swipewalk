#!/bin/zsh
# Builds the desktop app and runs its UI tests.
#   scripts/desktop-uitests.sh          quick tests (no device needed beyond what is connected)
#   CF_E2E=1 scripts/desktop-uitests.sh  also a full scan of the sample app on the Android emulator
# Needs, once per Mac: automationmodetool enable-automationmode-without-authentication
set -e
repo=${0:A:h:h}
cd "$repo"

# Support.swift launches this exact Debug build (unless CF_APP_PATH overrides it, which this script never
# sets), so its executable path uniquely identifies an app instance THIS script started -- never the person's
# own installed /Applications/Swipewalk.app or one they launched by hand.
app_exe="$repo/src/Swipewalk.Desktop/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Swipewalk.app/Contents/MacOS/Swipewalk.Desktop"

terminate_test_app() {
  local label=$1
  local -a pids
  pids=(${(f)"$(pgrep -f "$app_exe" 2>/dev/null || true)"})
  if (( ${#pids} > 0 )); then
    echo "desktop-uitests.sh: terminating $label desktop-uitests app instance(s) (pid: ${(j:, :)pids})" >&2
    kill $pids 2>/dev/null || true
    sleep 1
    kill -9 $pids 2>/dev/null || true
  fi
}

# Always clean up app instances this run starts, whether the script succeeds, fails, or is interrupted.
trap 'terminate_test_app "this run"' EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# A previous run that crashed or was killed harder than Ctrl-C can leave its instance behind; clean it up
# instead of letting the next run fail obscurely (refuses to start / flakes).
terminate_test_app "a leftover, previous run's"

dotnet build src/Swipewalk.Desktop -f net10.0-maccatalyst -v quiet -nologo | grep -E " error |Build succeeded" | sort -u
if [[ "$CF_E2E" == "1" ]]; then
  adb=${ANDROID_HOME:-$HOME/Library/Android/sdk}/platform-tools/adb
  device=${CF_E2E_DEVICE:-emulator-5554}
  "$adb" -s "$device" shell am start -n org.swipewalk.buggyapp/crc64d21699e466916214.MainActivity >/dev/null
fi
derived=${TMPDIR:-/tmp}/swipewalk-desktop-uitests

# A history with two runs of the sample app for the dashboard and compare tests: the saved Android capture, and a
# copy where one identifier-like label was fixed, one label removed and no large-text capture was made.
seed=$derived/seed
rm -rf "$seed" && mkdir -p "$seed"
cp -R tests/Swipewalk.Core.Tests/Fixtures/BuggyApp.Android "$seed/after" && rm -rf "$seed/after/large"
sed -i '' 's/content-desc="img_email_receipt"/content-desc="Email receipt"/; s/content-desc="Help"/content-desc=""/' "$seed"/after/uiautomator*.xml
dotnet build src/Swipewalk.Cli -v quiet -nologo | grep -E " error " || true
for capture in tests/Swipewalk.Core.Tests/Fixtures/BuggyApp.Android "$seed/after"; do
  dotnet run --project src/Swipewalk.Cli --no-build -- scan --platform android --package org.swipewalk.buggyapp \
    --from "$capture" --framework maui --out "$seed/out" --history "$seed/history" >/dev/null
  sleep 1  # run folders are named to the second
done

# Two more run.json's, written directly (not produced by a real record run), for the History page's
# Continue-button test: a "record" run that ended early (Continue must show) and one that finished normally
# (Continue must not show, even though it is also "record"). A fictional, distinct app id keeps these out of
# the Dashboard/Compare grouping for org.swipewalk.buggyapp above.
seed_run_json() {
  local id=$1 endedEarlyReason=$2
  local folder="$seed/history/$id"
  mkdir -p "$folder"
  # History/Dashboard list newest first: use "now" (not a fixed past date) so these sort above the
  # already-seeded org.swipewalk.buggyapp scans and are visible without scrolling the CollectionView.
  local startedAt=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
  sleep 1  # run folders/timestamps stay distinct and ordered, same as the scans seeded above
  local finishedAt=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
  if [[ -n "$endedEarlyReason" ]]; then
    local endedEarlyLine="  \"endedEarlyReason\": \"$endedEarlyReason\","
  else
    local endedEarlyLine=""
  fi
  cat > "$folder/run.json" <<JSON
{
  "id": "$id",
  "app": "org.swipewalk.recordtest",
  "appKey": "org.swipewalk.recordtest",
  "platform": "Android",
  "mode": "record",
  "startedAt": "$startedAt",
  "finishedAt": "$finishedAt",
  "counts": { "wcagIssues": 0, "needsReview": 0, "platformAdvisories": 0, "screens": 1 },
$endedEarlyLine
  "toolVersion": "0.0.0-test",
  "rulesetVersion": "0.0.0-test",
  "folder": "$folder"
}
JSON
}
seed_run_json "00000000-000001-org.swipewalk.recordtest" "The recording was cancelled. Screens after that point were not scanned; scan or test them manually."
seed_run_json "00000000-000002-org.swipewalk.recordtest" ""
(cd harness/desktop-uitests && xcodegen generate >/dev/null)
TEST_RUNNER_CF_E2E=${CF_E2E:-0} TEST_RUNNER_CF_SEEDED_HISTORY="$seed/history" TEST_RUNNER_CF_E2E_DEVICE=${CF_E2E_DEVICE:-emulator-5554} \
  xcodebuild test -project harness/desktop-uitests/SwipewalkDesktopUITests.xcodeproj -scheme DesktopUITests \
  -destination 'platform=macOS' -derivedDataPath "$derived" -skip-testing:DesktopUITests/DiscoveryTests 2>&1 \
  | grep -E "Test Case .*(passed|failed|skipped)|error:|\*\* TEST"
