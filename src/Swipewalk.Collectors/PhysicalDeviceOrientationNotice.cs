using Swipewalk.Core.Model;

namespace Swipewalk.Collectors;

/// <summary>
/// The one-time notice printed before the orientation rescan (<c>scan --orientation both</c>) rotates a
/// physical iPhone and restores it afterwards -- the same shape as <see cref="PhysicalDeviceTextSizeNotice"/>
/// for text size: a plain log line, not a prompt that waits for a person to act on it (it prints right
/// before the rotation call, not at the start of the scan, so there's no real window to react in time
/// anyway). Names Control Center's rotation lock, since a locked phone can make
/// <c>XCUIDevice.shared.orientation</c> report success while the interface never actually rotates (see
/// <c>Swipewalk.Collectors.Ios.IosCollector.SetOrientationAsync</c>'s remarks) -- there is no way to detect
/// that from here, so the wording only explains the ambiguity, never claims it was checked or ruled out.
/// Emulators and simulators are unaffected: Android sets rotation directly at the system-settings level, and
/// the Simulator has no physical rotation lock.
/// </summary>
public static class PhysicalDeviceOrientationNotice
{
    public const string Text =
        "The orientation check rotates this iPhone and restores it afterwards. If Control Center's rotation lock was on, a screen may be flagged for review as if it were restricted to one orientation when it isn't -- turn the lock off and scan again to confirm.";

    /// <summary>Whether the notice applies: only for a device known to be physical.</summary>
    public static bool AppliesTo(DeviceInfo? device) => device?.IsPhysical == true;
}
