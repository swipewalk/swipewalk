using System.Diagnostics;
using System.Text.Json;
using Swipewalk.Collectors.Android;
using Swipewalk.Collectors.Ios;
using Swipewalk.Core.Model;

namespace Swipewalk.Collectors.Preflight;

/// <summary>
/// Readiness checks run before scan and record (and by `swipewalk doctor`). Every platform answers the
/// same questions: tools present, device found and chosen, paired/authorized, unlocked and awake, app
/// installed and in front, automation allowed, and whether private screen areas can be blanked. Each failure
/// says how to fix it. Checks stop at the first failure that makes later checks meaningless.
/// </summary>
public static class Preflight
{
    public static async Task<IReadOnlyList<CheckResult>> AndroidAsync(
        string? serial, string? package = null, bool largeText = false, bool screenReaderCapture = false, string? androidHarnessDir = null)
    {
        var results = new List<CheckResult>();
        string devicesOutput;
        try
        {
            devicesOutput = await new Adb(null).RunAsync("devices");
            results.Add(new("adb", CheckStatus.Pass, "found"));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            results.Add(new("adb", CheckStatus.Fail, "not found or not working",
                "Install the Android SDK platform-tools and set ANDROID_HOME, or put adb on PATH."));
            return results;
        }

        foreach (var (id, state) in Adb.ParseStates(devicesOutput).Where(d => d.State != "device"))
            results.Add(new($"Device {id}", CheckStatus.Fail, $"state '{state}'", state == "unauthorized"
                ? "Unlock the phone and accept the \"Allow USB debugging?\" prompt (tick \"Always allow\")."
                : "Reconnect the cable, or run: adb kill-server"));

        string chosen;
        DeviceInfo info;
        try
        {
            chosen = await new Adb(serial).ResolveSerialAsync();
            info = await Devices.AndroidInfoAsync(chosen);
            results.Add(new("Device", CheckStatus.Pass, info.ToString()));
        }
        catch (InvalidOperationException ex)
        {
            results.Add(new("Device", CheckStatus.Fail, ex.Message, "Connect a device with USB debugging on, or pass --device <serial>."));
            return results;
        }

        if (largeText && PhysicalDeviceTextSizeNotice.AppliesTo(info))
            results.Add(new("Text size", CheckStatus.Warn, PhysicalDeviceTextSizeNotice.Text));

        var adb = new Adb(chosen);
        if (TextSizeRestore.Pending(chosen) is { } fontScale)
        {
            await adb.RunAsync("shell", "settings", "put", "system", "font_scale", fontScale);
            TextSizeRestore.Forget(chosen);
            results.Add(new("Text size", CheckStatus.Pass, $"restored to font scale {fontScale}; an interrupted large-text check had left it enlarged"));
        }
        else
        {
            // Cheap (one adb round trip): read the device's current text size and warn if it's already
            // enlarged, so a normal-size capture taken while it's like this isn't mistaken for default size.
            var currentFontScale = AndroidScreenSource.NormalFontScale((await adb.RunAsync("shell", "settings", "get", "system", "font_scale")).Trim());
            if (double.TryParse(currentFontScale, System.Globalization.CultureInfo.InvariantCulture, out var currentScale)
                && BaselineTextSize.IsAndroidEnlarged(currentScale))
                results.Add(new("Text size", CheckStatus.Warn, BaselineTextSize.Warning($"font_scale {currentFontScale}")));
        }
        if (AppearanceRestore.Pending(chosen) is { } appearance)
        {
            await AndroidAppearance.SetAsync(chosen, appearance);
            AppearanceRestore.Forget(chosen);
            results.Add(new("Appearance", CheckStatus.Pass, $"restored to {appearance}; an interrupted appearance check had left it switched"));
        }

        var window = await adb.RunAsync("shell", "dumpsys", "window");
        results.Add(AndroidCollector.IsAwakeAndUnlocked(await adb.RunAsync("shell", "dumpsys", "power"), window)
            ? new("Screen", CheckStatus.Pass, "awake and unlocked")
            : new("Screen", CheckStatus.Fail, "off or locked",
                "Unlock the device and keep it on (Developer options > Stay awake keeps it on while charging)."));

        var foreground = AndroidCollector.ForegroundPackage(window);
        if (package is not null)
        {
            var installed = (await adb.RunAsync("shell", "pm", "list", "packages", package)).Contains($"package:{package}", StringComparison.Ordinal);
            results.Add(!installed
                ? new("App", CheckStatus.Fail, $"{package} is not installed", "Install the app on the device.")
                : foreground == package
                    ? new("App", CheckStatus.Pass, $"{package} is in front")
                    : new("App", CheckStatus.Pass,
                        $"{package} is installed; {foreground ?? "another app"} is in front now, but Swipewalk brings it forward " +
                        "(starting it if it isn't running) before scanning"));
        }
        else
        {
            results.Add(new("App", CheckStatus.Pass, $"in front: {foreground ?? "unknown"} (pass --package to check a specific app)"));
        }

        results.Add(AndroidCollector.StatusBarFrame(window) is null
            ? new("Status bar", CheckStatus.Warn, "position not found; screenshots will not be blanked",
                "Check screenshots for notifications or other personal information before sharing reports.")
            : new("Status bar", CheckStatus.Pass, "will be blanked in screenshots"));

        // Mirrors the Text size leftover check above, for the Android TalkBack screen-reader capture (see
        // AndroidHarness.RunScreenReaderCaptureAsync's remarks): only checked when --screen-reader is in
        // play, since the device-side path needs the harness installed. A crashed or killed capture can
        // leave TalkBack enabled and its TTS engine/settings changed; this repairs it before the real
        // capture runs, from two independent sources -- the host-side record (no harness/adb-instrument
        // round trip needed, so it still works even if the device-side marker or the harness app itself
        // was lost) and the device-side marker (in case this host never got to write its own record, or a
        // different host started the interrupted capture). Either being restored already is harmless: the
        // other simply finds nothing to do.
        if (screenReaderCapture)
        {
            if (ScreenReaderSettingsRestore.Pending(chosen) is { } pending)
            {
                var restored = await RestoreFromHostRecordAsync(adb, pending);
                if (restored)
                {
                    // Genuinely confirmed restored (read back below), not just attempted -- only then is
                    // it safe to forget the host record and remove the app that held the privileged
                    // permission; see UninstallTtsEngineAsync's remarks. Best effort, and harmless if the
                    // app was already gone or was never installed on this device.
                    ScreenReaderSettingsRestore.Forget(chosen);
                    await AndroidHarness.UninstallTtsEngineAsync(adb);
                    results.Add(new("Screen reader settings", CheckStatus.Pass,
                        "restored from this machine's own record; an interrupted screen-reader capture may have left TalkBack enabled and its settings changed"));
                }
                else
                {
                    // Leave the host record in place (never forgotten) so the next attempt -- another
                    // scan/record with --screen-reader, or another `doctor` run -- tries again, rather
                    // than silently losing the one copy of "what to put back".
                    results.Add(new("Screen reader settings", CheckStatus.Warn,
                        "an interrupted screen-reader capture changed TalkBack and text-to-speech settings, and restoring them from this machine's own record could not be confirmed for every setting",
                        "Run `swipewalk doctor --platform android --screen-reader` again, or check TalkBack/Settings > Accessibility by hand."));
                }
            }
            else if (await AndroidHarness.HasLeftoverScreenReaderSettingsAsync(adb))
            {
                await AndroidHarness.RestoreLeftoverScreenReaderSettingsAsync(adb, androidHarnessDir);
                results.Add(new("Screen reader settings", CheckStatus.Pass,
                    "restored from the device's own record; an interrupted screen-reader capture may have left TalkBack enabled and its settings changed"));
            }
        }
        return results;
    }

    /// <summary>Restores directly with plain <c>adb shell settings</c> commands -- no harness or
    /// instrumentation needed, so this still works even if the harness app was uninstalled or is
    /// otherwise broken; see <see cref="ScreenReaderSettingsRestore"/>'s remarks. Returns whether every
    /// setting was confirmed to actually match <paramref name="original"/> afterward, read back fresh --
    /// not just that each `settings put`/`delete` command was attempted -- since only that confirms it's
    /// safe to forget the host record and remove the app that held the privileged permission (see the
    /// caller's remarks and <see cref="AndroidHarness.UninstallTtsEngineAsync"/>).</summary>
    private static async Task<bool> RestoreFromHostRecordAsync(Adb adb, ScreenReaderSettingsRestore.Snapshot original)
    {
        async Task<bool> PutOrDelete(string key, string value)
        {
            try
            {
                if (value == ScreenReaderSettingsRestore.Snapshot.Null)
                    await adb.RunAsync("shell", "settings", "delete", "secure", key);
                else
                    await adb.RunAsync("shell", "settings", "put", "secure", key, value);
                var readBack = (await adb.RunAsync("shell", "settings", "get", "secure", key)).Trim();
                var normalized = readBack is "null" or "" ? ScreenReaderSettingsRestore.Snapshot.Null : readBack;
                return normalized == value;
            }
            catch (InvalidOperationException)
            {
                // Best effort: one setting failing must not skip the others, but it does mean this
                // setting's own restore isn't confirmed.
                return false;
            }
        }
        var enabledServicesOk = await PutOrDelete("enabled_accessibility_services", original.EnabledServices);
        var accessibilityEnabledOk = await PutOrDelete("accessibility_enabled", original.AccessibilityEnabled);
        var touchExplorationOk = await PutOrDelete("touch_exploration_enabled", original.TouchExploration);
        var ttsDefaultSynthOk = await PutOrDelete("tts_default_synth", original.TtsDefaultSynth);
        return enabledServicesOk && accessibilityEnabledOk && touchExplorationOk && ttsDefaultSynthOk;
    }

    public static async Task<IReadOnlyList<CheckResult>> IosAsync(
        string? udid, string? bundleId, string? team, string? harnessProject, string? profile = null, string? bundlePrefix = null)
    {
        var results = new List<CheckResult>();
        var (xcodeExit, xcode) = await RunAsync("xcodebuild", "-version");
        if (xcodeExit != 0)
        {
            results.Add(new("Xcode", CheckStatus.Fail, "xcodebuild not found", "Install Xcode and run: xcode-select --install"));
            return results;
        }
        results.Add(new("Xcode", CheckStatus.Pass, xcode.Split('\n')[0].Trim()));

        try
        {
            IosCollector.HarnessProcess(harnessProject, "-", "-", "-", "-", serve: false);
            results.Add(new("Harness", CheckStatus.Pass, "found"));
        }
        catch (InvalidOperationException ex)
        {
            results.Add(new("Harness", CheckStatus.Fail, ex.Message, "Reinstall Swipewalk (the harness ships with it), run from a Swipewalk checkout, or pass --harness <path to .xcodeproj>."));
        }

        var all = await Devices.IosWithStatusAsync();
        udid ??= await IosCollector.BootedSimulatorAsync();
        var match = all.FirstOrDefault(d => d.Device.Id == udid);
        if (udid is null || match.Device is null)
        {
            results.Add(new("Device", CheckStatus.Fail, udid is null ? "no booted simulator or device chosen" : $"{udid} not found",
                "Boot a simulator, or connect an iPhone and pass --device <udid> (see: swipewalk devices)."));
            return results;
        }
        if (match.Problem is not null)
        {
            results.Add(new("Device", CheckStatus.Fail, $"{match.Device}: {match.Problem}",
                "Open Xcode > Window > Devices and Simulators, select the device and follow the prompts on it."));
            return results;
        }
        var device = match.Device;
        results.Add(new("Device", CheckStatus.Pass, device.ToString()));
        if (!device.IsPhysical && TextSizeRestore.Pending(udid) is { } contentSize)
        {
            await IosCollector.Simctl("ui", udid, "content_size", contentSize);
            TextSizeRestore.Forget(udid);
            results.Add(new("Text size", CheckStatus.Pass, $"restored to {contentSize}; an interrupted large-text check had left it enlarged"));
        }
        else if (!device.IsPhysical)
        {
            // Cheap (one simctl round trip): read the Simulator's current text size and warn if it's already
            // enlarged, so a normal-size capture taken while it's like this isn't mistaken for default size.
            // Skipped for a physical iPhone: reading its text size needs the harness's ~40 s Settings round
            // trip, done only as part of the large-text step itself, not here.
            var currentContentSize = (await IosCollector.Simctl("ui", udid, "content_size")).Trim();
            if (BaselineTextSize.IsSimulatorEnlarged(currentContentSize))
                results.Add(new("Text size", CheckStatus.Warn, BaselineTextSize.Warning(currentContentSize)));
        }

        // Appearance rescan (scan --appearance both), Simulator only for now -- see Reports.AppearanceLabels
        // .PhysicalIphoneNotSupportedReason; a physical iPhone never gets an AppearanceRestore marker.
        if (!device.IsPhysical && AppearanceRestore.Pending(udid) is { } appearance)
        {
            await IosCollector.SetAppearanceAsync(udid, appearance);
            AppearanceRestore.Forget(udid);
            results.Add(new("Appearance", CheckStatus.Pass, $"restored to {appearance}; an interrupted appearance check had left it switched"));
        }

        if (device.IsPhysical)
        {
            var (plan, problem) = SigningPlan.Create(team, profile, bundlePrefix, udid);
            results.Add(plan is null
                ? new("Signing", CheckStatus.Fail, "cannot sign the scanning harness", problem)
                : new("Signing", CheckStatus.Pass, $"{plan.Description}; only the harness is signed, not the app being scanned"));

            // A marker here means a large-text check left (or may have left) this iPhone's Settings changed --
            // whether it finished applying AX3, failed partway through, or failed restoring at the end --
            // since IosCollector/IosScreenSource always write the marker before attempting any change.
            // Restoring to the recorded state is safe either way (SettingsTextSize.applyState reads the
            // current state and moves it to match, regardless of where it started), and RestoreTextSizeAsync
            // itself verifies by reading back before returning, so forgetting the marker only on success
            // here already only happens after a verified restore.
            if (plan is not null && TextSizeRestore.Pending(udid) is { } state)
            {
                try
                {
                    await IosCollector.RestoreTextSizeAsync(harnessProject, udid, plan, state);
                    TextSizeRestore.Forget(udid);
                    results.Add(new("Text size", CheckStatus.Pass, $"restored ({state}); an interrupted large-text check had left it enlarged"));
                }
                catch (InvalidOperationException ex)
                {
                    results.Add(new("Text size", CheckStatus.Fail, $"could not restore ({state}): {ex.Message.Split('\n')[0]}",
                        "On the device: Settings > Accessibility > Display & Text Size > Larger Text; turn \"Larger Accessibility Sizes\" and the slider back to how you use the phone normally, then run `swipewalk doctor` again."));
                }
            }

            var details = await DevicectlJsonAsync("device", "info", "details", "--device", udid);
            var devMode = details?.TryGetProperty("deviceProperties", out var dp) == true && dp.TryGetProperty("developerModeStatus", out var dm)
                ? dm.GetString() : null;
            results.Add(devMode == "enabled"
                ? new("Developer Mode", CheckStatus.Pass, "enabled")
                : new("Developer Mode", CheckStatus.Fail, devMode ?? "unknown",
                    "On the device: Settings > Privacy & Security > Developer Mode, turn on and restart."));

            var lockState = await DevicectlJsonAsync("device", "info", "lockState", "--device", udid);
            var locked = lockState?.TryGetProperty("passcodeRequired", out var pr) == true && pr.GetBoolean();
            results.Add(locked
                ? new("Screen", CheckStatus.Fail, "locked", "Unlock the device and keep it unlocked while scanning.")
                : new("Screen", CheckStatus.Pass, "unlocked"));

            results.Add(new("UI automation", CheckStatus.Warn, "cannot be checked in advance",
                "The first scan asks for Face ID or the passcode on the device; approve it. If nothing appears, enable Settings > Developer > Enable UI Automation."));
        }

        if (bundleId is not null)
        {
            bool installed;
            if (device.IsPhysical)
            {
                // --include-all-apps: without it, Apple's built-in apps (Calculator, Settings) are not listed.
                var apps = await DevicectlJsonAsync("device", "info", "apps", "--device", udid, "--bundle-id", bundleId, "--include-all-apps");
                installed = apps?.TryGetProperty("apps", out var list) == true && list.GetArrayLength() > 0;
            }
            else
            {
                installed = (await RunAsync("xcrun", "simctl", "get_app_container", udid, bundleId)).ExitCode == 0;
            }
            results.Add(installed
                ? new("App", CheckStatus.Pass, $"{bundleId} is installed (the harness brings it to the front, starting it if it isn't running)")
                : new("App", CheckStatus.Fail, $"{bundleId} is not installed on {device.Name}", "Install the app on the device or simulator."));
        }

        results.Add(new("Status bar", CheckStatus.Pass, "will be blanked in screenshots (position from the system, or estimated above the navigation bar)"));
        return results;
    }

    public static bool CanProceed(IEnumerable<CheckResult> results) => results.All(r => r.Status != CheckStatus.Fail);

    private static async Task<JsonElement?> DevicectlJsonAsync(params string[] args)
    {
        var json = Path.Combine(Path.GetTempPath(), $"swipewalk-devicectl-{Guid.NewGuid():N}.json");
        try
        {
            await RunAsync("xcrun", ["devicectl", .. args, "--json-output", json]);
            if (!File.Exists(json))
                return null;
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(json));
            return doc.RootElement.TryGetProperty("result", out var result) ? result.Clone() : null;
        }
        finally
        {
            File.Delete(json);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string file, params string[] args)
    {
        var info = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        try
        {
            return await IosCollector.RunAsync(info);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (-1, "");
        }
    }
}
