using System.Text;
using IhiksTaxi.Application;
using IhiksTaxi.Infrastructure;

namespace IhiksTaxi.Infrastructure.Tests;

// локальный геокодер: адресный индекс + справочник мест (ТЦ, ТРК, микрорайоны)
public class LocalGeocoderTests
{
	private const string Addresses = """
		{"streets":
		 [{"n":"улица Ленина","lat":57.0,"lon":40.98},
		  {"n":"Московский микрорайон","lat":56.9680497,"lon":40.9908156}],
		 "houses":
		 [{"s":"улица Ленина","h":"5","lat":57.001,"lon":40.981},
		  {"s":"Московский микрорайон","h":"7","lat":56.9675,"lon":40.991}]}
		""";

	private const string Places = """
		{"places":
		 [{"n":"ТРК «Серебряный Город»","lat":57.0054861,"lon":40.9853489,
		   "a":["серебрянный город"]},
		  {"n":"Тополь","lat":56.9830435,"lon":40.9821051}]}
		""";

	private string _root = null!;

	[SetUp]
	public void SetUp()
	{
		_root = Path.Combine(Path.GetTempPath(), "ihiks-taxi-geocoder", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_root, "index"));
		WriteIndex("addresses.json", Addresses);
		WriteIndex("places.json", Places);
	}

	[TearDown]
	public void TearDown()
	{
		if (Directory.Exists(_root))
			Directory.Delete(_root, recursive: true);
	}

	[Test]
	public void FindsExactHouseOnStreet()
	{
		var found = Search("ул. Ленина", "5");

		Assert.AreEqual("улица Ленина, 5", found[0].DisplayName);
		Assert.IsTrue(found[0].Exact);
		Assert.AreEqual(57.001, found[0].Point.Latitude, 0.0001);
	}

	[Test]
	public void FindsHouseAddressedByPlaceInsteadOfStreet()
	{
		// дома в микрорайонах адресуются местом: «Московский микрорайон, 7»
		var found = Search("Московский микрорайон", "7");

		Assert.AreEqual("Московский микрорайон, 7", found[0].DisplayName);
		Assert.IsTrue(found[0].Exact);
	}

	[Test]
	public void FindsPlaceByTypeAndNameTogether()
	{
		// «трк» не часть названия, поэтому его убираем из запроса
		var found = Search("ТРК Серебряный город", string.Empty);

		Assert.AreEqual("ТРК «Серебряный Город»", found[0].DisplayName);
		Assert.IsTrue(found[0].FromPlaceIndex);
		Assert.AreEqual(57.0054861, found[0].Point.Latitude, 0.0001);
	}

	[Test]
	public void FindsPlaceByAlias()
	{
		// синоним из справочника ловит опечатку: «серебрянный» с двумя «н»
		var found = Search("серебрянный город", string.Empty);

		Assert.AreEqual("ТРК «Серебряный Город»", found[0].DisplayName);
		Assert.IsTrue(found[0].FromPlaceIndex);
	}

	[Test]
	public void FindsPlaceByBareName()
	{
		var found = Search("тц тополь", string.Empty);

		Assert.AreEqual("Тополь", found[0].DisplayName);
		Assert.IsTrue(found[0].FromPlaceIndex);
	}

	[Test]
	public void KeepsHouseInPlaceDisplayNameWhenHouseIsUnknown()
	{
		// дома «3» у Тополя нет — точку берём от самого места, но номер не выбрасываем
		var found = Search("Тополь", "3");

		Assert.AreEqual("Тополь, 3", found[0].DisplayName);
		Assert.IsTrue(found[0].FromPlaceIndex);
		Assert.IsFalse(found[0].Exact);
	}

	[Test]
	public void ReturnsStreetFallbackWhenHouseIsUnknown()
	{
		var found = Search("улица Ленина", "1000");

		Assert.AreEqual("улица Ленина", found[0].DisplayName);
		Assert.IsTrue(found[0].FromStreetFallback);
	}

	[Test]
	public void WorksWithoutPlacesIndex()
	{
		File.Delete(Path.Combine(_root, "index", "places.json"));

		var found = Search("ул. Ленина", "5");

		Assert.AreEqual("улица Ленина, 5", found[0].DisplayName);
	}

	[Test]
	public void ReturnsNothingForEmptyQuery()
	{
		Assert.IsEmpty(Search(string.Empty, string.Empty));
		Assert.IsEmpty(Search("   ", "5"));
	}

	private IReadOnlyList<GeocodeResult> Search(string street, string house)
	{
		var geocoder = new LocalGeocoder(Path.Combine(_root, "index", "addresses.json"));
		return geocoder.SearchAsync(street, house).GetAwaiter().GetResult();
	}

	private void WriteIndex(string name, string content)
	{
		File.WriteAllText(Path.Combine(_root, "index", name), content, Encoding.UTF8);
	}
}
