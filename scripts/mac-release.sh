#!/usr/bin/env bash
# Builds, signs, notarizes and packages the Swipewalk desktop app for macOS as a .dmg that anyone can
# download and open. Run it on a Mac with Xcode, the .NET 10 SDK and the MAUI workload installed.
#
# Signing details are personal (certificate name, team ID), so they are NOT in this repository: the
# script reads them from ~/.config/swipewalk/release.env, which you create once:
#
#   SWIPEWALK_CODESIGN_KEY="Developer ID Application: Your Name (TEAMID)"
#   SWIPEWALK_PROVISION="Swipewalk Developer ID"      # the Developer ID provisioning profile's name
#   SWIPEWALK_TEAM_ID="TEAMID"
#   SWIPEWALK_NOTARY_PROFILE="swipewalk-notary"       # xcrun notarytool store-credentials ...
#
# Prerequisites, once per machine:
#   - a "Developer ID Application" certificate in the login keychain;
#   - a Developer ID provisioning profile for org.swipewalk.desktop installed under
#     ~/Library/Developer/Xcode/UserData/Provisioning Profiles/;
#   - notarization credentials stored in the keychain:
#       xcrun notarytool store-credentials "swipewalk-notary" \
#         --key ~/.config/swipewalk/AuthKey_XXXXXXXX.p8 --key-id XXXXXXXX --issuer <issuer-uuid>
#
# Usage: scripts/mac-release.sh [output-directory]   (default: artifacts/)
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out_dir="${1:-$repo_root/artifacts}"
config="${SWIPEWALK_RELEASE_ENV:-$HOME/.config/swipewalk/release.env}"

if [[ ! -f "$config" ]]; then
  echo "error: $config not found. See the comment at the top of this script." >&2
  exit 1
fi
# shellcheck disable=SC1090
source "$config"

for var in SWIPEWALK_CODESIGN_KEY SWIPEWALK_PROVISION SWIPEWALK_TEAM_ID SWIPEWALK_NOTARY_PROFILE; do
  [[ -n "${!var:-}" ]] || { echo "error: $var is not set in $config" >&2; exit 1; }
done

version="$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$repo_root/Directory.Build.props" | head -1)"
[[ -n "$version" ]] || { echo "error: could not read <Version> from Directory.Build.props" >&2; exit 1; }

app_name="Swipewalk"
dmg="$out_dir/$app_name-$version.dmg"
build_dir="$repo_root/src/Swipewalk.Desktop/bin/Release/net10.0-maccatalyst"
harness_apk="$repo_root/harness/android/harness/build/outputs/apk/androidTest/debug/harness-debug-androidTest.apk"

# The desktop app's csproj only bundles this APK when it already exists (Condition="Exists(...)"),
# so build it fresh here -- otherwise a stale or missing copy ships silently, and the app just skips
# Google's Accessibility Test Framework checks with no obvious sign anything is wrong. CI's
# build-desktop job builds its own copy too, but that job is a compile check only; its output is
# never what actually gets released (this script's output is).
echo "==> Building the Android accessibility harness (bundled into the app)"
"$repo_root/harness/android/gradlew" -p "$repo_root/harness/android" :harness:assembleDebugAndroidTest
[[ -f "$harness_apk" ]] || { echo "error: Gradle did not produce $harness_apk" >&2; exit 1; }

echo "==> Building $app_name $version (Release defaults to a universal build: Intel + Apple silicon)"
rm -rf "$build_dir"
mkdir -p "$out_dir"
dotnet publish "$repo_root/src/Swipewalk.Desktop" \
  -f net10.0-maccatalyst -c Release \
  -p:CreatePackage=false \
  -p:EnableCodeSigning=true \
  -p:UseHardenedRuntime=true \
  -p:CodesignKey="$SWIPEWALK_CODESIGN_KEY" \
  -p:CodesignProvision="$SWIPEWALK_PROVISION" \
  -p:CodesignEntitlements="Platforms/MacCatalyst/Entitlements.plist" \
  --nologo -v minimal

# Release builds both architectures and merges them into one universal bundle at the top of the
# output directory; the per-architecture folders beside it hold single-architecture builds, which
# would leave Intel or Apple silicon users without an app.
app="$build_dir/$app_name.app"
[[ -d "$app" ]] || { echo "error: universal app not found at $app" >&2; exit 1; }
archs="$(lipo -archs "$app/Contents/MacOS/Swipewalk.Desktop")"
[[ "$archs" == *x86_64* && "$archs" == *arm64* ]] || { echo "error: $app is not universal (found: $archs)" >&2; exit 1; }
[[ -f "$app/Contents/Resources/harness/android/harness-debug-androidTest.apk" ]] \
  || { echo "error: $app does not contain the Android accessibility harness APK" >&2; exit 1; }
echo "==> Built $app ($archs)"

# `dotnet publish` already signed the nested binaries with the hardened runtime; this re-seals the
# top-level bundle with a secure timestamp, which notarization requires and which is also why
# already-released builds keep working after the certificate expires.
echo "==> Re-sealing the bundle with a secure timestamp"
codesign --force --timestamp --options runtime \
  --entitlements "$repo_root/src/Swipewalk.Desktop/Platforms/MacCatalyst/Entitlements.plist" \
  --sign "$SWIPEWALK_CODESIGN_KEY" "$app"
codesign --verify --strict --verbose=2 "$app"

echo "==> Packaging $dmg"
rm -f "$dmg"
staging="$(mktemp -d)"
cp -R "$app" "$staging/"
ln -s /Applications "$staging/Applications"
hdiutil create -volname "$app_name $version" -srcfolder "$staging" -ov -format UDZO "$dmg" >/dev/null
rm -rf "$staging"
# Sign the disk image too, so the download itself carries a signature, not only the app inside it.
codesign --force --timestamp --sign "$SWIPEWALK_CODESIGN_KEY" "$dmg"

echo "==> Notarizing (this waits for Apple, usually a few minutes)"
# notarytool exits 0 even when Apple rejects the build, so check the status and print the reasons.
submission="$(xcrun notarytool submit "$dmg" --keychain-profile "$SWIPEWALK_NOTARY_PROFILE" --wait --output-format json)"
status="$(echo "$submission" | /usr/bin/python3 -c 'import json,sys; print(json.load(sys.stdin).get("status",""))')"
if [[ "$status" != "Accepted" ]]; then
  id="$(echo "$submission" | /usr/bin/python3 -c 'import json,sys; print(json.load(sys.stdin).get("id",""))')"
  echo "error: notarization returned '$status'" >&2
  [[ -n "$id" ]] && xcrun notarytool log "$id" --keychain-profile "$SWIPEWALK_NOTARY_PROFILE" >&2
  exit 1
fi
echo "Notarization accepted."

echo "==> Stapling the ticket so it works offline"
xcrun stapler staple "$dmg"

echo "==> Verifying the way macOS will on a user's machine"
xcrun stapler validate "$dmg"
spctl --assess --type open --context context:primary-signature --verbose=2 "$dmg"
# Mount the image and check the app itself: this is what Gatekeeper evaluates when someone opens it.
mount_point="$(mktemp -d)"
trap 'hdiutil detach "$mount_point" -quiet 2>/dev/null || true; rm -rf "$mount_point" "${launch_dir:-}"' EXIT
hdiutil attach "$dmg" -nobrowse -quiet -mountpoint "$mount_point"
spctl --assess --type exec --verbose=2 "$mount_point/$app_name.app"

# Signing and notarization can both pass on an app that dies at launch (a missing entitlement under
# the hardened runtime does exactly that), so actually start it. macOS may relocate a downloaded app
# to a temporary path ("app translocation"), so look for the process by name, not by path.
echo "==> Launching it once to check it starts"
launch_dir="$(mktemp -d)"
cp -R "$mount_point/$app_name.app" "$launch_dir/"
open -n "$launch_dir/$app_name.app"
sleep 10
if pgrep -x "Swipewalk.Desktop" >/dev/null; then
  pkill -x "Swipewalk.Desktop" || true
  echo "The app launched."
else
  echo "error: the signed app did not launch. Check Console.app, and whether the hardened runtime" >&2
  echo "       needs entitlements (com.apple.security.cs.allow-jit and friends) in Entitlements.plist." >&2
  exit 1
fi

echo
echo "Done: $dmg"
echo "SHA-256: $(shasum -a 256 "$dmg" | cut -d' ' -f1)"
echo
echo "Attach it to the release:"
echo "  gh release upload v$version \"$dmg\""
