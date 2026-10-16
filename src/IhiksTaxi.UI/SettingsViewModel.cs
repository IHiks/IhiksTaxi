using System.Threading.Tasks;
using System.Windows.Input;
using IhiksTaxi.Application;

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

	private string _connectionStatus = "ещё не проверялось";
	public string ConnectionStatus
	{
		get => _connectionStatus;
		set => Set(ref _connectionStatus, value);
	}

	private async Task CheckConnectionAsync()
	{
		try
		{
			var tariffs = await _service.GetTariffsAsync();
			ConnectionStatus = $"подключение есть, доступно тарифов: {tariffs.Count}";
		}
		catch (Exception ex)
		{
			ConnectionStatus = "подключения нет. " + ErrorText.Describe(ex);
		}
	}
}
