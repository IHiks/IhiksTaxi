namespace IhiksTaxi.Domain;

public class Street
{
	public int Id { get; set; }
	public string Name { get; set; } = null!;
	public decimal Latitude { get; set; }
	public decimal Longitude { get; set; }
}
