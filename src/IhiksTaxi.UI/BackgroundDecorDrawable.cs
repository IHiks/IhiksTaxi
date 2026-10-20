using Microsoft.Maui.Graphics;

namespace IhiksTaxi.UI;

// Оранжевый декор фона. Оставлены только две диагональные полосы в правом
// верхнем углу — остальные фигуры убраны, чтобы фон не спорил с содержимым.
public sealed class BackgroundDecorDrawable : IDrawable
{
	private static readonly Color Orange = Color.FromArgb("#FF911F");

	public void Draw(ICanvas canvas, RectF rect)
	{
		float w = rect.Width;
		float h = rect.Height;
		if (w < 80 || h < 80)
			return;

		canvas.SaveState();
		canvas.StrokeLineCap = LineCap.Round;
		canvas.StrokeLineJoin = LineJoin.Round;

		// две толстые диагонали от верхнего края к правому — на всю высоту
		// страницы, чтобы на большом экране они не обрывались посередине.
		// Полосы одинакового цвета и параллельны: сдвиг по горизонтали
		// одинаковый у обеих (0.27w), поэтому угол наклона общий
		canvas.StrokeColor = Orange;
		canvas.StrokeSize = 20;
		canvas.Alpha = 0.12f;
		canvas.DrawLine(w * 0.60f, -24f, w * 0.87f, h + 24f);
		canvas.DrawLine(w * 0.72f, -24f, w * 0.99f, h + 24f);

		canvas.RestoreState();
	}
}
