namespace IhiksTaxi.Infrastructure;

public static class ConnectionStrings
{
	// строка подключения собирается из LocalSettings (файл taxi.settings.json или переменная окружения)
	public static string Default => LocalSettings.GetConnectionString();
}
