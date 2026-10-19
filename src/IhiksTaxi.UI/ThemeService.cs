using System;
using System.IO;
using Microsoft.Maui.Storage;

namespace IhiksTaxi.UI;

// Выбранная тема — светлая или тёмная — хранится рядом с файлом сессии.
public static class ThemeService
{
	private static Microsoft.Maui.Controls.Application? _app;

	public static bool IsDark { get; private set; }

	// подписчики — те, кто рисует не через XAML и потому сам перекрашивается
	// при смене темы: например, карта внутри WebView
	public static event Action? Changed;

	// приложение передаётся явно: в конструкторе App ссылка Application.Current ещё не надёжна
	public static void Initialize(Microsoft.Maui.Controls.Application app)
	{
		_app = app;
		IsDark = Read();
		Apply();
	}

	public static void SetDark(bool dark)
	{
		if (IsDark == dark)
			return;

		IsDark = dark;
		Write(dark);
		Apply();
	}

	private static void Apply()
	{
		if (_app is not null)
			_app.UserAppTheme = IsDark ? AppTheme.Dark : AppTheme.Light;

		// рамку окна рисует система, сама она о теме приложения не знает
		WindowChrome.ApplyFrameTheme();

		Changed?.Invoke();
	}

	private static string FilePath()
	{
		try
		{
			return Path.Combine(FileSystem.Current.AppDataDirectory, "theme.txt");
		}
		catch (Exception ex)
		{
			AppLog.Error("Тема: не удалось получить папку данных", ex);
			return Path.Combine(Path.GetTempPath(), "ihikstaxi-theme.txt");
		}
	}

	private static bool Read()
	{
		try
		{
			var path = FilePath();
			return File.Exists(path) && File.ReadAllText(path).Trim() == "dark";
		}
		catch (Exception ex)
		{
			AppLog.Error("Тема: не удалось прочитать настройку", ex);
			return false;
		}
	}

	private static void Write(bool dark)
	{
		try
		{
			File.WriteAllText(FilePath(), dark ? "dark" : "light");
		}
		catch (Exception ex)
		{
			AppLog.Error("Тема: не удалось сохранить настройку", ex);
		}
	}
}
