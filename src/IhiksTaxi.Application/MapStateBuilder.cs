using IhiksTaxi.Domain;

namespace IhiksTaxi.Application;

// собирает состояние карты: какие точки А/Б показать и где нарисовать машины.
// функция чистая — только читает входные данные, ничего не сохраняет и не меняет адрес водителя.
public static class MapStateBuilder
{
	public static MapState Build(
		IReadOnlyList<DriverMapPoint> drivers,
		GeoPoint? from, string? fromName,
		GeoPoint? to, string? toName,
		FareEstimate? estimate,
		TripInfo? activeTrip)
	{
		return new MapState
		{
			From = from,
			FromName = fromName,
			To = to,
			ToName = toName,
			Geometry = estimate?.Geometry,
			IsApproximate = estimate?.IsApproximate ?? false,
			Cars = BuildCars(drivers, from, to, activeTrip),
			StatusName = activeTrip?.StatusName,
		};
	}

	private static IReadOnlyList<MapCar> BuildCars(
		IReadOnlyList<DriverMapPoint> drivers, GeoPoint? from, GeoPoint? to, TripInfo? activeTrip)
	{
		var cars = new List<MapCar>();
		var assignedDriverId = activeTrip?.DriverId;

		// машина водителя, назначенного на текущую поездку: показываем по демонстрационному
		// положению, которое зависит от статуса поездки, а не от постоянного адреса водителя
		if (activeTrip is not null && assignedDriverId is not null)
		{
			var driver = drivers.FirstOrDefault(d => d.DriverId == assignedDriverId);
			var point = DemoCarPosition.Resolve(activeTrip.StatusName, driver?.Point, from, to);

			if (point is not null)
			{
				cars.Add(new MapCar
				{
					DriverId = assignedDriverId.Value,
					Name = driver?.Name ?? activeTrip.DriverName ?? string.Empty,
					Vehicle = driver?.Vehicle ?? activeTrip.Vehicle ?? string.Empty,
					Point = point.Value,
					IsAssigned = true,
				});
			}
		}

		// свободные водители — по своим реальным адресам; назначенного водителя сюда не добавляем,
		// чтобы на карте не было двух машин одного человека
		foreach (var driver in drivers)
		{
			if (driver.HasActiveTrip || driver.Point is null || driver.DriverId == assignedDriverId)
				continue;

			cars.Add(new MapCar
			{
				DriverId = driver.DriverId,
				Name = driver.Name,
				Vehicle = driver.Vehicle,
				Point = driver.Point.Value,
				IsAssigned = false,
			});
		}

		return cars;
	}
}
