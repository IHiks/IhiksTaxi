using System;
using System.IO;

namespace IhiksTaxi.UI;

// простой текстовый журнал приложения, чтобы после сбоя можно было понять причину
public static class AppLog
{
	private static readonly object Sync = new();
	private static readonly string Dir =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IhiksTaxi", "logs");
	private static readonly string LogFile = Path.Combine(Dir, "app.log");
	private const long MaxBytes = 1024 * 1024;

	public static void Info(string message) => Write("INFO", message, null);

	public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

	private static void Write(string level, string message, Exception? ex)
	{
		try
		{
			lock (Sync)
			{
				Directory.CreateDirectory(Dir);

				var file = new FileInfo(LogFile);
				if (file.Exists && file.Length > MaxBytes)
				{
					var older = LogFile + ".old";
					if (File.Exists(older))
						File.Delete(older);
					File.Move(LogFile, older);
				}

				var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
				if (ex is not null)
					line += Environment.NewLine + ex;

				File.AppendAllText(LogFile, line + Environment.NewLine);
			}
		}
		catch
		{
			// сбой самого журнала не должен ронять приложение
		}
	}
}
