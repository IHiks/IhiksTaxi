using System.Collections.ObjectModel;
using System.Threading.Tasks;
using IhiksTaxi.Application;

namespace IhiksTaxi.UI;

public class ProfileViewModel : ObservableObject
{
	private readonly TripService _service;
	private readonly SessionService _session;

	public ProfileViewModel(TripService service, SessionService session)
	{
		_service = service;
		_session = session;
	}

	public ObservableCollection<HistoryItem> History { get; } = new();

	private int PassengerId => _session.PassengerId ?? 0;

	private PassengerInfo? _passenger;
	public PassengerInfo? Passenger
	{
		get => _passenger;
		set
		{
			if (Set(ref _passenger, value))
				OnPropertyChanged(nameof(PhoneText));
		}
	}

	public string PhoneText => PhoneNumber.Format(Passenger?.Phone);

	public bool HasHistory => History.Count > 0;

	private string? _statusMessage;
	public string? StatusMessage
	{
		get => _statusMessage;
		set => Set(ref _statusMessage, value);
	}

	public async Task RefreshAsync()
	{
		try
		{
			Passenger = await _service.GetPassengerAsync(PassengerId);

			var history = await _service.GetHistoryAsync(PassengerId);
			History.Clear();
			foreach (var trip in history)
				History.Add(new HistoryItem(trip));

			OnPropertyChanged(nameof(HasHistory));
			StatusMessage = null;
		}
		catch (Exception ex)
		{
			StatusMessage = "Не удалось загрузить профиль. " + ErrorText.Describe(ex);
		}
	}
}
