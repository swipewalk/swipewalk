namespace Swipewalk.Desktop.Services;

/// <summary>
/// The Mac menu bar with keyboard shortcuts (WCAG 2.1.1): Go (⌘1-⌘4), File > New Scan (⌘N), Text (⌘=, ⌘-, ⌘0 via
/// key commands), Help. MAUI shows a page's menu bar items while that page is active, so every page attaches them.
/// </summary>
public static class AppMenus
{
	public static void Attach(ContentPage page)
	{
		page.MenuBarItems.Clear();
		page.MenuBarItems.Add(Menu("File", Item("New Scan", "N", () => Go("//scan"))));
		page.MenuBarItems.Add(Menu("Go",
			Item("Dashboard", "1", () => Go("//dashboard")),
			Item("New Scan", "2", () => Go("//scan")),
			Item("Devices", "3", () => Go("//devices")),
			Item("History", "4", () => Go("//history"))));
		// Shortcuts ⌘=, ⌘-, ⌘0 are key commands (Platforms/MacCatalyst/AppDelegate.cs): MAUI menu shortcuts accept only
		// letters and digits, and a menu with other keys is dropped. The titles show the shortcuts for discoverability.
		page.MenuBarItems.Add(Menu("Zoom",
			Item("Bigger Text (Command =)", null, Fonts.Bigger),
			Item("Smaller Text (Command -)", null, Fonts.Smaller),
			Item("Actual Size (Command 0)", null, Fonts.Reset)));
		page.MenuBarItems.Add(Menu("Help",
			Item("Keyboard Shortcuts", null, () => _ = page.DisplayAlertAsync("Keyboard shortcuts", Shortcuts, "OK")),
			Item("Accessibility Statement", null, () => _ = Launcher.Default.OpenAsync(new Uri(StatementUrl)))));
	}

	public const string StatementUrl = "https://github.com/swipewalk/swipewalk/blob/main/docs/accessibility-statement.md";

	private const string Shortcuts =
		"⌘1 Dashboard\n⌘2 or ⌘N New scan\n⌘3 Devices\n⌘4 History\n⌘= Bigger text\n⌘- Smaller text\n⌘0 Actual text size\n\n" +
		"With Full Keyboard Access on (System Settings > Accessibility > Keyboard), Tab moves between all controls.";

	private static MenuBarItem Menu(string text, params MenuFlyoutItem[] items)
	{
		var menu = new MenuBarItem { Text = text };
		foreach (var item in items)
			menu.Add(item);
		return menu;
	}

	private static MenuFlyoutItem Item(string text, string? key, Action action)
	{
		var item = new MenuFlyoutItem { Text = text };
		item.Clicked += (_, _) => action();
		if (key is not null)
			item.KeyboardAccelerators.Add(new KeyboardAccelerator { Modifiers = KeyboardAcceleratorModifiers.Cmd, Key = key });
		return item;
	}

	private static void Go(string route) => _ = Shell.Current.GoToAsync(route);
}
