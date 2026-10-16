namespace IhiksTaxi.Application;

public sealed class DriverInfo
{
	public int DriverId { get; init; }
	public string FullName { get; init; } = null!;
	public string Phone { get; init; } = null!;
	public int VehicleId { get; init; }
	public string Vehicle { get; init; } = null!;
}
