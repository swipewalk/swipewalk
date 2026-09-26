using Swipewalk.Collectors;
using Swipewalk.Core.Model;

namespace Swipewalk.Engine;

/// <summary>
/// Shared "is this a physical Android phone whose accessibility settings --screen-reader / screenReaderCapture
/// is about to change, without anyone confirming that first?" check. Used by the CLI (an interactive y/N
/// prompt, or --screen-reader-confirm for a non-interactive run -- see Program.cs) and by <c>swipewalk run</c>
/// (swipewalk.json's <c>screenReaderConfirm</c> standing in for the same answer, since a config file run is
/// never interactive -- see <see cref="Runner"/>), so both apply the same physical-device detection rather than
/// duplicating it. Never gates an emulator, or a device that can't be identified (more than one connected with
/// none chosen; the ordinary pre-flight check reports that on its own).
/// </summary>
public static class ScreenReaderCaptureGate
{
    /// <summary>The chosen device, only when it's a specific physical phone that needs confirmation before
    /// TalkBack capture changes its settings; null when there's nothing to confirm (an emulator, or an
    /// ambiguous/unresolved device -- <paramref name="device"/> null with more than one device connected).</summary>
    public static async Task<DeviceInfo?> PhysicalDeviceNeedingConfirmationAsync(string? device)
    {
        var candidates = await Devices.AndroidAsync();
        var chosen = device is not null
            ? candidates.FirstOrDefault(d => d.Id == device)
            : candidates.Count == 1 ? candidates[0] : null;
        return chosen is { IsPhysical: true } ? chosen : null;
    }
}
