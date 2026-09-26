namespace Swipewalk.Desktop;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		Services.Fonts.Initialize();
		// UI-test-only: forces a known starting state for the physical-device notice preference (see
		// Services/PhysicalDeviceNoticePreference.Reset) instead of leaving the shared macOS preference for the
		// app's bundle id however a previous run left it.
		if (Services.AppState.HasFlag("--reset-physical-device-notice"))
			Services.PhysicalDeviceNoticePreference.Reset();
		// UI-test-only: same reasoning as above, for the TalkBack confirmation (Services/TalkBackNoticePreference).
		if (Services.AppState.HasFlag("--reset-talkback-notice"))
			Services.TalkBackNoticePreference.Reset();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell()) { Title = "Swipewalk", Width = 1280, Height = 860, MinimumWidth = 900, MinimumHeight = 600 };
	}
}