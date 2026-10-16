using IhiksTaxi.Application;
using IhiksTaxi.Domain;

namespace IhiksTaxi.Application.Tests;

public class MapStateBuilderTests
{
	private static readonly GeoPoint Origin = new(57.00, 40.90);
	private static readonly GeoPoint Pickup = new(57.01, 40.95);
	private static readonly GeoPoint Destination = new(57.05, 41.00);

	[Test]
	public void Demo_position_follows_the_trip_status()
	{
		Assert.AreEqual(Origin,
			DemoCarPosition.Resolve(TripStatusNames.DriverAssigned, Origin, Pickup, Destination));
		Assert.AreEqual(Pickup,
			DemoCarPosition.Resolve(TripStatusNames.DriverArriving, Origin, Pickup, Destination));
		Assert.AreEqual(Pickup,
			DemoCarPosition.Resolve(TripStatusNames.PassengerPickedUp, Origin, Pickup, Destination));
		Assert.AreEqual(Pickup,
			DemoCarPosition.Resolve(TripStatusNames.InProgress, Origin, Pickup, Destination));
		Assert.AreEqual(Destination,
			DemoCarPosition.Resolve(TripStatusNames.Completed, Origin, Pickup, Destination));
	}

	[Test]
	public void Demo_position_is_absent_before_the_trip_and_after_cancellation()
	{
		Assert.IsNull(DemoCarPosition.Resolve(TripStatusNames.Created, Origin, Pickup, Destination));
		Assert.IsNull(DemoCarPosition.Resolve(TripStatusNames.SearchingDriver, Origin, Pickup, Destination));
		Assert.IsNull(DemoCarPosition.Resolve(TripStatusNames.Cancelled, Origin, Pickup, Destination));
	}

	[Test]
	public void Build_shows_only_point_a_without_point_b_and_without_a_route()
	{
		var state = MapStateBuilder.Build(
			Drivers(Free(1, Origin)),
			Pickup, "Откуда",
			null, null,
			null,
			null);

		Assert.AreEqual(Pickup, state.From);
		Assert.IsNull(state.To);
		Assert.IsNull(state.Geometry);
		Assert.AreEqual(1, state.Cars.Count);
	}

	[Test]
	public void Build_passes_the_road_route_when_both_points_are_known()
	{
		var estimate = new FareEstimate
		{
			DistanceKm = 5,
			DurationMin = 8,
			Cost = 200,
			Geometry = "{\"type\":\"LineString\",\"coordinates\":[]}",
		};

		var state = MapStateBuilder.Build(
			Array.Empty<DriverMapPoint>(),
			Pickup, "Откуда",
			Destination, "Куда",
			estimate,
			null);

		Assert.AreEqual(Pickup, state.From);
		Assert.AreEqual(Destination, state.To);
		Assert.AreEqual("{\"type\":\"LineString\",\"coordinates\":[]}", state.Geometry);
		Assert.IsFalse(state.IsApproximate);
	}

	[Test]
	public void Build_keeps_approximate_flag_without_inventing_a_route()
	{
		var estimate = new FareEstimate
		{
			DistanceKm = 3,
			DurationMin = 5,
			Cost = 100,
			IsApproximate = true,
			Geometry = null,
		};

		var state = MapStateBuilder.Build(
			Array.Empty<DriverMapPoint>(),
			Pickup, "Откуда",
			Destination, "Куда",
			estimate,
			null);

		Assert.IsNull(state.Geometry);
		Assert.IsTrue(state.IsApproximate);
	}

	[Test]
	public void Build_places_the_assigned_car_at_the_driver_location_right_after_assignment()
	{
		var state = MapStateBuilder.Build(
			Drivers(Assigned(1, Origin), Free(2, Destination)),
			Pickup, "Откуда",
			Destination, "Куда",
			null,
			Trip(TripStatusNames.DriverAssigned));

		var car = state.Cars.Single(c => c.IsAssigned);
		Assert.AreEqual(1, car.DriverId);
		Assert.AreEqual(Origin, car.Point);

		// назначенный водитель не появляется второй раз среди свободных
		Assert.IsFalse(state.Cars.Any(c => !c.IsAssigned && c.DriverId == 1));
		Assert.IsTrue(state.Cars.Any(c => !c.IsAssigned && c.DriverId == 2));
	}

	[Test]
	public void Build_moves_the_assigned_car_to_the_pickup_point_when_the_driver_arrives()
	{
		var state = MapStateBuilder.Build(
			Drivers(Assigned(1, Origin)),
			Pickup, "Откуда",
			Destination, "Куда",
			null,
			Trip(TripStatusNames.DriverArriving));

		Assert.AreEqual(Pickup, state.Cars.Single(c => c.IsAssigned).Point);
	}

	[Test]
	public void Build_keeps_the_car_in_place_during_the_ride()
	{
		var state = MapStateBuilder.Build(
			Drivers(Assigned(1, Origin)),
			Pickup, "Откуда",
			Destination, "Куда",
			null,
			Trip(TripStatusNames.InProgress));

		Assert.AreEqual(Pickup, state.Cars.Single(c => c.IsAssigned).Point);
	}

	[Test]
	public void Build_moves_the_assigned_car_to_the_destination_when_the_trip_finishes()
	{
		var state = MapStateBuilder.Build(
			Drivers(Assigned(1, Origin)),
			Pickup, "Откуда",
			Destination, "Куда",
			null,
			Trip(TripStatusNames.Completed));

		Assert.AreEqual(Destination, state.Cars.Single(c => c.IsAssigned).Point);
	}

	[Test]
	public void Build_never_changes_the_driver_coordinates_and_is_repeatable()
	{
		var drivers = Drivers(Assigned(1, Origin), Free(2, Destination));

		var first = MapStateBuilder.Build(drivers, Pickup, "Откуда", Destination, "Куда",
			null, Trip(TripStatusNames.PassengerPickedUp));
		var second = MapStateBuilder.Build(drivers, Pickup, "Откуда", Destination, "Куда",
			null, Trip(TripStatusNames.PassengerPickedUp));

		// постоянная координата водителя осталась прежней
		Assert.AreEqual(Origin, drivers[0].Point);

		Assert.AreEqual(first.Cars.Single(c => c.IsAssigned).Point,
			second.Cars.Single(c => c.IsAssigned).Point);
		Assert.AreEqual(first.Cars.Count, second.Cars.Count);
	}

	[Test]
	public void Build_skips_drivers_without_a_known_location()
	{
		var drivers = new[]
		{
			new DriverMapPoint { DriverId = 1, Name = "Без точки", Vehicle = "Car", Point = null, HasActiveTrip = false },
			Free(2, Origin),
		};

		var state = MapStateBuilder.Build(drivers, null, null, null, null, null, null);

		Assert.AreEqual(1, state.Cars.Count);
		Assert.AreEqual(2, state.Cars[0].DriverId);
	}

	[Test]
	public void Build_survives_missing_coordinates_without_throwing()
	{
		var state = MapStateBuilder.Build(
			Array.Empty<DriverMapPoint>(),
			null, null, null, null,
			null,
			Trip(TripStatusNames.DriverArriving));

		Assert.IsEmpty(state.Cars);
		Assert.IsFalse(state.IsApproximate);
		Assert.IsNull(state.From);
	}

	private static DriverMapPoint[] Drivers(params DriverMapPoint[] items) => items;

	private static DriverMapPoint Free(int id, GeoPoint point) => new()
	{
		DriverId = id,
		Name = $"Водитель {id}",
		Vehicle = "Лада Гранта · А000АА37",
		Point = point,
		HasActiveTrip = false,
	};

	private static DriverMapPoint Assigned(int id, GeoPoint point) => new()
	{
		DriverId = id,
		Name = $"Водитель {id}",
		Vehicle = "Лада Гранта · А000АА37",
		Point = point,
		HasActiveTrip = true,
	};

	private static TripInfo Trip(string status, int? driverId = 1) => new()
	{
		Id = 10,
		TariffId = 1,
		FromAddress = "Тестовая, 1",
		ToAddress = "Другая, 2",
		StatusName = status,
		DriverId = driverId,
		DriverName = driverId is null ? null : $"Водитель {driverId}",
		Vehicle = "Лада Гранта · А000АА37",
	};
}
