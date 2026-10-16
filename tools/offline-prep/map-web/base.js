/* Офлайн-подложка карты Иванова: Leaflet рисует локальные GeoJSON-слои.
   Ни одного внешнего запроса — все данные берутся рядом по относительным путям. */
(function () {
	"use strict";

	var center = [57.0, 40.97];

	var map = L.map("map", { preferCanvas: true, zoomControl: true });

	function fetchJson(url) {
		return fetch(url).then(function (response) {
			if (!response.ok) {
				throw new Error(url + " -> " + response.status);
			}
			return response.json();
		});
	}

	// отсутствие отдельного слоя не должно ломать всю карту
	function loadLayer(url, build) {
		return fetchJson(url).then(build).catch(function () { });
	}

	function roadStyle(highway, major) {
		var palette = {
			motorway: "#e892a2", trunk: "#e892a2", primary: "#f6c87a",
			secondary: "#f7e07a", tertiary: "#fdf6a8"
		};
		return {
			color: palette[highway] || (major ? "#ffffff" : "#cfcabf"),
			weight: major ? 3 : 1.4,
			opacity: 0.95
		};
	}

	var layers = [];

	layers.push(loadLayer("roads.geojson", function (geo) {
		L.geoJSON(geo, {
			style: function (feature) {
				return roadStyle(feature.properties.h, feature.properties.major);
			},
			onEachFeature: function (feature, layer) {
				if (feature.properties.n) {
					layer.bindTooltip(feature.properties.n, { sticky: true });
				}
			}
		}).addTo(map);
	}));

	layers.push(loadLayer("buildings.geojson", function (geo) {
		L.geoJSON(geo, {
			style: { color: "#b7afa2", weight: 0.5, fillColor: "#ddd6c9", fillOpacity: 0.9 }
		}).addTo(map);
	}));

	layers.push(loadLayer("places.geojson", function (geo) {
		var cities = L.layerGroup().addTo(map);
		L.geoJSON(geo, {
			pointToLayer: function (feature, latlng) {
				var kind = feature.properties.kind || "";
				var cls = ["city", "town", "village"].indexOf(kind) >= 0
					? "place-label"
					: "place-label district";
				return L.marker(latlng, {
					interactive: false,
					icon: L.divIcon({ className: "", html:
						'<span class="' + cls + '">' + feature.properties.name + "</span>",
						iconSize: [0, 0] })
				});
			}
		}).addTo(cities);
	}));

	var routeTask = window.__ROUTE__
		? Promise.resolve(window.__ROUTE__)
		: fetchJson("route.json").catch(function () { return {}; });

	function pin(text) {
		return L.divIcon({ className: "", html: '<div class="pin">' + text + "</div>",
			iconSize: [26, 26], iconAnchor: [13, 13] });
	}

	function drawRoute(data) {
		var a = data.from, b = data.to;
		if (!a || !b) {
			map.setView(center, 12);
			return;
		}

		L.marker(a, { icon: pin("\u0410") }).addTo(map).bindTooltip(data.fromName || "\u041e\u0442\u043a\u0443\u0434\u0430");
		L.marker(b, { icon: pin("\u0411") }).addTo(map).bindTooltip(data.toName || "\u041a\u0443\u0434\u0430");

		var routeLayer;
		if (data.geometry) {
			routeLayer = L.geoJSON(data.geometry, { style: { color: "#ff8000", weight: 5 } });
		} else {
			routeLayer = L.polyline([a, b], { color: "#ff8000", weight: 4, dashArray: "8 8" });
		}
		routeLayer.addTo(map);

		setTimeout(function () {
			map.invalidateSize();
			map.fitBounds(routeLayer.getBounds().pad(0.2));
		}, 200);
	}

	Promise.all([Promise.all(layers), routeTask]).then(function (results) {
		drawRoute(results[1]);
	}).catch(function (error) {
		document.getElementById("map").innerHTML =
			'<p style="padding:12px;font:13px sans-serif;color:#a00">' +
			"\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0437\u0430\u0433\u0440\u0443\u0437\u0438\u0442\u044c \u043b\u043e\u043a\u0430\u043b\u044c\u043d\u044b\u0435 \u0434\u0430\u043d\u043d\u044b\u0435 \u043a\u0430\u0440\u0442\u044b: " +
			error.message + "</p>";
	});
})();
