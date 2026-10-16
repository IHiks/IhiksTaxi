using System;
using System.IO;
using Microsoft.Maui.Storage;

namespace IhiksTaxi.UI;

public sealed class SessionService
{
	private readonly string _filePath;

	public SessionService()
	{
		_filePath = BuildPath();
		PassengerId = ReadSavedId();
	}

	private static string BuildPath()
	{
		try
		{
			return Path.Combine(FileSystem.Current.AppDataDirectory, "session.json");
		}
		catch (Exception ex)
		{
			AppLog.Error("Сессия: не удалось получить папку данных", ex);
			return Path.Combine(Path.GetTempPath(), "ihikstaxi-session.json");
		}
	}

	public int? PassengerId { get; private set; }
	public bool IsSignedIn => PassengerId is not null;

	public event Action? Changed;

	public void SignIn(int passengerId)
	{
		PassengerId = passengerId;
		WriteSavedId(passengerId);
		Changed?.Invoke();
	}

	public void SignOut()
	{
		PassengerId = null;
		DeleteSavedId();
		Changed?.Invoke();
	}

	// в файле храним только идентификатор пассажира, пароль нигде не сохраняется
	private int? ReadSavedId()
	{
		try
		{
			if (!File.Exists(_filePath))
				return null;

			return int.TryParse(File.ReadAllText(_filePath).Trim(), out var id) ? id : null;
		}
		catch (Exception ex)
		{
			AppLog.Error("Сессия: не удалось прочитать файл", ex);
			return null;
		}
	}

	private void WriteSavedId(int passengerId)
	{
		try
		{
			File.WriteAllText(_filePath, passengerId.ToString());
		}
		catch (Exception ex)
		{
			AppLog.Error("Сессия: не удалось сохранить файл", ex);
		}
	}

	private void DeleteSavedId()
	{
		try
		{
			if (File.Exists(_filePath))
				File.Delete(_filePath);
		}
		catch (Exception ex)
		{
			AppLog.Error("Сессия: не удалось удалить файл", ex);
		}
	}
}
