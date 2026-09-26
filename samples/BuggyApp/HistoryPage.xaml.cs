namespace BuggyApp;

public partial class HistoryPage : ContentPage
{
	// Planted bug (auto-update-1, see HistoryPage.xaml): cycles PromoBanner's text on its own, forever,
	// with no way for the person to pause, stop or hide it -- for `scan --auto-update-content` (WCAG
	// 2.2.2 Pause, Stop, Hide).
	private static readonly string[] PromoMessages =
	[
		"New: Autopay available -- never miss a due date",
		"Save 10% when you switch to paperless billing",
		"Refer a friend and get $5 off your next ticket",
	];

	private int promoIndex;
	private IDispatcherTimer? promoTimer;

	public HistoryPage()
	{
		InitializeComponent();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		promoTimer ??= Dispatcher.CreateTimer();
		promoTimer.Interval = TimeSpan.FromSeconds(2);
		promoTimer.Tick += (_, _) =>
		{
			promoIndex = (promoIndex + 1) % PromoMessages.Length;
			PromoBanner.Text = PromoMessages[promoIndex];
		};
		promoTimer.Start();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		promoTimer?.Stop();
	}
}
