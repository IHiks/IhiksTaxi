using System.Text;
using System.Text.Json;
using IhiksTaxi.Application;

namespace IhiksTaxi.Infrastructure;

// адрес ищем в локальном индексе, снятом с OpenStreetMap: улица + дом,
// без обращения к сети; индекс читается один раз и держится в памяти
public sealed class LocalGeocoder : IGeocoder
{
	private const int MaxResults = 8;

	private readonly string _indexPath;
	private readonly Lazy<Index> _index;

	public LocalGeocoder(string indexPath)
	{
		_indexPath = indexPath;
		_index = new Lazy<Index>(Load);
	}

	public Task<IReadOnlyList<GeocodeResult>> SearchAsync(string street, string house, CancellationToken ct = default)
	{
		var index = _index.Value;
		var wanted = Key(street);
		if (wanted.Length == 0)
			return Task.FromResult<IReadOnlyList<GeocodeResult>>(Array.Empty<GeocodeResult>());

		var keys = MatchKeys(index, wanted);
		if (keys.Count == 0)
			return Task.FromResult<IReadOnlyList<GeocodeResult>>(Array.Empty<GeocodeResult>());

		var byHouse = FindHouses(index, keys, NormalizeHouse(house));
		if (byHouse.Count > 0)
			return Task.FromResult<IReadOnlyList<GeocodeResult>>(byHouse);

		return Task.FromResult<IReadOnlyList<GeocodeResult>>(StreetFallback(index, keys));
	}

	private static List<GeocodeResult> FindHouses(Index index, List<string> keys, string house)
	{
		var found = new List<(int Count, GeocodeResult Result)>();
		if (house.Length == 0)
			return new List<GeocodeResult>();

		foreach (var key in keys)
		{
			if (!index.Houses.TryGetValue(key, out var list))
				continue;

			foreach (var candidate in list)
			{
				if (candidate.Key != house)
					continue;

				found.Add((index.CountForStreet(candidate.Street), new GeocodeResult
				{
					DisplayName = $"{candidate.Street}, {candidate.House}",
					Point = candidate.Point,
					Exact = true,
				}));
			}
		}

		return found
			.OrderByDescending(item => item.Count)
			.Select(item => item.Result)
			.DistinctBy(result => result.DisplayName)
			.Take(MaxResults)
			.ToList();
	}

	private static List<GeocodeResult> StreetFallback(Index index, List<string> keys)
	{
		var streets = new List<StreetEntry>();
		foreach (var key in keys)
			streets.AddRange(index.Streets[key]);

		return streets
			.OrderByDescending(street => index.CountForStreet(street.Name))
			.DistinctBy(street => street.Name)
			.Take(MaxResults)
			.Select(street => new GeocodeResult
			{
				DisplayName = street.Name,
				Point = street.Point,
				Exact = false,
				FromStreetFallback = true,
			})
			.ToList();
	}

	private static List<string> MatchKeys(Index index, string wanted)
	{
		if (index.Streets.ContainsKey(wanted))
			return new List<string> { wanted };

		var matches = new List<string>();
		foreach (var key in index.Streets.Keys)
		{
			if (key.Contains(wanted, StringComparison.Ordinal) || wanted.Contains(key, StringComparison.Ordinal))
				matches.Add(key);
		}

		matches.Sort((a, b) => Math.Abs(a.Length - wanted.Length).CompareTo(Math.Abs(b.Length - wanted.Length)));
		return matches;
	}

	private Index Load()
	{
		var streets = new Dictionary<string, List<StreetEntry>>();
		var houses = new Dictionary<string, List<HouseEntry>>();
		var houseCount = new Dictionary<string, int>();

		if (!File.Exists(_indexPath))
			return new Index(streets, houses, houseCount);

		using var document = JsonDocument.Parse(File.ReadAllText(_indexPath));
		var root = document.RootElement;

		if (root.TryGetProperty("streets", out var streetsJson))
		{
			foreach (var element in streetsJson.EnumerateArray())
			{
				var name = element.GetProperty("n").GetString() ?? string.Empty;
				var key = Key(name);
				if (key.Length == 0)
					continue;

				var point = new GeoPoint(element.GetProperty("lat").GetDouble(), element.GetProperty("lon").GetDouble());
				if (!streets.TryGetValue(key, out var list))
				{
					list = new List<StreetEntry>();
					streets[key] = list;
				}

				list.Add(new StreetEntry(name, point));
			}
		}

		if (root.TryGetProperty("houses", out var housesJson))
		{
			foreach (var element in housesJson.EnumerateArray())
			{
				var street = element.GetProperty("s").GetString() ?? string.Empty;
				var key = Key(street);
				if (key.Length == 0)
					continue;

				var house = element.GetProperty("h").GetString() ?? string.Empty;
				var point = new GeoPoint(element.GetProperty("lat").GetDouble(), element.GetProperty("lon").GetDouble());

				if (!houses.TryGetValue(key, out var list))
				{
					list = new List<HouseEntry>();
					houses[key] = list;
				}

				list.Add(new HouseEntry(street, house, NormalizeHouse(house), point));
				houseCount[street] = houseCount.GetValueOrDefault(street) + 1;
			}
		}

		return new Index(streets, houses, houseCount);
	}

	// тип улицы (улица/проспект/переулок...) не важен для поиска, поэтому в ключе его нет:
	// «ул. Ленина» и «проспект Ленина» сходятся в «ленина», а лишний порядок слов убираем
	private static readonly HashSet<string> StreetTypes = new(StringComparer.Ordinal)
	{
		"ул", "улица", "пр", "пр-т", "просп", "проспект", "пер", "переулок",
		"ш", "шоссе", "б-р", "бул", "бульвар", "наб", "набережная",
		"пл", "площадь", "туп", "тупик", "аллея", "проезд", "кв-л", "квартал",
	};

	private static string Key(string value)
	{
		var builder = new StringBuilder();
		foreach (var ch in (value ?? string.Empty).ToLowerInvariant())
			builder.Append(char.IsLetterOrDigit(ch) || ch == '-' ? ch : ' ');

		var tokens = new List<string>();
		foreach (var token in builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
		{
			if (token == "иваново" || StreetTypes.Contains(token))
				continue;

			tokens.Add(token);
		}

		tokens.Sort(StringComparer.Ordinal);
		return string.Join(' ', tokens);
	}

	private static string NormalizeHouse(string value)
	{
		var builder = new StringBuilder();
		foreach (var ch in (value ?? string.Empty).ToLowerInvariant())
		{
			if (char.IsLetterOrDigit(ch))
				builder.Append(ch);
		}

		return builder.ToString();
	}

	private sealed class Index
	{
		private readonly IReadOnlyDictionary<string, int> _houseCount;

		public Index(
			Dictionary<string, List<StreetEntry>> streets,
			Dictionary<string, List<HouseEntry>> houses,
			IReadOnlyDictionary<string, int> houseCount)
		{
			Streets = streets;
			Houses = houses;
			_houseCount = houseCount;
		}

		public Dictionary<string, List<StreetEntry>> Streets { get; }
		public Dictionary<string, List<HouseEntry>> Houses { get; }

		public int CountForStreet(string street) => _houseCount.GetValueOrDefault(street);
	}

	private sealed record StreetEntry(string Name, GeoPoint Point);

	private sealed record HouseEntry(string Street, string House, string Key, GeoPoint Point);
}
