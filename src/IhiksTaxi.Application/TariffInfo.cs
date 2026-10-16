namespace IhiksTaxi.Application;

public sealed class TariffInfo
{
	public int Id { get; init; }
	public string Name { get; init; } = null!;
	public decimal BaseCost { get; init; }
	public decimal RatePerKm { get; init; }
	public decimal RatePerMinute { get; init; }
	public decimal DemandFactorDefault { get; init; }
}
