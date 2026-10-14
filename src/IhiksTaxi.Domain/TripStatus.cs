namespace IhiksTaxi.Domain;

public class TripStatus
{
	public int Id { get; set; }
	public string Name { get; set; } = null!;

	public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
