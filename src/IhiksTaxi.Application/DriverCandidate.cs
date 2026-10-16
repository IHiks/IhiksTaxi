namespace IhiksTaxi.Application;

public sealed class DriverCandidate
{
	public int DriverId { get; init; }
	public int VehicleId { get; init; }
	public int TariffId { get; init; }
	public bool HasActiveTrip { get; init; }

	// где водитель находится сейчас; null, если улица водителя неизвестна
	public GeoPoint? CurrentPoint { get; init; }
}
