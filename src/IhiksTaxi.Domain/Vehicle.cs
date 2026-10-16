namespace IhiksTaxi.Domain;

public class Vehicle
{
	public int Id { get; set; }
	public int DriverId { get; set; }
	public string Brand { get; set; } = null!;
	public string Model { get; set; } = null!;
	// в БД колонка называется class, поэтому свойство VehicleClass
	public string VehicleClass { get; set; } = null!;
	public string Color { get; set; } = null!;
	public string Plate { get; set; } = null!;
	public int TariffId { get; set; }

	public Driver Driver { get; set; } = null!;
	public Tariff Tariff { get; set; } = null!;
}
