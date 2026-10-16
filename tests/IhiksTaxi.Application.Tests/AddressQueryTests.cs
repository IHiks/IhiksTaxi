using IhiksTaxi.Application;

namespace IhiksTaxi.Application.Tests;

public class AddressQueryTests
{
	[Test]
	public void Parse_splits_street_and_house_by_comma()
	{
		var (street, house) = AddressQuery.Parse("ул. Лежневская, 118");

		Assert.AreEqual("ул. Лежневская", street);
		Assert.AreEqual("118", house);
	}

	[Test]
	public void Parse_uses_last_word_as_house_when_no_comma()
	{
		var (street, house) = AddressQuery.Parse("Ленина 12");

		Assert.AreEqual("Ленина", street);
		Assert.AreEqual("12", house);
	}

	[Test]
	public void Parse_keeps_whole_line_as_street_when_no_house()
	{
		var (street, house) = AddressQuery.Parse("Площадь Революции");

		Assert.AreEqual("Площадь Революции", street);
		Assert.AreEqual(string.Empty, house);
	}

	[Test]
	public void Parse_removes_d_prefix_from_house()
	{
		var (street, house) = AddressQuery.Parse("ул. Лежневская, д. 118");

		Assert.AreEqual("ул. Лежневская", street);
		Assert.AreEqual("118", house);
	}

	[Test]
	public void Parse_removes_dom_prefix_from_house()
	{
		var (street, house) = AddressQuery.Parse("ул. Лежневская, дом 118");

		Assert.AreEqual("ул. Лежневская", street);
		Assert.AreEqual("118", house);
	}

	[Test]
	public void Parse_returns_empty_for_null_or_blank()
	{
		Assert.AreEqual((string.Empty, string.Empty), AddressQuery.Parse(null));
		Assert.AreEqual((string.Empty, string.Empty), AddressQuery.Parse("   "));
	}

	[Test]
	public void Parse_keeps_letter_suffix_in_house()
	{
		var (street, house) = AddressQuery.Parse("Ленина 12а");

		Assert.AreEqual("Ленина", street);
		Assert.AreEqual("12а", house);
	}
}
