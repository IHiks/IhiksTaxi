using IhiksTaxi.Domain;

namespace IhiksTaxi.Application;

// демонстрационное положение машины назначенного водителя в текущей поездке.
// это чистая функция от статуса: она ничего не сохраняет и не меняет адрес водителя.
public static class DemoCarPosition
{
	// origin — реальное местоположение водителя (его улица), from/to — точки подачи и назначения.
	// null — для этого статуса отдельную демонстрационную точку рисовать не нужно.
	public static GeoPoint? Resolve(string statusName, GeoPoint? origin, GeoPoint? from, GeoPoint? to)
		=> statusName switch
		{
			// водитель назначен: машина стоит там, где водитель реально находится
			TripStatusNames.DriverAssigned => origin,

			// водитель прибыл к точке подачи — машина у точки А
			TripStatusNames.DriverArriving => from,

			// пассажир сидит в машине и поездка идёт: движение пока не моделируем, машина у точки А
			TripStatusNames.PassengerPickedUp => from,
			TripStatusNames.InProgress => from,

			// поездка завершена — машина у точки назначения
			TripStatusNames.Completed => to,

			// Created/SearchingDriver/Cancelled: машина на точке подачи ещё не назначена,
			// отдельной демонстрационной точки нет — рисуем свободных водителей по их реальным адресам
			_ => null,
		};
}
