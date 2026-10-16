namespace IhiksTaxi.Application;

// результат одного адреса из геокодера; Exact — точный дом, Approximate — приблизительная точка
public sealed class GeocodeResult
{
	public string DisplayName { get; init; } = null!;
	public GeoPoint Point { get; init; }
	public bool Exact { get; init; }
	public bool FromStreetFallback { get; init; }

	public override string ToString() => DisplayName;
}
