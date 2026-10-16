namespace IhiksTaxi.Application;

// разбор строки из одного поля ввода адреса «улица, дом»
public static class AddressQuery
{
	public static (string Street, string House) Parse(string? input)
	{
		var text = (input ?? string.Empty).Trim();
		if (text.Length == 0)
			return (string.Empty, string.Empty);

		var comma = text.IndexOf(',');
		var street = comma < 0 ? text : text[..comma].Trim();
		var house = comma < 0 ? string.Empty : text[(comma + 1)..].Trim();

		// запятой может не быть: «Ленина 12» — номер дома стоит последним словом
		if (house.Length == 0)
			(street, house) = SplitTrailingHouse(street);

		return (street.Trim(), CleanHouse(house));
	}

	private static (string Street, string House) SplitTrailingHouse(string street)
	{
		var space = street.LastIndexOf(' ');
		if (space <= 0)
			return (street, string.Empty);

		var tail = street[(space + 1)..];
		return char.IsDigit(tail[0])
			? (street[..space], tail)
			: (street, string.Empty);
	}

	private static string CleanHouse(string house)
	{
		var text = house.Trim();

		if (text.StartsWith("д.", StringComparison.OrdinalIgnoreCase))
			return text[2..].Trim();

		if (text.StartsWith("дом", StringComparison.OrdinalIgnoreCase) && text.Length > 3)
			return text[3..].Trim();

		return text;
	}
}
