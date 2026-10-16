namespace IhiksTaxi.Application;

// всё, что карта должна показать прямо сейчас; передаётся в JavaScript как состояние
public sealed class MapState
{
	public GeoPoint? From { get; init; }
	public string? FromName { get; init; }
	public GeoPoint? To { get; init; }
	public string? ToName { get; init; }

	// линия маршрута GeoJSON; null — дорожного маршрута нет, рисовать выдуманный путь нельзя
	public string? Geometry { get; init; }

	// true — дорожный роутер не нашёл путь, расстояние посчитано по прямой
	public bool IsApproximate { get; init; }

	public IReadOnlyList<MapCar> Cars { get; init; } = Array.Empty<MapCar>();

	// статус текущей поездки или null, если активной поездки нет
	public string? StatusName { get; init; }
}
