namespace IhiksTaxi.Application;

public sealed class MapCar
{
	// стабильный id водителя: по нему JS обновляет маркер, а не создаёт заново
	public int DriverId { get; init; }
	public string Name { get; init; } = null!;
	public string Vehicle { get; init; } = null!;
	public GeoPoint Point { get; init; }

	// true — это машина, назначенная на текущий заказ; false — свободный водитель
	public bool IsAssigned { get; init; }
}
