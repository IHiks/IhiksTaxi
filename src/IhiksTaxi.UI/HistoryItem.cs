using IhiksTaxi.Application;
using IhiksTaxi.Domain;
using Microsoft.Maui.Graphics;

namespace IhiksTaxi.UI;

public sealed class HistoryItem
{
	public HistoryItem(TripInfo trip)
	{
		Trip = trip;
	}

	public TripInfo Trip { get; }

	public string Route => $"{Trip.FromAddress} → {Trip.ToAddress}";
	public string DateText => Trip.CreatedAt.ToString("dd.MM.yyyy HH:mm");
	public string CostText => $"{Trip.Cost:0.##} ₽";
	public string StatusText => StatusLabels.Of(Trip.StatusName);
	public string DriverText => Trip.DriverName ?? "водитель не назначен";
	public string RatingText => Trip.Rating is null ? "без оценки" : $"оценка {Trip.Rating}/5";

	public Color StatusColor => Trip.StatusName switch
	{
		TripStatusNames.Completed => Color.FromArgb("#1F7A4C"),
		TripStatusNames.Cancelled => Color.FromArgb("#B42318"),
		_ => Color.FromArgb("#486581"),
	};
}
