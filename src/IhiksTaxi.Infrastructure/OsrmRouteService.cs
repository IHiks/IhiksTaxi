using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using IhiksTaxi.Application;

namespace IhiksTaxi.Infrastructure;

// расстояние и время поездки спрашиваем у дорожного роутера OSRM;
// если он недоступен, считаем по прямой — маршрут помечаем как приблизительный
public sealed class OsrmRouteService : IRouteService
{
	private const string OsrmBaseUrl = "https://router.project-osrm.org";

	private readonly HttpClient _http;

	public OsrmRouteService(HttpClient http)
	{
		_http = http;
	}

	public async Task<RouteResult> GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken ct = default)
	{
		try
		{
			var json = await _http.GetStringAsync(BuildUrl(from, to), ct);
			return Parse(json) ?? Approximate(from, to);
		}
		catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException
			&& !ct.IsCancellationRequested)
		{
			return Approximate(from, to);
		}
	}

	public static RouteResult? Parse(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return null;
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;

		if (root.GetProperty("code").GetString() != "Ok")
			return null;

		var routes = root.GetProperty("routes");
		if (routes.GetArrayLength() == 0)
			return null;

		var route = routes[0];
		var distanceKm = Math.Round((decimal)(route.GetProperty("distance").GetDouble() / 1000.0), 2, MidpointRounding.AwayFromZero);
		var durationMin = Math.Max(1, (int)Math.Ceiling(route.GetProperty("duration").GetDouble() / 60.0));

		return new RouteResult
		{
			DistanceKm = distanceKm,
			DurationMin = durationMin,
			Geometry = route.GetProperty("geometry").GetRawText(),
			IsRoadRoute = true,
		};
	}

	private static string BuildUrl(GeoPoint from, GeoPoint to)
	{
		var coordinates = string.Format(CultureInfo.InvariantCulture, "{0},{1};{2},{3}",
			from.Longitude, from.Latitude, to.Longitude, to.Latitude);

		return $"{OsrmBaseUrl}/route/v1/driving/{coordinates}?overview=full&geometries=geojson";
	}

	private static RouteResult Approximate(GeoPoint from, GeoPoint to)
	{
		var distance = GeoDistance.Km(from, to);

		return new RouteResult
		{
			DistanceKm = distance,
			DurationMin = GeoDistance.DurationMin(distance),
			Geometry = null,
			IsRoadRoute = false,
		};
	}
}