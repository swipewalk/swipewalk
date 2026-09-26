namespace Swipewalk.Desktop.Services;

/// <summary>
/// Remembers whether the person has already confirmed New scan's Accessibility Inspector permission notice
/// (Services/InspectorNotice: turning on "Read Accessibility Inspector evidence (what VoiceOver would read)"
/// on iOS uses the macOS Accessibility permission), so a returning person isn't shown that explanation again --
/// only the first time, as with Services/TalkBackNoticePreference. This does NOT cover the device-and-click
/// setup step InspectorNotice also shows: that one is asked every time, since nothing can confirm it was
/// actually done (Accessibility Inspector's own selection isn't remembered across its sessions).
/// </summary>
public static class InspectorNoticePreference
{
	private const string PreferenceKey = "InspectorNoticeConfirmed";

	public static bool Confirmed => Preferences.Default.Get(PreferenceKey, false);

	public static void Confirm() => Preferences.Default.Set(PreferenceKey, true);

	/// <summary>Clears the preference, as if it had never been confirmed. Only called at startup for
	/// --reset-inspector-notice (App.xaml.cs), so the UI test harness can force a known starting state without
	/// leaving the shared macOS preference (keyed on the app's bundle id, the same for every Debug build)
	/// permanently confirmed for whoever runs the app normally afterwards.</summary>
	public static void Reset() => Preferences.Default.Remove(PreferenceKey);
}
