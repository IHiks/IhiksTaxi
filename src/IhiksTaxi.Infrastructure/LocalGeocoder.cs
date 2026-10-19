using System.Text;
using System.Text.Json;
using IhiksTaxi.Application;

namespace IhiksTaxi.Infrastructure;

// адрес ищем в локальном индексе, снятом с OpenStreetMap: улица + дом,
// без обращения к сети; индекс читается один раз и держится в памяти.
// Рядом лежит справочник мест (ТЦ, ТРК, микрорайоны): сам тип объекта в адресе
// не хранится, но по нему можно искать — «трк Серебряный город» найдётся.
public sealed class LocalGeocoder : IGeocoder
{
	private const int MaxResults = 8;

	// для подбора «по куску» не берём совсем короткие запросы, иначе они цепляют пол-справочника
	private const int MinPartialKeyLength = 4;

	private readonly string _indexPath;
	private readonly string _placesPath;
	private readonly Lazy<Index> _index;

	public LocalGeocoder(string indexPath)
	{
		_indexPath = indexPath;
		_placesPath = Path.Combine(Path.GetDirectoryName(indexPath) ?? string.Empty, "places.json");
		_index = new Lazy<Index>(Load);
	}

	public Task<IReadOnlyList<GeocodeResult>> SearchAsync(string street, string house, CancellationToken ct = default)
	{
		var index = _index.Value;
		var wanted = Key(street);
		if (wanted.Length == 0)
			return Task.FromResult<IReadOnlyList<GeocodeResult>>(Array.Empty<GeocodeResult>());

		var keys = MatchKeys(index, wanted);

		// точный дом из адресного индекса важнее любого места из справочника
		if (keys.Count > 0)
		{
			var byHouse = FindHouses(index, keys, NormalizeHouse(house));
			if (byHouse.Count > 0)
				return Task.FromResult<IReadOnlyList<GeocodeResult>>(byHouse);
		}

		// иначе показываем сначала места из справочника, потом улицы
		var found = new List<GeocodeResult>();
		found.AddRange(FindPlaces(index, wanted, house));
		found.AddRange(StreetFallback(index, keys));

		return Task.FromResult<IReadOnlyList<GeocodeResult>>(
			found.DistinctBy(result => result.DisplayName).Take(MaxResults).ToList());
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

	// справочник мест: ТЦ, ТРК и прочие заметные точки города.
	// Сначала пробуем точное совпадение, потом — вхождение запроса в название.
	private static List<GeocodeResult> FindPlaces(Index index, string wanted, string house)
	{
		if (index.Places.TryGetValue(wanted, out var exact))
			return ComposePlaces(exact, house);

		if (wanted.Length < MinPartialKeyLength)
			return new List<GeocodeResult>();

		var partial = new List<PlaceEntry>();
		foreach (var (key, entries) in index.Places)
		{
			if (key.Contains(wanted, StringComparison.Ordinal) || wanted.Contains(key, StringComparison.Ordinal))
				partial.AddRange(entries);
		}

		return ComposePlaces(partial.Distinct().Take(MaxResults).ToList(), house);
	}

	private static List<GeocodeResult> ComposePlaces(IReadOnlyList<PlaceEntry> places, string house)
	{
		var trimmed = (house ?? string.Empty).Trim();

		return places
			.Select(place => new GeocodeResult
			{
				DisplayName = trimmed.Length == 0 ? place.Name : $"{place.Name}, {trimmed}",
				Point = place.Point,
				Exact = false,
				FromPlaceIndex = true,
			})
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
			return new Index(streets, houses, houseCount, LoadPlaces());

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

		return new Index(streets, houses, houseCount, LoadPlaces());
	}

	// справочник мест — необязательный файл: без него карта и адреса работают как раньше
	private Dictionary<string, List<PlaceEntry>> LoadPlaces()
	{
		var places = new Dictionary<string, List<PlaceEntry>>();
		if (!File.Exists(_placesPath))
			return places;

		using var document = JsonDocument.Parse(File.ReadAllText(_placesPath));
		if (!document.RootElement.TryGetProperty("places", out var placesJson))
			return places;

		foreach (var element in placesJson.EnumerateArray())
		{
			var name = element.GetProperty("n").GetString() ?? string.Empty;
			if (name.Length == 0
				|| !element.TryGetProperty("lat", out var lat)
				|| !element.TryGetProperty("lon", out var lon))
			{
				continue;
			}

			var entry = new PlaceEntry(name, new GeoPoint(lat.GetDouble(), lon.GetDouble()));
			AddPlace(places, Key(name), entry);

			// синонимы нужны там, где привычное название не совпадает с официальным
			if (element.TryGetProperty("a", out var aliases) && aliases.ValueKind == JsonValueKind.Array)
			{
				foreach (var alias in aliases.EnumerateArray())
					AddPlace(places, Key(alias.GetString() ?? string.Empty), entry);
			}
		}

		return places;
	}

	private static void AddPlace(Dictionary<string, List<PlaceEntry>> places, string key, PlaceEntry entry)
	{
		if (key.Length == 0)
			return;

		if (!places.TryGetValue(key, out var list))
		{
			list = new List<PlaceEntry>();
			places[key] = list;
		}

		if (!list.Contains(entry))
			list.Add(entry);
	}

	// тип улицы (улица/проспект/переулок...) не важен для поиска, поэтому в ключе его нет:
	// «ул. Ленина» и «проспект Ленина» сходятся в «ленина», а лишний порядок слов убираем.
	// Так же убираем тип места: «ТРК Тополь» и «Тополь» — это одно и то же.
	private static readonly HashSet<string> StreetTypes = new(StringComparer.Ordinal)
	{
		"ул", "улица", "пр", "пр-т", "просп", "проспект", "пер", "переулок",
		"ш", "шоссе", "б-р", "бул", "бульвар", "наб", "набережная",
		"пл", "площадь", "туп", "тупик", "аллея", "проезд", "кв-л", "квартал",
	};

	private static readonly HashSet<string> PlaceTypes = new(StringComparer.Ordinal)
	{
		"трк", "трц", "тц", "тк", "тд", "микрорайон", "мкр", "мкрн",
	};

	private static string Key(string value)
	{
		var builder = new StringBuilder();
		foreach (var ch in (value ?? string.Empty).ToLowerInvariant())
			builder.Append(char.IsLetterOrDigit(ch) || ch == '-' ? ch : ' ');

		var tokens = new List<string>();
		foreach (var token in builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
		{
			if (token == "иваново" || StreetTypes.Contains(token) || PlaceTypes.Contains(token))
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
			IReadOnlyDictionary<string, int> houseCount,
			Dictionary<string, List<PlaceEntry>> places)
		{
			Streets = streets;
			Houses = houses;
			_houseCount = houseCount;
			Places = places;
		}

		public Dictionary<string, List<StreetEntry>> Streets { get; }
		public Dictionary<string, List<HouseEntry>> Houses { get; }
		public Dictionary<string, List<PlaceEntry>> Places { get; }

		public int CountForStreet(string street) => _houseCount.GetValueOrDefault(street);
	}

	private sealed record StreetEntry(string Name, GeoPoint Point);

	private sealed record HouseEntry(string Street, string House, string Key, GeoPoint Point);

	private sealed record PlaceEntry(string Name, GeoPoint Point);
}
