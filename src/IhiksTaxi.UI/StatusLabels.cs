using IhiksTaxi.Domain;

namespace IhiksTaxi.UI;

public static class StatusLabels
{
	public static string Of(string statusName) => statusName switch
	{
		TripStatusNames.Created => "Создана",
		TripStatusNames.SearchingDriver => "Поиск водителя",
		TripStatusNames.DriverAssigned => "Водитель назначен",
		TripStatusNames.DriverArriving => "Водитель в пути",
		TripStatusNames.PassengerPickedUp => "Пассажир в машине",
		TripStatusNames.InProgress => "В поездке",
		TripStatusNames.Completed => "Завершена",
		TripStatusNames.Cancelled => "Отменена",
		_ => statusName,
	};
}
