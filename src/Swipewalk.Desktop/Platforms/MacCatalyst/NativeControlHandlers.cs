using Swipewalk.Desktop.Controls;
using Microsoft.Maui.Handlers;
using Foundation;
using UIKit;
using AppFonts = Swipewalk.Desktop.Services.Fonts;

namespace Swipewalk.Desktop.Platforms.MacCatalyst;

/// <summary>
/// In the Mac idiom MAUI's button colors and entry borders are not applied, leaving dim, low-contrast buttons
/// and borderless text fields (WCAG 1.4.3, 1.4.11). Use the system's macOS styles, whose contrast the OS manages.
/// Buttons with StyleClass "primary" get the prominent (accent) style.
/// </summary>
public static class MacStyles
{
	public static void Apply()
	{
		ButtonHandler.Mapper.AppendToMapping("MacSystemStyle", (handler, view) =>
		{
			var primary = view is Button { StyleClass: { } classes } && classes.Contains("primary");
			var configuration = primary
				? UIButtonConfiguration.BorderedProminentButtonConfiguration
				: UIButtonConfiguration.BorderedButtonConfiguration;
			configuration.Title = (view as Button)?.Text;
			configuration.TitleTextAttributesTransformer = ScaledFont;
			if (primary)
			{
				// The system accent (white on #0A84FF, about 3.6:1) is below 4.5:1; use the app's verified palette:
				// white on #1F5FA8 (6.2:1) in light mode, #0F1216 on #8AB4F8 (9:1) in dark mode.
				configuration.BaseBackgroundColor = Dynamic(light: (0x1F, 0x5F, 0xA8), dark: (0x8A, 0xB4, 0xF8));
				configuration.BaseForegroundColor = Dynamic(light: (0xFF, 0xFF, 0xFF), dark: (0x0F, 0x12, 0x16));
			}
			handler.PlatformView.Configuration = configuration;
		});
	}

	/// <summary>Button titles follow the app's text size (WCAG 1.4.4); native Mac controls don't scale otherwise.</summary>
	internal static NSDictionary ScaledFont(NSDictionary attributes)
	{
		var copy = new NSMutableDictionary(attributes);
		copy[UIStringAttributeKey.Font] = UIFont.SystemFontOfSize((nfloat)AppFonts.Size("FontBody"));
		return copy;
	}

	private static UIColor Dynamic((byte R, byte G, byte B) light, (byte R, byte G, byte B) dark) =>
		UIColor.FromDynamicProvider(traits => traits.UserInterfaceStyle == UIUserInterfaceStyle.Dark
			? UIColor.FromRGB(dark.R, dark.G, dark.B)
			: UIColor.FromRGB(light.R, light.G, light.B));
}


/// <summary>A macOS pop-up button (UIButton with a selection menu): role "pop up button", value = the selected item.</summary>
public class SelectButtonHandler() : ViewHandler<SelectButton, UIButton>(Mapper)
{
	public static readonly IPropertyMapper<SelectButton, SelectButtonHandler> Mapper =
		new PropertyMapper<SelectButton, SelectButtonHandler>(ViewMapper)
		{
			[nameof(SelectButton.Items)] = (h, _) => h.Rebuild(),
			[nameof(SelectButton.SelectedIndex)] = (h, _) => h.Rebuild(),
		};

	protected override UIButton CreatePlatformView()
	{
		var button = new UIButton(UIButtonType.System)
		{
			ShowsMenuAsPrimaryAction = true,
			ChangesSelectionAsPrimaryAction = true,
			HorizontalAlignment = UIControlContentHorizontalAlignment.Leading,
		};
		button.PreferredBehavioralStyle = UIBehavioralStyle.Mac;
		var configuration = UIButtonConfiguration.BorderedButtonConfiguration;
		configuration.TitleTextAttributesTransformer = MacStyles.ScaledFont;
		button.Configuration = configuration;
		// Catalyst exposes this as a button with a value; say that it opens a list of choices.
		button.AccessibilityHint = "Opens a menu of choices";
		return button;
	}

	protected override void ConnectHandler(UIButton platformView)
	{
		base.ConnectHandler(platformView);
		AppFonts.Changed += OnFontsChanged;
	}

	protected override void DisconnectHandler(UIButton platformView)
	{
		AppFonts.Changed -= OnFontsChanged;
		base.DisconnectHandler(platformView);
	}

	private void OnFontsChanged()
	{
		PlatformView.SetNeedsUpdateConfiguration();
		VirtualView.InvalidateMeasure();
	}

	private void Rebuild()
	{
		var view = VirtualView;
		if (view.Items.Count == 0)
		{
			PlatformView.Menu = null;
			PlatformView.SetTitle(view.EmptyText ?? "None", UIControlState.Normal);
			PlatformView.Enabled = false;
			return;
		}
		PlatformView.Enabled = view.IsEnabled;
		var actions = view.Items.Select((item, index) =>
		{
			var action = UIAction.Create(item, null, null, _ =>
			{
				if (view.SelectedIndex != index)
					view.SelectedIndex = index;
			});
			action.State = index == view.SelectedIndex ? UIMenuElementState.On : UIMenuElementState.Off;
			return (UIMenuElement)action;
		}).ToArray();
		if (view.SelectedIndex < 0 && view.PlaceholderText is { } placeholder)
		{
			// No selection yet: a leading, already-On placeholder entry, so UIKit's own selection-title sync
			// (already what drives every other SelectButton's displayed title correctly) shows it as the
			// button's title/value. Calling SetTitle directly here instead was tried and found unreliable on a
			// real run: a later configuration-update pass silently replaced it with the first real item's
			// title once the menu itself was assigned, so the placeholder never actually showed. Choosing this
			// inert entry (its handler does nothing) is harmless if someone clicks it.
			var none = UIAction.Create(placeholder, null, null, _ => { });
			none.State = UIMenuElementState.On;
			actions = [none, .. actions];
		}
		PlatformView.Menu = UIMenu.Create(actions);
	}
}

/// <summary>A native macOS checkbox (UISwitch, checkbox style) with its label built in, so name and state belong together.</summary>
public class CheckOptionHandler() : ViewHandler<CheckOption, UISwitch>(Mapper)
{
	public static readonly IPropertyMapper<CheckOption, CheckOptionHandler> Mapper =
		new PropertyMapper<CheckOption, CheckOptionHandler>(ViewMapper)
		{
			[nameof(CheckOption.Text)] = (h, v) => h.PlatformView.Title = v.Text,
			[nameof(CheckOption.IsChecked)] = (h, v) => { if (h.PlatformView.On != v.IsChecked) h.PlatformView.SetState(v.IsChecked, false); },
		};

	protected override UISwitch CreatePlatformView() => new() { PreferredStyle = UISwitchStyle.Checkbox };

	protected override void ConnectHandler(UISwitch platformView)
	{
		base.ConnectHandler(platformView);
		platformView.ValueChanged += OnValueChanged;
	}

	protected override void DisconnectHandler(UISwitch platformView)
	{
		platformView.ValueChanged -= OnValueChanged;
		base.DisconnectHandler(platformView);
	}

	private void OnValueChanged(object? sender, EventArgs e) => VirtualView.IsChecked = PlatformView.On;
}
