namespace Swipewalk.Desktop.Controls;

/// <summary>
/// Choose one of several options, exposed to assistive technology as a pop-up button (macOS) or combo box
/// (Windows). Replaces MAUI's Picker, which on Mac Catalyst is exposed as a text field (wrong role, WCAG 4.1.2).
/// The accessible name comes from SemanticProperties.Description.
/// </summary>
public class SelectButton : View
{
	public static readonly BindableProperty ItemsProperty =
		BindableProperty.Create(nameof(Items), typeof(IList<string>), typeof(SelectButton), Array.Empty<string>());

	public static readonly BindableProperty SelectedIndexProperty =
		BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(SelectButton), -1, BindingMode.TwoWay,
			propertyChanged: (b, _, _) => ((SelectButton)b).SelectedIndexChanged?.Invoke(b, EventArgs.Empty));

	public IList<string> Items
	{
		get => (IList<string>)GetValue(ItemsProperty);
		set => SetValue(ItemsProperty, value);
	}

	public int SelectedIndex
	{
		get => (int)GetValue(SelectedIndexProperty);
		set => SetValue(SelectedIndexProperty, value);
	}

	/// <summary>Shown when there are no items, e.g. "No device found".</summary>
	public string? EmptyText { get; set; }

	/// <summary>Shown as the button's title (and so its accessible value) when there are items but none is
	/// selected yet (SelectedIndex -1), e.g. "Choose a device" -- distinct from <see cref="EmptyText"/>, which
	/// is for when there are no items at all.</summary>
	public string? PlaceholderText { get; set; }

	public event EventHandler? SelectedIndexChanged;
}

/// <summary>
/// A checkbox whose label is part of the control, exposed as a checkbox with that name. Replaces MAUI's CheckBox,
/// which on Mac Catalyst is exposed as a button, with its label as a separate text.
/// </summary>
public class CheckOption : View
{
	public static readonly BindableProperty TextProperty =
		BindableProperty.Create(nameof(Text), typeof(string), typeof(CheckOption), "");

	public static readonly BindableProperty IsCheckedProperty =
		BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(CheckOption), false, BindingMode.TwoWay);

	public string Text
	{
		get => (string)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public bool IsChecked
	{
		get => (bool)GetValue(IsCheckedProperty);
		set => SetValue(IsCheckedProperty, value);
	}
}
