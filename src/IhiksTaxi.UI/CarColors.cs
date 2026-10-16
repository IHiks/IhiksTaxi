using System.Collections.Generic;
using Microsoft.Maui.Graphics;

namespace IhiksTaxi.UI;

// сопоставление названия цвета автомобиля (как хранится в БД) и цвета-плашки для интерфейса
public static class CarColors
{
	private static readonly Dictionary<string, Color> Known = new()
	{
		["Белый"] = Color.FromArgb("#FFFFFF"),
		["Чёрный"] = Color.FromArgb("#1F2933"),
		["Черный"] = Color.FromArgb("#1F2933"),
		["Серый"] = Color.FromArgb("#7B8794"),
		["Серебристый"] = Color.FromArgb("#C7CDD4"),
		["Синий"] = Color.FromArgb("#2E5AAC"),
		["Красный"] = Color.FromArgb("#D64545"),
		["Коричневый"] = Color.FromArgb("#7A5230"),
		["Зелёный"] = Color.FromArgb("#3B7A57"),
	};

	private static readonly Color Fallback = Color.FromArgb("#9FB3C8");

	public static Color Resolve(string? colorName) =>
		colorName is not null && Known.TryGetValue(colorName, out var color) ? color : Fallback;
}
