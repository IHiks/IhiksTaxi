using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using IhiksTaxi.Application;
using IhiksTaxi.Domain;
using Microsoft.Maui.Graphics;

namespace IhiksTaxi.UI;

public class OrderViewModel : ObservableObject
{
	private const int DebounceMs = 300;
	private const int MaxSuggestions = 5;

	private readonly TripService _service;
	private readonly SessionService _session;
	private readonly RouteMapPresenter _map;
	private int _estimateToken;
	private CancellationTokenSource? _fromSearch;
	private CancellationTokenSource? _toSearch;

	private IReadOnlyList<DriverMapPoint> _driverPoints = Array.Empty<DriverMapPoint>();
	private FareEstimate? _lastEstimate;

	// точки активной поездки, найденные заново после запуска (в самой поездке хранятся только адреса)
	private GeocodeResult? _activeFrom;
	private GeocodeResult? _activeTo;

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
		SelectFromCommand = new Command<GeocodeResult>(item => SelectSuggestion(true, item));
		SelectToCommand = new Command<GeocodeResult>(item => SelectSuggestion(false, item));
	}

	public ObservableCollection<TariffOption> TariffOptions { get; } = new();
	public ObservableCollection<GeocodeResult> FromSuggestions { get; } = new();
	public ObservableCollection<GeocodeResult> ToSuggestions { get; } = new();

	public ICommand CreateCommand { get; }
	public ICommand SearchDriverCommand { get; }
	public ICommand AdvanceCommand { get; }
	public ICommand CancelCommand { get; }
	public ICommand RateCommand { get; }
	public ICommand SelectFromCommand { get; }
	public ICommand SelectToCommand { get; }

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

	private string _fromQuery = string.Empty;
	public string FromQuery
	{
		get => _fromQuery;
		set
		{
			if (!Set(ref _fromQuery, value) || _applyingSuggestion)
				return;

			FromResolved = null;
			_fromEmpty = false;
			OnPropertyChanged(nameof(FromPointStatus));
			_ = SearchSuggestionsAsync(true);
		}
	}

	private string _toQuery = string.Empty;
	public string ToQuery
	{
		get => _toQuery;
		set
		{
			if (!Set(ref _toQuery, value) || _applyingSuggestion)
				return;

			ToResolved = null;
			_toEmpty = false;
			OnPropertyChanged(nameof(ToPointStatus));
			_ = SearchSuggestionsAsync(false);
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

	private GeocodeResult? _fromResolved;
	public GeocodeResult? FromResolved
	{
		get => _fromResolved;
		private set
		{
			if (Set(ref _fromResolved, value))
			{
				OnPropertyChanged(nameof(FromPointStatus));
				OnPropertyChanged(nameof(HasFromPoint));
				_ = RefreshEstimateAsync();
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
				OnPropertyChanged(nameof(ToPointStatus));
				OnPropertyChanged(nameof(HasToPoint));
				_ = RefreshEstimateAsync();
			}
		}
	}

	public bool HasFromSuggestions => FromSuggestions.Count > 0;
	public bool HasToSuggestions => ToSuggestions.Count > 0;

	public string FromPointStatus => StatusOf(FromResolved, FromQuery, _fromEmpty);
	public string ToPointStatus => StatusOf(ToResolved, ToQuery, _toEmpty);
	public bool HasFromPoint => FromResolved is not null;
	public bool HasToPoint => ToResolved is not null;

	private bool _fromEmpty;
	private bool _toEmpty;
	private bool _applyingSuggestion;

	private static string StatusOf(GeocodeResult? resolved, string query, bool empty)
	{
		if (resolved is not null)
			return resolved.FromPlaceIndex
				? $"Найдено место: {resolved.DisplayName}"
				: resolved.Exact
					? $"Найден адрес: {resolved.DisplayName}"
					: $"Найдена улица: {resolved.DisplayName} (без номера дома)";

		return empty && query.Trim().Length > 0
			? $"По запросу «{query.Trim()}» ничего не найдено."
			: string.Empty;
	}

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
	public bool HasDriver => ActiveTrip?.DriverId is not null;
	public string ActiveDriverName => ActiveTrip?.DriverName ?? "Водитель ещё не назначен";
	public string ActiveDriverPhone => ActiveTrip?.DriverPhone is null
		? string.Empty
		: PhoneNumber.Format(ActiveTrip.DriverPhone);
	public string ActiveDriverInitials => Initials(ActiveTrip?.DriverName);
	public string ActiveVehicleName => ActiveTrip?.Vehicle ?? "Автомобиль уточняется";
	public string ActiveVehiclePlate => ActiveTrip?.VehiclePlate ?? "—";
	public string ActiveVehicleColor => ActiveTrip?.VehicleColor ?? string.Empty;
	public Color ActiveVehicleColorSwatch => CarColors.Resolve(ActiveTrip?.VehicleColor);
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

	public bool CanCreate => !IsBusy
		&& HasEstimate
		&& SelectedTariff is not null
		&& HasFromPoint
		&& HasToPoint
		&& !SameAddress();
	public bool CanSearchDriver => ActiveTrip?.StatusName == TripStatusNames.SearchingDriver;
	public bool CanAdvance => ActiveTrip is not null
		&& !TripStatusRules.IsTerminal(ActiveTrip.StatusName)
		&& ActiveTrip.StatusName != TripStatusNames.SearchingDriver;
	public bool CanCancel => ActiveTrip is not null && TripStatusRules.CanCancel(ActiveTrip.StatusName);
	public bool CanRate => ActiveTrip?.StatusName == TripStatusNames.Completed && ActiveTrip.Rating is null;

	public async Task ActivateAsync()
	{
		MapSource = _map.HasMap ? new UrlWebViewSource { Url = _map.Source! } : null;

		await LoadTariffsAsync();
		await LoadActiveTripAsync();
		await LoadDriverPointsAsync();
		RefreshMap();
	}

	public async Task LoadTariffsAsync()
	{
		try
		{
			var tariffs = await _service.GetTariffsAsync();
			TariffOptions.Clear();
			foreach (var tariff in tariffs)
			{
				var option = new TariffOption(tariff, SelectTariff) { Column = TariffOptions.Count };
				TariffOptions.Add(option);
			}

			if (SelectedTariff is null && TariffOptions.Count > 0)
				SelectTariff(TariffOptions[0]);

			OnPropertyChanged(nameof(SelectedTariffDetails));
		}
		catch (Exception ex)
		{
			Message = "Не удалось загрузить тарифы. " + ErrorText.Describe(ex);
		}
	}

	public async Task LoadActiveTripAsync()
	{
		try
		{
			var trip = await _service.GetActiveTripAsync(PassengerId);
			SetActive(trip);

			if (trip is not null)
				await ResolveActiveAddressesAsync(trip);
		}
		catch (Exception ex)
		{
			Message = "Нет связи с базой данных. " + ErrorText.Describe(ex);
		}
	}

	public async Task LoadDriverPointsAsync()
	{
		try
		{
			_driverPoints = await _service.GetDriverMapPointsAsync();
		}
		catch (Exception ex)
		{
			Message = "Не удалось загрузить водителей для карты. " + ErrorText.Describe(ex);
		}
	}

	// при запуске у активной поездки есть только адреса: пробуем найти их точки,
	// чтобы показать маршрут и машину; если не вышло — карта покажет хотя бы водителей
	private async Task ResolveActiveAddressesAsync(TripInfo trip)
	{
		if (FromResolved is null)
			_activeFrom = await TryGeocodeAsync(trip.FromAddress);
		if (ToResolved is null)
			_activeTo = await TryGeocodeAsync(trip.ToAddress);

		var from = FromResolved ?? _activeFrom;
		var to = ToResolved ?? _activeTo;
		if (from is not null && to is not null)
		{
			try
			{
				_lastEstimate = await _service.EstimateAsync(from.Point, to.Point, trip.TariffId);
				MapNote = _lastEstimate.IsApproximate
					? "Маршрут по дорогам не построен — показано расстояние по прямой."
					: string.Empty;
			}
			catch (Exception ex)
			{
				Message = "Не удалось построить маршрут текущей поездки. " + ErrorText.Describe(ex);
			}
		}

		RefreshMap();
	}

	private async Task<GeocodeResult?> TryGeocodeAsync(string address)
	{
		try
		{
			var found = await _service.SearchAddressesAsync(address);
			return found.Count > 0 ? found[0] : null;
		}
		catch
		{
			return null;
		}
	}

	private void RefreshMap()
	{
		if (MapSource is null)
			return;

		var from = FromResolved ?? _activeFrom;
		var to = ToResolved ?? _activeTo;

		_map.Update(MapStateBuilder.Build(
			_driverPoints,
			from?.Point, from?.DisplayName,
			to?.Point, to?.DisplayName,
			_lastEstimate,
			ActiveTrip));
	}

	private void SelectTariff(TariffOption option)
	{
		foreach (var item in TariffOptions)
			item.IsSelected = ReferenceEquals(item, option);

		SelectedTariff = option.Info;
	}

	// подсказки по текущему тексту поля; каждое новое нажатие клавиши отменяет предыдущий поиск
	private async Task SearchSuggestionsAsync(bool isFrom)
	{
		var cts = new CancellationTokenSource();
		if (isFrom)
		{
			_fromSearch?.Cancel();
			_fromSearch = cts;
		}
		else
		{
			_toSearch?.Cancel();
			_toSearch = cts;
		}

		var token = cts.Token;

		try
		{
			await Task.Delay(DebounceMs, token);

			var query = isFrom ? FromQuery : ToQuery;
			var found = await _service.SearchAddressesAsync(query, token);
			if (token.IsCancellationRequested)
				return;

			var shown = found.Take(MaxSuggestions).ToList();

			if (isFrom)
			{
				_fromEmpty = shown.Count == 0;
				PublishSuggestions(true, shown);
				OnPropertyChanged(nameof(FromPointStatus));
			}
			else
			{
				_toEmpty = shown.Count == 0;
				PublishSuggestions(false, shown);
				OnPropertyChanged(nameof(ToPointStatus));
			}
		}
		catch (OperationCanceledException)
		{
			// поиск уже устарел, результаты прежнего запроса показывать не нужно
		}
		catch (Exception ex)
		{
			// старые подсказки и выбранный адрес не трогаем, сообщаем о сбое
			if (token.IsCancellationRequested)
				return;

			Message = "Не удалось выполнить поиск адреса. " + ErrorText.Describe(ex);
		}
	}

	private void PublishSuggestions(bool isFrom, IReadOnlyList<GeocodeResult> shown)
	{
		var target = isFrom ? FromSuggestions : ToSuggestions;

		target.Clear();
		foreach (var item in shown)
			target.Add(item);

		if (isFrom)
			OnPropertyChanged(nameof(HasFromSuggestions));
		else
			OnPropertyChanged(nameof(HasToSuggestions));
	}

	private void SelectSuggestion(bool isFrom, GeocodeResult? result)
	{
		if (result is null)
			return;

		_applyingSuggestion = true;
		try
		{
			if (isFrom)
				FromQuery = result.DisplayName;
			else
				ToQuery = result.DisplayName;
		}
		finally
		{
			_applyingSuggestion = false;
		}

		if (isFrom)
		{
			PublishSuggestions(true, Array.Empty<GeocodeResult>());
			_fromEmpty = false;
			FromResolved = result;
		}
		else
		{
			PublishSuggestions(false, Array.Empty<GeocodeResult>());
			_toEmpty = false;
			ToResolved = result;
		}
	}

	private bool SameAddress()
		=> FromResolved is not null && ToResolved is not null && FromResolved.Point.Equals(ToResolved.Point);

	private async Task RefreshEstimateAsync()
	{
		int token = ++_estimateToken;

		if (SelectedTariff is null || !HasFromPoint || !HasToPoint)
		{
			ClearEstimate();
			OnPropertyChanged(nameof(CanCreate));
			return;
		}

		if (SameAddress())
		{
			ClearEstimate();
			Message = "Адрес подачи и адрес назначения должны различаться.";
			OnPropertyChanged(nameof(CanCreate));
			return;
		}

		try
		{
			var estimate = await _service.EstimateAsync(FromResolved!.Point, ToResolved!.Point, SelectedTariff.Id);

			if (token != _estimateToken)
				return;

			DistanceText = $"{estimate.DistanceKm:0.##} км";
			DurationText = $"{estimate.DurationMin} мин";
			EstimatedCost = estimate.Cost;
			_lastEstimate = estimate;
			MapNote = estimate.IsApproximate
				? "Маршрут по дорогам не построен — показано расстояние по прямой."
				: string.Empty;
			RefreshMap();
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
		_lastEstimate = null;
		MapNote = string.Empty;
		RefreshMap();
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

		if (!HasFromPoint || !HasToPoint)
		{
			Message = "Выберите адреса подачи и назначения из подсказок.";
			return;
		}

		if (SameAddress())
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
			var trip = await _service.CreateTripAsync(PassengerId, SelectedTariff.Id,
				(FromResolved!.DisplayName, string.Empty, FromResolved!.Point),
				(ToResolved!.DisplayName, string.Empty, ToResolved!.Point));

			SetActive(trip);
			Message = trip.DriverId is null
				? "Свободных водителей этого тарифа нет. Можно повторить поиск."
				: null;
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
			var trip = await _service.AdvanceAsync(ActiveTrip.Id);
			SetActive(trip);
			Message = null;

			// поездка завершена — водитель остался в точке Б, перечитываем его положение
			if (trip.StatusName == TripStatusNames.Completed)
			{
				await LoadDriverPointsAsync();
				RefreshMap();
			}
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
		_activeFrom = null;
		_activeTo = null;
		OnPropertyChanged(nameof(HasActiveTrip));
		OnPropertyChanged(nameof(ShowOrderForm));
		OnPropertyChanged(nameof(ActiveStatusText));
		OnPropertyChanged(nameof(ActiveRoute));
		OnPropertyChanged(nameof(HasDriver));
		OnPropertyChanged(nameof(ActiveDriverName));
		OnPropertyChanged(nameof(ActiveDriverPhone));
		OnPropertyChanged(nameof(ActiveDriverInitials));
		OnPropertyChanged(nameof(ActiveVehicleName));
		OnPropertyChanged(nameof(ActiveVehiclePlate));
		OnPropertyChanged(nameof(ActiveVehicleColor));
		OnPropertyChanged(nameof(ActiveVehicleColorSwatch));
		OnPropertyChanged(nameof(ActiveCost));
		OnPropertyChanged(nameof(ActiveTariffText));
		OnPropertyChanged(nameof(AdvanceLabel));
		OnPropertyChanged(nameof(RatingText));
		OnPropertyChanged(nameof(CanSearchDriver));
		OnPropertyChanged(nameof(CanAdvance));
		OnPropertyChanged(nameof(CanCancel));
		OnPropertyChanged(nameof(CanRate));
		OnPropertyChanged(nameof(CanCreate));
		RefreshMap();
	}

	private static string Initials(string? fullName)
	{
		if (string.IsNullOrWhiteSpace(fullName))
			return "—";

		var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		return parts.Length == 1
			? parts[0][..1].ToUpperInvariant()
			: string.Concat(parts[0][..1], parts[1][..1]).ToUpperInvariant();
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
