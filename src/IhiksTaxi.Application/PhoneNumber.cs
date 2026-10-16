namespace IhiksTaxi.Application;

public static class PhoneNumber
{
	// для поиска и уникальности храним только цифры: «+7 (921) 100-20-30» и «89211002030» — один номер
	public static string Normalize(string? phone)
	{
		if (string.IsNullOrWhiteSpace(phone))
			return string.Empty;

		var digits = new string(phone.Where(char.IsDigit).ToArray());

		if (digits.Length == 11 && digits[0] == '8')
			digits = "7" + digits[1..];

		return digits;
	}

	// в базе номер храним как «+79991234567» — плюс и цифры, без скобок и дефисов
	public static string ToStored(string? phone)
	{
		var digits = Normalize(phone);
		return digits.Length == 0 ? string.Empty : "+" + digits;
	}

	// в интерфейсе номер показываем красиво: «+7 (999) 123-45-67»
	public static string Format(string? phone)
	{
		var digits = Normalize(phone);
		if (digits.Length == 11)
			return $"+{digits[0]} ({digits[1..4]}) {digits[4..7]}-{digits[7..9]}-{digits[9..11]}";

		return ToStored(phone);
	}
}
