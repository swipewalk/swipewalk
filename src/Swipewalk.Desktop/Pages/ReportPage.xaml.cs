using System.Diagnostics;

using Swipewalk.Desktop.Services;

namespace Swipewalk.Desktop.Pages;

/// <summary>Shows a saved run's report.html inside the app. Navigate with ?folder=&lt;run folder&gt;.</summary>
[QueryProperty(nameof(Folder), "folder")]
public partial class ReportPage : ContentPage
{
	private string _folder = "";

	// Kept in a field (not an inline lambda) so OnAppearing/OnDisappearing below can (un)subscribe the exact same
	// delegate: this page is created fresh on every "report?folder=..." navigation (Routing.RegisterRoute, not a
	// Shell tab page reused for the app's life -- see AppShell.xaml), so subscribing once in the constructor and
	// only ever unsubscribing in OnDisappearing would leak every earlier instance -- including its native WebView
	// -- forever (kept alive by Fonts' static event, each one reloading its HTML on every later text-size change
	// even though it's no longer shown). It would also leave THIS instance permanently unresponsive to text-size
	// changes once merely covered rather than popped: OnGuidedChecks below pushes GuidedChecksPage on top of this
	// same instance, and coming back from it re-runs OnAppearing without the constructor running again.
	// Subscribing/unsubscribing symmetrically in OnAppearing/OnDisappearing keeps exactly one live subscription
	// while shown, none while not, however many times this instance is covered and revealed again.
	private readonly Action _onFontsChanged;

	public ReportPage()
	{
		InitializeComponent();
		AppMenus.Attach(this);
		_onFontsChanged = () => { if (_folder.Length > 0) Render(); };
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Fonts.Changed += _onFontsChanged;
	}

	protected override void OnDisappearing()
	{
		Fonts.Changed -= _onFontsChanged;
		base.OnDisappearing();
	}

	public string Folder
	{
		get => _folder;
		set
		{
			_folder = Uri.UnescapeDataString(value);
			TitleLabel.Text = Path.GetFileName(_folder);
			Render();
		}
	}

	/// <summary>Loads the report, scaled with the app's text size (View > Text Size).</summary>
	private void Render()
	{
		var report = Path.Combine(_folder, "report.html");
		var html = File.Exists(report) ? File.ReadAllText(report) : "<p>The report file is missing.</p>";
		var zoom = $"<style>html {{ zoom: {Fonts.Scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}; }}</style>";
		Viewer.Source = new HtmlWebViewSource { Html = html.Replace("</head>", zoom + "</head>", StringComparison.Ordinal) };
	}

	private void OnOpenInBrowser(object? sender, EventArgs e) => Open(Path.Combine(_folder, "report.html"));

	private void OnShowFolder(object? sender, EventArgs e) => Open(_folder);

	private async void OnGuidedChecks(object? sender, EventArgs e) =>
		await Shell.Current.GoToAsync($"guide?folder={Uri.EscapeDataString(_folder)}");

	private static void Open(string path) =>
		Process.Start(new ProcessStartInfo(OperatingSystem.IsWindows() ? "explorer" : "open", $"\"{path}\"") { UseShellExecute = false });
}
