using Swipewalk.Desktop.Controls;
using Microsoft.Maui.Handlers;
using WinUI = Microsoft.UI.Xaml.Controls;

namespace Swipewalk.Desktop.Platforms.Windows;

// Not yet compiled or tested: needs a Windows machine (roadmap: Windows). WinUI ComboBox and CheckBox already
// expose the right UI Automation roles, so these handlers only adapt the Swipewalk controls to them.

public class SelectButtonHandler() : ViewHandler<SelectButton, WinUI.ComboBox>(Mapper)
{
	public static readonly IPropertyMapper<SelectButton, SelectButtonHandler> Mapper =
		new PropertyMapper<SelectButton, SelectButtonHandler>(ViewMapper)
		{
			[nameof(SelectButton.Items)] = (h, v) => { h.PlatformView.ItemsSource = v.Items; h.PlatformView.SelectedIndex = v.SelectedIndex; },
			[nameof(SelectButton.SelectedIndex)] = (h, v) => h.PlatformView.SelectedIndex = v.SelectedIndex,
		};

	protected override WinUI.ComboBox CreatePlatformView() => new() { PlaceholderText = VirtualView.PlaceholderText ?? "" };

	protected override void ConnectHandler(WinUI.ComboBox platformView)
	{
		base.ConnectHandler(platformView);
		platformView.SelectionChanged += OnSelectionChanged;
	}

	protected override void DisconnectHandler(WinUI.ComboBox platformView)
	{
		platformView.SelectionChanged -= OnSelectionChanged;
		base.DisconnectHandler(platformView);
	}

	private void OnSelectionChanged(object sender, WinUI.SelectionChangedEventArgs e) => VirtualView.SelectedIndex = PlatformView.SelectedIndex;
}

public class CheckOptionHandler() : ViewHandler<CheckOption, WinUI.CheckBox>(Mapper)
{
	public static readonly IPropertyMapper<CheckOption, CheckOptionHandler> Mapper =
		new PropertyMapper<CheckOption, CheckOptionHandler>(ViewMapper)
		{
			[nameof(CheckOption.Text)] = (h, v) => h.PlatformView.Content = v.Text,
			[nameof(CheckOption.IsChecked)] = (h, v) => h.PlatformView.IsChecked = v.IsChecked,
		};

	protected override WinUI.CheckBox CreatePlatformView() => new();

	protected override void ConnectHandler(WinUI.CheckBox platformView)
	{
		base.ConnectHandler(platformView);
		platformView.Checked += OnChanged;
		platformView.Unchecked += OnChanged;
	}

	protected override void DisconnectHandler(WinUI.CheckBox platformView)
	{
		platformView.Checked -= OnChanged;
		platformView.Unchecked -= OnChanged;
		base.DisconnectHandler(platformView);
	}

	private void OnChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => VirtualView.IsChecked = PlatformView.IsChecked == true;
}
