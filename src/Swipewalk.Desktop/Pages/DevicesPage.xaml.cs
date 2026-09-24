using Swipewalk.Collectors;
using Swipewalk.Collectors.Preflight;
using Swipewalk.Core.Model;
using Swipewalk.Desktop.Services;
using Swipewalk.Engine;
using DeviceInfo = Swipewalk.Core.Model.DeviceInfo;
using Platform = Swipewalk.Core.Model.Platform;

namespace Swipewalk.Desktop.Pages;

public partial class DevicesPage : ContentPage
{
	public DevicesPage()
	{
		InitializeComponent();
		AppMenus.Attach(this);
		Fonts.Changed += () => _ = RefreshAsync();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RefreshAsync();
	}

	private async void OnRefresh(object? sender, EventArgs e) => await RefreshAsync();

	private async Task RefreshAsync()
	{
		Busy.IsRunning = Busy.IsVisible = true;
		DeviceList.Children.Clear();
		var android = await Task.Run(Devices.AndroidAsync);
		var ios = await Task.Run(Devices.IosWithStatusAsync);
		foreach (var device in android)
			DeviceList.Children.Add(Row(device, null));
		foreach (var (device, problem) in ios.Where(d => d.Device.IsPhysical || d.Problem is null))
			DeviceList.Children.Add(Row(device, problem));
		if (DeviceList.Children.Count == 0)
			DeviceList.Children.Add(new Label { Text = "No devices found. Connect a phone with USB debugging, start an emulator, or boot an iOS Simulator.", FontSize = Fonts.Size("FontBody") });
		Busy.IsRunning = Busy.IsVisible = false;
	}

	private View Row(DeviceInfo device, string? problem)
	{
		var check = new Button { Text = "Check", IsEnabled = problem is null };
		SemanticProperties.SetDescription(check, $"Check whether {device.Name} is ready to scan");
		check.Clicked += async (_, _) => await CheckAsync(device);
		var scan = new Button { Text = "Scan…", IsEnabled = problem is null };
		SemanticProperties.SetDescription(scan, $"New scan on {device.Name}");
		scan.Clicked += async (_, _) => await Shell.Current.GoToAsync($"//scan?device={Uri.EscapeDataString(device.Id)}");
		var text = new VerticalStackLayout
		{
			Spacing = 2,
			Children =
			{
				new Label { Text = device.Name, FontSize = Fonts.Size("FontSubheading"), FontAttributes = FontAttributes.Bold },
				new Label { Text = $"{(device.Platform == Platform.Android ? "Android" : "iOS")} {device.OsVersion} · {device.Kind} · {device.Id}", FontSize = Fonts.Size("FontCaption") },
			},
		};
		if (problem is not null)
			text.Children.Add(new Label { Text = problem, FontSize = Fonts.Size("FontCaption"), TextColor = AppState.ThemeColor("Issue") });
		return new Border
		{
			Padding = new Thickness(14, 10),
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
			Content = new Grid
			{
				ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
				ColumnSpacing = 8,
				Children = { text, check.Column(1), scan.Column(2) },
			},
		};
	}

	private async Task CheckAsync(DeviceInfo device)
	{
		CheckList.Children.Clear();
		CheckList.Children.Add(new Label { Text = $"Checking {device.Name}...", FontSize = Fonts.Size("FontBody") });
		var options = new ScanOptions
		{
			Platform = device.Platform == Platform.iOS ? TargetPlatform.Ios : TargetPlatform.Android,
			Device = device.Id,
			OutputDirectory = "",
		};
		var checks = await Task.Run(() => new ScanService(new Progress<string>()).CheckAsync(options));
		CheckList.Children.Clear();
		foreach (var c in checks)
		{
			var (mark, color) = c.Status switch
			{
				CheckStatus.Pass => ("✓ Ready", "Advisory"),
				CheckStatus.Warn => ("⚠ Note", "Review"),
				_ => ("✗ Fix", "Issue"),
			};
			var item = new VerticalStackLayout { Spacing = 2 };
			item.Children.Add(new Label
			{
				FormattedText = new FormattedString
				{
					Spans =
					{
						new Span { Text = $"{mark}  ", FontAttributes = FontAttributes.Bold, TextColor = AppState.ThemeColor(color) },
						new Span { Text = $"{c.Name}: {c.Detail}" },
					},
				},
				FontSize = Fonts.Size("FontBody"),
			});
			if (c.Fix is not null)
				item.Children.Add(new Label { Text = c.Fix, FontSize = Fonts.Size("FontCaption"), Margin = new Thickness(24, 0, 0, 0) });
			CheckList.Children.Add(item);
		}
	}
}

internal static class ViewExtensions
{
	public static T Column<T>(this T view, int column) where T : View
	{
		Grid.SetColumn(view, column);
		return view;
	}
}
