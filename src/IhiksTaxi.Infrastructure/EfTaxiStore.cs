using IhiksTaxi.Application;
using IhiksTaxi.Domain;
using Microsoft.EntityFrameworkCore;

namespace IhiksTaxi.Infrastructure;

public sealed class EfTaxiStore : ITaxiStore
{
	private readonly IDbContextFactory<IhiksTaxiDbContext> _factory;

	public EfTaxiStore(IDbContextFactory<IhiksTaxiDbContext> factory)
	{
		_factory = factory;
	}

	public async Task<IReadOnlyList<TariffInfo>> GetTariffsAsync(CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		return await db.Tariffs
			.OrderBy(t => t.Id)
			.Select(t => new TariffInfo
			{
				Id = t.Id,
				Name = t.Name,
				BaseCost = t.BaseCost,
				RatePerKm = t.RatePerKm,
				RatePerMinute = t.RatePerMinute,
				DemandFactorDefault = t.DemandFactorDefault,
			})
			.ToListAsync(ct);
	}

	public async Task<TariffInfo?> GetTariffAsync(int tariffId, CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		return await db.Tariffs
			.Where(t => t.Id == tariffId)
			.Select(t => new TariffInfo
			{
				Id = t.Id,
				Name = t.Name,
				BaseCost = t.BaseCost,
				RatePerKm = t.RatePerKm,
				RatePerMinute = t.RatePerMinute,
				DemandFactorDefault = t.DemandFactorDefault,
			})
			.FirstOrDefaultAsync(ct);
	}

	public async Task<IReadOnlyList<StreetInfo>> GetStreetsAsync(CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		return await db.Streets
			.OrderBy(s => s.Name)
			.Select(s => new StreetInfo
			{
				Id = s.Id,
				Name = s.Name,
				Latitude = s.Latitude,
				Longitude = s.Longitude,
			})
			.ToListAsync(ct);
	}

	public async Task<bool> PhoneExistsAsync(string normalizedPhone, CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		return await db.Passengers.AnyAsync(p => p.PhoneNormalized == normalizedPhone, ct);
	}

	public async Task<PassengerAccount?> GetAccountByPhoneAsync(string normalizedPhone, CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		return await db.Passengers
			.Where(p => p.PhoneNormalized == normalizedPhone)
			.Select(p => new PassengerAccount
			{
				Id = p.Id,
				FullName = p.FullName,
				PhoneNormalized = p.PhoneNormalized!,
				PasswordHash = p.PasswordHash,
			})
			.FirstOrDefaultAsync(ct);
	}

	public async Task<int> CreatePassengerAsync(string fullName, string phone, string normalizedPhone,
		string passwordHash, CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		var entity = new Passenger
		{
			FullName = fullName,
			Phone = phone,
			PhoneNormalized = normalizedPhone,
			PasswordHash = passwordHash,
		};

		db.Passengers.Add(entity);
		await db.SaveChangesAsync(ct);

		return entity.Id;
	}

	public async Task<PassengerInfo?> GetPassengerAsync(int passengerId, CancellationToken ct = default)	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		var passenger = await db.Passengers.FirstOrDefaultAsync(p => p.Id == passengerId, ct);
		if (passenger is null)
			return null;

		var tripsCount = await db.Trips.CountAsync(t => t.PassengerId == passengerId, ct);

		return new PassengerInfo
		{
			Id = passenger.Id,
			FullName = passenger.FullName,
			Phone = passenger.Phone,
			Email = passenger.Email,
			TripsCount = tripsCount,
		};
	}

	public async Task<TripInfo?> GetTripAsync(int tripId, CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		var trip = await TripsWithDetails(db).FirstOrDefaultAsync(t => t.Id == tripId, ct);
		return trip is null ? null : ToTripInfo(trip);
	}

	public async Task<TripInfo?> GetActiveTripAsync(int passengerId, CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		var statusIds = await GetStatusIdsAsync(db, ct);

		// активной считаем незавершённую поездку; завершённую — пока её не оценили
		var trip = await TripsWithDetails(db)
			.Where(t => t.PassengerId == passengerId
				&& t.TripStatusId != statusIds[TripStatusNames.Cancelled]
				&& (t.TripStatusId != statusIds[TripStatusNames.Completed] || t.Rating == null))
			.OrderByDescending(t => t.Id)
			.FirstOrDefaultAsync(ct);

		return trip is null ? null : ToTripInfo(trip);
	}

	public async Task<IReadOnlyList<TripInfo>> GetHistoryAsync(int passengerId, CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		var statusIds = await GetStatusIdsAsync(db, ct);

		var trips = await TripsWithDetails(db)
			.Where(t => t.PassengerId == passengerId
				&& (t.TripStatusId == statusIds[TripStatusNames.Completed]
					|| t.TripStatusId == statusIds[TripStatusNames.Cancelled]))
			.OrderByDescending(t => t.CreatedAt)
			.ToListAsync(ct);

		return trips.Select(ToTripInfo).ToList();
	}

	public async Task<int> CreateTripAsync(NewTrip trip, CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		var statusIds = await GetStatusIdsAsync(db, ct);

		var entity = new Trip
		{
			PassengerId = trip.PassengerId,
			TariffId = trip.TariffId,
			FromAddress = trip.FromAddress,
			ToAddress = trip.ToAddress,
			DistanceKm = trip.DistanceKm,
			DurationMin = trip.DurationMin,
			Cost = trip.Cost,
			CreatedAt = DateTime.Now,
			TripStatusId = statusIds[TripStatusNames.Created],
		};

		db.Trips.Add(entity);
		await db.SaveChangesAsync(ct);

		return entity.Id;
	}

	public async Task<IReadOnlyList<DriverCandidate>> GetDriverCandidatesAsync(int tariffId,
		CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		var statusIds = await GetStatusIdsAsync(db, ct);
		var completed = statusIds[TripStatusNames.Completed];
		var cancelled = statusIds[TripStatusNames.Cancelled];

		var rows = await (
			from driver in db.Drivers
			from vehicle in driver.Vehicles
			where vehicle.TariffId == tariffId
			select new
			{
				DriverId = driver.Id,
				VehicleId = vehicle.Id,
				vehicle.TariffId,
				CurrentLatitude = driver.CurrentStreet == null ? (decimal?)null : driver.CurrentStreet.Latitude,
				CurrentLongitude = driver.CurrentStreet == null ? (decimal?)null : driver.CurrentStreet.Longitude,
				HasActiveTrip = db.Trips.Any(t => t.DriverId == driver.Id
					&& t.TripStatusId != completed
					&& t.TripStatusId != cancelled),
			}).ToListAsync(ct);

		return rows
			.Select(r => new DriverCandidate
			{
				DriverId = r.DriverId,
				VehicleId = r.VehicleId,
				TariffId = r.TariffId,
				HasActiveTrip = r.HasActiveTrip,
				CurrentPoint = r.CurrentLatitude is null || r.CurrentLongitude is null
					? null
					: new GeoPoint((double)r.CurrentLatitude.Value, (double)r.CurrentLongitude.Value),
			})
			.ToList();
	}

	public async Task<IReadOnlyList<DriverMapPoint>> GetDriverMapPointsAsync(CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		var statusIds = await GetStatusIdsAsync(db, ct);
		var completed = statusIds[TripStatusNames.Completed];
		var cancelled = statusIds[TripStatusNames.Cancelled];

		var rows = await (
			from driver in db.Drivers
			select new
			{
				DriverId = driver.Id,
				driver.FullName,
				Brand = driver.Vehicles.OrderBy(v => v.Id).Select(v => v.Brand).FirstOrDefault(),
				Model = driver.Vehicles.OrderBy(v => v.Id).Select(v => v.Model).FirstOrDefault(),
				Plate = driver.Vehicles.OrderBy(v => v.Id).Select(v => v.Plate).FirstOrDefault(),
				CurrentLatitude = driver.CurrentStreet == null ? (decimal?)null : driver.CurrentStreet.Latitude,
				CurrentLongitude = driver.CurrentStreet == null ? (decimal?)null : driver.CurrentStreet.Longitude,
				HasActiveTrip = db.Trips.Any(t => t.DriverId == driver.Id
					&& t.TripStatusId != completed
					&& t.TripStatusId != cancelled),
			}).ToListAsync(ct);

		return rows
			.Select(r => new DriverMapPoint
			{
				DriverId = r.DriverId,
				Name = r.FullName,
				Vehicle = r.Brand is null ? string.Empty : $"{r.Brand} {r.Model} · {r.Plate}",
				HasActiveTrip = r.HasActiveTrip,
				Point = r.CurrentLatitude is null || r.CurrentLongitude is null
					? null
					: new GeoPoint((double)r.CurrentLatitude.Value, (double)r.CurrentLongitude.Value),
			})
			.ToList();
	}

	public async Task<bool> TryAssignDriverAsync(int tripId, int driverId, CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		var statusIds = await GetStatusIdsAsync(db, ct);
		var assigned = statusIds[TripStatusNames.DriverAssigned];
		var completed = statusIds[TripStatusNames.Completed];
		var cancelled = statusIds[TripStatusNames.Cancelled];

		// одним запросом назначаем водителя, только если у него нет другой активной поездки
		var affected = await db.Database.ExecuteSqlInterpolatedAsync($@"
			UPDATE trip
			SET driver_id = {driverId}, trip_status_id = {assigned}
			WHERE id = {tripId}
			  AND NOT EXISTS (
				SELECT 1 FROM trip other
				WHERE other.driver_id = {driverId}
				  AND other.id <> {tripId}
				  AND other.trip_status_id NOT IN ({completed}, {cancelled}))", ct);

		return affected == 1;
	}

	public async Task SetDriverStreetAsync(int driverId, int streetId, CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == driverId, ct)
			?? throw new InvalidOperationException("Водитель не найден.");

		driver.CurrentStreetId = streetId;
		await db.SaveChangesAsync(ct);
	}

	public async Task SetStatusAsync(int tripId, string statusName, CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		var statusIds = await GetStatusIdsAsync(db, ct);
		if (!statusIds.TryGetValue(statusName, out var statusId))
			throw new InvalidOperationException($"Статус «{statusName}» не найден в справочнике.");

		var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == tripId, ct)
			?? throw new InvalidOperationException("Поездка не найдена.");

		trip.TripStatusId = statusId;
		await db.SaveChangesAsync(ct);
	}

	public async Task SetRatingAsync(int tripId, int rate, CancellationToken ct = default)
	{
		await using var db = await _factory.CreateDbContextAsync(ct);

		var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == tripId, ct)
			?? throw new InvalidOperationException("Поездка не найдена.");

		if (trip.DriverId is null)
			throw new InvalidOperationException("У поездки нет водителя, оценивать нечего.");

		var rating = await db.Ratings.FirstOrDefaultAsync(r => r.TripId == tripId, ct);
		if (rating is null)
			db.Ratings.Add(new Rating { TripId = tripId, DriverId = trip.DriverId.Value, Rate = rate });
		else
			rating.Rate = rate;

		await db.SaveChangesAsync(ct);
	}

	private static IQueryable<Trip> TripsWithDetails(IhiksTaxiDbContext db) =>
		db.Trips
			.Include(t => t.TripStatus)
			.Include(t => t.Rating)
			.Include(t => t.Driver!).ThenInclude(d => d.Vehicles);

	private static async Task<Dictionary<string, int>> GetStatusIdsAsync(IhiksTaxiDbContext db,
		CancellationToken ct) =>
		await db.TripStatuses.ToDictionaryAsync(s => s.Name, s => s.Id, ct);

	private static TripInfo ToTripInfo(Trip trip)
	{
		var vehicle = trip.Driver?.Vehicles.FirstOrDefault(v => v.TariffId == trip.TariffId)
			?? trip.Driver?.Vehicles.FirstOrDefault();

		return new TripInfo
		{
			Id = trip.Id,
			TariffId = trip.TariffId,
			FromAddress = trip.FromAddress,
			ToAddress = trip.ToAddress,
			DistanceKm = trip.DistanceKm,
			DurationMin = trip.DurationMin,
			Cost = trip.Cost,
			CreatedAt = trip.CreatedAt,
			StatusName = trip.TripStatus?.Name ?? string.Empty,
			DriverId = trip.DriverId,
			DriverName = trip.Driver?.FullName,
			DriverPhone = trip.Driver?.Phone,
			Vehicle = vehicle is null ? null : $"{vehicle.Brand} {vehicle.Model} · {vehicle.Plate}",
			Rating = trip.Rating?.Rate,
		};
	}
}
