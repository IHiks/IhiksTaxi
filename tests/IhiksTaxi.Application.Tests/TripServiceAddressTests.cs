using IhiksTaxi.Application;

namespace IhiksTaxi.Application.Tests;

public class TripServiceAddressTests
{
	private static TripService CreateService(ITaxiStore store, IGeocoder geocoder)
		=> new(store, new StubRouteService(), geocoder);

	[Test]
	public async Task FindAddressesAsync_returns_geocoder_results_when_found()
	{
		var point = new GeoPoint(56.98, 40.97);
		var geocoder = new FakeGeocoder(new[]
		{
			new GeocodeResult { DisplayName = "улица Лежневская, 118, Иваново", Point = point, Exact = true },
		});
		var service = CreateService(new FakeTaxiStore(), geocoder);

		var results = await service.FindAddressesAsync("ул. Лежневская", "118");

		Assert.AreEqual(1, results.Count);
		Assert.AreEqual(point, results[0].Point);
		Assert.IsTrue(results[0].Exact);
	}

	[Test]
	public async Task FindAddressesAsync_returns_empty_when_nothing_matches()
	{
		var geocoder = new FakeGeocoder(Array.Empty<GeocodeResult>());
		var service = CreateService(new FakeTaxiStore(), geocoder);

		var results = await service.FindAddressesAsync("Неизвестная улица", "999");

		Assert.IsEmpty(results);
	}

	[Test]
	public async Task SearchAddressesAsync_passes_parsed_street_and_house_to_geocoder()
	{
		var geocoder = new FakeGeocoder(Array.Empty<GeocodeResult>());
		var service = CreateService(new FakeTaxiStore(), geocoder);

		await service.SearchAddressesAsync("ул. Лежневская, 118");

		Assert.AreEqual("ул. Лежневская", geocoder.LastStreet);
		Assert.AreEqual("118", geocoder.LastHouse);
	}

	[Test]
	public async Task SearchAddressesAsync_returns_empty_without_calling_geocoder_on_blank_query()
	{
		var geocoder = new FakeGeocoder(Array.Empty<GeocodeResult>());
		var service = CreateService(new FakeTaxiStore(), geocoder);

		var results = await service.SearchAddressesAsync("   ");

		Assert.IsEmpty(results);
		Assert.IsNull(geocoder.LastStreet);
	}

	private sealed class StubRouteService : IRouteService
	{
		public Task<RouteResult> GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken ct = default)
			=> Task.FromResult(new RouteResult
			{
				DistanceKm = 5m,
				DurationMin = 10,
				Geometry = "{\"type\":\"LineString\",\"coordinates\":[]}",
				IsRoadRoute = true,
			});
	}
}
