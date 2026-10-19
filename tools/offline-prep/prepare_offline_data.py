#!/usr/bin/env python3
"""Подготовка локальных данных для офлайн-режима IHIKS Taxi.

Скрипт запускается один раз при наличии интернета и складывает в папку
offline-data всё, что нужно приложению без сети: геометрию улиц, зданий и
подписей, локальный адресный индекс и граф дорог для маршрутизации.

Источник — OpenStreetMap через Overpass API. Данные OSM распространяются
по лицензии ODbL, поэтому рядом сохраняется атрибуция.

Зависимости: только стандартная библиотека Python 3.
"""

import json
import math
import os
import sys
import time
import urllib.parse
import urllib.request

BBOX = (56.90, 40.83, 57.10, 41.12)  # south, west, north, east — Иваново и окрестности
# публичные зеркала Overpass; из-за нагрузки одно место может отвечать 504,
# поэтому просто перебираем их по кругу
OVERPASS_ENDPOINTS = [
    "https://overpass-api.de/api/interpreter",
    "https://overpass.kumi.systems/api/interpreter",
    "https://overpass.openstreetmap.fr/api/interpreter",
    "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
    "https://overpass.private.coffee/api/interpreter",
    "https://overpass.osm.jp/api/interpreter",
]
USER_AGENT = "IHIKS-Taxi/1.0 (educational project; offline data preparation)"

# ходим в сеть напрямую: системный прокси на этой машине нестабилен,
# а прямой доступ к Overpass и CDN работает
OPENER = urllib.request.build_opener(urllib.request.ProxyHandler({}))

HERE = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(HERE, "cache")
MAP_WEB = os.path.join(HERE, "map-web")
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "src", "IhiksTaxi.UI", "offline-data"))
# ручной справочник мест: синонимы и переименования поверх того, что есть в OSM
PLACES_EXTRA = os.path.join(HERE, "places-extra.json")

# объекты, которые ищут не по адресу, а по названию — ТЦ, ТРК, универмаги
MALL_SHOPS = ["mall", "department_store"]

# для маршрутизации берём только проезжие дороги, а не тропинки и лестницы
DRIVABLE = {
    "motorway", "motorway_link", "trunk", "trunk_link", "primary", "primary_link",
    "secondary", "secondary_link", "tertiary", "tertiary_link", "unclassified",
    "residential", "living_street", "service", "road",
}

# средняя скорость по классу дороги, км/ч
SPEED = {
    "motorway": 90, "motorway_link": 60, "trunk": 70, "trunk_link": 50,
    "primary": 60, "primary_link": 50, "secondary": 50, "secondary_link": 40,
    "tertiary": 40, "tertiary_link": 30, "unclassified": 40, "residential": 30,
    "living_street": 20, "service": 20, "road": 30,
}

# заметные линии для подложки: всё, что рисуем, но не обязательно едем
DISPLAY_MAJOR = {
    "motorway", "motorway_link", "trunk", "trunk_link", "primary", "primary_link",
    "secondary", "secondary_link", "tertiary", "tertiary_link",
}


def log(message):
    print(message, flush=True)


def read_cache(cache_name):
    path = os.path.join(CACHE, cache_name + ".json")
    if not os.path.exists(path):
        return None
    try:
        with open(path, "r", encoding="utf-8") as handle:
            parsed = json.load(handle)
    except (OSError, ValueError):
        return None
    if parsed.get("elements"):
        return parsed
    return None


def overpass(query, cache_name, force=False, allow_fetch=True):
    os.makedirs(CACHE, exist_ok=True)
    path = os.path.join(CACHE, cache_name + ".json")

    if not force:
        cached = read_cache(cache_name)
        if cached is not None:
            log(f"[cache] {cache_name}: {len(cached['elements'])} элементов")
            return cached

    if not allow_fetch:
        log(f"[skip] {cache_name} нет в кэше, сеть отключена флагом --skip-missing")
        return None

    data = urllib.parse.urlencode({"data": query}).encode("utf-8")
    last_error = None
    for endpoint in OVERPASS_ENDPOINTS * 2:
        try:
            log(f"[fetch] {cache_name} <- {endpoint}")
            request = urllib.request.Request(endpoint, data=data, headers={"User-Agent": USER_AGENT})
            with OPENER.open(request, timeout=300) as response:
                raw = response.read()
            parsed = json.loads(raw) if raw.lstrip()[:1] == b"{" else None
            if not parsed or not parsed.get("elements"):
                raise RuntimeError("Overpass не вернул данные (сервер занят)")
            with open(path, "wb") as handle:
                handle.write(raw)
            log(f"        {len(parsed['elements'])} элементов, {len(raw)} байт")
            return parsed
        except Exception as error:  # noqa: BLE001 — перебираем зеркала и пробуем снова
            last_error = error
            log(f"        ошибка: {error}")
            time.sleep(8)
    log(f"[warn] {cache_name} не получен: {last_error}")
    return None


def haversine(lat1, lon1, lat2, lon2):
    radius = 6371000.0
    phi1, phi2 = math.radians(lat1), math.radians(lat2)
    d_phi = phi2 - phi1
    d_lambda = math.radians(lon2 - lon1)
    a = math.sin(d_phi / 2) ** 2 + math.cos(phi1) * math.cos(phi2) * math.sin(d_lambda / 2) ** 2
    return 2 * radius * math.atan2(math.sqrt(a), math.sqrt(1 - a))


def bbox_query(selector, output="out geom;"):
    s, w, n, e = BBOX
    return f'[out:json][timeout:300];({selector}({s},{w},{n},{e}););{output}'


def building_query(south, west, north, east):
    # зданий в Иванове очень много, поэтому берём их не одной большой выборкой,
    # а по плиткам: большой запрос Overpass обрывает по таймауту
    return (f'[out:json][timeout:180];'
            f'(way["building"]({south},{west},{north},{east}););out geom;')


def fetch_buildings(grid, force, fetch):
    south, west, north, east = BBOX
    lat_step = (north - south) / grid
    lon_step = (east - west) / grid
    merged = {}
    missing = []
    for row in range(grid):
        for col in range(grid):
            s = round(south + row * lat_step, 6)
            n = round(south + (row + 1) * lat_step, 6)
            w = round(west + col * lon_step, 6)
            e = round(west + (col + 1) * lon_step, 6)
            name = f"buildings_{row}_{col}"
            raw = overpass(building_query(s, w, n, e), name, force, fetch)
            if not raw:
                missing.append(name)
                continue
            # плитки на границе могут вернуть одно и то же здание — храним по id
            for element in raw["elements"]:
                merged[(element["type"], element["id"])] = element
    if missing:
        log(f"[warn] здания не получены в плитках: {', '.join(missing)}")
    return {"elements": list(merged.values())}


def road_direction(tags):
    if tags.get("junction") in ("roundabout", "circular"):
        return 1
    oneway = tags.get("oneway", "").lower()
    if oneway in ("yes", "true", "1"):
        return 1
    if oneway in ("-1", "reverse"):
        return -1
    return 0


def simplify_indices(coords, tolerance):
    # алгоритм Дугласа — Пекера: возвращает индексы вершин, которые остаются
    # после упрощения ломаной. tolerance задаётся в тех же единицах, что и coords.
    if len(coords) <= 3:
        return list(range(len(coords)))
    keep = [False] * len(coords)
    keep[0] = keep[-1] = True
    stack = [(0, len(coords) - 1)]
    while stack:
        first, last = stack.pop()
        ax, ay = coords[first]
        bx, by = coords[last]
        dx, dy = bx - ax, by - ay
        norm = math.hypot(dx, dy)
        max_dist, index = -1.0, -1
        for i in range(first + 1, last):
            px, py = coords[i]
            if norm == 0:
                dist = math.hypot(px - ax, py - ay)
            else:
                dist = abs(dx * (ay - py) - (ax - px) * dy) / norm
            if dist > max_dist:
                max_dist, index = dist, i
        if max_dist > tolerance and index > 0:
            keep[index] = True
            stack.append((first, index))
            stack.append((index, last))
    return [i for i, k in enumerate(keep) if k]


def build_roads(raw):
    # индексы узлов графа по id точки OSM, чтобы перекрёстки склеивались сами
    node_index = {}
    node_coords = []
    edges = []
    features = []

    def node_of(node_id, lat, lon):
        index = node_index.get(node_id)
        if index is None:
            index = len(node_coords)
            node_index[node_id] = index
            node_coords.append([round(lat, 6), round(lon, 6)])
        return index

    for element in raw["elements"]:
        tags = element.get("tags", {})
        highway = tags.get("highway")
        geometry = element.get("geometry") or []
        if not highway or len(geometry) < 2:
            continue

        coords = [(node["lat"], node["lon"]) for node in geometry]
        features.append({
            "type": "Feature",
            "properties": {"h": highway, "n": tags.get("name", ""),
                           "major": highway in DISPLAY_MAJOR},
            "geometry": {"type": "LineString",
                         "coordinates": [[round(lon, 6), round(lat, 6)] for lat, lon in coords]},
        })

        node_ids = element.get("nodes") or []
        if highway not in DRIVABLE or len(node_ids) != len(coords):
            continue

        speed = SPEED.get(highway, 30)
        direction = road_direction(tags)
        indices = [node_of(node_ids[i], coords[i][0], coords[i][1]) for i in range(len(coords))]

        for i in range(len(indices) - 1):
            a, b = indices[i], indices[i + 1]
            if a == b:
                continue
            length = haversine(coords[i][0], coords[i][1], coords[i + 1][0], coords[i + 1][1])
            if length < 0.5:
                continue
            edges.append([a, b, round(length, 2), speed, direction])

    return features, {"nodes": node_coords, "edges": edges}


def split_visual_roads(features):
    major = [feature for feature in features if feature["properties"].get("major")]
    detail = [feature for feature in features if not feature["properties"].get("major")]
    return major, detail


def build_buildings(raw):
    # расстояния в градусах считать нельзя, поэтому контуры переводим в метры
    # вокруг центра карты и упрощаем уже в метрах (допуск 2 м)
    lat0 = (BBOX[0] + BBOX[2]) / 2
    kx = math.cos(math.radians(lat0)) * 111320.0
    ky = 111320.0
    features = []
    for element in raw["elements"]:
        geometry = element.get("geometry") or []
        if len(geometry) < 4:
            continue
        coords = [(node["lat"], node["lon"]) for node in geometry]
        flat = [(lon * kx, lat * ky) for lat, lon in coords]
        indices = simplify_indices(flat, tolerance=2.0)
        if len(indices) < 3:
            continue
        ring = [[round(coords[i][1], 6), round(coords[i][0], 6)] for i in indices]
        if ring[0] != ring[-1]:
            ring.append(ring[0])
        features.append({
            "type": "Feature",
            "properties": {"b": 1},
            "geometry": {"type": "Polygon", "coordinates": [ring]},
        })
    return features


def overpass_center(selector):
    return bbox_query(selector, output="out center;")


def build_addresses(raw):
    addresses = []
    street_points = {}
    for element in raw["elements"]:
        tags = element.get("tags", {})
        # адрес дома может опираться либо на улицу, либо на место:
        # в микрорайонах дома нумеруются как «Московский микрорайон, 5» (addr:place)
        street = (tags.get("addr:street") or tags.get("addr:place") or "").strip()
        house = (tags.get("addr:housenumber") or "").strip()
        if not street or not house:
            continue
        if element["type"] == "node":
            lat, lon = element.get("lat"), element.get("lon")
        else:
            center = element.get("center") or {}
            lat, lon = center.get("lat"), center.get("lon")
        if lat is None or lon is None:
            continue
        addresses.append({"s": street, "h": house,
                          "lat": round(lat, 6), "lon": round(lon, 6)})
        street_points.setdefault(street, []).append((lat, lon))

    streets = []
    for name, points in street_points.items():
        lat = sum(p[0] for p in points) / len(points)
        lon = sum(p[1] for p in points) / len(points)
        streets.append({"n": name, "lat": round(lat, 6), "lon": round(lon, 6)})

    addresses.sort(key=lambda item: (item["s"], item["h"]))
    streets.sort(key=lambda item: item["n"])
    return addresses, streets


def build_places(raw):
    features = []
    for element in raw["elements"]:
        tags = element.get("tags", {})
        name = tags.get("name")
        if not name:
            continue
        features.append({
            "type": "Feature",
            "properties": {"name": name, "kind": tags.get("place", "")},
            "geometry": {"type": "Point",
                         "coordinates": [element["lon"], element["lat"]]},
        })
    return features


def point_of(element):
    lat = element.get("lat") or (element.get("center") or {}).get("lat")
    lon = element.get("lon") or (element.get("center") or {}).get("lon")
    return (lat, lon) if lat is not None and lon is not None else (None, None)


def mall_query():
    s, w, n, e = BBOX
    selectors = "".join(f'nwr["shop"="{shop}"]({s},{w},{n},{e});' for shop in MALL_SHOPS)
    return f'[out:json][timeout:180];({selectors});out center;'


def addr_place_query():
    return bbox_query('nwr["addr:place"]["addr:housenumber"]', output="out center;")


# торговые центры: в адресах их нет, а для подсказок и подписей они нужны
def build_mall_features(raw):
    features = []
    for element in (raw or {}).get("elements", []):
        tags = element.get("tags", {})
        name = (tags.get("name") or "").strip()
        lat, lon = point_of(element)
        if not name or lat is None:
            continue
        features.append({
            "type": "Feature",
            "properties": {"name": name, "kind": "mall"},
            "geometry": {"type": "Point", "coordinates": [round(lon, 6), round(lat, 6)]},
        })
    return features


# подписи микрорайонов: центр всех адресных объектов с адресом «микрорайон …»
def build_district_features(raw):
    groups = {}
    for element in (raw or {}).get("elements", []):
        tags = element.get("tags", {})
        place = (tags.get("addr:place") or "").strip()
        if "микрорайон" not in place.lower():
            continue
        lat, lon = point_of(element)
        if lat is None:
            continue
        groups.setdefault(place, []).append((lat, lon))

    features = []
    for name, points in sorted(groups.items()):
        lat = sum(point[0] for point in points) / len(points)
        lon = sum(point[1] for point in points) / len(points)
        features.append({
            "type": "Feature",
            "properties": {"name": name, "kind": "neighbourhood"},
            "geometry": {"type": "Point", "coordinates": [round(lon, 6), round(lat, 6)]},
        })
    return features


# справочник мест для подсказок: ТЦ и ТРК из OSM плюс ручные записи с синонимами.
# Тип объекта в адресе не хранится, поэтому ищем его по списку синонимов.
def build_places_index(mall_raw):
    places = {}

    def add(name, lat=None, lon=None, aliases=()):
        entry = places.get(name.lower())
        if entry is None:
            entry = {"n": name, "a": []}
            if lat is not None and lon is not None:
                entry["lat"], entry["lon"] = round(lat, 6), round(lon, 6)
            places[name.lower()] = entry
        for alias in aliases:
            if alias and alias.lower() != name.lower():
                if alias.lower() not in [item.lower() for item in entry["a"]]:
                    entry["a"].append(alias)
        return entry

    for element in (mall_raw or {}).get("elements", []):
        tags = element.get("tags", {})
        name = (tags.get("name") or "").strip()
        lat, lon = point_of(element)
        if name and lat is not None:
            add(name, lat, lon)

    # ручные записи: привычные названия, синонимы и места, которых нет в OSM
    if os.path.exists(PLACES_EXTRA):
        with open(PLACES_EXTRA, "r", encoding="utf-8") as handle:
            for item in json.load(handle):
                name = (item.get("n") or "").strip()
                if not name:
                    continue
                entry = add(name, item.get("lat"), item.get("lon"), item.get("a") or [])
                entry["n"] = name  # руками указанное название важнее того, что в OSM

    return sorted(places.values(), key=lambda item: item["n"])


# одна подпись на одно имя: в OSM одно и то же место может встречаться дважды
def dedupe_names(features):
    seen = set()
    unique = []
    for feature in features:
        name = feature["properties"]["name"]
        if name in seen:
            continue
        seen.add(name)
        unique.append(feature)
    return unique


def write_json(path, payload):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(payload, handle, ensure_ascii=False, separators=(",", ":"))
    size = os.path.getsize(path)
    log(f"[write] {os.path.relpath(path, OUT)} — {size / 1024 / 1024:.2f} МБ")


def download(url, path, cache_name=None):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    cached = os.path.join(CACHE, cache_name) if cache_name else None
    if cached and os.path.exists(cached):
        with open(cached, "rb") as handle:
            data = handle.read()
    else:
        request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
        with OPENER.open(request, timeout=120) as response:
            data = response.read()
        if cached:
            with open(cached, "wb") as handle:
                handle.write(data)
    with open(path, "wb") as handle:
        handle.write(data)
    log(f"[asset] {os.path.relpath(path, OUT)} — {len(data)} байт")


def copy_text(source, target):
    os.makedirs(os.path.dirname(target), exist_ok=True)
    with open(source, "r", encoding="utf-8") as src, open(target, "w", encoding="utf-8") as dst:
        dst.write(src.read())
    log(f"[asset] {os.path.relpath(target, OUT)}")


def main():
    force = "--force" in sys.argv
    force_buildings = force or "--force-buildings" in sys.argv
    fetch = "--skip-missing" not in sys.argv
    with_buildings = "--with-buildings" in sys.argv
    buildings_grid = 4
    for arg in sys.argv:
        if arg.startswith("--buildings-grid="):
            buildings_grid = max(1, int(arg.split("=", 1)[1]))
    os.makedirs(OUT, exist_ok=True)

    roads_raw = overpass(bbox_query('way["highway"]'), "roads", force, fetch)
    if not roads_raw:
        raise SystemExit("Нет данных по улицам — без них карта и маршруты невозможны.")
    # здания — самый тяжёлый слой; тянем их только по явному запросу, по плиткам
    buildings_raw = fetch_buildings(buildings_grid, force_buildings, fetch) if with_buildings else None
    places_raw = overpass(bbox_query('node["place"]'), "places", force, fetch)
    addresses_raw = overpass(
        bbox_query('nwr["addr:housenumber"]["addr:street"]', output="out center;"),
        "addresses", force, fetch)
    # дома в микрорайонах адресуются местом, а не улицей («Московский микрорайон, 5»)
    addr_place_raw = overpass(addr_place_query(), "addr_place", force, fetch)
    mall_raw = overpass(mall_query(), "malls", force, fetch)

    if addr_place_raw:
        if addresses_raw:
            merged = {(el["type"], el["id"]): el for el in addresses_raw["elements"]}
            for element in addr_place_raw["elements"]:
                merged.setdefault((element["type"], element["id"]), element)
            addresses_raw = {"elements": list(merged.values())}
        else:
            addresses_raw = addr_place_raw

    log("Строим слой улиц и граф дорог...")
    road_features, graph = build_roads(roads_raw)
    major_roads, detail_roads = split_visual_roads(road_features)

    log("Строим слой зданий...")
    building_features = build_buildings(buildings_raw) if buildings_raw else []

    log("Строим подписи и адресный индекс...")
    place_features = build_places(places_raw) if places_raw else []
    addresses, streets = build_addresses(addresses_raw) if addresses_raw else ([], [])

    # к подписям городов и посёлков добавляем микрорайоны и торговые центры
    place_features = dedupe_names(
        place_features + build_district_features(addr_place_raw) + build_mall_features(mall_raw))
    places_index = build_places_index(mall_raw)
    log(f"Справочник мест: {len(places_index)} записей")

    write_json(os.path.join(OUT, "map", "roads.geojson"),
               {"type": "FeatureCollection", "features": road_features})
    write_json(os.path.join(OUT, "map", "roads-major.geojson"),
               {"type": "FeatureCollection", "features": major_roads})
    write_json(os.path.join(OUT, "map", "roads-detail.geojson"),
               {"type": "FeatureCollection", "features": detail_roads})
    write_json(os.path.join(OUT, "map", "buildings.geojson"),
               {"type": "FeatureCollection", "features": building_features})
    write_json(os.path.join(OUT, "map", "places.geojson"),
               {"type": "FeatureCollection", "features": place_features})
    write_json(os.path.join(OUT, "graph", "roadgraph.json"), graph)
    write_json(os.path.join(OUT, "index", "addresses.json"),
               {"streets": streets, "houses": addresses})
    write_json(os.path.join(OUT, "index", "places.json"), {"places": places_index})

    log("Складываем Leaflet и веб-часть карты...")
    download("https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/leaflet.js",
             os.path.join(OUT, "leaflet", "leaflet.js"), cache_name="leaflet.js")
    download("https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/leaflet.css",
             os.path.join(OUT, "leaflet", "leaflet.css"), cache_name="leaflet.css")
    for name in ("map.css", "base.js", "index.html"):
        copy_text(os.path.join(MAP_WEB, name), os.path.join(OUT, "map", name))

    s, w, n, e = BBOX
    write_json(os.path.join(OUT, "manifest.json"), {
        "source": "OpenStreetMap via Overpass API",
        "license": "ODbL 1.0, © OpenStreetMap contributors",
        "generated": time.strftime("%Y-%m-%d %H:%M"),
        "bbox": [s, w, n, e],
        "counts": {
            "roads": len(road_features),
            "roads_major": len(major_roads),
            "roads_detail": len(detail_roads),
            "buildings": len(building_features),
            "places": len(place_features),
            "streets": len(streets),
            "houses": len(addresses),
            "graph_nodes": len(graph["nodes"]),
            "graph_edges": len(graph["edges"]),
        },
    })
    log("Готово.")


if __name__ == "__main__":
    main()
