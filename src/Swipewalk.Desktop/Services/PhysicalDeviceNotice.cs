namespace Swipewalk.Desktop.Services;

/// <summary>
/// Notice shown from New scan (NewScanPage.OnDeviceChanged) each time the Device picker resolves to a physical
/// phone, unless "Don't Show Again" was chosen before -- not a true one-time notice, since plain OK lets it
/// come back next time (checked by the desktop UI test). Physical phones are a person's own day-to-day device,
/// unlike emulators and simulators, so the large-text check changing that phone's own system text size
/// deserves a clear heads-up rather than only the persistent line next to the large-text option. See
/// src/Swipewalk.Collectors/PhysicalDeviceTextSizeNotice.cs for the same fact already surfaced by the CLI.
///
/// Shown as a native system alert (a UIAlertController on Mac Catalyst), not a custom modal page: a real
/// VoiceOver test on the Mac found a custom page's own heading+body announcement unreliable (VoiceOver kept
/// reading only the heading, whether the announcement was posted after a delay or immediately with a
/// queue-behind-current-speech attribute). A system alert's title and message are announced by the system
/// rather than by the app; confirmed by listening with VoiceOver on the Mac, it reads both in full.
/// </summary>
public static class PhysicalDeviceNotice
{
	public const string Heading = "Physical phone selected";

	public const string Body =
		"Use a test device. On a physical phone, the large-text check temporarily changes the phone's " +
		"text size and restores it afterwards. If a run is interrupted, the next run or the Check button " +
		"on the Devices page will try to restore it, and will say how to change it back by hand if it " +
		"can't. Avoid running the large-text check on a phone someone relies on every day.";

	/// <summary>
	/// Shows the notice and remembers "Don't Show Again" if that's what was chosen (Services/PhysicalDeviceNoticePreference).
	/// "OK" is never the deliberate-suppression choice: only clicking, or tabbing to and choosing, "Don't Show
	/// Again" suppresses the notice for good -- Escape and Return both behave like clicking OK instead.
	/// </summary>
	public static async Task<bool> ShowAsync(Page page)
	{
#if MACCATALYST
		var dontShowAgain = await ShowMacAlertAsync();
#else
		// Portable fallback (Windows, once built): Page.DisplayAlertAsync's own native alert, without the
		// PreferredAction override below -- not yet verified on that platform.
		var dontShowAgain = await page.DisplayAlertAsync(Heading, Body, "Don't Show Again", "OK");
#endif
		if (dontShowAgain)
			PhysicalDeviceNoticePreference.Dismiss();
		return dontShowAgain;
	}

#if MACCATALYST
	/// <summary>
	/// Built directly with UIAlertController, rather than through Page.DisplayAlertAsync: DisplayAlertAsync's
	/// alert left no button as the Return-key action at all on a real run of the desktop UI test (pressing
	/// Return did nothing until the alert was dismissed some other way). PreferredAction fixes that -- it can
	/// be set to any action regardless of style, so it is set here to OK, not "Don't Show Again", so Return
	/// never silently suppresses the notice for good.
	///
	/// "OK" uses the Cancel style, which Apple documents as the Mac Catalyst convention for Escape, and a real
	/// Escape key press does close this alert -- confirmed on the Mac. This app's own Services/ModalDismiss +
	/// AppDelegate key-command wiring (which dismisses custom modal pages like Pages/AppPickerPage on Escape)
	/// does not reach this alert at all -- it's presented through Mac Catalyst's AppKit bridge as a separate
	/// native panel (its accessibility dump shows "_NS:"-prefixed identifiers, unlike a pushed MAUI page),
	/// outside the UIKit responder chain that wiring depends on -- but Escape still works, presumably because
	/// the native alert handles its own Cancel-style key equivalent independently of that wiring. XCUITest's
	/// synthesized Escape key, unlike a real key press, does not reach this native panel either, which is a
	/// test-only gap (see the desktop UI test).
	/// </summary>
	private static Task<bool> ShowMacAlertAsync()
	{
		var tcs = new TaskCompletionSource<bool>();
		var alert = UIKit.UIAlertController.Create(Heading, Body, UIKit.UIAlertControllerStyle.Alert);
		var ok = UIKit.UIAlertAction.Create("OK", UIKit.UIAlertActionStyle.Cancel, _ => tcs.TrySetResult(false));
		var dontShowAgain = UIKit.UIAlertAction.Create("Don't Show Again", UIKit.UIAlertActionStyle.Default, _ => tcs.TrySetResult(true));
		alert.AddAction(ok);
		alert.AddAction(dontShowAgain);
		alert.PreferredAction = ok;

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
