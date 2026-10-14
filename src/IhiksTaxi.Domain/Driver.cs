namespace IhiksTaxi.Domain;

public class Driver
{
	public int Id { get; set; }
	public string FullName { get; set; } = null!;
	public string Phone { get; set; } = null!;
	public int ExperienceYears { get; set; }
	public string Category { get; set; } = null!;

	public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
	public ICollection<Trip> Trips { get; set; } = new List<Trip>();
	public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
}
