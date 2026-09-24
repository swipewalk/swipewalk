namespace BuggyApp;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	private async void OnHistoryClicked(object? sender, EventArgs e) => await Navigation.PushAsync(new HistoryPage());
}
