using System.Runtime.InteropServices;

namespace Swipewalk.Collectors.Ios;

/// <summary>
/// Checks whether this process is trusted for the macOS Accessibility permission that
/// <see cref="IosInspectorWalk"/> needs to drive Xcode's Accessibility Inspector over the AX API. macOS
/// attributes a spawned child process's trust to whichever app is "responsible" for it (for example a
/// Terminal running the Swipewalk CLI, or Swipewalk's own desktop app), so granting that app the permission
/// is enough -- nothing here, or in the Swift walk helper it launches, needs its own separate grant.
/// </summary>
public static class IosAccessibilityPermission
{
    // ApplicationServices.framework is macOS-only. IsTrusted() below only ever reaches this call after
    // OperatingSystem.IsMacOS() is true, so this DllImport is never resolved on another host OS.
    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern bool AXIsProcessTrustedWithOptions(IntPtr options);

    /// <summary>
    /// True when this process (or its responsible parent -- see this type's remarks) is already trusted for
    /// Accessibility. Passes no options (a null dictionary), which only reads the current state -- this
    /// never shows the system's own "grant access" prompt or otherwise touches System Settings itself: the
    /// Accessibility Inspector route's product design is to explain what the permission is for in its own
    /// words first (see the CLI's confirmation prompt), then leave granting it, in System Settings > Privacy
    /// &amp; Security > Accessibility, entirely to the person. Always false on a non-macOS host.
    /// </summary>
    public static bool IsTrusted() => OperatingSystem.IsMacOS() && AXIsProcessTrustedWithOptions(IntPtr.Zero);
}
