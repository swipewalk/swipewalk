using Swipewalk.Desktop.Services;
using Swipewalk.Engine;

namespace Swipewalk.Desktop.Pages;

public partial class HistoryPage : ContentPage
{
	public HistoryPage()
	{
		InitializeComponent();
		AppMenus.Attach(this);
		AppState.RunsChanged += Load;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Load();
	}

	private void Load()
	{
		var runs = AppState.History.List();
		Runs.ItemsSource = runs;
		Summary.Text = runs.Count == 0 ? $"Runs are saved in {AppState.History.Root}." : $"{runs.Count} saved run(s) in {AppState.History.Root}";
	}

	private async void OnRunSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is not RunRecord run)
			return;
		Runs.SelectedItem = null;
		await Shell.Current.GoToAsync($"report?folder={Uri.EscapeDataString(run.Folder)}");
	}

	private async void OnDelete(object? sender, EventArgs e)
	{
		if ((sender as Button)?.CommandParameter is not RunRecord run)
			return;
		if (!await DisplayAlertAsync("Delete run", $"Delete the run of {run.App} from {run.StartedAt:yyyy-MM-dd HH:mm}? Its report and screenshots are removed.", "Delete", "Cancel"))
			return;
		AppState.History.Delete(run);
		Load();
	}

	/// <summary>Only shown for a recording that ended early (see RunRecord.CanContinue).
	/// Hands off to New scan, which does the actual resuming (RecorderControl, RecordAsync) the same way it
	/// starts any other recording -- see NewScanPage's ContinueRunId query property.</summary>
	private async void OnContinue(object? sender, EventArgs e)
	{
		if ((sender as Button)?.CommandParameter is not RunRecord run)
			return;
		await Shell.Current.GoToAsync($"//scan?continue={Uri.EscapeDataString(run.Id)}");
	}
}
