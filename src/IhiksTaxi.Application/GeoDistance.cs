namespace IhiksTaxi.Application;

// расстояние по прямой между двумя точками земли; нужно для выбора ближайшего водителя
// и как резерв, когда дорожный роутер недоступен
public static class GeoDistance
{
	private const double EarthRadiusKm = 6371.0;

	private const decimal AverageSpeedKmH = 25m;
	private const int MinimumDurationMin = 5;

	public static decimal Km(GeoPoint from, GeoPoint to)
	{
		var lat1 = ToRadians(from.Latitude);
		var lat2 = ToRadians(to.Latitude);
		var dLat = lat2 - lat1;
		var dLon = ToRadians(to.Longitude - from.Longitude);

		var half = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
			+ Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
		var arc = 2 * Math.Atan2(Math.Sqrt(half), Math.Sqrt(1 - half));

		return Math.Round((decimal)(EarthRadiusKm * arc), 2, MidpointRounding.AwayFromZero);
	}

	public static int DurationMin(decimal distanceKm)
	{
		if (distanceKm <= 0)
			return MinimumDurationMin;

		var minutes = (int)Math.Ceiling(distanceKm / AverageSpeedKmH * 60m);
		return Math.Max(minutes, MinimumDurationMin);
	}

	private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
