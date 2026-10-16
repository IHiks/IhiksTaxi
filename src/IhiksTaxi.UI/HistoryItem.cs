using IhiksTaxi.Application;

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
	public string RatingText => Trip.Rating is null ? "без оценки" : $"оценка {Trip.Rating}/5";
	public string DriverText => Trip.DriverName ?? "водитель не назначен";
}
