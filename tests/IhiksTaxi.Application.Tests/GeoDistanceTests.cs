using IhiksTaxi.Application;

namespace IhiksTaxi.Application.Tests;

public class GeoDistanceTests
{
	[Test]
	public void Km_of_one_degree_of_latitude_is_about_111_kilometres()
	{
		var distance = GeoDistance.Km(new GeoPoint(56.0, 41.0), new GeoPoint(57.0, 41.0));

		Assert.That(distance, Is.InRange(110.5m, 112.0m));
	}
}
