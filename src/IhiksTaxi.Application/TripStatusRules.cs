using IhiksTaxi.Domain;

namespace IhiksTaxi.Application;

public static class TripStatusRules
{
	private static readonly string[] Order =
	{
		TripStatusNames.Created,
		TripStatusNames.SearchingDriver,
		TripStatusNames.DriverAssigned,
		TripStatusNames.DriverArriving,
		TripStatusNames.PassengerPickedUp,
		TripStatusNames.InProgress,
		TripStatusNames.Completed,
	};

	private static readonly HashSet<string> Cancellable = new()
	{
		TripStatusNames.Created,
		TripStatusNames.SearchingDriver,
		TripStatusNames.DriverAssigned,
		TripStatusNames.DriverArriving,
	};

	public static string? Next(string current)
	{
		var index = Array.IndexOf(Order, current);
		return index >= 0 && index + 1 < Order.Length ? Order[index + 1] : null;
	}

	public static bool CanMove(string from, string to)
	{
		var index = Array.IndexOf(Order, from);
		return index >= 0 && index + 1 < Order.Length && Order[index + 1] == to;
	}

	public static bool CanCancel(string current) => Cancellable.Contains(current);

	public static bool IsTerminal(string current) =>
		current == TripStatusNames.Completed || current == TripStatusNames.Cancelled;
}
