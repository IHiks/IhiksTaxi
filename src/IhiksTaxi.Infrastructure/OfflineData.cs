namespace IhiksTaxi.Infrastructure;

// офлайн-данные карты лежат папкой рядом с приложением; путь можно переопределить в настройках
public static class OfflineData
{
	public const string FolderName = "offline-data";

	public static string Root(LocalSettings settings)
		=> string.IsNullOrWhiteSpace(settings.OfflineDataPath)
			? Path.Combine(AppContext.BaseDirectory, FolderName)
			: settings.OfflineDataPath;

	public static string AddressIndex(string root) => Path.Combine(root, "index", "addresses.json");

	public static string RoadGraph(string root) => Path.Combine(root, "graph", "roadgraph.json");
}
