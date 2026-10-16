using IhiksTaxi.Domain;

namespace IhiksTaxi.Application;

public sealed class TripService
{
	private readonly ITaxiStore _store;
	private readonly IRouteService _routes;
	private readonly IGeocoder _geocoder;

	public TripService(ITaxiStore store, IRouteService routes, IGeocoder geocoder)
	{
		_store = store;
		_routes = routes;
		_geocoder = geocoder;
	}

	// ищем адрес с домом в геокодере; если сервис не нашёл — берём центр улицы из справочника
	public async Task<IReadOnlyList<GeocodeResult>> FindAddressesAsync(string street, string house,
		CancellationToken ct = default)
	{
		var trimmedStreet = (street ?? string.Empty).Trim();
		var trimmedHouse = (house ?? string.Empty).Trim();

		var found = await _geocoder.SearchAsync(trimmedStreet, trimmedHouse, ct);

		if (found.Count > 0)
			return found;

		var streets = await _store.GetStreetsAsync(ct);
		var center = streets.FirstOrDefault(s => NameMatches(s.Name, trimmedStreet))
			?? streets.FirstOrDefault(s => trimmedStreet.Length >= 4 && s.Name.Contains(trimmedStreet, StringComparison.OrdinalIgnoreCase));

		if (center is null)
			return Array.Empty<GeocodeResult>();

		return new[]
		{
			new GeocodeResult
			{
				DisplayName = ComposePlace(center.Name, trimmedHouse),
				Point = center.ToGeoPoint(),
				Exact = false,
				FromStreetFallback = true,
			},
		};
	}

	// расстояние и время считает дорожный роутер по координатам улиц, стоимость — по выбранному тарифу
	public async Task<FareEstimate> EstimateAsync(int fromStreetId, int toStreetId, int tariffId,
		CancellationToken ct = default)
	{
		var streets = await _store.GetStreetsAsync(ct);
		var from = FindStreet(streets, fromStreetId, "отправления").ToGeoPoint();
		var to = FindStreet(streets, toStreetId, "назначения").ToGeoPoint();

		return await EstimateAsync(from, to, tariffId, ct);
	}

	// тот же расчёт, но по конкретным точкам адреса с домом
	public async Task<FareEstimate> EstimateAsync(GeoPoint from, GeoPoint to, int tariffId,
		CancellationToken ct = default)
	{
		var route = await _routes.GetRouteAsync(from, to, ct);
		var tariff = await _store.GetTariffAsync(tariffId, ct)
			?? throw new InvalidOperationException("Тариф не найден.");
		var cost = FareCalculator.Calculate(tariff, route.DistanceKm, route.DurationMin);

		return new FareEstimate
		{
			DistanceKm = route.DistanceKm,
			DurationMin = route.DurationMin,
			Cost = cost,
			IsApproximate = !route.IsRoadRoute,
			Geometry = route.Geometry,
		};
	}

	public Task<IReadOnlyList<TariffInfo>> GetTariffsAsync(CancellationToken ct = default)
		=> _store.GetTariffsAsync(ct);

	public Task<IReadOnlyList<StreetInfo>> GetStreetsAsync(CancellationToken ct = default)
		=> _store.GetStreetsAsync(ct);

	public Task<PassengerInfo?> GetPassengerAsync(int passengerId, CancellationToken ct = default)
		=> _store.GetPassengerAsync(passengerId, ct);

	public Task<TripInfo?> GetActiveTripAsync(int passengerId, CancellationToken ct = default)
		=> _store.GetActiveTripAsync(passengerId, ct);

	public Task<IReadOnlyList<TripInfo>> GetHistoryAsync(int passengerId, CancellationToken ct = default)
		=> _store.GetHistoryAsync(passengerId, ct);

	public async Task<TripInfo> CreateTripAsync(int passengerId, int tariffId, int fromStreetId, string fromHouse,
		int toStreetId, string toHouse, CancellationToken ct = default)
	{
		var streets = await _store.GetStreetsAsync(ct);
		var fromStreet = FindStreet(streets, fromStreetId, "отправления");
		var toStreet = FindStreet(streets, toStreetId, "назначения");

		var route = await _routes.GetRouteAsync(fromStreet.ToGeoPoint(), toStreet.ToGeoPoint(), ct);

		var tariff = await _store.GetTariffAsync(tariffId, ct)
			?? throw new InvalidOperationException("Тариф не найден.");

		var cost = FareCalculator.Calculate(tariff, route.DistanceKm, route.DurationMin);

		var tripId = await _store.CreateTripAsync(new NewTrip
		{
			PassengerId = passengerId,
			TariffId = tariffId,
			FromAddress = ComposePlace(fromStreet.Name, fromHouse),
			ToAddress = ComposePlace(toStreet.Name, toHouse),
			DistanceKm = route.DistanceKm,
			DurationMin = route.DurationMin,
			Cost = cost,
		}, ct);

		var assigned = await AssignDriverAsync(tripId, tariffId, fromStreet.ToGeoPoint(), ct);
		await _store.SetStatusAsync(tripId, assigned
			? TripStatusNames.DriverAssigned
			: TripStatusNames.SearchingDriver, ct);

		return await GetRequiredTripAsync(tripId, ct);
	}

	// создание поездки по уже найденным точкам адреса (пункт А и пункт Б подтверждены пользователем)
	public async Task<TripInfo> CreateTripAsync(int passengerId, int tariffId,
		(string Name, string House, GeoPoint Point) fromAddress,
		(string Name, string House, GeoPoint Point) toAddress, CancellationToken ct = default)
	{
		var route = await _routes.GetRouteAsync(fromAddress.Point, toAddress.Point, ct);

		var tariff = await _store.GetTariffAsync(tariffId, ct)
			?? throw new InvalidOperationException("Тариф не найден.");

		var cost = FareCalculator.Calculate(tariff, route.DistanceKm, route.DurationMin);

		var tripId = await _store.CreateTripAsync(new NewTrip
		{
			PassengerId = passengerId,
			TariffId = tariffId,
			FromAddress = ComposePlace(fromAddress.Name, fromAddress.House),
			ToAddress = ComposePlace(toAddress.Name, toAddress.House),
			DistanceKm = route.DistanceKm,
			DurationMin = route.DurationMin,
			Cost = cost,
		}, ct);

		var assigned = await AssignDriverAsync(tripId, tariffId, fromAddress.Point, ct);
		await _store.SetStatusAsync(tripId, assigned
			? TripStatusNames.DriverAssigned
			: TripStatusNames.SearchingDriver, ct);

		return await GetRequiredTripAsync(tripId, ct);
	}

	public async Task<TripInfo> SearchDriverAsync(int tripId, CancellationToken ct = default)
	{
		var trip = await GetRequiredTripAsync(tripId, ct);

		if (trip.StatusName != TripStatusNames.SearchingDriver)
			throw new InvalidOperationException("Поиск водителя возможен только до его назначения.");

		var assigned = await AssignDriverAsync(tripId, trip.TariffId, null, ct);
		await _store.SetStatusAsync(tripId, assigned
			? TripStatusNames.DriverAssigned
			: TripStatusNames.SearchingDriver, ct);

		return await GetRequiredTripAsync(tripId, ct);
	}

	public async Task<TripInfo> AdvanceAsync(int tripId, CancellationToken ct = default)
	{
		var trip = await GetRequiredTripAsync(tripId, ct);

		var next = TripStatusRules.Next(trip.StatusName)
			?? throw new InvalidOperationException($"Поездку из состояния «{trip.StatusName}» дальше перевести нельзя.");

		await _store.SetStatusAsync(tripId, next, ct);
		return await GetRequiredTripAsync(tripId, ct);
	}

	public async Task<TripInfo> CancelAsync(int tripId, CancellationToken ct = default)
	{
		var trip = await GetRequiredTripAsync(tripId, ct);

		if (!TripStatusRules.CanCancel(trip.StatusName))
			throw new InvalidOperationException($"Поездку из состояния «{trip.StatusName}» отменить нельзя.");

		await _store.SetStatusAsync(tripId, TripStatusNames.Cancelled, ct);
		return await GetRequiredTripAsync(tripId, ct);
	}

	public async Task RateAsync(int tripId, int rate, CancellationToken ct = default)
	{
		if (rate is < 1 or > 5)
			throw new ArgumentOutOfRangeException(nameof(rate), "Оценка должна быть от 1 до 5.");

		var trip = await GetRequiredTripAsync(tripId, ct);

		if (trip.StatusName != TripStatusNames.Completed)
			throw new InvalidOperationException("Оценить можно только завершённую поездку.");

		await _store.SetRatingAsync(tripId, rate, ct);
	}

	private async Task<bool> AssignDriverAsync(int tripId, int tariffId, GeoPoint? from, CancellationToken ct)
	{
		var candidates = await _store.GetDriverCandidatesAsync(tariffId, ct);
		var remaining = candidates.ToList();

		while (true)
		{
			var candidate = DriverSelector.Select(remaining, tariffId, from);
			if (candidate is null)
				return false;

			if (await _store.TryAssignDriverAsync(tripId, candidate.DriverId, ct))
				return true;

			remaining.Remove(candidate);
		}
	}

	private async Task<TripInfo> GetRequiredTripAsync(int tripId, CancellationToken ct)
		=> await _store.GetTripAsync(tripId, ct)
			?? throw new InvalidOperationException("Поездка не найдена.");

	private static StreetInfo FindStreet(IReadOnlyList<StreetInfo> streets, int streetId, string role)
		=> streets.FirstOrDefault(s => s.Id == streetId)
			?? throw new InvalidOperationException($"Улица {role} не найдена.");

	// улицу из справочника находим без учёта типа и порядка слов: «проспект Ленина» == «Ленина проспект»
	private static bool NameMatches(string stored, string requested)
	{
		var a = Normalize(stored);
		var b = Normalize(requested);

		return a.Length > 0 && a == b || a.Length > 0 && b.Length > 0 && a.Contains(b, StringComparison.Ordinal);

		static string Normalize(string value)
		{
			var parts = (value ?? string.Empty)
				.ToLowerInvariant()
				.Split(new[] { ' ', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);

			Array.Sort(parts, StringComparer.Ordinal);
			return string.Join(" ", parts);
		}
	}

	private static string ComposePlace(string streetName, string? house)
	{
		var trimmed = (house ?? string.Empty).Trim();
		return trimmed.Length == 0 ? streetName : $"{streetName}, {trimmed}";
	}
}
