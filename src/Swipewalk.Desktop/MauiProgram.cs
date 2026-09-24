using Microsoft.Extensions.Logging;

namespace Swipewalk.Desktop;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureMauiHandlers(handlers =>
			{
				// Native controls with correct accessibility roles (see Controls/SelectButton.cs).
#if MACCATALYST
				Platforms.MacCatalyst.MacStyles.Apply();
				handlers.AddHandler<Controls.SelectButton, Platforms.MacCatalyst.SelectButtonHandler>();
				handlers.AddHandler<Controls.CheckOption, Platforms.MacCatalyst.CheckOptionHandler>();
#elif WINDOWS
				handlers.AddHandler<Controls.SelectButton, Platforms.Windows.SelectButtonHandler>();
				handlers.AddHandler<Controls.CheckOption, Platforms.Windows.CheckOptionHandler>();
#endif
			})
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
