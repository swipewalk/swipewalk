using System.Collections.ObjectModel;
using Swipewalk.Collectors;
using Swipewalk.Collectors.Preflight;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Standards;
using Swipewalk.Desktop.Controls;
using Swipewalk.Desktop.Services;
using Swipewalk.Engine;
using DeviceInfo = Swipewalk.Core.Model.DeviceInfo;
using Platform = Swipewalk.Core.Model.Platform;

namespace Swipewalk.Desktop.Pages;

/// <summary>New scan; navigate with ?device=&lt;id&gt; to preselect a device (from the Devices page), or
/// ?continue=&lt;run id&gt; to resume an ended-early recording (from History's Continue action).</summary>
[QueryProperty(nameof(DeviceId), "device")]
[QueryProperty(nameof(ContinueRunId), "continue")]
public partial class NewScanPage : ContentPage
{
	private string? _preferredDevice;

	public string DeviceId
	{
		set
		{
			_preferredDevice = Uri.UnescapeDataString(value);
			_ = SelectPreferredDeviceAsync();
		}
	}

	/// <summary>Set once the run to continue and its <see cref="RecordContinuation"/> are resolved (see
	/// <see cref="LoadContinuationAsync"/>); null starts a fresh recording, as before.</summary>
	private RunRecord? _continuingRun;
	private RecordContinuation? _continuation;

	/// <summary>The continuing run's own report, loaded alongside <see cref="_continuation"/>: passed to
	/// <see cref="RunHistory.StartRecordingAsync"/> so the "recording in progress" run.json written before the
	/// first new screen still shows the screens already captured, not zero -- see <see cref="OnStart"/>.</summary>
	private ScanReport? _continuingReport;

	public string ContinueRunId
	{
		set => _ = LoadContinuationAsync(Uri.UnescapeDataString(value));
	}

	/// <summary>
	/// Resolves the run to continue (see <see cref="ScanService.ResolveContinuation"/>) and prefills the page
	/// from it: platform, app id, and Record mode locked on (only a recording can be continued). The device,
	/// standard and the large-text/auto-scan options aren't saved per run, so they keep their own defaults --
	/// the person can change any of them for this session, the same as starting a fresh recording. Only the
	/// app identity and where new screens get appended to are inherited.
	/// </summary>
	private async Task LoadContinuationAsync(string runId)
	{
		RunRecord run;
		RecordContinuation continuation;
		ScanReport report;
		try
		{
			(run, report, continuation) = await Task.Run(() => ScanService.ResolveContinuation(AppState.History, runId));
		}
		catch (InvalidOperationException ex)
		{
			await DisplayAlertAsync("Can't continue this run", ex.Message, "OK");
			return;
		}
		_continuingRun = run;
		_continuation = continuation;
		_continuingReport = report;

		var index = run.Platform == "iOS" ? 1 : 0;
		if (PlatformPicker.SelectedIndex != index)
			PlatformPicker.SelectedIndex = index; // reloads devices for that platform
		// run.AppKey is null only when the run's app was never identified (rare); leave the field empty rather
		// than filling in a display label like "Unknown app" as if it were a real package/bundle id.
		AppIdEntry.Text = run.AppKey ?? "";
		ModePicker.SelectedIndex = 1; // Record: continuing only ever resumes a recording
		ModePicker.IsEnabled = false;
		_log.Add($"Continuing {run.Id}: session {continuation.PriorSessions.Count + 1}, " +
				 $"{continuation.PriorResults.Count} screen(s) already captured.");
		// continuation.LearnedRestartReason being null on its own doesn't mean anything was lost -- it's also
		// what a run with nothing to learn yet looks like (text grew live, or large text wasn't checked). Only
		// StateWasSaved false means record-state.json itself wasn't there to load from.
		if (!continuation.StateWasSaved && continuation.PriorResults.Count > 0)
			_log.Add("(This run's earlier session(s) saved no resume state -- an older run, or a damaged file: starting fresh on the " +
					 "large-text-restart behaviour, and their screens won't be recognized if you rescan them.)");
	}

	private async Task SelectPreferredDeviceAsync()
	{
		if (_preferredDevice is null)
			return;
		var ios = (await Task.Run(Devices.IosAsync)).Any(d => d.Id == _preferredDevice);
		var index = ios ? 1 : 0;
		if (PlatformPicker.SelectedIndex != index)
			PlatformPicker.SelectedIndex = index; // reloads devices, then selects the preferred one
		else
			SelectDevice();
	}

	private void SelectDevice()
	{
		var index = _devices.FindIndex(d => d.Id == _preferredDevice);
		if (index >= 0)
			DevicePicker.SelectedIndex = index;
	}

	private readonly ObservableCollection<string> _log = [];
	private List<DeviceInfo> _devices = [];
	private RecorderControl? _recording;

	public NewScanPage()
	{
		InitializeComponent();
		AppMenus.Attach(this);
		LogView.ItemsSource = _log;
		StandardPicker.Items = [.. new[] { "All standards" }.Concat(KnownStandards.All.Select(s => $"{s.ShortName} ({s.Basis})"))];
		StandardPicker.SelectedIndex = 0;
		ModePicker.Items = ["The screen shown now", "Record: each screen as I use the app"];
		ModePicker.SelectedIndex = 0;
		PlatformPicker.Items = ["Android", "iOS"];
		PlatformPicker.SelectedIndex = 0;
		LargeTextRestartPicker.Items = ["Ask me each time", "Restart the app and check", "Don't check larger text on that screen"];
		LargeTextRestartPicker.SelectedIndex = 0;
		PhysicalDeviceTextSizeLabel.Text = PhysicalDeviceTextSizeNotice.Text;
		LargeTextOption.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(CheckOption.IsChecked))
				UpdatePhysicalDeviceLine();
		};
		// No device is chosen yet (OnPlatformChanged below never preselects one): reflect that in Start and
		// Choose app… before the first device list even loads.
		UpdateDeviceRequiredControls();
	}

	private bool Recording => ModePicker.SelectedIndex == 1;

	private TargetPlatform Platform => PlatformPicker.SelectedIndex == 1 ? TargetPlatform.Ios : TargetPlatform.Android;

	private async void OnPlatformChanged(object? sender, EventArgs e)
	{
		var devices = Platform == TargetPlatform.Ios ? await Task.Run(Devices.IosAsync) : await Task.Run(Devices.AndroidAsync);
		_devices = [.. devices];
		// No device is preselected: the person must choose one, and switching platform clears whatever was
		// chosen for the other one. Defaulting to the first device in the list used to mean that on a machine
		// where a physical phone happens to be first (e.g. plugged in before an emulator is started), opening
		// New scan silently selected that phone -- popping the physical-phone notice with no action from the person,
		// or (worse) not popping it at all, since picking the very same already-selected item again in the
		// picker's menu doesn't raise a change. Start and Choose app… stay disabled until a real choice is made
		// (UpdateDeviceRequiredControls) -- see SelectDevice below for the one deliberate exception.
		DevicePicker.SelectedIndex = -1;
		DevicePicker.Items = [.. _devices.Select(d => d.ToString())];
		SelectDevice();
		UpdateDeviceRequiredControls();
		// TalkBack capture is Android only for now (see AndroidHarness.RunScreenReaderCaptureAsync); hide the
		// option entirely for iOS rather than show a checkbox that would do nothing if left checked.
		TalkBackOption.IsVisible = Platform == TargetPlatform.Android;
		// .aab install (bundletool/keystore) is Android only; hide the whole section for iOS the same way.
		var androidAab = Platform == TargetPlatform.Android;
		AndroidAabOptionsLabel.IsVisible = androidAab;
		AndroidAabOptionsCaptionLabel.IsVisible = androidAab;
		BundletoolRow.IsVisible = androidAab;
		KeystoreRow.IsVisible = androidAab;
		KeystoreAliasRow.IsVisible = androidAab;
		KeystorePasswordHintLabel.IsVisible = androidAab;
	}

	private DeviceInfo? SelectedDevice() =>
		DevicePicker.SelectedIndex >= 0 && DevicePicker.SelectedIndex < _devices.Count ? _devices[DevicePicker.SelectedIndex] : null;

	/// <summary>Start and Choose app… both need a device chosen first; shows a short visible reason
	/// (NoDeviceHintLabel) while they're disabled for that reason. Called on load, on every device/platform
	/// change, and from SetRunning (a running scan disables Start for a different reason, which this combines
	/// with "no device chosen" rather than one silently overriding the other).</summary>
	private void UpdateDeviceRequiredControls()
	{
		var hasDevice = SelectedDevice() is not null;
		StartButton.IsEnabled = hasDevice && !_running;
		ChooseAppButton.IsEnabled = hasDevice;
		NoDeviceHintLabel.IsVisible = !hasDevice;
	}

	/// <summary>Shown once per page instance, and only when the notice hasn't already been dismissed for good
	/// (Services/PhysicalDeviceNoticePreference): guards against SelectDevice reselecting the same preferred
	/// physical device more than once while this page is open, e.g. switching platform away and back to one
	/// given by ?device=/Continue.</summary>
	private bool _physicalDeviceNoticeShown;

	/// <summary>
	/// Fires for every change to DevicePicker's selection -- the person's own choice, or SelectDevice resolving
	/// a preferred device from ?device=/Continue. Scanning the wrong device is an easy mistake to make, so the
	/// device actually selected is what decides both the persistent reminder and the physical-phone notice
	/// below, never the platform or a guess. A preferred device counts as much as a click here -- the person
	/// already chose it, by starting from Devices/History -- so both trigger the notice the same way; nothing
	/// preselects a device the person didn't choose at all any more.
	/// </summary>
	private async void OnDeviceChanged(object? sender, EventArgs e)
	{
		UpdatePhysicalDeviceLine();
		UpdateDeviceRequiredControls();
		if (!_physicalDeviceNoticeShown && !PhysicalDeviceNoticePreference.Dismissed && SelectedDevice() is { IsPhysical: true })
		{
			_physicalDeviceNoticeShown = true;
			await PhysicalDeviceNotice.ShowAsync(this);
		}
	}

	/// <summary>PhysicalDeviceTextSizeLabel (NewScanPage.xaml): visible only while the selected device is a
	/// physical phone AND the large-text option is checked -- the combination that actually changes the phone's
	/// own system text size (see Services/PhysicalDeviceNotice for the physical-phone notice shown regardless of this
	/// option's state). Announced when it newly appears (WCAG 4.1.3 Status Messages, same convention as
	/// UiLog.Report): it can appear from ticking the large-text option alone, with no other visible change a
	/// screen reader user would otherwise notice.</summary>
	private void UpdatePhysicalDeviceLine()
	{
		var visible = SelectedDevice() is { IsPhysical: true } && LargeTextOption.IsChecked;
		if (visible && !PhysicalDeviceTextSizeLabel.IsVisible)
			SemanticScreenReader.Announce(PhysicalDeviceTextSizeLabel.Text);
		PhysicalDeviceTextSizeLabel.IsVisible = visible;
	}

	private async void OnChooseBuild(object? sender, EventArgs e)
	{
		var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose a build to install" });
		if (file is not null)
			BuildEntry.Text = file.FullPath;
	}

	/// <summary>Only the keystore file path is picked here -- its password is never asked for or stored; see
	/// KeystorePasswordHintLabel and ScanOptions.AndroidKeystore's remarks.</summary>
	private async void OnChooseKeystore(object? sender, EventArgs e)
	{
		var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose a keystore" });
		if (file is not null)
			KeystoreEntry.Text = file.FullPath;
	}

	private async void OnChooseBundletool(object? sender, EventArgs e)
	{
		var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose bundletool" });
		if (file is not null)
			BundletoolEntry.Text = file.FullPath;
	}

	/// <summary>
	/// Opens the app picker (Pages/AppPickerPage) for the selected device. The App field stays required and
	/// always editable by hand -- this only offers a way to fill it without typing an id from memory.
	/// ChooseAppButton itself is disabled until a device is chosen (UpdateDeviceRequiredControls), so this
	/// guard is a defensive fallback, not the normal way "no device yet" is communicated any more.
	/// </summary>
	private async void OnChooseApp(object? sender, EventArgs e)
	{
		if (DevicePicker.SelectedIndex < 0 || DevicePicker.SelectedIndex >= _devices.Count)
		{
			await DisplayAlertAsync("Choose a device first", "Select a platform and device above before searching for an app.", "OK");
			return;
		}
		var picker = new AppPickerPage(_devices[DevicePicker.SelectedIndex]);
		await Navigation.PushModalAsync(picker);
		if (await picker.Result is { } chosen)
			AppIdEntry.Text = chosen.Id;
	}

	private void OnScanNow(object? sender, EventArgs e) => _recording?.RequestScanNow();

	private void OnFinish(object? sender, EventArgs e) => _recording?.Stop();

	/// <summary>
	/// Live, changeable at any time during a recording (not just a one-time choice from the per-screen
	/// prompt): what to do once a screen shows that the larger size needs a restart to check further.
	/// </summary>
	private void OnLargeTextRestartPolicyChanged(object? sender, EventArgs e) => _recording?.SetLargeTextRestartPolicy(LargeTextRestartPicker.SelectedIndex switch
	{
		1 => LargeTextRestartPolicy.Always,
		2 => LargeTextRestartPolicy.Never,
		_ => LargeTextRestartPolicy.Ask,
	});

	/// <summary>
	/// Asks, on the main thread, whether to check a screen at the larger size (see LargeTextRestartPolicy);
	/// called only while the picker above is at "Ask each time" -- either right after the live attempt that
	/// first taught this recording a restart is needed, or (for every later screen) before the size is
	/// touched at all, since the cost is already known. Also updates the picker itself when the answer is
	/// "always check"/"never check", so it stays a truthful, changeable live setting rather than a one-time
	/// choice the person can't see or undo. Reused for a single scan too:
	/// <see cref="Recording"/> picks the record-mode wording ("navigate back twice") or scan's own
	/// ("Swipewalk will restart the app and try again") -- "always check"/"never check" still work for a scan,
	/// they just have no later screen to apply to.
	/// </summary>
	private Task<LargeTextRestartChoice> AskLargeTextRestartAsync(
		string screenName, string reason, bool learnedFromEarlierScreen, Platform platform, CancellationToken cancellationToken) =>
		MainThread.InvokeOnMainThreadAsync(async () =>
		{
			// A scan is one screen, so "always"/"never" have no later screen to apply to -- offer just the
			// two choices that mean something there; a recording still gets its own standing-choice buttons.
			var answer = Recording
				? await DisplayActionSheetAsync(
					LargeTextAskWording.Message(screenName, reason, learnedFromEarlierScreen, platform, recordMode: true),
					"Don't check this screen", null, "Check anyway", "Always check", "Never check")
				: await DisplayActionSheetAsync(
					LargeTextAskWording.Message(screenName, reason, learnedFromEarlierScreen, platform, recordMode: false),
					"Don't check this screen", null, "Check anyway");
			var choice = answer switch
			{
				"Check anyway" => LargeTextRestartChoice.RestartAndCheck,
				"Always check" => LargeTextRestartChoice.AlwaysRestart,
				"Never check" => LargeTextRestartChoice.AlwaysSkip,
				_ => LargeTextRestartChoice.SkipForThisScreen,
			};
			if (choice == LargeTextRestartChoice.AlwaysRestart)
				LargeTextRestartPicker.SelectedIndex = 1;
			else if (choice == LargeTextRestartChoice.AlwaysSkip)
				LargeTextRestartPicker.SelectedIndex = 2;
			return choice;
		});

	/// <summary>
	/// Asked once, when Finish is pressed, if any screen wasn't checked at the larger size: offers to keep
	/// recording so the person can navigate back and check them (opt-in, declinable -- nothing here navigates
	/// or captures on its own).
	/// </summary>
	private Task<bool> AskRevisitSkippedScreensAsync(IReadOnlyList<string> screenNames, CancellationToken cancellationToken) =>
		MainThread.InvokeOnMainThreadAsync(() => DisplayAlertAsync(
			$"{screenNames.Count} screen(s) weren't checked at the larger text size",
			$"{string.Join(", ", screenNames)}. Keep recording so you can navigate back and check them?",
			"Keep recording", "Finish now"));

	private async void OnStart(object? sender, EventArgs e)
	{
		var appId = string.IsNullOrWhiteSpace(AppIdEntry.Text) ? null : AppIdEntry.Text.Trim();
		var build = string.IsNullOrWhiteSpace(BuildEntry.Text) ? null : BuildEntry.Text.Trim();
		if (appId is null && build is null)
		{
			await DisplayAlertAsync("Which app?", "Enter the app's package or bundle id, or choose a build file to install.", "OK");
			return;
		}
		// TalkBack capture changes a physical phone's accessibility settings (temporarily -- see
		// Services/TalkBackNotice); confirm once before the first run that does this on a given phone, the
		// same way the CLI's --screen-reader asks on a physical device (Program.cs,
		// ConfirmScreenReaderOnPhysicalDeviceAsync). An emulator isn't gated at all.
		if (Platform == TargetPlatform.Android && TalkBackOption.IsChecked && SelectedDevice() is { IsPhysical: true }
			&& !TalkBackNoticePreference.Confirmed && !await TalkBackNotice.ShowAsync(this))
		{
			return;
		}

		_log.Clear();
		SetRunning(true, record: Recording);
		SemanticScreenReader.Announce("Scan started");
		var log = new UiLog(_log);
		var service = new ScanService(log);
		// The run's own StartedAt (History/Dashboard ordering, RunHistory.Previous) must stay the original
		// recording's start, not when this continued session began.
		var started = _continuingRun?.StartedAt ?? DateTimeOffset.Now;
		try
		{
			var options = new ScanOptions
			{
				Platform = Platform,
				Device = DevicePicker.SelectedIndex >= 0 ? _devices[DevicePicker.SelectedIndex].Id : null,
				Package = Platform == TargetPlatform.Android ? appId : null,
				BundleId = Platform == TargetPlatform.Ios ? appId : null,
				InstallFile = build,
				// Android only (the section is hidden for iOS -- OnPlatformChanged -- but guard on Platform
				// here too, the same reason ScreenReaderCapture below does); ignored anyway unless InstallFile
				// ends in .aab (see AppInstaller.InstallAndroidAsync), so leaving them blank for a .apk/.ipa
				// install is harmless.
				BundletoolPath = Platform == TargetPlatform.Android && !string.IsNullOrWhiteSpace(BundletoolEntry.Text)
					? BundletoolEntry.Text.Trim() : null,
				AndroidKeystore = Platform == TargetPlatform.Android && !string.IsNullOrWhiteSpace(KeystoreEntry.Text)
					? KeystoreEntry.Text.Trim() : null,
				AndroidKeystoreAlias = Platform == TargetPlatform.Android && !string.IsNullOrWhiteSpace(KeystoreAliasEntry.Text)
					? KeystoreAliasEntry.Text.Trim() : null,
				Standard = StandardPicker.SelectedIndex > 0 ? KnownStandards.All[StandardPicker.SelectedIndex - 1].Id : null,
				LargeText = LargeTextOption.IsChecked,
				Framework = MauiOption.IsChecked ? AppFramework.Maui : null,
				AutoScanOnScreenChange = AutoScanOption.IsChecked,
				// Android only for now (TalkBackOption is hidden for iOS -- OnPlatformChanged -- but guard
				// on Platform here too, so a stale checked state from before a platform switch can never
				// silently turn this on for an iOS run).
				ScreenReaderCapture = Platform == TargetPlatform.Android && TalkBackOption.IsChecked,
				// Same picker, same mapping as OnLargeTextRestartPolicyChanged: it now applies to a single
				// scan too, not just a recording's later screens.
				LargeTextRestartPolicy = LargeTextRestartPicker.SelectedIndex switch
				{
					1 => LargeTextRestartPolicy.Always,
					2 => LargeTextRestartPolicy.Never,
					_ => LargeTextRestartPolicy.Ask,
				},
				OutputDirectory = "",
			};

			options = await Task.Run(() => service.InstallAsync(options));
			if (options.AppId is null)
			{
				log.Report("Stopped: could not tell which app to scan. Enter its package or bundle id.");
				return;
			}
			// Continuing writes back into the same run's own folder and appends to what it already has --
			// never a new folder, and never another run (separate runs are never merged, overwritten or replaced).
			options = options with { OutputDirectory = _continuingRun?.Folder ?? AppState.History.NewRunFolder(options.AppId) };

			var checks = await Task.Run(() => service.CheckAsync(options));
			foreach (var check in checks.Where(c => c.Status != CheckStatus.Pass))
				log.Report(check.ToString());
			if (!Preflight.CanProceed(checks))
			{
				log.Report("Not ready: fix the items marked FAIL (details on the Devices page).");
				SemanticScreenReader.Announce("Scan stopped: the device is not ready");
				return;
			}

			RunResult result;
			if (Recording)
			{
				_recording = new RecorderControl();
				log.Report(AutoScanOption.IsChecked
					? "Recording. Each new screen is scanned automatically; choose Finish when done."
					: "Recording. Nothing is captured until you press Scan this screen now; choose Finish when done.");
				// Written before the first screen, not just after it (that's OnScreenSaved below): a process
				// killed before it captures anything (a crash, a force-quit) still leaves this run in History
				// marked "in progress" -- see RunHistory.StartRecordingAsync.
				await AppState.History.StartRecordingAsync(options, started, options.OutputDirectory, _continuingReport);
				// Saved to history after every screen (not just at the end), so a recording interrupted by an
				// error, the app crashing, or the app itself being force-quit still appears in
				// History/Dashboard with whatever it captured -- see RunHistory.OnScreenSaved.
				var saveProgress = AppState.History.OnScreenSaved(options, "record", started, options.OutputDirectory, log);
				result = await Task.Run(() => service.RecordAsync(options, _recording, cancellationToken: default, onScreen: screen =>
				{
					MainThread.BeginInvokeOnMainThread(() => SemanticScreenReader.Announce($"Scanned {screen.ScreenName}"));
					saveProgress(screen);
				}, largeTextRestartAsk: AskLargeTextRestartAsync, revisitSkippedScreensAsk: AskRevisitSkippedScreensAsync, continuation: _continuation));
			}
			else
			{
				result = await Task.Run(() => service.ScanAsync(options, largeTextRestartAsk: AskLargeTextRestartAsync));
			}

			var run = await AppState.History.SaveAsync(result, options, Recording ? "record" : "scan", started);
			log.Report(ReportWriter.Summary(result.Report));
			if (result.Report.EndedEarlyReason is { } endedEarly)
				log.Report($"Note: {endedEarly}");
			// Done with this continuation either way (ended early again, or reached Finish): a further Start
			// press on this same page instance must not silently keep appending to it without a fresh Continue
			// from History -- re-enable Mode so it reads as an ordinary new scan/recording again.
			_continuingRun = null;
			_continuation = null;
			_continuingReport = null;
			ModePicker.IsEnabled = true;
			AppState.NotifyRunsChanged();
			SemanticScreenReader.Announce($"Scan finished: {run.Counts.WcagIssues} WCAG issues");
			await Shell.Current.GoToAsync($"report?folder={Uri.EscapeDataString(run.Folder)}");
		}
		catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
		{
			log.Report($"Failed: {ex.Message}");
			SemanticScreenReader.Announce("Scan failed");
		}
		catch (Exception ex)
		{
			// Last resort, not a substitute for the specific catch above: this is an async void event
			// handler, so anything that escapes it becomes an unhandled exception on Mac Catalyst -- with no
			// global handler registered for this app, that crashes the whole process, mid-scan or
			// mid-recording, instead of leaving a working app with a plain error message. A crash here is
			// also more likely to land during Shell/page teardown, the same moment a known MAUI issue
			// (ShellSectionRootRenderer disposing mid-trait-change) can turn an ordinary crash into a hang-
			// like stall -- so an unexpected error is reported the same way a recognized one is, rather than
			// left to reach that path at all.
			log.Report($"Failed: {ex.Message}");
			SemanticScreenReader.Announce("Scan failed");
		}
		finally
		{
			_recording = null;
			SetRunning(false, record: false);
		}
	}

	/// <summary>Whether a scan/recording is under way -- StartButton also needs a device chosen
	/// (UpdateDeviceRequiredControls combines both reasons it might be disabled).</summary>
	private bool _running;

	private void SetRunning(bool running, bool record)
	{
		_running = running;
		ScanNowButton.IsVisible = FinishButton.IsVisible = running && record;
		if (!running)
			LargeTextRestartPicker.SelectedIndex = 0;
		UpdateDeviceRequiredControls();
	}
}
