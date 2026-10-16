using System.Text.Json;
using IhiksTaxi.Application;
using Microsoft.Maui.Controls;

namespace IhiksTaxi.UI;

// карту показывает офлайн-страница из локальных данных. Страница загружается один раз,
// а её состояние (точки, маршрут, машины) обновляем через локальный сервер без перезагрузки.
public sealed class RouteMapPresenter
{
	private readonly MapServer _server;

	public RouteMapPresenter(MapServer server) => _server = server;

	public bool HasMap => _server.HasMapPage && _server.IndexUrl is not null;

	// адрес страницы карты; он не меняется, поэтому WebView не перезагружается
	public string? Source => HasMap ? _server.IndexUrl : null;

	// передаём карте новое состояние: точки А/Б, линию маршрута и машины
	public void Update(MapState state)
	{
		if (!_server.HasMapPage)
			return;

		JsonElement? geometry = null;
		if (!string.IsNullOrWhiteSpace(state.Geometry))
		{
			using var document = JsonDocument.Parse(state.Geometry);
			geometry = document.RootElement.Clone();
		}

		var payload = JsonSerializer.Serialize(new
		{
			from = ToArray(state.From),
			to = ToArray(state.To),
			fromName = state.FromName,
			toName = state.ToName,
			approximate = state.IsApproximate,
			geometry,
			status = state.StatusName,
			cars = state.Cars.Select(car => new
			{
				id = car.DriverId,
				name = car.Name,
				vehicle = car.Vehicle,
				lat = car.Point.Latitude,
				lng = car.Point.Longitude,
				assigned = car.IsAssigned,
			}).ToList(),
		});

		_server.SetRoute(payload);
	}

	private static double[]? ToArray(GeoPoint? point)
		=> point is null ? null : new[] { point.Value.Latitude, point.Value.Longitude };
}
