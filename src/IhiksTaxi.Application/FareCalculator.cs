namespace IhiksTaxi.Application;

public static class FareCalculator
{
	public static decimal Calculate(TariffInfo tariff, decimal distanceKm, int durationMin)
	{
		if (distanceKm < 0)
			throw new ArgumentOutOfRangeException(nameof(distanceKm), "Расстояние не может быть отрицательным.");

		if (durationMin < 0)
			throw new ArgumentOutOfRangeException(nameof(durationMin), "Длительность не может быть отрицательной.");

		if (tariff.DemandFactorDefault <= 0)
			throw new ArgumentOutOfRangeException(nameof(tariff), "Коэффициент спроса должен быть положительным.");

		var cost = (tariff.BaseCost
			+ distanceKm * tariff.RatePerKm
			+ durationMin * tariff.RatePerMinute) * tariff.DemandFactorDefault;

		return Math.Round(cost, 2, MidpointRounding.AwayFromZero);
	}
}
