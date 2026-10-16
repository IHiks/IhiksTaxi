using IhiksTaxi.Application;

namespace IhiksTaxi.Application.Tests;

public class FareCalculatorTests
{
	private static TariffInfo Economy => new()
	{
		Id = 1,
		Name = "Эконом",
		BaseCost = 150m,
		RatePerKm = 20m,
		RatePerMinute = 8m,
		DemandFactorDefault = 1m,
	};

	private static TariffInfo Business => new()
	{
		Id = 3,
		Name = "Бизнес",
		BaseCost = 300m,
		RatePerKm = 35m,
		RatePerMinute = 15m,
		DemandFactorDefault = 1.3m,
	};

	[Test]
	public void Calculate_uses_base_distance_and_time()
	{
		var cost = FareCalculator.Calculate(Economy, 6.4m, 19);

		Assert.AreEqual(430m, cost);
	}

	[Test]
	public void Calculate_applies_tariff_demand_factor()
	{
		var cost = FareCalculator.Calculate(Business, 21.5m, 38);

		Assert.AreEqual(2109.25m, cost);
	}

	[TestCase(-1, 10)]
	[TestCase(10, -1)]
	public void Calculate_rejects_negative_inputs(decimal distance, int duration)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			FareCalculator.Calculate(Economy, distance, duration));
	}
}
