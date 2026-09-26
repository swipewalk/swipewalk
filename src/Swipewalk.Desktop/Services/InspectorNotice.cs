namespace Swipewalk.Desktop.Services;

/// <summary>
/// New scan's iOS equivalent of Services/TalkBackNotice: turning on "Read Accessibility Inspector evidence
/// (properties VoiceOver uses)" reads real accessibility evidence from Xcode's Accessibility Inspector over
/// the macOS Accessibility API instead of turning VoiceOver on (see
/// Swipewalk.Collectors.Ios.IosCollector.RunInspectorCaptureAsync) -- the same route the CLI's --screen-reader
/// offers on iOS (Program.cs, IosInspectorGuide.AskAsync). Confirmed 2026-09-26 (a hardware experiment):
/// VoiceOver does not speak during this walk -- it never runs -- so this is Accessibility Inspector evidence,
/// not a VoiceOver recording, and the label deliberately doesn't say "what VoiceOver would read/say": VoiceOver
/// was never run here, so nothing confirms it would actually announce these exact items -- only that the
/// Inspector reports the same underlying accessibility properties (label, value, traits, identifier, hint,
/// class) VoiceOver is known to read from the tree in general. Wording here and in the desktop/record text
/// stays framed that way. Unlike TalkBack, it needs a person present for
/// TWO separate things every time this option is used: granting the macOS Accessibility permission to whichever
/// app is running Swipewalk (this app itself, when run from the desktop), and choosing the target device in
/// Accessibility Inspector and clicking the first element on the app's screen so its walk starts from the top --
/// nothing here can do either over the Accessibility API. That is why this option defaults OFF on iOS (unlike
/// TalkBack, on by default for Android): it can't run unattended, and the report says which route produced its
/// screen-reader evidence either way.
///
/// The permission is a real, persistent state on this Mac, so its explanation is confirmed once
/// (Services/InspectorNoticePreference), the same as TalkBackNotice; the device-and-click step is asked every
/// time this route is used (once per recording SESSION when recording, not per screen -- see
/// IosScreenSource.CaptureScreenReaderAsync; a continued recording asks again, since a new session means a new
/// IosScreenSource and cache), since Accessibility Inspector's own selection isn't remembered across its
/// sessions and nothing here can confirm it was actually done.
///
/// Shown as native system alerts (UIAlertController on Mac Catalyst), the same approach as
/// Services/TalkBackNotice and Services/PhysicalDeviceNotice: a real VoiceOver test on the Mac found a custom
/// page's own heading+body announcement unreliable, while a system alert's title and message are announced by
/// the system in full.
/// </summary>
public static class InspectorNotice
{
	public const string PermissionHeading = "Use Xcode's Accessibility Inspector?";

	public const string PermissionBody =
		"Swipewalk will read real accessibility evidence from Xcode's Accessibility Inspector, next to the " +
		"predicted transcript -- it will not turn VoiceOver on. This uses macOS's Accessibility permission " +
		"(System Settings > Privacy & Security > Accessibility) for Swipewalk itself, which lets it, and " +
		"anything it runs, operate other apps on this Mac; Swipewalk uses it only to step through Accessibility " +
		"Inspector, and you can turn it off there at any time, including right after this run. " +
		"You won't be asked to confirm this again on this Mac.";

	public const string PermissionMissingHeading = "Accessibility permission not granted";

	public const string PermissionMissingBody =
		"That permission isn't granted yet for Swipewalk. Open System Settings > Privacy & Security > " +
		"Accessibility; if Swipewalk isn't listed there yet, add it with the \"+\" button, then turn it on. " +
		"Then try again. The predicted transcript is used for this run instead.";

	public const string SetupHeading = "Set up Accessibility Inspector";

	private const string SetupBodyCommon =
		"Open Accessibility Inspector (Xcode > Open Developer Tool > Accessibility Inspector) if it isn't open " +
		"already. Choose your device from its target menu, then click the first element on the app's screen -- " +
		"for example its title or top-most control, so the walk starts from the top.";

	/// <summary>Shown for a single scan: no later screen for the setup step to apply to.</summary>
	public const string SetupBodyScan = SetupBodyCommon;

	/// <summary>Shown when recording: says the step is once per session up front, and that "check again" means
	/// pressing Scan this screen now, not implying the setup step repeats.</summary>
	public const string SetupBodyRecord = SetupBodyCommon +
		" This is asked once for the whole recording session, not for every screen: later screens usually " +
		"follow along without another click (seen so far on one app, not a guarantee for every app); if one " +
		"ever comes back short or incomplete, click an element in the Inspector and press Scan this screen now again.";

	/// <summary>
	/// The full guide a IosCollector.IosInspectorGuide needs: the permission explanation (skipped once
	/// confirmed on this Mac -- Services/InspectorNoticePreference), a check that the permission is actually
	/// granted, then the device-and-click setup step (always shown, never remembered). False at any step means
	/// the manual VoiceOver route applies for this run instead, the same as declining the CLI's prompt.
	/// </summary>
	/// <param name="recordMode">True for a recording (SetupBodyRecord's wording -- once per session, not per
	/// screen); false for a single scan (SetupBodyScan, with no "later screens" sentence that wouldn't apply).</param>
	public static async Task<bool> GuideAsync(Page page, bool recordMode)
	{
		if (!InspectorNoticePreference.Confirmed)
		{
			if (!await ShowAsync(page, PermissionHeading, PermissionBody))
				return false;
			InspectorNoticePreference.Confirm();
		}
		if (!Swipewalk.Collectors.Ios.IosAccessibilityPermission.IsTrusted())
		{
			await page.DisplayAlertAsync(PermissionMissingHeading, PermissionMissingBody, "OK");
			return false;
		}
		return await ShowAsync(page, SetupHeading, recordMode ? SetupBodyRecord : SetupBodyScan);
	}

	private static Task<bool> ShowAsync(Page page, string heading, string body)
	{
#if MACCATALYST
		return ShowMacAlertAsync(heading, body);
#else
		// Portable fallback (Windows, once built): Page.DisplayAlertAsync's own native alert, without the
		// PreferredAction override below -- not yet verified on that platform.
		return page.DisplayAlertAsync(heading, body, "Continue", "Cancel");
#endif
	}

#if MACCATALYST
	/// <summary>
	/// Built directly with UIAlertController, rather than through Page.DisplayAlertAsync: see
	/// Services/PhysicalDeviceNotice.ShowMacAlertAsync for why (DisplayAlertAsync left no button as the
	/// Return-key action in a real desktop UI test run). PreferredAction is set to Cancel here (not Continue),
	/// so Return never silently proceeds with a step that changes Mac permissions or is claimed done without
	/// actually being done.
	/// </summary>
	private static Task<bool> ShowMacAlertAsync(string heading, string body)
	{
		var tcs = new TaskCompletionSource<bool>();
		var alert = UIKit.UIAlertController.Create(heading, body, UIKit.UIAlertControllerStyle.Alert);
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
