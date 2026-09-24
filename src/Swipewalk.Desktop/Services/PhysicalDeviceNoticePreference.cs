namespace Swipewalk.Desktop.Services;

/// <summary>
/// Remembers whether the person chose Don't Show Again on New scan's physical-phone notice
/// (Services/PhysicalDeviceNotice: selecting a physical phone changes and restores its system text size for
/// the large-text check), so it doesn't reappear once they've told it not to. Wrapped here, rather than calling
/// Preferences.Default directly from the page, so the key lives in one findable place (see Services/Fonts.cs
/// for the same pattern with the text-size preference).
/// </summary>
public static class PhysicalDeviceNoticePreference
{
	private const string PreferenceKey = "PhysicalDeviceNoticeDismissed";

	public static bool Dismissed => Preferences.Default.Get(PreferenceKey, false);

	public static void Dismiss() => Preferences.Default.Set(PreferenceKey, true);

	/// <summary>Clears the preference, as if it had never been dismissed. Only called at startup for
	/// --reset-physical-device-notice (App.xaml.cs), so the UI test harness can force a known starting state
	/// without leaving the shared macOS preference (keyed on the app's bundle id, the same for every Debug
	/// build) permanently dismissed for whoever runs the app normally afterwards.</summary>
	public static void Reset() => Preferences.Default.Remove(PreferenceKey);
}
