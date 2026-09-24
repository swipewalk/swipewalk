using Swipewalk.Desktop.Controls;
using Swipewalk.Desktop.Services;
using Swipewalk.Engine;

namespace Swipewalk.Desktop.Pages;

/// <summary>Summary of saved runs: totals, and per app the latest run, its change since the previous run and a trend.</summary>
public partial class DashboardPage : ContentPage
{
	public DashboardPage()
	{
		InitializeComponent();
		AppMenus.Attach(this);
		AppState.RunsChanged += Load;
		Fonts.Changed += Load;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Load();
	}

	private void Load()
	{
		var runs = AppState.History.List();
		// GroupKey, not App: two runs with no identified app share the "Unknown app" label but could be
		// different apps, so grouping by App would lump them onto one card and compare them with each other
		// (see RunRecord.GroupKey).
		var latest = runs.GroupBy(r => (r.GroupKey, r.Platform)).Select(g => g.First()).ToList();

		// Counted separately: a run with no identified app is its own card (see GroupKey above), but it isn't
		// an additional "app" the way an identified one is, and the same app scanned on two platforms is one
		// app, not two -- so this counts distinct AppKeys, not cards.
		var identifiedApps = latest.Select(r => r.AppKey).Where(k => k is not null).Distinct().Count();
		var unidentifiedRuns = latest.Count(r => r.AppKey is null);

		Tiles.Children.Clear();
		Tiles.Children.Add(Tile(runs.Count.ToString(), "runs saved"));
		Tiles.Children.Add(Tile(identifiedApps.ToString(), "apps identified"));
		if (unidentifiedRuns > 0)
			Tiles.Children.Add(Tile(unidentifiedRuns.ToString(), unidentifiedRuns == 1 ? "run with no identified app" : "runs with no identified app"));
		Tiles.Children.Add(Tile(latest.Sum(r => r.Counts.WcagIssues).ToString(), "WCAG issues in latest runs", "Issue"));
		Tiles.Children.Add(Tile(latest.Sum(r => r.Counts.NeedsReview).ToString(), "items to review in latest runs", "Review"));

		LatestList.Children.Clear();
		if (latest.Count == 0)
			LatestList.Children.Add(new Label { Text = "No runs yet.", FontSize = Fonts.Size("FontBody") });
		foreach (var run in latest)
			LatestList.Children.Add(AppCard(run, [.. runs.Where(r => r.GroupKey == run.GroupKey && r.Platform == run.Platform)]));
	}

	/// <summary>Latest run of one app: counts, change since the previous run, trend and actions. No description on
	/// the card itself: it would hide the texts and buttons inside from screen readers.</summary>
	private static View AppCard(RunRecord run, IReadOnlyList<RunRecord> appRuns)
	{
		var title = new Label { Text = $"{run.App} · {run.Platform}", FontSize = Fonts.Size("FontSubheading"), FontAttributes = FontAttributes.Bold };
		SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level3);
		var counts = new Label
		{
			Text = $"{run.Counts.WcagIssues} WCAG issues, {run.Counts.NeedsReview} to review, {run.Counts.Screens} screen(s) · {run.StartedAt:yyyy-MM-dd HH:mm} · {run.StandardLabel}",
			FontSize = Fonts.Size("FontBody"),
		};

		// appRuns is grouped by GroupKey (see Load), which is unique per run when run.AppKey is null, so
		// appRuns.Count is always 1 there and previous/diff stay null; the AppKey is null check below only
		// picks the right wording for that case rather than the ordinary "first run" one.
		var previous = appRuns.Count > 1 ? appRuns[1] : null;
		var diff = previous is null ? null : RunHistory.Compare(previous, run);
		var change = new Label
		{
			Text = run.AppKey is null ? RunRecord.NotIdentifiedComparisonNote
				: previous is null ? "First run of this app; later runs are compared with it."
				: diff is null ? "The previous run's results could not be read."
				: $"Since the previous run: {diff.New.Count} new, {diff.NoLongerFound.Count} no longer found",
			FontSize = Fonts.Size("FontBody"),
		};

		var open = new Button { Text = "Open report" };
		SemanticProperties.SetDescription(open, $"Open report: {run.App}");
		open.Clicked += async (_, _) => await Shell.Current.GoToAsync($"report?folder={Uri.EscapeDataString(run.Folder)}");
		var buttons = new HorizontalStackLayout { Spacing = 12, Children = { open } };
		if (diff is not null)
		{
			var compare = new Button { Text = "Compare with previous run" };
			SemanticProperties.SetDescription(compare, $"Compare with previous run: {run.App}");
			compare.Clicked += async (_, _) => await Shell.Current.GoToAsync($"compare?run={Uri.EscapeDataString(run.Folder)}");
			buttons.Children.Add(compare);
		}

		var content = new VerticalStackLayout { Spacing = 6, Children = { title, counts, change } };
		if (appRuns.Count > 1)
		{
			content.Children.Add(new Label { Text = $"WCAG issues per run, oldest to newest (last {Math.Min(appRuns.Count, TrendChart.MaxRuns)})", FontSize = Fonts.Size("FontCaption") });
			content.Children.Add(new TrendChart(run.App, appRuns));
		}
		content.Children.Add(buttons);
		return new Border
		{
			Padding = new Thickness(18, 12),
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
			Content = content,
		};
	}

	private static View Tile(string value, string label, string? color = null)
	{
		var number = new Label { Text = value, FontSize = Fonts.Size("FontDisplay"), FontAttributes = FontAttributes.Bold };
		if (color is not null)
			number.TextColor = AppState.ThemeColor(color);
		var tile = new Border
		{
			Padding = new Thickness(18, 12),
			Margin = new Thickness(0, 0, 12, 12),
			WidthRequest = 220,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
			Content = new VerticalStackLayout { Children = { number, new Label { Text = label, FontSize = Fonts.Size("FontBody") } } },
		};
		SemanticProperties.SetDescription(tile, $"{value} {label}");
		return tile;
	}

	private async void OnNewScan(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//scan");
}
