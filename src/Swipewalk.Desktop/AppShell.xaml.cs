using Swipewalk.Desktop.Pages;

namespace Swipewalk.Desktop;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("report", typeof(ReportPage));
		Routing.RegisterRoute("compare", typeof(ComparePage));
		Routing.RegisterRoute("guide", typeof(GuidedChecksPage));
		// The sidebar grows with the text size so its text reflows instead of breaking inside words.
		FlyoutWidth = SidebarWidth();
		Services.Fonts.Changed += () => FlyoutWidth = SidebarWidth();

		// Optional start page: Swipewalk --page history|devices|scan|dashboard|report|compare (latest run).
		var page = Services.AppState.ArgumentValue("--page");
		if (page is "dashboard" or "scan" or "devices" or "history")
			Loaded += async (_, _) => await GoToAsync("//" + page);
		else if (page == "report" && Services.AppState.History.List().FirstOrDefault() is { } latest)
			Loaded += async (_, _) => await GoToAsync($"report?folder={Uri.EscapeDataString(latest.Folder)}");
		else if (page == "compare" && Services.AppState.History.List().FirstOrDefault() is { } newest)
			Loaded += async (_, _) => await GoToAsync($"compare?run={Uri.EscapeDataString(newest.Folder)}");
		else if (page == "guide" && Services.AppState.History.List().FirstOrDefault() is { } forGuide)
			Loaded += async (_, _) => await GoToAsync($"guide?folder={Uri.EscapeDataString(forGuide.Folder)}");
	}

	/// <summary>Grows with the text size, but less than the text, so the page keeps room to reflow.</summary>
	private static double SidebarWidth() => 220 + (Services.Fonts.Scale - 1) * 110;
}
