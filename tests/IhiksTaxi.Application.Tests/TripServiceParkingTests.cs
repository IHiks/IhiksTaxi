using IhiksTaxi.Application;
using IhiksTaxi.Domain;

namespace IhiksTaxi.Application.Tests;

// после завершения поездки водитель должен остаться на улице точки назначения
public class TripServiceParkingTests
{
	private static readonly GeoPoint DestinationNearSovetskaya = new(57.05, 41.00);
	private static readonly GeoPoint DestinationNearLenina = new(57.001, 40.901);

	[Test]
	public async Task Completing_a_trip_parks_the_driver_at_the_destination_street()
	{
		var store = new RecordingStore
		{
			Trip = Trip(TripStatusNames.InProgress, driverId: 7, to: "ул. Советская, 10"),
			Streets =
			{
				new StreetInfo { Id = 1, Name = "Ленина", Latitude = 57.00m, Longitude = 40.90m },
				new StreetInfo { Id = 2, Name = "Советская", Latitude = 57.05m, Longitude = 41.00m },
			},
		};

		var service = new TripService(store, new StubRouteService(),
			new FakeGeocoder(new[] { Result(DestinationNearSovetskaya) }));

		await service.AdvanceAsync(1);

		Assert.AreEqual(TripStatusNames.Completed, store.Trip!.StatusName);
		Assert.AreEqual(7, store.DriverId);
		Assert.AreEqual(2, store.StreetId);
	}

	[Test]
	public async Task Parking_picks_the_street_closest_to_the_destination()
	{
		var store = new RecordingStore
		{
			Trip = Trip(TripStatusNames.InProgress, driverId: 3, to: "ул. Ленина, 1"),
			Streets =
			{
				new StreetInfo { Id = 1, Name = "Ленина", Latitude = 57.00m, Longitude = 40.90m },
				new StreetInfo { Id = 2, Name = "Советская", Latitude = 57.05m, Longitude = 41.00m },
			},
		};

		var service = new TripService(store, new StubRouteService(),
			new FakeGeocoder(new[] { Result(DestinationNearLenina) }));

		await service.AdvanceAsync(1);

		Assert.AreEqual(1, store.StreetId);
	}

	[Test]
	public async Task The_driver_is_not_moved_until_the_trip_completes()
	{
		var store = new RecordingStore
		{
			Trip = Trip(TripStatusNames.DriverAssigned, driverId: 7, to: "ул. Советская, 10"),
			Streets = { new StreetInfo { Id = 2, Name = "Советская", Latitude = 57.05m, Longitude = 41.00m } },
		};

		var service = new TripService(store, new StubRouteService(),
			new FakeGeocoder(new[] { Result(DestinationNearSovetskaya) }));

		await service.AdvanceAsync(1);

		Assert.AreEqual(TripStatusNames.DriverArriving, store.Trip!.StatusName);
		Assert.IsNull(store.StreetId);
	}

	[Test]
	public async Task Completing_a_trip_without_a_driver_moves_nobody()
	{
		var store = new RecordingStore
		{
			Trip = Trip(TripStatusNames.InProgress, driverId: null, to: "ул. Советская, 10"),
			Streets = { new StreetInfo { Id = 2, Name = "Советская", Latitude = 57.05m, Longitude = 41.00m } },
		};

		var service = new TripService(store, new StubRouteService(),
			new FakeGeocoder(new[] { Result(DestinationNearSovetskaya) }));

		await service.AdvanceAsync(1);

		Assert.AreEqual(TripStatusNames.Completed, store.Trip!.StatusName);
		Assert.IsNull(store.StreetId);
	}

	[Test]
	public async Task Parking_is_skipped_when_the_destination_cannot_be_resolved()
	{
		var store = new RecordingStore
		{
			Trip = Trip(TripStatusNames.InProgress, driverId: 7, to: "Неизвестная улица, 1"),
			Streets = { new StreetInfo { Id = 2, Name = "Советская", Latitude = 57.05m, Longitude = 41.00m } },
		};

		var service = new TripService(store, new StubRouteService(),
			new FakeGeocoder(Array.Empty<GeocodeResult>()));

		await service.AdvanceAsync(1);

		Assert.IsNull(store.StreetId);
	}

	private static TripInfo Trip(string status, int? driverId, string to) => new()
	{
		Id = 1,
		TariffId = 1,
		FromAddress = "ул. Ленина, 1",
		ToAddress = to,
		DistanceKm = 4,
		Cost = 150,
		CreatedAt = DateTime.Now,
		StatusName = status,
		DriverId = driverId,
	};

	private static GeocodeResult Result(GeoPoint point) => new()
	{
		DisplayName = "точка назначения",
		Point = point,
		Exact = true,
	};

	private sealed class RecordingStore : ITaxiStore
	{
		public TripInfo? Trip { get; set; }
		public List<StreetInfo> Streets { get; } = new();
		public int? DriverId { get; private set; }
		public int? StreetId { get; private set; }

		public Task<IReadOnlyList<StreetInfo>> GetStreetsAsync(CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<StreetInfo>>(Streets);

		public Task<TripInfo?> GetTripAsync(int tripId, CancellationToken ct = default)
			=> Task.FromResult(Trip);

		public Task SetStatusAsync(int tripId, string statusName, CancellationToken ct = default)
		{
			if (Trip is not null)
				Trip = new TripInfo
				{
					Id = Trip.Id,
					TariffId = Trip.TariffId,
					FromAddress = Trip.FromAddress,
					ToAddress = Trip.ToAddress,
					DistanceKm = Trip.DistanceKm,
					DurationMin = Trip.DurationMin,
					Cost = Trip.Cost,
					CreatedAt = Trip.CreatedAt,
					StatusName = statusName,
					DriverId = Trip.DriverId,
					DriverName = Trip.DriverName,
					Vehicle = Trip.Vehicle,
					Rating = Trip.Rating,
				};

			return Task.CompletedTask;
		}

		public Task SetDriverStreetAsync(int driverId, int streetId, CancellationToken ct = default)
		{
			DriverId = driverId;
			StreetId = streetId;
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<TariffInfo>> GetTariffsAsync(CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<TariffInfo>>(Array.Empty<TariffInfo>());

		public Task<TariffInfo?> GetTariffAsync(int tariffId, CancellationToken ct = default)
			=> Task.FromResult<TariffInfo?>(null);

		public Task<PassengerInfo?> GetPassengerAsync(int passengerId, CancellationToken ct = default)
			=> Task.FromResult<PassengerInfo?>(null);

		public Task<bool> PhoneExistsAsync(string normalizedPhone, CancellationToken ct = default)
			=> Task.FromResult(false);

		public Task<PassengerAccount?> GetAccountByPhoneAsync(string normalizedPhone, CancellationToken ct = default)
			=> Task.FromResult<PassengerAccount?>(null);

		public Task<int> CreatePassengerAsync(string fullName, string phone, string normalizedPhone, string passwordHash,
			CancellationToken ct = default)
			=> throw new NotSupportedException();

		public Task<TripInfo?> GetActiveTripAsync(int passengerId, CancellationToken ct = default)
			=> Task.FromResult<TripInfo?>(null);

		public Task<IReadOnlyList<TripInfo>> GetHistoryAsync(int passengerId, CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<TripInfo>>(Array.Empty<TripInfo>());

		public Task<int> CreateTripAsync(NewTrip trip, CancellationToken ct = default)
			=> throw new NotSupportedException();

		public Task<IReadOnlyList<DriverCandidate>> GetDriverCandidatesAsync(int tariffId,
			CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<DriverCandidate>>(Array.Empty<DriverCandidate>());

		public Task<IReadOnlyList<DriverMapPoint>> GetDriverMapPointsAsync(CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<DriverMapPoint>>(Array.Empty<DriverMapPoint>());

		public Task<bool> TryAssignDriverAsync(int tripId, int driverId, CancellationToken ct = default)
			=> throw new NotSupportedException();

		public Task SetRatingAsync(int tripId, int rate, CancellationToken ct = default)
			=> throw new NotSupportedException();
	}

	private sealed class StubRouteService : IRouteService
	{
		public Task<RouteResult> GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken ct = default)
			=> Task.FromResult(new RouteResult
			{
				DistanceKm = 4m,
				DurationMin = 10,
				Geometry = null,
				IsRoadRoute = false,
			});
	}
}
