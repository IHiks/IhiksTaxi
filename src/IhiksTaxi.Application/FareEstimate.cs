namespace IhiksTaxi.Application;

public sealed class FareEstimate
{
	public decimal DistanceKm { get; init; }
	public int DurationMin { get; init; }
	public decimal Cost { get; init; }

	// true — это запасной расчёт по прямой линии, дорожный роутер был недоступен
	public bool IsApproximate { get; init; }

	// линия маршрута GeoJSON для карты; null — рисовать нечего
	public string? Geometry { get; init; }
}
