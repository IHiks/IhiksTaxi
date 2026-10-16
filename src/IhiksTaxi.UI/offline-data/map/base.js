/* Офлайн-подложка карты Иванова: Leaflet рисует локальные GeoJSON-слои.
   Ни одного внешнего запроса — все данные берутся рядом по относительным путям.
   Состояние заказа (точки А/Б, маршрут, машины) страница сама опрашивает у локального
   сервера, поэтому при смене статуса поездки карта не перезагружается и не сбрасывается. */
(function () {
	"use strict";

	var center = [57.0, 40.97];
	var POLL_MS = 1000;
	var DETAIL_ROADS_MIN_ZOOM = 14;

	var map = L.map("map", { preferCanvas: true, zoomControl: true });
	map.setView(center, 12);
	var roadRenderer = L.canvas({ padding: 0.5 });

	// здания кладём в отдельную панель под дорогами,
	// иначе их заливка перекрывает улицы при крупном масштабе
	map.createPane("buildings");
	map.getPane("buildings").style.zIndex = 350;
	var buildingRenderer = L.canvas({ pane: "buildings" });

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
			motorway: "#243b53", trunk: "#243b53", primary: "#334e68",
			secondary: "#486581", tertiary: "#627d98"
		};
		return {
			color: palette[highway] || (major ? "#486581" : "#9fb3c8"),
			weight: major ? 3 : 1.2,
			opacity: major ? 0.88 : 0.68
		};
	}

	function buildRoadLayer(geo, forceMajor) {
		return L.geoJSON(geo, {
			renderer: roadRenderer,
			style: function (feature) {
				var major = forceMajor || feature.properties.major === true;
				return roadStyle(feature.properties.h, major);
			},
			onEachFeature: function (feature, layer) {
				if (forceMajor && feature.properties.n) {
					layer.bindTooltip(feature.properties.n, { sticky: true });
				}
			}
		}).addTo(map);
	}

	// подписи населённых пунктов показываем не сразу: чем крупнее объект,
	// тем при меньшем масштабе он появляется, чтобы карта не была забита именами
	var PLACE_MIN_ZOOM = {
		city: 9, town: 11, quarter: 12, neighbourhood: 12, suburb: 12,
		village: 13, hamlet: 14, locality: 14
	};
	var PLACE_FALLBACK_ZOOM = 14;
	var BUILDINGS_MIN_ZOOM = 16;

	var layers = [];
	var placeMarkers = [];
	var roadsLayer = null;
	var detailRoadsLayer = null;
	var detailRoadsTask = null;

	layers.push(fetchJson("roads-major.geojson").catch(function () {
		return fetchJson("roads.geojson");
	}).then(function (geo) {
		roadsLayer = buildRoadLayer(geo, false);
	}).catch(function () { }));

	layers.push(loadLayer("places.geojson", function (geo) {
		var group = L.layerGroup().addTo(map);
		L.geoJSON(geo, {
			pointToLayer: function (feature, latlng) {
				var kind = feature.properties.kind || "";
				var district = ["city", "town", "village"].indexOf(kind) < 0;
				var minZoom = PLACE_MIN_ZOOM.hasOwnProperty(kind)
					? PLACE_MIN_ZOOM[kind]
					: PLACE_FALLBACK_ZOOM;
				var marker = L.marker(latlng, {
					interactive: false,
					icon: L.divIcon({ className: "place-marker", html:
						'<span class="' + (district ? "place-label district" : "place-label") + '">' +
						feature.properties.name + "</span>",
						iconSize: [0, 0] })
				});
				placeMarkers.push({ marker: marker, minZoom: minZoom });
				return marker;
			}
		}).addTo(group);
	}));

	var buildingsLayer = null;
	var buildingsTask = null;

	function updateLabels() {
		var zoom = map.getZoom();
		for (var i = 0; i < placeMarkers.length; i++) {
			var element = placeMarkers[i].marker.getElement();
			if (element) {
				element.classList.toggle("place-hidden", zoom < placeMarkers[i].minZoom);
			}
		}
	}

	function updateBuildings() {
		if (map.getZoom() < BUILDINGS_MIN_ZOOM) {
			if (buildingsLayer && map.hasLayer(buildingsLayer)) {
				map.removeLayer(buildingsLayer);
			}
			return;
		}
		if (buildingsLayer) {
			if (!map.hasLayer(buildingsLayer)) {
				buildingsLayer.addTo(map);
			}
			return;
		}
		if (!buildingsTask) {
			// слой зданий тяжёлый, поэтому читаем его только когда он реально нужен
			buildingsTask = loadLayer("buildings.geojson", function (geo) {
				if (!geo || !geo.features || !geo.features.length) {
					return;
				}
				buildingsLayer = L.geoJSON(geo, {
					renderer: buildingRenderer,
					style: { color: "#f0b88a", weight: 0.5, fillColor: "#ffe3c4", fillOpacity: 0.95 }
				});
				updateBuildings();
			});
		}
	}

	function updateRoads() {
		if (map.getZoom() < DETAIL_ROADS_MIN_ZOOM) {
			if (detailRoadsLayer && map.hasLayer(detailRoadsLayer)) {
				map.removeLayer(detailRoadsLayer);
			}
			return;
		}

		if (detailRoadsLayer) {
			if (!map.hasLayer(detailRoadsLayer)) {
				detailRoadsLayer.addTo(map);
			}
			return;
		}

		if (!detailRoadsTask) {
			detailRoadsTask = loadLayer("roads-detail.geojson", function (geo) {
				if (geo && geo.features && geo.features.length) {
					detailRoadsLayer = buildRoadLayer(geo, false);
				}
			});
		}
	}

	function updateLayerVisibility() {
		updateLabels();
		updateRoads();
		updateBuildings();
	}

	map.on("zoomend", updateLayerVisibility);

	// --- состояние заказа: точки А/Б, маршрут и машины ---

	var pinA = null;
	var pinB = null;
	var routeLayer = null;
	var carMarkers = {};
	var lastFit = "";
	var lastRouteKey = "";
	var lastCarsKey = "";
	var lastStateJson = "";

	function pinIcon(text) {
		return L.divIcon({ className: "", html: '<div class="pin">' + text + "</div>",
			iconSize: [26, 26], iconAnchor: [13, 13] });
	}

	function updatePin(current, latlng, text, name) {
		if (!latlng) {
			if (current) {
				map.removeLayer(current);
			}
			return null;
		}
		if (current) {
			current.setLatLng(latlng);
			current.setTooltipContent(name || text);
			return current;
		}
		return L.marker(latlng, { icon: pinIcon(text) })
			.addTo(map)
			.bindTooltip(name || text);
	}

	// маршрут рисуем только когда есть обе точки; без дорожной линии показываем
	// лишь при приблизительном расстоянии — выдуманный путь не рисуем
	function updateRoute(a, b, geometry, approximate) {
		var routeKey = JSON.stringify([a, b, geometry, !!approximate]);
		if (routeKey === lastRouteKey) {
			return;
		}
		lastRouteKey = routeKey;

		if (routeLayer) {
			map.removeLayer(routeLayer);
			routeLayer = null;
		}
		if (!a || !b) {
			return;
		}
		if (geometry) {
			routeLayer = L.geoJSON(geometry, { style: { color: "#ff8000", weight: 5 } }).addTo(map);
		} else if (approximate) {
			routeLayer = L.polyline([a, b], { color: "#ff8000", weight: 4, dashArray: "8 8" }).addTo(map);
		}
	}

	function carIcon(assigned) {
		var cls = assigned ? "car-marker assigned" : "car-marker";
		return L.divIcon({ className: "", html:
			'<div class="' + cls + '">' +
			'<svg viewBox="0 0 24 24" width="18" height="18" xmlns="http://www.w3.org/2000/svg">' +
			'<path fill="#fff" d="M18.92 6.01C18.72 5.42 18.16 5 17.5 5h-11c-.66 0-1.21.42-1.42 1.01L3 12v8c0' +
			' .55.45 1 1 1h1c.55 0 1-.45 1-1v-1h12v1c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-8l-2.08-5.99zM6.5 16c-.83' +
			' 0-1.5-.67-1.5-1.5S5.67 13 6.5 13 8 13.67 8 14.5 7.33 16 6.5 16zm11 0c-.83 0-1.5-.67-1.5-1.5s.67-1.5' +
			' 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zM5 11l1.5-4.5h11L19 11H5z"/></svg></div>',
			iconSize: [26, 26], iconAnchor: [13, 13] });
	}

	function carLabel(car) {
		return car.vehicle ? car.name + " — " + car.vehicle : car.name;
	}

	// машины обновляем по стабильному id водителя: они не мигают и не создаются заново
	function updateCars(cars) {
		var carsKey = JSON.stringify(cars.map(function (car) {
			return [car.id, car.lat, car.lng, car.name, car.vehicle, car.assigned];
		}));
		if (carsKey === lastCarsKey) {
			return;
		}
		lastCarsKey = carsKey;

		var seen = {};
		for (var i = 0; i < cars.length; i++) {
			var car = cars[i];
			var id = String(car.id);
			seen[id] = true;

			var marker = carMarkers[id];
			var latlng = [car.lat, car.lng];

			if (!marker) {
				marker = L.marker(latlng, { icon: carIcon(car.assigned) })
					.bindTooltip(carLabel(car))
					.addTo(map);
				marker.assigned = car.assigned;
				carMarkers[id] = marker;
			} else {
				marker.setLatLng(latlng);
				if (marker.assigned !== car.assigned) {
					marker.setIcon(carIcon(car.assigned));
					marker.assigned = car.assigned;
				}
				marker.setTooltipContent(carLabel(car));
			}
		}

		Object.keys(carMarkers).forEach(function (id) {
			if (!seen[id]) {
				map.removeLayer(carMarkers[id]);
				delete carMarkers[id];
			}
		});
	}

	// вид подгоняем только когда меняются сами точки А/Б, а не при каждом обновлении статуса
	function fitView(a, b) {
		if (!a && !b) {
			return;
		}
		var key = JSON.stringify([a, b]);
		if (key === lastFit) {
			return;
		}
		lastFit = key;

		setTimeout(function () {
			map.invalidateSize();
			if (a && b) {
				map.fitBounds(L.latLngBounds([a, b]).pad(0.3));
			} else {
				map.setView(a || b, 13);
			}
		}, 120);
	}

	function render(state) {
		var a = state.from;
		var b = state.to;

		pinA = updatePin(pinA, a, "\u0410", state.fromName);
		pinB = updatePin(pinB, b, "\u0411", state.toName);

		updateRoute(a, b, state.geometry, state.approximate);
		updateCars(state.cars || []);
		fitView(a, b);
	}

	function pull() {
		fetch("route.json?t=" + Date.now(), { cache: "no-store" })
			.then(function (response) {
				if (!response.ok) {
					throw new Error("route.json -> " + response.status);
				}
				return response.text();
			})
			.then(function (text) {
				if (text === lastStateJson) {
					return;
				}
				lastStateJson = text;
				render(JSON.parse(text));
			})
			.catch(function () { });
	}

	Promise.all(layers).then(function () {
		updateLayerVisibility();
	});

	pull();
	setInterval(pull, POLL_MS);
})();
