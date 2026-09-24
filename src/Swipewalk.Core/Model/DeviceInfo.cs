namespace Swipewalk.Core.Model;

/// <summary>The device a screen was captured on, recorded in reports as evidence of the test setup.</summary>
/// <param name="Id">adb serial or iOS UDID. Empty in saved results (see <see cref="WithoutIdentifiers"/>).</param>
/// <param name="Name">What the device is called in device lists: on iOS the name its owner gave it.</param>
/// <param name="IsPhysical">False for emulators and simulators.</param>
public sealed record DeviceInfo(string Id, string Name, Platform Platform, string OsVersion, bool IsPhysical)
{
    /// <summary>Model name ("iPhone 15"), when it differs from <see cref="Name"/>.</summary>
    public string? Model { get; init; }

    /// <summary>
    /// What is saved with captures and results, which people share: the model instead of the owner's device name
    /// ("Alex's iPhone"), and no serial or UDID.
    /// </summary>
    public DeviceInfo WithoutIdentifiers() => this with { Id = "", Name = Model ?? Name, Model = null };

    public string Kind => IsPhysical ? "physical device" : Platform == Platform.Android ? "emulator" : "simulator";

    public override string ToString() => $"{Name} · {(Platform == Platform.Android ? "Android" : "iOS")} {OsVersion} · {Kind}";
}
