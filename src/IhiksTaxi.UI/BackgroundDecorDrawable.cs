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

		// две толстые диагонали в правом верхнем углу
		canvas.StrokeColor = Orange;
		canvas.StrokeSize = 20;
		canvas.Alpha = 0.12f;
		canvas.DrawLine(w * 0.60f, -24f, w * 0.84f, h * 0.30f);
		canvas.Alpha = 0.07f;
		canvas.DrawLine(w * 0.72f, -24f, w * 0.96f, h * 0.30f);

		canvas.RestoreState();
	}
}
