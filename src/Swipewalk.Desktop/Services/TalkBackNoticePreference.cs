namespace Swipewalk.Desktop.Services;

/// <summary>
/// Remembers whether the person has already confirmed New scan's TalkBack notice
/// (Services/TalkBackNotice: turning on the "Listen with TalkBack" option for a physical Android phone changes
/// and restores its accessibility settings for the length of each capture), so a returning person isn't asked
/// again every run -- only the first time, as with Services/PhysicalDeviceNoticePreference.
/// </summary>
public static class TalkBackNoticePreference
{
	private const string PreferenceKey = "TalkBackNoticeConfirmed";

	public static bool Confirmed => Preferences.Default.Get(PreferenceKey, false);

	public static void Confirm() => Preferences.Default.Set(PreferenceKey, true);

	/// <summary>Clears the preference, as if it had never been confirmed. Only called at startup for
	/// --reset-talkback-notice (App.xaml.cs), so the UI test harness can force a known starting state without
	/// leaving the shared macOS preference (keyed on the app's bundle id, the same for every Debug build)
	/// permanently confirmed for whoever runs the app normally afterwards.</summary>
	public static void Reset() => Preferences.Default.Remove(PreferenceKey);
}
