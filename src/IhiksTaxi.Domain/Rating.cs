namespace IhiksTaxi.Domain;

public class Rating
{
	public int Id { get; set; }
	public int DriverId { get; set; }
	// одна оценка на поездку
	public int? TripId { get; set; }
	public int Rate { get; set; }

	public Driver Driver { get; set; } = null!;
	public Trip? Trip { get; set; }
}
