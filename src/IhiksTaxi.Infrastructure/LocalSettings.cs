using System.Text;
using System.Text.Json;

namespace IhiksTaxi.Infrastructure;

// параметры подключения читаем из файла рядом с приложением, а не из кода
public sealed class LocalSettings
{
	public string Host { get; set; } = "localhost";
	public int Port { get; set; } = 5432;
	public string Database { get; set; } = "IhiksTaxiCourse";
	public string Username { get; set; } = "postgres";
	public string Password { get; set; } = string.Empty;

	// "offline" — карта и маршруты из локальных данных, "online" — внешние сервисы OSRM/Photon
	public string RoutingMode { get; set; } = "offline";

	// пусто — папка offline-data рядом с приложением
	public string OfflineDataPath { get; set; } = string.Empty;

	public string BuildConnectionString() =>
		$"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password}";

	public static LocalSettings Load()
	{
		var path = Path.Combine(AppContext.BaseDirectory, "taxi.settings.json");
		if (!File.Exists(path))
			return new LocalSettings();

		var json = File.ReadAllText(path, Encoding.UTF8);
		var settings = JsonSerializer.Deserialize<LocalSettings>(json,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		return settings ?? new LocalSettings();
	}

	// переменная окружения удобна для миграций и тестов, чтобы не хранить файл с паролем
	public static string GetConnectionString()
	{
		var fromEnvironment = Environment.GetEnvironmentVariable("IHIKSTAXI_CONNECTION");
		if (!string.IsNullOrWhiteSpace(fromEnvironment))
			return fromEnvironment;

		return Load().BuildConnectionString();
	}
}
