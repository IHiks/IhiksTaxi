namespace IhiksTaxi.Application;

// поиск адреса по улице и, если указан, номеру дома
public interface IGeocoder
{
	Task<IReadOnlyList<GeocodeResult>> SearchAsync(string street, string house, CancellationToken ct = default);
}
