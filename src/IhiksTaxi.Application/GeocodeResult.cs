namespace IhiksTaxi.Application;

// результат одного адреса из геокодера; Exact — точный дом, FromPlaceIndex — точка из справочника мест
public sealed class GeocodeResult
{
	public string DisplayName { get; init; } = null!;
	public GeoPoint Point { get; init; }
	public bool Exact { get; init; }
	public bool FromStreetFallback { get; init; }
	public bool FromPlaceIndex { get; init; }

	public override string ToString() => DisplayName;
}
