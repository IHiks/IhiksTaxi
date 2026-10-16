using IhiksTaxi.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace IhiksTaxi.Infrastructure;

public static class DependencyInjection
{
	// фабрика контекстов: каждый запрос к БД получает свой короткоживущий контекст
	public static IServiceCollection AddInfrastructure(this IServiceCollection services)
	{
		services.AddDbContextFactory<IhiksTaxiDbContext>(options =>
			options.UseNpgsql(ConnectionStrings.Default));

		services.AddSingleton<ITaxiStore, EfTaxiStore>();

		var settings = LocalSettings.Load();
		var root = OfflineData.Root(settings);

		// по умолчанию карта и маршруты локальные; внешние сервисы включаются настройкой RoutingMode
		if (string.Equals(settings.RoutingMode, "online", StringComparison.OrdinalIgnoreCase))
		{
			var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
			httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("IHIKS-Taxi/1.0 (educational project)");
			services.AddSingleton(httpClient);
			services.AddSingleton<IRouteService, OsrmRouteService>();
			services.AddSingleton<IGeocoder, PhotonGeocoder>();
		}
		else
		{
			services.AddSingleton<IRouteService>(_ => new LocalRouteService(OfflineData.RoadGraph(root)));
			services.AddSingleton<IGeocoder>(_ => new LocalGeocoder(OfflineData.AddressIndex(root)));
		}

		return services;
	}
}
