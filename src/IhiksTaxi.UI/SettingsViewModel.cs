using System.Threading.Tasks;
using System.Windows.Input;
using IhiksTaxi.Application;
using Microsoft.Maui.Graphics;

namespace IhiksTaxi.UI;

public class SettingsViewModel : ObservableObject
{
	private readonly TripService _service;

	public SettingsViewModel(TripService service)
	{
		_service = service;
		CheckConnectionCommand = new Command(async () => await CheckConnectionAsync());
	}

	public ICommand CheckConnectionCommand { get; }

	public string ApplicationName => "IHIKS Taxi";
	public string Description => "Учебный проект: система заказа такси.";
	public string DatabaseInfo => "PostgreSQL, база данных IhiksTaxiCourse";
	public string Version => "Версия 1.0";

	private string _connectionStatus = "Ещё не проверялось";
	public string ConnectionStatus
	{
		get => _connectionStatus;
		set => Set(ref _connectionStatus, value);
	}

	private bool _isBusy;
	public bool IsBusy
	{
		get => _isBusy;
		private set
		{
			if (Set(ref _isBusy, value))
				OnPropertyChanged(nameof(CanCheck));
		}
	}

	public bool CanCheck => !IsBusy;

	public string StatusText => ConnectionStatus;

	public Color StatusColor => _statusState switch
	{
		StatusState.Ok => Color.FromArgb("#1F7A4C"),
		StatusState.Failed => Color.FromArgb("#B42318"),
		_ => Color.FromArgb("#486581"),
	};

	private enum StatusState
	{
		Unknown,
		Ok,
		Failed,
	}

	private StatusState _statusState = StatusState.Unknown;

	private async Task CheckConnectionAsync()
	{
		if (IsBusy)
			return;

		IsBusy = true;
		try
		{
			var tariffs = await _service.GetTariffsAsync();
			ConnectionStatus = $"Подключение есть. Доступно тарифов: {tariffs.Count}.";
			_statusState = StatusState.Ok;
		}
		catch (Exception ex)
		{
			ConnectionStatus = "Не удалось подключиться. " + ErrorText.Describe(ex);
			_statusState = StatusState.Failed;
		}
		finally
		{
			OnPropertyChanged(nameof(StatusText));
			OnPropertyChanged(nameof(StatusColor));
			IsBusy = false;
		}
	}
}
