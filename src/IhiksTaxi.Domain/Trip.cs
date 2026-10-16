namespace IhiksTaxi.Domain;

public class Trip
{
	public int Id { get; set; }
	public int PassengerId { get; set; }
	// null — водитель ещё не назначен
	public int? DriverId { get; set; }
	public string FromAddress { get; set; } = null!;
	public string ToAddress { get; set; } = null!;
	public decimal DistanceKm { get; set; }
	public decimal Cost { get; set; }
	// в БД колонка trip_date
	public DateTime CreatedAt { get; set; }
	public int? DurationMin { get; set; }
	public int TripStatusId { get; set; }
	public int TariffId { get; set; }

	public Passenger Passenger { get; set; } = null!;
	public Driver? Driver { get; set; }
	public TripStatus TripStatus { get; set; } = null!;
	public Tariff Tariff { get; set; } = null!;
	public Rating? Rating { get; set; }
}
