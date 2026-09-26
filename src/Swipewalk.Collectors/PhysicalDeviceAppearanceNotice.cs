using Swipewalk.Core.Model;

namespace Swipewalk.Collectors;

/// <summary>
/// The one-time notice shown before the appearance rescan (<c>scan --appearance both</c>) switches a physical
/// iPhone's Settings &gt; Appearance and restores it afterwards -- the same shape as
/// <see cref="PhysicalDeviceTextSizeNotice"/> for text size. Emulators and the Simulator are unaffected:
/// their appearance is a virtual device setting, not something the device's owner uses day to day.
/// </summary>
public static class PhysicalDeviceAppearanceNotice
{
    public const string Text =
        "The appearance check switches this iPhone between light and dark and restores it afterwards. Use a test device where you can.";

    /// <summary>Whether the notice applies: only for a device known to be physical.</summary>
    public static bool AppliesTo(DeviceInfo? device) => device?.IsPhysical == true;
}
