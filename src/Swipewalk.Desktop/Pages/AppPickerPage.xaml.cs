using Swipewalk.Collectors;
using Swipewalk.Desktop.Controls;
using Swipewalk.Desktop.Services;
using DeviceInfo = Swipewalk.Core.Model.DeviceInfo;
using Platform = Swipewalk.Core.Model.Platform;

namespace Swipewalk.Desktop.Pages;

/// <summary>
/// Search-and-choose app picker shown modally from New scan's "Choose app…" button (which requires a device to
/// already be selected: see NewScanPage.OnChooseApp). Recently scanned apps (from the run history) come first,
/// then apps installed on the device; typing filters both by id and, where known, by name. The App field on New
/// scan stays a normal text entry throughout -- typing an id directly always still works, including one not
/// (yet) installed, whether or not this page is ever opened.
/// </summary>
public partial class AppPickerPage : ContentPage
{
	private readonly DeviceInfo _device;
	private readonly TaskCompletionSource<AppListing?> _result = new();
	private readonly IReadOnlyList<string> _recentIds;
	private IReadOnlyList<AppListing> _installed = [];
	private List<AppListing> _filtered = [];
	private bool _loaded;
	private bool _closed;
	private int _loadGeneration;

	/// <summary>The chosen app, or null if the page was cancelled (Cancel, Escape).</summary>
	public Task<AppListing?> Result => _result.Task;

	public AppPickerPage(DeviceInfo device)
	{
		InitializeComponent();
		_device = device;
		Heading.Text = $"Choose app on {device.Name}";
		Subtitle.Text = device.Platform == Platform.Android
			? "Recently scanned apps, then apps installed on the device. Android does not give a fast way to read app names, so only ids are shown."
			: "Recently scanned apps, then apps installed on the device, with names where the device reports them.";
		_recentIds = [.. AppState.History.List()
			.Where(r => r.Platform == PlatformLabel(device) && r.AppKey is not null)
			.Select(r => r.AppKey!)
			.Distinct()];

		Apps.SelectionChanged += OnAppSelected;
		SearchEntry.TextChanged += (_, _) => Render();
		SearchEntry.Completed += OnSearchCompleted;
		ShowSystemOption.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(CheckOption.IsChecked))
				_ = LoadAsync();
		};
	}

	private static string PlatformLabel(DeviceInfo device) => device.Platform == Platform.Android ? "Android" : "iOS";

	protected override void OnAppearing()
	{
		base.OnAppearing();
		ModalDismiss.Current = Cancel;
		if (!_loaded)
		{
			_loaded = true;
			_ = LoadAsync();
		}
		SearchEntry.Focus();
	}

	protected override void OnDisappearing()
	{
		ModalDismiss.Current = null;
		base.OnDisappearing();
	}

	private async Task LoadAsync()
	{
		var generation = ++_loadGeneration;
		var includeSystem = ShowSystemOption.IsChecked;
		Busy.IsRunning = Busy.IsVisible = true;
		StatusLabel.Text = "";
		IReadOnlyList<AppListing> installed;
		string status = "";
		try
		{
			installed = await Task.Run(() => AppsList.ForDeviceAsync(_device, includeSystem));
		}
		catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
		{
			installed = [];
			status = $"Could not list installed apps: {ex.Message} You can still type the app's id above and press Enter.";
		}
		// Discard a result overtaken by a newer request (e.g. "Show system apps" toggled again before this returned).
		if (generation != _loadGeneration)
			return;
		_installed = installed;
		StatusLabel.Text = status;
		Busy.IsRunning = Busy.IsVisible = false;
		Render();
	}

	/// <summary>Recently scanned apps first (de-duplicated, most recent first), then installed apps not already listed.</summary>
	private List<AppListing> Merged()
	{
		var byId = _installed.ToDictionary(a => a.Id, a => a);
		var seen = new HashSet<string>(StringComparer.Ordinal);
		var ordered = new List<AppListing>();
		foreach (var id in _recentIds)
			if (seen.Add(id))
				ordered.Add(byId.TryGetValue(id, out var installed) ? installed : new AppListing(id, null));
		foreach (var app in _installed)
			if (seen.Add(app.Id))
				ordered.Add(app);
		return ordered;
	}

	private void Render()
	{
		var filter = SearchEntry.Text?.Trim() ?? "";
		var merged = Merged();
		_filtered = filter.Length == 0
			? merged
			: [.. merged.Where(a => a.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)
				|| (a.Label is not null && a.Label.Contains(filter, StringComparison.OrdinalIgnoreCase)))];
		Apps.ItemsSource = _filtered;

		EmptyLabel.IsVisible = _filtered.Count == 0 && StatusLabel.Text.Length == 0;
		if (_filtered.Count == 0)
			EmptyLabel.Text = filter.Length == 0
				? "No apps found on this device. Try \"Show system apps\", or type the app's id above and press Enter."
				: "No matching app. Press Enter to use the typed id as-is.";
	}

	private void OnSearchCompleted(object? sender, EventArgs e)
	{
		var filter = SearchEntry.Text?.Trim() ?? "";
		if (_filtered.Count == 1)
			Choose(_filtered[0]);
		else if (_filtered.Count == 0 && filter.Length > 0)
			Choose(new AppListing(filter, null));
		// Two or more matches: ambiguous from the keyboard here; Tab into the list, then arrow keys and Space to pick one.
	}

	private void OnAppSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is AppListing app)
			Choose(app);
	}

	private void OnCancel(object? sender, EventArgs e) => Cancel();

	private async void Choose(AppListing app)
	{
		if (_closed)
			return;
		_closed = true;
		_result.TrySetResult(app);
		await Navigation.PopModalAsync();
	}

	private async void Cancel()
	{
		if (_closed)
			return;
		_closed = true;
		_result.TrySetResult(null);
		await Navigation.PopModalAsync();
	}
}
