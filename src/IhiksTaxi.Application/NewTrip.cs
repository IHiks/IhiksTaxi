namespace IhiksTaxi.Application;

public sealed class NewTrip
{
	public int PassengerId { get; init; }
	public int TariffId { get; init; }
	public string FromAddress { get; init; } = null!;
	public string ToAddress { get; init; } = null!;
	public decimal DistanceKm { get; init; }
	public int DurationMin { get; init; }
	public decimal Cost { get; init; }
}
