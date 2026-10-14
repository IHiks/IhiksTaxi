namespace IhiksTaxi.Domain;

public static class TripStatusNames
{
	public const string Created = "Created";
	public const string SearchingDriver = "SearchingDriver";
	public const string DriverAssigned = "DriverAssigned";
	public const string DriverArriving = "DriverArriving";
	public const string PassengerPickedUp = "PassengerPickedUp";
	public const string InProgress = "InProgress";
	public const string Completed = "Completed";
	public const string Cancelled = "Cancelled";
}
