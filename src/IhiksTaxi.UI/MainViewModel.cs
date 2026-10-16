using System.Threading.Tasks;
using System.Windows.Input;
using IhiksTaxi.Application;

namespace IhiksTaxi.UI;

public class MainViewModel : ObservableObject
{
	private enum Section
	{
		Order,
		Profile,
		Settings,
	}

	private readonly SessionService _session;
	private Section _section = Section.Order;

	public MainViewModel(TripService service, SessionService session, RouteMapPresenter map)
	{
		_session = session;
		Order = new OrderViewModel(service, session, map);
		Profile = new ProfileViewModel(service, session);
		Settings = new SettingsViewModel(service);

		ShowOrderCommand = new Command(() => Select(Section.Order));
		ShowProfileCommand = new Command(() => Select(Section.Profile));
		ShowSettingsCommand = new Command(() => Select(Section.Settings));
		LogoutCommand = new Command(() => _session.SignOut());

		MainThread.BeginInvokeOnMainThread(async () => await Order.ActivateAsync());
	}

	public OrderViewModel Order { get; }
	public ProfileViewModel Profile { get; }
	public SettingsViewModel Settings { get; }

	public ICommand ShowOrderCommand { get; }
	public ICommand ShowProfileCommand { get; }
	public ICommand ShowSettingsCommand { get; }
	public ICommand LogoutCommand { get; }

	private Thickness _contentMargin = new(24, 20);
	public Thickness ContentMargin
	{
		get => _contentMargin;
		private set => Set(ref _contentMargin, value);
	}

	private bool _showSubtitle = true;
	public bool ShowSubtitle
	{
		get => _showSubtitle;
		private set => Set(ref _showSubtitle, value);
	}

	private double _orderColumnSpacing = 12;
	public double OrderColumnSpacing
	{
		get => _orderColumnSpacing;
		private set => Set(ref _orderColumnSpacing, value);
	}

	private double _stackSpacing = 12;
	public double StackSpacing
	{
		get => _stackSpacing;
		private set => Set(ref _stackSpacing, value);
	}

	private Thickness _cardPadding = new(12);
	public Thickness CardPadding
	{
		get => _cardPadding;
		private set => Set(ref _cardPadding, value);
	}

	private bool _isStacked;
	public bool IsStacked
	{
		get => _isStacked;
		private set
		{
			if (!Set(ref _isStacked, value))
				return;

			OnPropertyChanged(nameof(OrderMapRow));
			OnPropertyChanged(nameof(OrderMapColumn));
			OnPropertyChanged(nameof(OrderMapRowSpan));
			OnPropertyChanged(nameof(OrderMapColumnSpan));
			OnPropertyChanged(nameof(OrderPanelColumn));
			OnPropertyChanged(nameof(OrderPanelRowSpan));
			OnPropertyChanged(nameof(OrderPanelColumnSpan));
		}
	}

	public int OrderMapRow => IsStacked ? 1 : 0;
	public int OrderMapColumn => 0;
	public int OrderMapRowSpan => IsStacked ? 1 : 2;
	public int OrderMapColumnSpan => IsStacked ? 2 : 1;
	public int OrderPanelColumn => IsStacked ? 0 : 1;
	public int OrderPanelRowSpan => IsStacked ? 1 : 2;
	public int OrderPanelColumnSpan => IsStacked ? 2 : 1;

	public void ApplySize(double width, double height)
	{
		if (width <= 0 || height <= 0)
			return;

		double aspectRatio = width / height;
		bool stacked = IsStacked
			? width < 940 || aspectRatio < 1.25
			: width < 860 || aspectRatio < 1.05;
		IsStacked = stacked;

		double side = width < 620 ? 12 : Math.Max(24, (width - 1180) / 2);
		bool compact = height < 700 || width < 840;
		ContentMargin = new Thickness(side, compact ? 12 : 20);
		ShowSubtitle = width >= 720 && height >= 650;

		bool wide = width >= 1100;
		OrderColumnSpacing = stacked ? 0 : wide ? 20 : 14;
		StackSpacing = compact ? 8 : wide ? 14 : 12;
		CardPadding = new Thickness(compact ? 10 : wide ? 16 : 12);
	}

	public bool ShowOrder => _section == Section.Order;
	public bool ShowProfile => _section == Section.Profile;
	public bool ShowSettings => _section == Section.Settings;

	private void Select(Section section)
	{
		_section = section;
		OnPropertyChanged(nameof(ShowOrder));
		OnPropertyChanged(nameof(ShowProfile));
		OnPropertyChanged(nameof(ShowSettings));

		if (section == Section.Profile)
			_ = Profile.RefreshAsync();
		else if (section == Section.Order)
			_ = Order.LoadActiveTripAsync();
	}
}
