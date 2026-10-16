namespace IhiksTaxi.Application;

// где находится водитель на карте; точка берётся из улицы водителя (CurrentStreet)
public sealed class DriverMapPoint
{
	public int DriverId { get; init; }
	public string Name { get; init; } = null!;
	public string Vehicle { get; init; } = null!;
	public GeoPoint? Point { get; init; }
	public bool HasActiveTrip { get; init; }
}
