using IhiksTaxi.Application;

namespace IhiksTaxi.Application.Tests;

// заглушка геокодера: возвращает заранее заданный результат, без сети
internal sealed class FakeGeocoder : IGeocoder
{
	private readonly IReadOnlyList<GeocodeResult> _results;

	public FakeGeocoder(IReadOnlyList<GeocodeResult> results) => _results = results;

	public Task<IReadOnlyList<GeocodeResult>> SearchAsync(string street, string house, CancellationToken ct = default)
		=> Task.FromResult(_results);
}
