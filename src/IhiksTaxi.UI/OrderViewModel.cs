using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using IhiksTaxi.Application;
using IhiksTaxi.Domain;

namespace IhiksTaxi.UI;

public class OrderViewModel : ObservableObject
{
	private readonly TripService _service;
	private readonly SessionService _session;
	private readonly RouteMapPresenter _map;
	private int _estimateToken;
	private int _searchToken;

	public OrderViewModel(TripService service, SessionService session, RouteMapPresenter map)
	{
		_service = service;
		_session = session;
		_map = map;

		CreateCommand = new Command(async () => await CreateAsync());
		SearchDriverCommand = new Command(async () => await SearchDriverAsync());
		AdvanceCommand = new Command(async () => await AdvanceAsync());
		CancelCommand = new Command(async () => await CancelAsync());
		RateCommand = new Command<string>(async value => await RateAsync(value));
		PickFromCommand = new Command(async () => await PickStreetAsync(true));
		PickToCommand = new Command(async () => await PickStreetAsync(false));
		FindFromCommand = new Command(async () => await FindAddressAsync(true));
		FindToCommand = new Command(async () => await FindAddressAsync(false));
		ClearFromCommand = new Command(() => ClearResolvedAddress(true));
		ClearToCommand = new Command(() => ClearResolvedAddress(false));
	}

	public ObservableCollection<TariffOption> TariffOptions { get; } = new();
	public ObservableCollection<StreetInfo> Streets { get; } = new();

	public ICommand CreateCommand { get; }
	public ICommand SearchDriverCommand { get; }
	public ICommand AdvanceCommand { get; }
	public ICommand CancelCommand { get; }
	public ICommand RateCommand { get; }
	public ICommand PickFromCommand { get; }
	public ICommand PickToCommand { get; }
	public ICommand FindFromCommand { get; }
	public ICommand FindToCommand { get; }
	public ICommand ClearFromCommand { get; }
	public ICommand ClearToCommand { get; }

	private int PassengerId => _session.PassengerId ?? 0;

	private TariffInfo? _selectedTariff;
	public TariffInfo? SelectedTariff
	{
		get => _selectedTariff;
		private set
		{
			if (Set(ref _selectedTariff, value))
			{
				OnPropertyChanged(nameof(SelectedTariffDetails));
				_ = RefreshEstimateAsync();
			}
		}
	}

	private StreetInfo? _fromStreet;
	public StreetInfo? FromStreet
	{
		get => _fromStreet;
		set
		{
			if (Set(ref _fromStreet, value))
			{
				OnPropertyChanged(nameof(FromAddressText));
				_ = RefreshEstimateAsync();
			}
		}
	}

	private StreetInfo? _toStreet;
	public StreetInfo? ToStreet
	{
		get => _toStreet;
		set
		{
			if (Set(ref _toStreet, value))
			{
				OnPropertyChanged(nameof(ToAddressText));
				_ = RefreshEstimateAsync();
			}
		}
	}

	private WebViewSource? _mapSource;
	public WebViewSource? MapSource
	{
		get => _mapSource;
		private set
		{
			if (Set(ref _mapSource, value))
				OnPropertyChanged(nameof(HasMap));
		}
	}

	private string _mapNote = string.Empty;
	public string MapNote
	{
		get => _mapNote;
		private set => Set(ref _mapNote, value);
	}

	public bool HasMap => MapSource is not null;

	private string _fromHouse = string.Empty;
	public string FromHouse
	{
		get => _fromHouse;
		set
		{
			if (Set(ref _fromHouse, value))
			{
				ClearResolvedAddress(true);
				OnPropertyChanged(nameof(FromAddressText));
			}
		}
	}

	private string _toHouse = string.Empty;
	public string ToHouse
	{
		get => _toHouse;
		set
		{
			if (Set(ref _toHouse, value))
			{
				ClearResolvedAddress(false);
				OnPropertyChanged(nameof(ToAddressText));
			}
		}
	}

	private GeocodeResult? _fromResolved;
	public GeocodeResult? FromResolved
	{
		get => _fromResolved;
		private set
		{
			if (Set(ref _fromResolved, value))
			{
				OnPropertyChanged(nameof(FromAddressText));
				OnPropertyChanged(nameof(FromPointStatus));
			}
		}
	}

	private GeocodeResult? _toResolved;
	public GeocodeResult? ToResolved
	{
		get => _toResolved;
		private set
		{
			if (Set(ref _toResolved, value))
			{
				OnPropertyChanged(nameof(ToAddressText));
				OnPropertyChanged(nameof(ToPointStatus));
			}
		}
	}

	private bool _isSearching;
	public bool IsSearching
	{
		get => _isSearching;
		private set => Set(ref _isSearching, value);
	}

	public string FromAddressText => ComposeAddress(FromStreet, FromHouse);
	public string ToAddressText => ComposeAddress(ToStreet, ToHouse);

	public string FromPointStatus => StatusOf(FromResolved);
	public string ToPointStatus => StatusOf(ToResolved);
	public bool HasFromPoint => FromResolved is not null;
	public bool HasToPoint => ToResolved is not null;

	private static string StatusOf(GeocodeResult? result) => result is null
		? string.Empty
		: result.FromStreetFallback
			? $"Найдена только улица: {result.DisplayName}. Точный дом не найден — координаты примерные."
			: result.Exact
				? $"Найден адрес: {result.DisplayName}"
				: $"Найдена улица: {result.DisplayName} (без номера дома)";

	private string _distanceText = "—";
	public string DistanceText
	{
		get => _distanceText;
		set => Set(ref _distanceText, value);
	}

	private string _durationText = "—";
	public string DurationText
	{
		get => _durationText;
		set => Set(ref _durationText, value);
	}

	private decimal _estimatedCost;
	public decimal EstimatedCost
	{
		get => _estimatedCost;
		set => Set(ref _estimatedCost, value);
	}

	public bool HasEstimate => EstimatedCost > 0;
	public string EstimateText => HasEstimate ? $"{EstimatedCost:0.##} ₽" : "—";
	public string SelectedTariffDetails => SelectedTariff is null
		? string.Empty
		: $"{SelectedTariff.BaseCost:0.##} ₽ посадка · {SelectedTariff.RatePerKm:0.##} ₽/км · {SelectedTariff.RatePerMinute:0.##} ₽/мин";

	private string? _message;
	public string? Message
	{
		get => _message;
		set => Set(ref _message, value);
	}

	private bool _isBusy;
	public bool IsBusy
	{
		get => _isBusy;
		set
		{
			if (Set(ref _isBusy, value))
				OnPropertyChanged(nameof(CanCreate));
		}
	}

	private TripInfo? _activeTrip;
	public TripInfo? ActiveTrip
	{
		get => _activeTrip;
		set => Set(ref _activeTrip, value);
	}

	public bool HasActiveTrip => ActiveTrip is not null;
	public bool ShowOrderForm => ActiveTrip is null;
	public string ActiveStatusText => ActiveTrip is null ? string.Empty : StatusLabels.Of(ActiveTrip.StatusName);
	public string ActiveRoute => ActiveTrip is null ? string.Empty : $"{ActiveTrip.FromAddress} → {ActiveTrip.ToAddress}";
	public string ActiveDriver => ActiveTrip?.DriverName is null
		? "Водитель ещё не назначен"
		: $"{ActiveTrip.DriverName}, {PhoneNumber.Format(ActiveTrip.DriverPhone)}";
	public string ActiveVehicle => ActiveTrip?.Vehicle ?? "Автомобиль уточняется";
	public string ActiveCost => ActiveTrip is null ? string.Empty : $"{ActiveTrip.Cost:0.##} ₽";
	public string ActiveTariffText
	{
		get
		{
			if (ActiveTrip is null)
				return string.Empty;

			var tariff = TariffOptions.FirstOrDefault(t => t.Id == ActiveTrip.TariffId);
			return tariff is null ? string.Empty : $"Тариф «{tariff.Info.Name}»";
		}
	}

	public string AdvanceLabel => ActiveTrip is null ? string.Empty : AdvanceText(ActiveTrip.StatusName);
	public string RatingText => ActiveTrip?.Rating is null ? string.Empty : $"Оценка водителю: {ActiveTrip.Rating}/5";

	// точка на карте: подтверждённый адрес с домом, иначе центр выбранной улицы
	private GeoPoint FromDisplayPoint => FromResolved?.Point ?? FromStreet!.ToGeoPoint();
	private GeoPoint ToDisplayPoint => ToResolved?.Point ?? ToStreet!.ToGeoPoint();
	private string FromDisplayName => FromResolved?.DisplayName ?? FromAddressText;
	private string ToDisplayName => ToResolved?.DisplayName ?? ToAddressText;

	public bool CanCreate => !IsBusy
		&& HasEstimate
		&& SelectedTariff is not null
		&& FromStreet is not null
		&& ToStreet is not null
		&& FromStreet.Id != ToStreet.Id;
	public bool CanSearchDriver => ActiveTrip?.StatusName == TripStatusNames.SearchingDriver;
	public bool CanAdvance => ActiveTrip is not null
		&& !TripStatusRules.IsTerminal(ActiveTrip.StatusName)
		&& ActiveTrip.StatusName != TripStatusNames.SearchingDriver;
	public bool CanCancel => ActiveTrip is not null && TripStatusRules.CanCancel(ActiveTrip.StatusName);
	public bool CanRate => ActiveTrip?.StatusName == TripStatusNames.Completed && ActiveTrip.Rating is null;

	public async Task ActivateAsync()
	{
		await LoadTariffsAsync();
		await LoadStreetsAsync();
		await LoadActiveTripAsync();
	}

	public async Task LoadTariffsAsync()
	{
		try
		{
			var tariffs = await _service.GetTariffsAsync();
			TariffOptions.Clear();
			foreach (var tariff in tariffs)
				TariffOptions.Add(new TariffOption(tariff, SelectTariff));

			if (SelectedTariff is null && TariffOptions.Count > 0)
				SelectTariff(TariffOptions[0]);

			OnPropertyChanged(nameof(SelectedTariffDetails));
		}
		catch (Exception ex)
		{
			Message = "Не удалось загрузить тарифы. " + ErrorText.Describe(ex);
		}
	}

	public async Task LoadStreetsAsync()
	{
		try
		{
			var streets = await _service.GetStreetsAsync();
			Streets.Clear();
			foreach (var street in streets)
				Streets.Add(street);

			FromStreet ??= Streets.FirstOrDefault();
			ToStreet ??= Streets.Skip(1).FirstOrDefault();
			await RefreshEstimateAsync();
		}
		catch (Exception ex)
		{
			Message = "Не удалось загрузить список улиц. " + ErrorText.Describe(ex);
		}
	}

	public async Task LoadActiveTripAsync()
	{
		try
		{
			SetActive(await _service.GetActiveTripAsync(PassengerId));
		}
		catch (Exception ex)
		{
			Message = "Нет связи с базой данных. " + ErrorText.Describe(ex);
		}
	}

	private void SelectTariff(TariffOption option)
	{
		foreach (var item in TariffOptions)
			item.IsSelected = ReferenceEquals(item, option);

		SelectedTariff = option.Info;
	}

	private async Task PickStreetAsync(bool isFrom)
	{
		if (Streets.Count == 0)
		{
			Message = "Список улиц пуст. Проверьте подключение к базе данных.";
			return;
		}

		var names = Streets.Select(s => s.Name).ToArray();
		var choice = await Microsoft.Maui.Controls.Application.Current!.MainPage!
			.DisplayActionSheet(isFrom ? "Откуда" : "Куда", "Отмена", null, names);

		if (string.IsNullOrEmpty(choice) || choice == "Отмена")
			return;

		var street = Streets.FirstOrDefault(s => s.Name == choice);
		if (street is null)
			return;

		if (isFrom)
		{
			FromStreet = street;
			ClearResolvedAddress(true);
		}
		else
		{
			ToStreet = street;
			ClearResolvedAddress(false);
		}
	}

	// ищем адрес в геокодере по явному нажатию кнопки; старый результат отменяем через токен
	private async Task FindAddressAsync(bool isFrom)
	{
		var street = isFrom ? FromStreet : ToStreet;
		if (street is null)
		{
			Message = "Сначала выберите улицу.";
			return;
		}

		var house = (isFrom ? FromHouse : ToHouse).Trim();
		int token = ++_searchToken;
		IsSearching = true;
		try
		{
			var found = await _service.FindAddressesAsync(street.Name, house);
			if (token != _searchToken)
				return;

			if (found.Count == 0)
			{
				Message = $"Адрес «{street.Name}{(house.Length > 0 ? ", " + house : string.Empty)}» не найден. Уточните улицу или дом.";
				return;
			}

			Message = null;
			var variants = found
				.Select(r => r.FromStreetFallback
					? $"{r.DisplayName} — точный дом не найден, координаты примерные"
					: r.DisplayName)
				.ToArray();

			var choice = variants.Length == 1
				? variants[0]
				: await Microsoft.Maui.Controls.Application.Current!.MainPage!
					.DisplayActionSheet("Найденные адреса", "Отмена", null, variants);

			if (string.IsNullOrEmpty(choice) || choice == "Отмена")
				return;

			var index = Array.IndexOf(variants, choice);
			if (index < 0)
				return;

			if (isFrom)
				FromResolved = found[index];
			else
				ToResolved = found[index];
		}
		catch (Exception ex)
		{
			if (token != _searchToken)
				return;

			Message = "Не удалось выполнить поиск адреса. " + ErrorText.Describe(ex);
		}
		finally
		{
			if (token == _searchToken)
				IsSearching = false;
		}
	}

	private void ClearResolvedAddress(bool isFrom)
	{
		if (isFrom)
			FromResolved = null;
		else
			ToResolved = null;
	}

	private async Task RefreshEstimateAsync()
	{
		int token = ++_estimateToken;

		if (SelectedTariff is null || FromStreet is null || ToStreet is null)
		{
			ClearEstimate();
			OnPropertyChanged(nameof(CanCreate));
			return;
		}

		if (FromStreet.Id == ToStreet.Id)
		{
			ClearEstimate();
			Message = "Адрес подачи и адрес назначения должны различаться.";
			OnPropertyChanged(nameof(CanCreate));
			return;
		}

		try
		{
			var estimate = HasFromPoint && HasToPoint
				? await _service.EstimateAsync(FromResolved!.Point, ToResolved!.Point, SelectedTariff.Id)
				: await _service.EstimateAsync(FromStreet.Id, ToStreet.Id, SelectedTariff.Id);

			if (token != _estimateToken)
				return;

			DistanceText = $"{estimate.DistanceKm:0.##} км";
			DurationText = $"{estimate.DurationMin} мин";
			EstimatedCost = estimate.Cost;
			MapSource = _map.Create(estimate, FromDisplayPoint, FromDisplayName, ToDisplayPoint, ToDisplayName);
			MapNote = estimate.IsApproximate
				? "Маршрут по дорогам не построен — показано расстояние по прямой."
				: string.Empty;
			Message = null;
		}
		catch (Exception ex)
		{
			if (token != _estimateToken)
				return;

			ClearEstimate();
			Message = "Не удалось рассчитать поездку. " + ErrorText.Describe(ex);
		}

		OnPropertyChanged(nameof(HasEstimate));
		OnPropertyChanged(nameof(EstimateText));
		OnPropertyChanged(nameof(CanCreate));
	}

	private void ClearEstimate()
	{
		DistanceText = "—";
		DurationText = "—";
		EstimatedCost = 0;
		MapSource = null;
		MapNote = string.Empty;
		OnPropertyChanged(nameof(HasEstimate));
		OnPropertyChanged(nameof(EstimateText));
	}

	private async Task CreateAsync()
	{
		if (IsBusy)
			return;

		if (SelectedTariff is null)
		{
			Message = "Выберите тариф.";
			return;
		}

		if (FromStreet is null || ToStreet is null)
		{
			Message = "Выберите улицы подачи и назначения.";
			return;
		}

		if (FromStreet.Id == ToStreet.Id)
		{
			Message = "Адрес подачи и адрес назначения должны различаться.";
			return;
		}

		if (!HasEstimate)
		{
			Message = "Дождитесь расчёта стоимости поездки.";
			return;
		}

		IsBusy = true;
		try
		{
			var trip = HasFromPoint && HasToPoint
				? await _service.CreateTripAsync(PassengerId, SelectedTariff.Id,
					(FromStreet.Name, FromHouse, FromResolved!.Point),
					(ToStreet.Name, ToHouse, ToResolved!.Point))
				: await _service.CreateTripAsync(PassengerId, SelectedTariff.Id,
					FromStreet.Id, FromHouse, ToStreet.Id, ToHouse);

			SetActive(trip);
			Message = trip.DriverId is null
				? "Свободных водителей этого тарифа нет. Можно повторить поиск."
				: "Заказ принят, водитель назначен.";
		}
		catch (Exception ex)
		{
			Message = "Не удалось оформить заказ. " + ErrorText.Describe(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task SearchDriverAsync()
	{
		if (IsBusy || ActiveTrip is null)
			return;

		IsBusy = true;
		try
		{
			var trip = await _service.SearchDriverAsync(ActiveTrip.Id);
			SetActive(trip);
			Message = trip.DriverId is null ? "Свободных водителей пока нет." : "Водитель найден.";
		}
		catch (Exception ex)
		{
			Message = ErrorText.Describe(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task AdvanceAsync()
	{
		if (IsBusy || ActiveTrip is null)
			return;

		IsBusy = true;
		try
		{
			SetActive(await _service.AdvanceAsync(ActiveTrip.Id));
			Message = null;
		}
		catch (Exception ex)
		{
			Message = ErrorText.Describe(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task CancelAsync()
	{
		if (IsBusy || ActiveTrip is null)
			return;

		bool confirmed = await Microsoft.Maui.Controls.Application.Current!.MainPage!
			.DisplayAlert("Отмена поездки", "Отменить текущую поездку?", "Да, отменить", "Нет");

		if (!confirmed)
			return;

		IsBusy = true;
		try
		{
			await _service.CancelAsync(ActiveTrip.Id);
			SetActive(null);
			Message = "Поездка отменена.";
		}
		catch (Exception ex)
		{
			Message = ErrorText.Describe(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task RateAsync(string? value)
	{
		if (IsBusy || ActiveTrip is null || !int.TryParse(value, out var rate))
			return;

		IsBusy = true;
		try
		{
			await _service.RateAsync(ActiveTrip.Id, rate);
			SetActive(null);
			Message = "Спасибо за оценку!";
		}
		catch (Exception ex)
		{
			Message = ErrorText.Describe(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}

	private void SetActive(TripInfo? trip)
	{
		ActiveTrip = trip;
		OnPropertyChanged(nameof(HasActiveTrip));
		OnPropertyChanged(nameof(ShowOrderForm));
		OnPropertyChanged(nameof(ActiveStatusText));
		OnPropertyChanged(nameof(ActiveRoute));
		OnPropertyChanged(nameof(ActiveDriver));
		OnPropertyChanged(nameof(ActiveVehicle));
		OnPropertyChanged(nameof(ActiveCost));
		OnPropertyChanged(nameof(ActiveTariffText));
		OnPropertyChanged(nameof(AdvanceLabel));
		OnPropertyChanged(nameof(RatingText));
		OnPropertyChanged(nameof(CanSearchDriver));
		OnPropertyChanged(nameof(CanAdvance));
		OnPropertyChanged(nameof(CanCancel));
		OnPropertyChanged(nameof(CanRate));
		OnPropertyChanged(nameof(CanCreate));
	}

	private static string ComposeAddress(StreetInfo? street, string? house)
	{
		if (street is null)
			return "Выберите улицу";

		var trimmed = (house ?? string.Empty).Trim();
		return trimmed.Length == 0 ? street.Name : $"{street.Name}, {trimmed}";
	}

	private static string AdvanceText(string statusName) => statusName switch
	{
		TripStatusNames.DriverAssigned => "Водитель выехал",
		TripStatusNames.DriverArriving => "Пассажир в машине",
		TripStatusNames.PassengerPickedUp => "Начать поездку",
		TripStatusNames.InProgress => "Завершить поездку",
		_ => "Далее",
	};
}
