namespace IhiksTaxi.UI;

public partial class LoginPage : ContentPage
{
	public LoginPage(AuthViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		SizeChanged += (_, _) => CenterForm();
		Scroller.SizeChanged += (_, _) => CenterForm();
		FormContent.SizeChanged += (_, _) => CenterForm();
		Loaded += (_, _) => WindowChrome.Attach(AppHeader, UpdateMaximizeGlyph);
	}

	// системные кнопки окна заменены своими, поэтому за разворотом следим сами
	private void UpdateMaximizeGlyph(bool maximized)
		=> MaximizeButton.Text = maximized ? "\uE923" : "\uE922";

	private void OnMinimizeClicked(object? sender, EventArgs e) => WindowChrome.Minimize();

	private void OnMaximizeClicked(object? sender, EventArgs e) => WindowChrome.ToggleMaximize();

	private void OnCloseClicked(object? sender, EventArgs e) => WindowChrome.Close();

	// потянуть окно за шапку — как в MainPage: шапку рисует страница
	private void OnHeaderPanUpdated(object? sender, PanUpdatedEventArgs e)
	{
		switch (e.StatusType)
		{
			case GestureStatus.Started:
				WindowChrome.DragStart();
				break;
			case GestureStatus.Running:
				WindowChrome.DragMove();
				break;
		}
	}

	// двойной щелчок по шапке разворачивает окно, но по кнопкам окна — нет
	private void OnHeaderDoubleTapped(object? sender, TappedEventArgs e)
	{
		var point = e.GetPosition(AppHeader);
		if (point is null || point.Value.X >= AppHeader.Width - WindowChrome.ButtonsWidth)
			return;

		WindowChrome.ToggleMaximize();
	}

	// форма входа короткая: при достатке места держим её по центру, при нехватке — прокрутка
	private void CenterForm()
	{
		if (Scroller.Height <= 0 || FormContent.Height <= 0)
			return;

		double extra = Scroller.Height - FormContent.Height - 64;
		double pad = extra > 0 ? Math.Min(extra / 2, 160) : 0;
		FormContent.Padding = new Thickness(24, 32 + pad, 24, 32 + pad);
	}
}
