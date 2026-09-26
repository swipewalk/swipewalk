namespace Swipewalk.Desktop.Services;

/// <summary>
/// Confirmation shown from New scan (NewScanPage.OnStart) the first time "Listen with TalkBack" is turned on
/// for a physical Android phone -- unlike Services/PhysicalDeviceNotice (an FYI that never blocks starting),
/// this is a real gate: TalkBack capture installs a small helper app and changes which accessibility service is
/// enabled, touch exploration and the default text-to-speech engine, and TalkBack speaks nothing aloud for the
/// length of each capture, so it needs a deliberate "yes, on this phone" before it touches anything -- the same
/// thing the CLI's --screen-reader asks for with its interactive y/N prompt (Program.cs,
/// ConfirmScreenReaderOnPhysicalDeviceAsync). Confirmed once (Services/TalkBackNoticePreference), not asked
/// again on this Mac; declining doesn't remember anything, so the same phone is asked again next time.
///
/// Shown as a native system alert (a UIAlertController on Mac Catalyst), the same approach as
/// Services/PhysicalDeviceNotice: a real VoiceOver test on the Mac found a custom page's own heading+body
/// announcement unreliable, while a system alert's title and message are announced by the system in full.
/// </summary>
public static class TalkBackNotice
{
	public const string Heading = "Turn on TalkBack for this phone?";

	public const string Body =
		"Swipewalk will turn TalkBack on and change this phone's accessibility settings (which service is " +
		"enabled, touch exploration, and the default text-to-speech engine) so it can capture exactly what " +
		"TalkBack says, next to the predicted transcript. TalkBack will speak nothing aloud while it listens -- " +
		"don't use this on a phone someone is relying on TalkBack with right now. Those settings are restored " +
		"afterwards, even if the run is interrupted; the small helper app Swipewalk installs to receive the " +
		"exact spoken text is removed once that restore is confirmed (it stays only if a restore fails or is " +
		"pending). Use a test device. You won't be asked again on this Mac.";

	/// <summary>
	/// Shows the confirmation and remembers it if "Continue" was chosen (Services/TalkBackNoticePreference).
	/// "Cancel" is the safe default (Escape and Return both act like Cancel, matching Apple's Cancel-style
	/// convention for Mac Catalyst) -- unlike PhysicalDeviceNotice's OK/Don't-Show-Again, both buttons here have
	/// a real effect: Continue proceeds and remembers; Cancel stops the scan/recording from starting at all,
	/// same as declining the CLI's prompt.
	/// </summary>
	public static async Task<bool> ShowAsync(Page page)
	{
#if MACCATALYST
		var proceed = await ShowMacAlertAsync();
#else
		// Portable fallback (Windows, once built): Page.DisplayAlertAsync's own native alert, without the
		// PreferredAction override below -- not yet verified on that platform.
		var proceed = await page.DisplayAlertAsync(Heading, Body, "Continue", "Cancel");
#endif
		if (proceed)
			TalkBackNoticePreference.Confirm();
		return proceed;
	}

#if MACCATALYST
	/// <summary>
	/// Built directly with UIAlertController, rather than through Page.DisplayAlertAsync: see
	/// Services/PhysicalDeviceNotice.ShowMacAlertAsync for why (DisplayAlertAsync left no button as the
	/// Return-key action in a real desktop UI test run). PreferredAction is set to Cancel here (not Continue),
	/// so Return never silently starts a run that changes a physical phone's accessibility settings.
	/// </summary>
	private static Task<bool> ShowMacAlertAsync()
	{
		var tcs = new TaskCompletionSource<bool>();
		var alert = UIKit.UIAlertController.Create(Heading, Body, UIKit.UIAlertControllerStyle.Alert);
		var cancel = UIKit.UIAlertAction.Create("Cancel", UIKit.UIAlertActionStyle.Cancel, _ => tcs.TrySetResult(false));
		var continueAction = UIKit.UIAlertAction.Create("Continue", UIKit.UIAlertActionStyle.Default, _ => tcs.TrySetResult(true));
		alert.AddAction(cancel);
		alert.AddAction(continueAction);
		alert.PreferredAction = cancel;

		var controller = Microsoft.Maui.ApplicationModel.Platform.GetCurrentUIViewController();
		if (controller is null)
		{
			tcs.TrySetResult(false);
			return tcs.Task;
		}
		controller.PresentViewController(alert, animated: true, completionHandler: null);
		return tcs.Task;
	}
#endif
}
