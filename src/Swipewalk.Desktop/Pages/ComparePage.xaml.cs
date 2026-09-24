using Swipewalk.Core.Reports;
using Swipewalk.Desktop.Services;
using Swipewalk.Engine;

namespace Swipewalk.Desktop.Pages;

/// <summary>A run compared with the previous run of the same app. Navigate with ?run=&lt;run folder&gt;.</summary>
[QueryProperty(nameof(Run), "run")]
public partial class ComparePage : ContentPage
{
	private RunRecord? _later;
	private RunRecord? _earlier;

	public ComparePage()
	{
		InitializeComponent();
		AppMenus.Attach(this);
		Fonts.Changed += Render;
	}

	public string Run
	{
		set
		{
			var folder = Uri.UnescapeDataString(value);
			_later = AppState.History.List().FirstOrDefault(r => r.Folder == folder);
			_earlier = _later is null ? null : AppState.History.Previous(_later);
			Render();
		}
	}

	private void Render()
	{
		Sections.Children.Clear();
		OpenEarlier.IsVisible = _earlier is not null;
		OpenLater.IsVisible = _later is not null;
		if (_later is null || _earlier is null)
		{
			Subtitle.Text = _later is null ? "This run is no longer in the history."
				: _later.AppKey is null ? RunRecord.NotIdentifiedComparisonNote
				: $"{_later.App} has no earlier run to compare with.";
			Summary.Text = "";
			return;
		}

		Subtitle.Text = $"{_later.App} · {_later.Platform}: this run ({_later.StartedAt:yyyy-MM-dd HH:mm:ss}) compared with the previous run ({_earlier.StartedAt:yyyy-MM-dd HH:mm:ss})";
		if (RunHistory.Compare(_earlier, _later) is not { } diff)
		{
			Summary.Text = "The results of one of these runs could not be read.";
			return;
		}

		Summary.Text = $"{diff.New.Count} new, {diff.NoLongerFound.Count} no longer found, {diff.StillFound.Count} still found";
		Section("New", diff.New.Select(ReportComparison.Describe), "Nothing new.");
		Section("No longer found", diff.NoLongerFound.Select(ReportComparison.Describe), "Nothing.");
		if (diff.NotCheckedAgain.Count > 0)
			Section("Not checked again", diff.NotCheckedAgain.Select(ReportComparison.Describe), "",
				"These findings could not be compared with this run. Each line says why: either the screen wasn't scanned again, or the check that found it didn't run this time (for example, no large-text capture).");
		if (diff.ScreensNotScannedAgain.Count > 0)
			Section("Screens not scanned again", diff.ScreensNotScannedAgain, "",
				"Their findings are listed under \"Not checked again\" above. Scan these screens again to see what changed.");
		Section("Still found", diff.StillFound.Select(ReportComparison.Describe), "Nothing.");
	}

	private void Section(string title, IEnumerable<string> lines, string empty, string? note = null)
	{
		var items = lines.ToList();
		var heading = new Label { Text = $"{title} ({items.Count})", FontSize = Fonts.Size("FontHeading"), FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 12, 0, 0) };
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level2);
		Sections.Children.Add(heading);
		if (note is not null)
			Sections.Children.Add(new Label { Text = note, FontSize = Fonts.Size("FontCaption") });
		if (items.Count == 0)
			Sections.Children.Add(new Label { Text = empty, FontSize = Fonts.Size("FontBody") });
		foreach (var line in items)
			Sections.Children.Add(new Label { Text = "• " + line, FontSize = Fonts.Size("FontBody"), LineBreakMode = LineBreakMode.WordWrap });
	}

	private async void OnOpenLater(object? sender, EventArgs e) => await OpenReport(_later);

	private async void OnOpenEarlier(object? sender, EventArgs e) => await OpenReport(_earlier);

	private static async Task OpenReport(RunRecord? run)
	{
		if (run is not null)
			await Shell.Current.GoToAsync($"report?folder={Uri.EscapeDataString(run.Folder)}");
	}
}
