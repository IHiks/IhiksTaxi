namespace IhiksTaxi.Application;

public interface IRouteService
{
	Task<RouteResult> GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken ct = default);
}
