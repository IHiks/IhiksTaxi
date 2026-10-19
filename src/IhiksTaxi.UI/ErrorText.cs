namespace IhiksTaxi.UI;

public static class ErrorText
{
	// бизнес-ошибки приложения бросаются без внутреннего исключения и показываются как есть;
	// всё остальное (сбой БД, таймауты, «transient failure» от EF) заменяем понятным текстом,
	// полная запись исключения при этом попадает в журнал app.log
	public static string Describe(Exception ex) =>
		ex is InvalidOperationException or ArgumentException && ex.InnerException is null
			? ex.Message
			: "Не удалось выполнить операцию. Проверьте подключение к базе данных.";
}
