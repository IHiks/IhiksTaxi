namespace IhiksTaxi.Application;

public sealed class TripInfo
{
	public int Id { get; init; }
	public int TariffId { get; init; }
	public string FromAddress { get; init; } = null!;
	public string ToAddress { get; init; } = null!;
	public decimal DistanceKm { get; init; }
	public int? DurationMin { get; init; }
	public decimal Cost { get; init; }
	public DateTime CreatedAt { get; init; }
	public string StatusName { get; init; } = null!;
	public int? DriverId { get; init; }
	public string? DriverName { get; init; }
	public string? DriverPhone { get; init; }
	public string? Vehicle { get; init; }
	public int? Rating { get; init; }
}
