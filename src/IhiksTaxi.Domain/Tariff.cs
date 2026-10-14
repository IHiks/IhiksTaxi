namespace IhiksTaxi.Domain;

public class Tariff
{
	public int Id { get; set; }
	public string Name { get; set; } = null!;
	public decimal BaseCost { get; set; }
	public decimal RatePerKm { get; set; }
	public decimal RatePerMinute { get; set; }
	public decimal DemandFactorDefault { get; set; }

	public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
