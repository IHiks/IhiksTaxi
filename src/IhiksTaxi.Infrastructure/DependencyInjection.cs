using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IhiksTaxi.Infrastructure;

public static class DependencyInjection
{
	// DbContext регистрируем как Singleton: у настольного приложения один поток UI,
	// scoped-провайдер в MAUI не создаётся сам
	public static IServiceCollection AddInfrastructure(this IServiceCollection services)
	{
		services.AddDbContext<IhiksTaxiDbContext>(options => options.UseNpgsql(ConnectionStrings.Default),
			ServiceLifetime.Singleton);

		return services;
	}
}
