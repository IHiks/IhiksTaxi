namespace IhiksTaxi.Application;

public sealed class PassengerInfo
{
	public int Id { get; init; }
	public string FullName { get; init; } = null!;
	public string Phone { get; init; } = null!;
	public string? Email { get; init; }
	public int TripsCount { get; init; }
}
