using System.Diagnostics;

using Swipewalk.Desktop.Services;

namespace Swipewalk.Desktop.Pages;

/// <summary>Shows a saved run's report.html inside the app. Navigate with ?folder=&lt;run folder&gt;.</summary>
[QueryProperty(nameof(Folder), "folder")]
public partial class ReportPage : ContentPage
{
	private string _folder = "";

	public ReportPage()
	{
		InitializeComponent();
		AppMenus.Attach(this);
		Fonts.Changed += () => { if (_folder.Length > 0) Render(); };
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

	private static void Open(string path) =>
		Process.Start(new ProcessStartInfo(OperatingSystem.IsWindows() ? "explorer" : "open", $"\"{path}\"") { UseShellExecute = false });
}
