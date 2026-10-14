using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IhiksTaxi.Infrastructure;

// нужен dotnet-ef для создания миграций, берёт строку подключения здесь, а не из UI
public class IhiksTaxiDbContextFactory : IDesignTimeDbContextFactory<IhiksTaxiDbContext>
{
	public IhiksTaxiDbContext CreateDbContext(string[] args)
	{
		var options = new DbContextOptionsBuilder<IhiksTaxiDbContext>()
			.UseNpgsql(ConnectionStrings.Default)
			.Options;

		return new IhiksTaxiDbContext(options);
	}
}
