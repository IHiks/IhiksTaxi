namespace IhiksTaxi.UI;

public static class ErrorText
{
	// бизнес-ошибки показываем как есть, а технические (сбой БД и т.п.) заменяем понятным текстом
	public static string Describe(Exception ex) =>
		ex is InvalidOperationException or ArgumentException
			? ex.Message
			: "Не удалось выполнить операцию. Проверьте подключение к базе данных.";
}
