using Swipewalk.Core.Model;

namespace Swipewalk.Collectors;

/// <summary>
/// The one-time notice shown before the large-text check changes a physical device's own system text size
/// (Android phones, and now physical iPhones: the iOS harness drives Settings &gt; Accessibility &gt; Display &amp;
/// Text Size to AX3 and restores the original switch/slider state afterwards). Emulators and simulators are
/// unaffected: their text size is a virtual device setting, not something the device's owner uses day to day.
/// </summary>
public static class PhysicalDeviceTextSizeNotice
{
    public const string Text =
        "The large-text check changes this phone's system text size and restores it afterwards. Use a test device where you can.";

    /// <summary>Whether the notice applies: only for a device known to be physical.</summary>
    public static bool AppliesTo(DeviceInfo? device) => device?.IsPhysical == true;
}
