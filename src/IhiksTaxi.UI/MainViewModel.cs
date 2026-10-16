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

	// на широком окне контент остаётся узкой читаемой колонкой по центру, на узком — прижимается к краям
	public void ApplyWidth(double width)
	{
		if (width <= 0)
			return;

		double side = width < 620 ? 12 : Math.Max(24, (width - 620) / 2);
		ContentMargin = new Thickness(side, width < 620 ? 12 : 20);
		ShowSubtitle = width >= 720;
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
