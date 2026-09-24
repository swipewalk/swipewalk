namespace Swipewalk.Desktop.Services;

/// <summary>
/// Lets the page currently shown modally (e.g. Pages/AppPickerPage) register an Escape-to-dismiss action.
/// MAUI's own keyboard accelerators only accept letters and digits (see AppMenus), so Escape is wired as a
/// platform key command instead (Platforms/MacCatalyst/AppDelegate.cs), which calls whatever is registered
/// here. Kept platform-neutral so the page itself doesn't need platform-specific code.
/// </summary>
public static class ModalDismiss
{
    public static Action? Current { get; set; }
}
