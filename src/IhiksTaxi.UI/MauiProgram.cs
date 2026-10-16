using IhiksTaxi.Application;
using IhiksTaxi.Infrastructure;
using Microsoft.Extensions.Logging;

namespace IhiksTaxi.UI;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		builder.Services.AddInfrastructure();
		builder.Services.AddSingleton(_ => new MapServer(OfflineData.Root(LocalSettings.Load())));
		builder.Services.AddSingleton<RouteMapPresenter>();
		builder.Services.AddSingleton<SessionService>();
		builder.Services.AddSingleton<AuthService>();
		builder.Services.AddSingleton<TripService>();
		builder.Services.AddTransient<AuthViewModel>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<MainViewModel>();
		builder.Services.AddTransient<MainPage>();

		return builder.Build();
	}
}
