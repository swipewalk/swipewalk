namespace Swipewalk.Desktop.Services;

/// <summary>
/// App-wide text size (WCAG 1.4.4): every font size is a named resource scaled by one factor, 100% to 200%,
/// set from View > Text Size and remembered. Views built in code call <see cref="Size"/> and are rebuilt on change.
/// </summary>
public static class Fonts
{
	private static readonly Dictionary<string, double> BaseSizes = new()
	{
		["FontCaption"] = 13,
		["FontBody"] = 15,
		["FontSubheading"] = 16,
		["FontHeading"] = 20,
		["FontTitle"] = 26,
		["FontDisplay"] = 30,
	};

	public const double Minimum = 1.0;
	public const double Maximum = 2.0;
	private const string PreferenceKey = "TextScale";

	public static double Scale { get; private set; } = 1.0;

	public static event Action? Changed;

	public static double Size(string key) => BaseSizes[key] * Scale;

	/// <summary>False when started with --text-scale (UI tests): changes are not remembered.</summary>
	private static bool _remember = true;

	/// <summary>Applies the remembered scale, or --text-scale &lt;n&gt; for this session only; call once at startup.</summary>
	public static void Initialize()
	{
		if (AppState.ArgumentValue("--text-scale") is { } value
			&& double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scale))
		{
			_remember = false;
			Apply(scale, announce: false);
			return;
		}
		Apply(Preferences.Default.Get(PreferenceKey, 1.0), announce: false);
	}

	public static void Bigger() => Apply(Scale + 0.25);

	public static void Smaller() => Apply(Scale - 0.25);

	public static void Reset() => Apply(1.0);

#if MACCATALYST
	private static void Refresh(Element element)
	{
		if (element is Button { Handler.PlatformView: UIKit.UIButton button } view)
		{
			button.SetNeedsUpdateConfiguration();
			view.InvalidateMeasure();
		}
		foreach (var child in ((IElementController)element).LogicalChildren)
			Refresh(child);
	}
#endif

	private static void Apply(double scale, bool announce = true)
	{
		Scale = Math.Clamp(Math.Round(scale * 4) / 4, Minimum, Maximum);
		foreach (var (key, size) in BaseSizes)
			Application.Current!.Resources[key] = size * Scale;
		if (_remember)
			Preferences.Default.Set(PreferenceKey, Scale);
		Changed?.Invoke();
#if MACCATALYST
		// Native Mac buttons read the scaled font when their configuration updates.
		foreach (var window in Application.Current!.Windows)
			if (window.Page is { } page)
				Refresh(page);
#endif
		if (announce)
			SemanticScreenReader.Announce($"Text size {Scale:P0}");
	}
}
