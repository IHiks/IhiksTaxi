using System.Globalization;
using System.Text;
using System.Text.Json;
using IhiksTaxi.Application;

namespace IhiksTaxi.Infrastructure;

// маршрут строим прямо в приложении по локальному графу дорог (данные OpenStreetMap),
// без внешнего роутера: алгоритм Дейкстры по времени проезда, с учётом односторонних улиц
public sealed class LocalRouteService : IRouteService
{
	// если ближайшая дорога дальше этого расстояния — точка вне зоны покрытия
	private const double MaxSnapMeters = 3000;

	private readonly string _graphPath;
	private readonly Lazy<Graph> _graph;

	public LocalRouteService(string graphPath)
	{
		_graphPath = graphPath;
		_graph = new Lazy<Graph>(() => Load(_graphPath));
	}

	public Task<RouteResult> GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken ct = default)
		=> Task.FromResult(Route(_graph.Value, from, to));

	private static RouteResult Route(Graph graph, GeoPoint from, GeoPoint to)
	{
		if (graph.NodeCount == 0)
			return Approximate(from, to);

		var start = NearestNode(graph, from);
		var goal = NearestNode(graph, to);

		if (start < 0 || goal < 0 || start == goal)
			return Approximate(from, to);

		if (DistanceMeters(from.Latitude, from.Longitude, graph.Lat[start], graph.Lon[start]) > MaxSnapMeters
			|| DistanceMeters(to.Latitude, to.Longitude, graph.Lat[goal], graph.Lon[goal]) > MaxSnapMeters)
			return Approximate(from, to);

		var seconds = new double[graph.NodeCount];
		var previousEdge = new int[graph.NodeCount];
		var visited = new bool[graph.NodeCount];
		Array.Fill(seconds, double.PositiveInfinity);
		Array.Fill(previousEdge, -1);

		var queue = new PriorityQueue<int, double>();
		seconds[start] = 0;
		queue.Enqueue(start, 0);

		while (queue.TryDequeue(out var node, out var cost))
		{
			if (visited[node])
				continue;

			visited[node] = true;
			if (node == goal)
				break;

			foreach (var edge in graph.Out[node])
			{
				var next = graph.DirectedTo[edge];
				if (visited[next])
					continue;

				var candidate = cost + graph.Seconds[edge];
				if (candidate < seconds[next])
				{
					seconds[next] = candidate;
					previousEdge[next] = edge;
					queue.Enqueue(next, candidate);
				}
			}
		}

		if (!visited[goal])
			return Approximate(from, to);

		var path = new List<int>();
		var meters = 0.0;
		var cursor = goal;
		while (cursor != start)
		{
			var edge = previousEdge[cursor];
			if (edge < 0)
				return Approximate(from, to);

			path.Add(cursor);
			meters += graph.Meters[edge];
			cursor = graph.DirectedFrom[edge];
		}

		path.Add(start);
		path.Reverse();

		var distanceKm = Math.Round((decimal)(meters / 1000.0), 2, MidpointRounding.AwayFromZero);
		var durationMin = Math.Max(1, (int)Math.Ceiling(seconds[goal] / 60.0));

		return new RouteResult
		{
			DistanceKm = distanceKm,
			DurationMin = durationMin,
			Geometry = BuildGeometry(graph, path, from, to),
			IsRoadRoute = true,
		};
	}

	private static string BuildGeometry(Graph graph, List<int> path, GeoPoint from, GeoPoint to)
	{
		var builder = new StringBuilder();
		builder.Append("{\"type\":\"LineString\",\"coordinates\":[");
		var first = true;

		void Add(double latitude, double longitude)
		{
			if (!first)
				builder.Append(',');
			first = false;
			builder.Append('[')
				.Append(longitude.ToString("0.000000", CultureInfo.InvariantCulture))
				.Append(',')
				.Append(latitude.ToString("0.000000", CultureInfo.InvariantCulture))
				.Append(']');
		}

		Add(from.Latitude, from.Longitude);
		foreach (var node in path)
			Add(graph.Lat[node], graph.Lon[node]);
		Add(to.Latitude, to.Longitude);

		builder.Append("]}");
		return builder.ToString();
	}

	private static int NearestNode(Graph graph, GeoPoint point)
	{
		var best = -1;
		var bestDistance = double.PositiveInfinity;
		var cosLat = Math.Cos(point.Latitude * Math.PI / 180.0);

		for (var i = 0; i < graph.NodeCount; i++)
		{
			var dLat = graph.Lat[i] - point.Latitude;
			var dLon = (graph.Lon[i] - point.Longitude) * cosLat;
			var distance = dLat * dLat + dLon * dLon;
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = i;
			}
		}

		return best;
	}

	private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
	{
		const double earthRadius = 6371000.0;
		var phi1 = lat1 * Math.PI / 180.0;
		var phi2 = lat2 * Math.PI / 180.0;
		var dPhi = (lat2 - lat1) * Math.PI / 180.0;
		var dLambda = (lon2 - lon1) * Math.PI / 180.0;

		var a = Math.Sin(dPhi / 2) * Math.Sin(dPhi / 2)
			+ Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(dLambda / 2) * Math.Sin(dLambda / 2);
		return 2 * earthRadius * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
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

	private static Graph Load(string graphPath)
	{
		if (!File.Exists(graphPath))
			return Graph.Empty;

		using var document = JsonDocument.Parse(File.ReadAllText(graphPath));
		var root = document.RootElement;

		var nodes = root.GetProperty("nodes");
		var nodeCount = nodes.GetArrayLength();
		var latitude = new double[nodeCount];
		var longitude = new double[nodeCount];
		var index = 0;
		foreach (var node in nodes.EnumerateArray())
		{
			latitude[index] = node[0].GetDouble();
			longitude[index] = node[1].GetDouble();
			index++;
		}

		var directedFrom = new List<int>();
		var directedTo = new List<int>();
		var meters = new List<double>();
		var seconds = new List<double>();

		foreach (var edge in root.GetProperty("edges").EnumerateArray())
		{
			var a = edge[0].GetInt32();
			var b = edge[1].GetInt32();
			var length = edge[2].GetDouble();
			var speed = edge[3].GetDouble();
			var direction = edge[4].GetInt32();

			var speedMps = (speed <= 0 ? 30.0 : speed) / 3.6;
			var time = length / speedMps;

			if (direction >= 0)
			{
				directedFrom.Add(a);
				directedTo.Add(b);
				meters.Add(length);
				seconds.Add(time);
			}

			if (direction <= 0)
			{
				directedFrom.Add(b);
				directedTo.Add(a);
				meters.Add(length);
				seconds.Add(time);
			}
		}

		var outEdges = new List<int>[nodeCount];
		for (var i = 0; i < nodeCount; i++)
			outEdges[i] = new List<int>();

		for (var e = 0; e < directedFrom.Count; e++)
			outEdges[directedFrom[e]].Add(e);

		return new Graph
		{
			Lat = latitude,
			Lon = longitude,
			NodeCount = nodeCount,
			DirectedFrom = directedFrom.ToArray(),
			DirectedTo = directedTo.ToArray(),
			Meters = meters.ToArray(),
			Seconds = seconds.ToArray(),
			Out = outEdges,
		};
	}

	private sealed class Graph
	{
		public static readonly Graph Empty = new()
		{
			Lat = Array.Empty<double>(),
			Lon = Array.Empty<double>(),
			NodeCount = 0,
			DirectedFrom = Array.Empty<int>(),
			DirectedTo = Array.Empty<int>(),
			Meters = Array.Empty<double>(),
			Seconds = Array.Empty<double>(),
			Out = Array.Empty<List<int>>(),
		};

		public required double[] Lat { get; init; }
		public required double[] Lon { get; init; }
		public required int NodeCount { get; init; }
		public required int[] DirectedFrom { get; init; }
		public required int[] DirectedTo { get; init; }
		public required double[] Meters { get; init; }
		public required double[] Seconds { get; init; }
		public required List<int>[] Out { get; init; }
	}
}
