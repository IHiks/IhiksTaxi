namespace IhiksTaxi.Application;

public sealed class RouteResult
{
	public decimal DistanceKm { get; init; }
	public int DurationMin { get; init; }

	// линия маршрута в формате GeoJSON; null — маршрута по дорогам нет
	public string? Geometry { get; init; }

	// true — расстояние и время взяты из дорожного роутера, false — это прямая линия
	public bool IsRoadRoute { get; init; }
}
