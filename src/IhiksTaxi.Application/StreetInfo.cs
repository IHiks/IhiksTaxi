namespace IhiksTaxi.Application;

public sealed class StreetInfo
{
	public int Id { get; init; }
	public string Name { get; init; } = null!;
	public decimal Latitude { get; init; }
	public decimal Longitude { get; init; }

	public GeoPoint ToGeoPoint() => new((double)Latitude, (double)Longitude);

	public override string ToString() => Name;
}
