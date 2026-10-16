namespace IhiksTaxi.Application;

public interface ITaxiStore
{
	Task<IReadOnlyList<TariffInfo>> GetTariffsAsync(CancellationToken ct = default);

	Task<TariffInfo?> GetTariffAsync(int tariffId, CancellationToken ct = default);

	Task<IReadOnlyList<StreetInfo>> GetStreetsAsync(CancellationToken ct = default);

	Task<PassengerInfo?> GetPassengerAsync(int passengerId, CancellationToken ct = default);

	Task<bool> PhoneExistsAsync(string normalizedPhone, CancellationToken ct = default);

	Task<PassengerAccount?> GetAccountByPhoneAsync(string normalizedPhone, CancellationToken ct = default);

	Task<int> CreatePassengerAsync(string fullName, string phone, string normalizedPhone, string passwordHash,
		CancellationToken ct = default);

	Task<TripInfo?> GetTripAsync(int tripId, CancellationToken ct = default);

	Task<TripInfo?> GetActiveTripAsync(int passengerId, CancellationToken ct = default);

	Task<IReadOnlyList<TripInfo>> GetHistoryAsync(int passengerId, CancellationToken ct = default);

	Task<int> CreateTripAsync(NewTrip trip, CancellationToken ct = default);

	Task<IReadOnlyList<DriverCandidate>> GetDriverCandidatesAsync(int tariffId, CancellationToken ct = default);

	// водители с их текущим местоположением (улицей) для отображения машин на карте
	Task<IReadOnlyList<DriverMapPoint>> GetDriverMapPointsAsync(CancellationToken ct = default);

	// назначает водителя только если у него нет активной поездки; false — водителя уже заняли
	Task<bool> TryAssignDriverAsync(int tripId, int driverId, CancellationToken ct = default);

	// сохраняет новое положение водителя (улицу), например после завершения поездки
	Task SetDriverStreetAsync(int driverId, int streetId, CancellationToken ct = default);

	Task SetStatusAsync(int tripId, string statusName, CancellationToken ct = default);

	Task SetRatingAsync(int tripId, int rate, CancellationToken ct = default);
}
