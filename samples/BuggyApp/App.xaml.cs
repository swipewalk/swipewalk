namespace BuggyApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		// Ground truth assumes light colors (contrast values in ground-truth.json).
		UserAppTheme = AppTheme.Light;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new NavigationPage(new MainPage()));
	}
}