using System.Diagnostics;
using System.Text.Json;
using Swipewalk.Collectors.Android;
using Swipewalk.Core.Model;

namespace Swipewalk.Collectors;

/// <summary>Finds connected Android devices/emulators (adb) and iOS devices/simulators (devicectl).</summary>
public static class Devices
{
    public const string DeviceFile = "device.json";

    public static async Task<IReadOnlyList<DeviceInfo>> ListAsync() =>
        [.. await AndroidAsync(), .. await IosAsync()];

    public static async Task<IReadOnlyList<DeviceInfo>> AndroidAsync()
    {
        string output;
        try
        {
            output = await new Adb(null).RunAsync("devices");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return [];
        }

        var devices = new List<DeviceInfo>();
        foreach (var serial in Adb.ParseSerials(output))
            devices.Add(await AndroidInfoAsync(serial));
        return devices;
    }

    public static async Task<DeviceInfo> AndroidInfoAsync(string serial)
    {
        var adb = new Adb(serial);
        async Task<string> Prop(string name) => (await adb.RunAsync("shell", "getprop", name)).Trim();
        var emulator = serial.StartsWith("emulator-", StringComparison.Ordinal)
                       || await Prop("ro.boot.qemu") == "1" || await Prop("ro.kernel.qemu") == "1";
        var name = $"{await Prop("ro.product.manufacturer")} {await Prop("ro.product.model")}".Trim();
        return new DeviceInfo(serial, name, Platform.Android, await Prop("ro.build.version.release"), !emulator);
    }

    /// <summary>Booted simulators and connected physical iOS devices.</summary>
    public static async Task<IReadOnlyList<DeviceInfo>> IosAsync() =>
        [.. (await IosWithStatusAsync()).Where(d => d.Problem is null).Select(d => d.Device)];

    /// <summary>Every iOS device and simulator devicectl knows, with the reason when it can't be used yet.</summary>
    public static async Task<IReadOnlyList<(DeviceInfo Device, string? Problem)>> IosWithStatusAsync()
    {
        var json = Path.Combine(Path.GetTempPath(), $"swipewalk-devicectl-{Guid.NewGuid():N}.json");
        try
        {
            var info = new ProcessStartInfo("xcrun") { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "devicectl", "list", "devices", "--json-output", json })
                info.ArgumentList.Add(arg);
            using var process = Process.Start(info);
            if (process is null)
                return [];
            await process.WaitForExitAsync();
            return File.Exists(json) ? ParseDevicectlWithStatus(await File.ReadAllTextAsync(json)) : [];
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return []; // not on macOS / no Xcode
        }
        finally
        {
            File.Delete(json);
        }
    }

    /// <summary>Booted simulators and connected physical iOS devices, ready to scan.</summary>
    internal static IReadOnlyList<DeviceInfo> ParseDevicectl(string json) =>
        [.. ParseDevicectlWithStatus(json).Where(d => d.Problem is null).Select(d => d.Device)];

    /// <summary>
    /// All iOS devices with a reason when one can't be used yet (simulator not booted, phone not paired).
    /// Physical devices may lack "reality" and "marketingName" until paired; simulators always report "simulated".
    /// </summary>
    internal static IReadOnlyList<(DeviceInfo Device, string? Problem)> ParseDevicectlWithStatus(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var devices = new List<(DeviceInfo, string?)>();
        foreach (var d in doc.RootElement.GetProperty("result").GetProperty("devices").EnumerateArray())
        {
            if (!d.TryGetProperty("properties", out var p) || !p.TryGetProperty("hardware", out var hw)
                || Str(hw, "platform") != "iOS")
                continue;
            var physical = Str(hw, "reality") != "simulated";
            var connection = p.TryGetProperty("connection", out var c) ? c : default;
            var bootState = d.TryGetProperty("deviceProperties", out var dp) ? Str(dp, "bootState") : null;
            var name = (dp.ValueKind == JsonValueKind.Object ? Str(dp, "name") : null) ?? Str(hw, "marketingName") ?? "iOS device";
            var os = p.TryGetProperty("software", out var sw) && sw.TryGetProperty("osVersionNumber", out var v) ? Str(v, "stringValue") : null;
            var udid = Str(hw, "udid");
            if (udid is null)
                continue;

            string? problem = physical
                ? Str(connection, "pairingState") is "unpaired"
                    ? "not paired: unlock the phone, then run: xcrun devicectl manage pair --device " + udid
                    : Str(connection, "state") != "connected" ? "not connected" : null
                : bootState != "booted" ? "simulator not booted" : null;
            devices.Add((new DeviceInfo(udid, name, Platform.iOS, os ?? "?", physical) { Model = Str(hw, "marketingName") }, problem));
        }
        return devices;
    }

    private static string? Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Saves the device details with a capture, without the device's own name, serial or UDID.</summary>
    public static async Task SaveAsync(DeviceInfo device, string captureDir) =>
        await File.WriteAllTextAsync(Path.Combine(captureDir, DeviceFile), JsonSerializer.Serialize(device.WithoutIdentifiers()));

    public static DeviceInfo? Load(string captureDir)
    {
        var path = Path.Combine(captureDir, DeviceFile);
        return File.Exists(path) ? JsonSerializer.Deserialize<DeviceInfo>(File.ReadAllText(path)) : null;
    }
}
