using System.Text.Json;
using IhiksTaxi.Application;
using Microsoft.Maui.Controls;

namespace IhiksTaxi.UI;

// карту показывает офлайн-страница из локальных данных; маршрут передаём ей
// через локальный сервер и каждый раз перезагружаем со свежим номером версии
public sealed class RouteMapPresenter
{
	private readonly MapServer _server;
	private int _revision;

	public RouteMapPresenter(MapServer server) => _server = server;

	public WebViewSource Create(FareEstimate estimate, GeoPoint from, string fromName,
		GeoPoint to, string toName)
	{
		if (!_server.HasMapPage || _server.IndexUrl is null)
			return new HtmlWebViewSource { Html = MissingDataHtml };

		JsonElement? geometry = null;
		if (!string.IsNullOrWhiteSpace(estimate.Geometry))
		{
			using var document = JsonDocument.Parse(estimate.Geometry);
			geometry = document.RootElement.Clone();
		}

		var payload = JsonSerializer.Serialize(new
		{
			from = new[] { from.Latitude, from.Longitude },
			to = new[] { to.Latitude, to.Longitude },
			fromName,
			toName,
			approximate = estimate.IsApproximate,
			geometry,
		});

		_server.SetRoute(payload);
		return new UrlWebViewSource { Url = $"{_server.IndexUrl}?r={++_revision}" };
	}

	private const string MissingDataHtml = """
		<!DOCTYPE html>
		<html>
		<head><meta charset="utf-8" /></head>
		<body style="margin:0;font:13px sans-serif;color:#a00;padding:12px">
		Локальные данные карты не найдены. Запустите tools/offline-prep/prepare_offline_data.py
		при наличии интернета.
		</body>
		</html>
		""";
}
