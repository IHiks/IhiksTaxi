using IhiksTaxi.Application;

namespace IhiksTaxi.Application.Tests;

// заглушка геокодера: возвращает заранее заданный результат, без сети
internal sealed class FakeGeocoder : IGeocoder
{
	private readonly IReadOnlyList<GeocodeResult> _results;

	public FakeGeocoder(IReadOnlyList<GeocodeResult> results) => _results = results;

	public string? LastStreet { get; private set; }
	public string? LastHouse { get; private set; }

	public Task<IReadOnlyList<GeocodeResult>> SearchAsync(string street, string house, CancellationToken ct = default)
	{
		LastStreet = street;
		LastHouse = house;
		return Task.FromResult(_results);
	}
}
