namespace IhiksTaxi.Application;

public static class DriverSelector
{
	// выбираем свободного водителя нужного тарифа; ближайшего считаем по прямой линии
	// между точками улиц, чтобы не делать запрос дорожному роутеру за каждого кандидата
	public static DriverCandidate? Select(IEnumerable<DriverCandidate> candidates, int tariffId, GeoPoint? from = null)
	{
		return candidates
			.Where(candidate => candidate.TariffId == tariffId && !candidate.HasActiveTrip)
			.OrderBy(candidate => Approach(candidate, from))
			.ThenBy(candidate => candidate.DriverId)
			.ThenBy(candidate => candidate.VehicleId)
			.FirstOrDefault();
	}

	private static decimal Approach(DriverCandidate candidate, GeoPoint? from)
	{
		if (from is null || candidate.CurrentPoint is null)
			return decimal.MaxValue / 2;

		return GeoDistance.Km(candidate.CurrentPoint.Value, from.Value);
	}
}
