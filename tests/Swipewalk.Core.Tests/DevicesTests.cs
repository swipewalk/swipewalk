using Swipewalk.Collectors;
using Swipewalk.Collectors.Android;
using Swipewalk.Core.Model;

namespace Swipewalk.Core.Tests;

public class DevicesTests
{
    [Fact]
    public void ParseSerials_KeepsOnlyReadyDevices()
    {
        const string output = "List of devices attached\nemulator-5554\tdevice\nR58M123\tunauthorized\n1A2B3C\tdevice product:x model:Pixel_8\n\n";

        Assert.Equal(["emulator-5554", "1A2B3C"], Adb.ParseSerials(output));
    }

    [Fact]
    public void ParseStates_ReportsUnauthorizedDevices()
    {
        const string output = "List of devices attached\nR58M123\tunauthorized usb:1-1\nemulator-5554\tdevice\n";

        Assert.Equal([("R58M123", "unauthorized"), ("emulator-5554", "device")], Adb.ParseStates(output));
    }

    [Fact]
    public void ParseDevicectl_ListsBootedSimulatorsAndConnectedPhysicalDevices()
    {
        const string json = """
            {"result":{"devices":[
              {"deviceProperties":{"name":"iPhone 17","bootState":"booted"},
               "properties":{"connection":{"state":"connected"},"hardware":{"platform":"iOS","reality":"simulated","udid":"SIM-1","marketingName":"iPhone 17"},
                             "software":{"osVersionNumber":{"stringValue":"26.5"}}}},
              {"deviceProperties":{"name":"iPad mini","bootState":"shutdown"},
               "properties":{"connection":{"state":"disconnected"},"hardware":{"platform":"iOS","reality":"simulated","udid":"SIM-2","marketingName":"iPad mini"},
                             "software":{"osVersionNumber":{"stringValue":"26.5"}}}},
              {"deviceProperties":{"name":"Test iPhone"},
               "properties":{"connection":{"state":"connected"},"hardware":{"platform":"iOS","reality":"physical","udid":"PHONE-1","marketingName":"iPhone 16"},
                             "software":{"osVersionNumber":{"stringValue":"26.1"}}}}
            ]}}
            """;

        var devices = Devices.ParseDevicectl(json);

        Assert.Equal(["SIM-1", "PHONE-1"], devices.Select(d => d.Id));
        Assert.Equal("Test iPhone · iOS 26.1 · physical device", devices[1].ToString());
    }

    [Fact]
    public async Task SavedDevice_HasModelButNoOwnerNameOrId()
    {
        var phone = new DeviceInfo("PHONE-1", "Alex's iPhone", Platform.iOS, "26.1", IsPhysical: true) { Model = "iPhone 16" };
        var dir = Directory.CreateTempSubdirectory("cf-device-").FullName;
        try
        {
            await Devices.SaveAsync(phone, dir);

            var saved = Devices.Load(dir)!;
            Assert.Equal("iPhone 16 · iOS 26.1 · physical device", saved.ToString());
            Assert.Equal("", saved.Id);
            Assert.DoesNotContain("Alex", File.ReadAllText(Path.Combine(dir, Devices.DeviceFile)));
            Assert.DoesNotContain("PHONE-1", File.ReadAllText(Path.Combine(dir, Devices.DeviceFile)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void DeviceInfo_DescribesEmulator()
    {
        Assert.Equal("Google Pixel 8 · Android 16 · emulator",
            new DeviceInfo("emulator-5554", "Google Pixel 8", Platform.Android, "16", IsPhysical: false).ToString());
    }

    [Fact]
    public void ParseDevicectl_UnpairedPhoneWithoutRealityField_IsPhysicalButNotReady()
    {
        const string json = """
            {"result":{"devices":[
              {"deviceProperties":{"name":"Unpaired iPhone","bootState":"booted"},
               "properties":{"connection":{"pairingState":"unpaired","state":"disconnected","transportType":"wired"},
                             "hardware":{"platform":"iOS","udid":"00008110-X"},
                             "software":{"osVersionNumber":{"stringValue":"18.6"}}}}
            ]}}
            """;

        var (device, problem) = Assert.Single(Devices.ParseDevicectlWithStatus(json));

        Assert.True(device.IsPhysical);
        Assert.Equal("18.6", device.OsVersion);
        Assert.StartsWith("not paired", problem);
        Assert.Empty(Devices.ParseDevicectl(json));
    }

    [Fact]
    public void PhysicalDeviceTextSizeNotice_AppliesToPhysicalDevicesOnly()
    {
        var physical = new DeviceInfo("R58M123", "Pixel 7a", Platform.Android, "14", IsPhysical: true);
        var emulator = new DeviceInfo("emulator-5554", "Google Pixel 8", Platform.Android, "16", IsPhysical: false);

        Assert.True(PhysicalDeviceTextSizeNotice.AppliesTo(physical));
        Assert.False(PhysicalDeviceTextSizeNotice.AppliesTo(emulator));
        Assert.False(PhysicalDeviceTextSizeNotice.AppliesTo(null));
    }

    [Fact]
    public void FastDeploymentApk_IsDetected()
    {
        Assert.True(AppInstaller.IsFastDeploymentApk(["AndroidManifest.xml", "lib/arm64-v8a/libmonodroid.so", "classes.dex"]));
        Assert.False(AppInstaller.IsFastDeploymentApk(["lib/arm64-v8a/libmonodroid.so", "lib/arm64-v8a/libassemblies.arm64-v8a.blob.so"]));
        Assert.False(AppInstaller.IsFastDeploymentApk(["lib/arm64-v8a/libxamarin-app.so", "lib/arm64-v8a/lib_System.Collections.dll.so"]));
        Assert.False(AppInstaller.IsFastDeploymentApk(["AndroidManifest.xml", "classes.dex"])); // not a .NET app
    }
}
