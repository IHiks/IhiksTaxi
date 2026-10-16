using IhiksTaxi.Application;

namespace IhiksTaxi.Application.Tests;

public class DriverSelectorTests
{
	[Test]
	public void Select_takes_the_free_driver_of_the_requested_tariff()
	{
		var candidates = new[]
		{
			new DriverCandidate { DriverId = 2, VehicleId = 2, TariffId = 2, HasActiveTrip = false },
			new DriverCandidate { DriverId = 1, VehicleId = 1, TariffId = 1, HasActiveTrip = false },
		};

		var chosen = DriverSelector.Select(candidates, tariffId: 1);

		Assert.IsNotNull(chosen);
		Assert.AreEqual(1, chosen!.DriverId);
	}

	[Test]
	public void Select_skips_drivers_that_are_already_busy()
	{
		var candidates = new[]
		{
			new DriverCandidate { DriverId = 1, VehicleId = 1, TariffId = 1, HasActiveTrip = true },
			new DriverCandidate { DriverId = 5, VehicleId = 5, TariffId = 1, HasActiveTrip = false },
		};

		var chosen = DriverSelector.Select(candidates, tariffId: 1);

		Assert.AreEqual(5, chosen!.DriverId);
	}

	[Test]
	public void Select_prefers_the_nearest_driver_when_the_pickup_point_is_known()
	{
		var pickup = new GeoPoint(57.0, 40.95);
		var candidates = new[]
		{
			new DriverCandidate
			{
				DriverId = 1, VehicleId = 1, TariffId = 1, HasActiveTrip = false,
				CurrentPoint = new GeoPoint(57.10, 41.10),
			},
			new DriverCandidate
			{
				DriverId = 2, VehicleId = 2, TariffId = 1, HasActiveTrip = false,
				CurrentPoint = new GeoPoint(57.00, 40.951),
			},
		};

		var chosen = DriverSelector.Select(candidates, tariffId: 1, from: pickup);

		Assert.AreEqual(2, chosen!.DriverId);
	}
}
