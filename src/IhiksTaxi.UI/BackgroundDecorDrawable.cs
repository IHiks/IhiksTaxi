using Microsoft.Maui.Graphics;

namespace IhiksTaxi.UI;

// Оранжевые геометрические фигуры на фоне. Стиль минималистичный:
// несколько толстых линий и треугольников по краям экрана, чтобы
// центральное содержимое оставалось чистым.
public sealed class BackgroundDecorDrawable : IDrawable
{
	private static readonly Color Orange = Color.FromArgb("#FF911F");
	private static readonly Color LightOrange = Color.FromArgb("#FFD2A6");

	public void Draw(ICanvas canvas, RectF rect)
	{
		float w = rect.Width;
		float h = rect.Height;
		if (w < 80 || h < 80)
			return;

		float unit = Math.Min(w, h);

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

		// дуга большого круга в левом нижнем углу
		canvas.StrokeColor = LightOrange;
		canvas.StrokeSize = 26;
		canvas.Alpha = 0.30f;
		canvas.DrawCircle(w * 0.04f, h * 0.96f, unit * 0.30f);

		// залитый треугольник у левого края
		canvas.FillColor = Orange;
		canvas.Alpha = 0.10f;
		var triangle = new PathF();
		triangle.MoveTo(-10f, h * 0.34f);
		triangle.LineTo(unit * 0.16f, h * 0.34f + unit * 0.10f);
		triangle.LineTo(-10f, h * 0.34f + unit * 0.20f);
		triangle.Close();
		canvas.FillPath(triangle);

		// маленький треугольник над нижней панелью справа
		canvas.StrokeColor = Orange;
		canvas.StrokeSize = 14;
		canvas.Alpha = 0.16f;
		var small = new PathF();
		small.MoveTo(w - unit * 0.22f, h - 96f);
		small.LineTo(w - 28f, h - 96f);
		small.LineTo(w - 28f, h - 96f - unit * 0.14f);
		small.Close();
		canvas.DrawPath(small);

		canvas.RestoreState();
	}
}
