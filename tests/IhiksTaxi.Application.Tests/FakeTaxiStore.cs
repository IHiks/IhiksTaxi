using IhiksTaxi.Application;

namespace IhiksTaxi.Application.Tests;

// простая заглушка хранилища для проверки логики авторизации без базы данных
internal sealed class FakeTaxiStore : ITaxiStore
{
	private readonly List<(int Id, string FullName, string Phone, string Normalized, string Hash)> _passengers = new();

	public Task<bool> PhoneExistsAsync(string normalizedPhone, CancellationToken ct = default)
		=> Task.FromResult(_passengers.Any(p => p.Normalized == normalizedPhone));

	public Task<PassengerAccount?> GetAccountByPhoneAsync(string normalizedPhone, CancellationToken ct = default)
	{
		var found = _passengers.FirstOrDefault(p => p.Normalized == normalizedPhone);
		return Task.FromResult(found.Id == 0
			? null
			: new PassengerAccount
			{
				Id = found.Id,
				FullName = found.FullName,
				PhoneNormalized = found.Normalized,
				PasswordHash = found.Hash,
			});
	}

	public Task<int> CreatePassengerAsync(string fullName, string phone, string normalizedPhone, string passwordHash,
		CancellationToken ct = default)
	{
		var id = _passengers.Count + 1;
		_passengers.Add((id, fullName, phone, normalizedPhone, passwordHash));
		return Task.FromResult(id);
	}

	public Task<PassengerInfo?> GetPassengerAsync(int passengerId, CancellationToken ct = default)
	{
		var found = _passengers.FirstOrDefault(p => p.Id == passengerId);
		return Task.FromResult(found.Id == 0
			? null
			: new PassengerInfo
			{
				Id = found.Id,
				FullName = found.FullName,
				Phone = found.Phone,
				TripsCount = 0,
			});
	}

	public Task<IReadOnlyList<TariffInfo>> GetTariffsAsync(CancellationToken ct = default)
		=> Task.FromResult<IReadOnlyList<TariffInfo>>(Array.Empty<TariffInfo>());

	public Task<TariffInfo?> GetTariffAsync(int tariffId, CancellationToken ct = default)
		=> Task.FromResult<TariffInfo?>(null);

	public Task<IReadOnlyList<StreetInfo>> GetStreetsAsync(CancellationToken ct = default)
		=> Task.FromResult<IReadOnlyList<StreetInfo>>(Array.Empty<StreetInfo>());

	public Task<TripInfo?> GetTripAsync(int tripId, CancellationToken ct = default)
		=> Task.FromResult<TripInfo?>(null);

	public Task<TripInfo?> GetActiveTripAsync(int passengerId, CancellationToken ct = default)
		=> Task.FromResult<TripInfo?>(null);

	public Task<IReadOnlyList<TripInfo>> GetHistoryAsync(int passengerId, CancellationToken ct = default)
		=> Task.FromResult<IReadOnlyList<TripInfo>>(Array.Empty<TripInfo>());

	public Task<int> CreateTripAsync(NewTrip trip, CancellationToken ct = default)
		=> throw new NotSupportedException();

	public Task<IReadOnlyList<DriverCandidate>> GetDriverCandidatesAsync(int tariffId, CancellationToken ct = default)
		=> Task.FromResult<IReadOnlyList<DriverCandidate>>(Array.Empty<DriverCandidate>());

	public Task<IReadOnlyList<DriverMapPoint>> GetDriverMapPointsAsync(CancellationToken ct = default)
		=> Task.FromResult<IReadOnlyList<DriverMapPoint>>(Array.Empty<DriverMapPoint>());

	public Task<bool> TryAssignDriverAsync(int tripId, int driverId, CancellationToken ct = default)
		=> throw new NotSupportedException();

	public Task SetDriverStreetAsync(int driverId, int streetId, CancellationToken ct = default)
		=> throw new NotSupportedException();

	public Task SetStatusAsync(int tripId, string statusName, CancellationToken ct = default)
		=> throw new NotSupportedException();

	public Task SetRatingAsync(int tripId, int rate, CancellationToken ct = default)
		=> throw new NotSupportedException();
}
