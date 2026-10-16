using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using IhiksTaxi.Application;

namespace IhiksTaxi.Infrastructure;

// адрес ищем у Photon (Komoot) — публичный сервис геокодирования на данных OpenStreetMap;
// без ключа, возвращает координаты домов Иванова; лимитируем частоту и кэшируем результаты
public sealed class PhotonGeocoder : IGeocoder
{
	private const string PhotonBaseUrl = "https://photon.komoot.io";
	private const int MinDelayMs = 1100;

	private static readonly ConcurrentDictionary<string, IReadOnlyList<GeocodeResult>> Cache = new();
	private static readonly SemaphoreSlim Throttle = new(1, 1);
	private static DateTimeOffset _lastCall = DateTimeOffset.MinValue;

	private readonly HttpClient _http;

	public PhotonGeocoder(HttpClient http)
	{
		_http = http;
	}

	public async Task<IReadOnlyList<GeocodeResult>> SearchAsync(string street, string house, CancellationToken ct = default)
	{
		var key = BuildQuery(street, house);
		if (key.Length == 0)
			return Array.Empty<GeocodeResult>();

		if (Cache.TryGetValue(key, out var cached))
			return cached;

		await Throttle.WaitAsync(ct);
		try
		{
			if (Cache.TryGetValue(key, out cached))
				return cached;

			var wait = _lastCall.AddMilliseconds(MinDelayMs) - DateTimeOffset.UtcNow;
			if (wait > TimeSpan.Zero)
				await Task.Delay(wait, ct);

			var results = await QueryAsync(key, ct);
			_lastCall = DateTimeOffset.UtcNow;
			Cache[key] = results;
			return results;
		}
		finally
		{
			Throttle.Release();
		}
	}

	private async Task<IReadOnlyList<GeocodeResult>> QueryAsync(string query, CancellationToken ct)
	{
		try
		{
			var url = $"{PhotonBaseUrl}/api/?q={Uri.EscapeDataString(query)}&limit=5";
			var json = await _http.GetStringAsync(url, ct);
			return Parse(json);
		}
		catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException
			&& !ct.IsCancellationRequested)
		{
			return Array.Empty<GeocodeResult>();
		}
	}

	public static IReadOnlyList<GeocodeResult> Parse(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return Array.Empty<GeocodeResult>();

		using var document = JsonDocument.Parse(json);
		var features = document.RootElement.GetProperty("features");

		var results = new List<GeocodeResult>();
		foreach (var feature in features.EnumerateArray())
		{
			var geometry = feature.GetProperty("geometry");
			var coords = geometry.GetProperty("coordinates");
			var lon = coords[0].GetDouble();
			var lat = coords[1].GetDouble();

			var props = feature.GetProperty("properties");
			var display = BuildDisplayName(props);
			if (display.Length == 0)
				continue;

			results.Add(new GeocodeResult
			{
				DisplayName = display,
				Point = new GeoPoint(lat, lon),
				Exact = HasHouseNumber(props),
			});
		}

		return results;
	}

	private static string BuildQuery(string street, string house)
	{
		var trimmedStreet = (street ?? string.Empty).Trim();
		if (trimmedStreet.Length == 0)
			return string.Empty;

		var trimmedHouse = (house ?? string.Empty).Trim();
		return trimmedHouse.Length == 0
			? $"{trimmedStreet}, Иваново"
			: $"{trimmedStreet}, {trimmedHouse}, Иваново";
	}

	private static bool HasHouseNumber(JsonElement props)
		=> props.TryGetProperty("housenumber", out var hn)
			&& !string.IsNullOrWhiteSpace(hn.GetString());

	private static string BuildDisplayName(JsonElement props)
	{
		var street = Get(props, "street");
		var house = Get(props, "housenumber");
		var city = Get(props, "city");
		var name = Get(props, "name");

		var streetPart = string.IsNullOrWhiteSpace(street) ? name : street;
		if (!string.IsNullOrWhiteSpace(house))
			streetPart = $"{streetPart}, {house}";

		var parts = new List<string>();
		if (streetPart.Length > 0)
			parts.Add(streetPart);
		if (city.Length > 0)
			parts.Add(city);

		return string.Join(", ", parts);
	}

	private static string Get(JsonElement props, string name)
		=> props.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;
}
