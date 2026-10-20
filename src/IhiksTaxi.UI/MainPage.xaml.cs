using System;
using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;

namespace IhiksTaxi.UI;

public partial class MainPage : ContentPage
{
	private readonly MainViewModel _model;

	// кнопка навигации, над которой сейчас курсор; null — ни над одной
	private Button? _navHovered;

	public MainPage(MainViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		_model = viewModel;
		SizeChanged += (_, _) => viewModel.ApplySize(Width, Height);
		MapWebView.SizeChanged += (_, _) => UpdateMapClip();

		// карта нарисована в WebView, поэтому о смене темы ей сообщаем сами
		MapWebView.Navigated += (_, _) => ApplyMapTheme();

		TieNav(NavSettingsButton);
		TieNav(NavOrderButton);
		TieNav(NavProfileButton);
		PaintNav();

		Loaded += (_, _) =>
		{
			WindowChrome.Attach(AppHeader, UpdateMaximizeGlyph);
			WindowChrome.TieWindowButtons(AppHeader, MinimizeButton, MaximizeButton, CloseButton);
			ThemeService.Changed += ApplyMapTheme;
			ThemeService.Changed += PaintNav;
			_model.PropertyChanged += OnModelPropertyChanged;
			ApplyMapTheme();
			PaintNav();
		};

		Unloaded += (_, _) =>
		{
			ThemeService.Changed -= ApplyMapTheme;
			ThemeService.Changed -= PaintNav;
			_model.PropertyChanged -= OnModelPropertyChanged;
			WindowChrome.UntieWindowButtons(AppHeader);
		};
	}

	// Навигацию внизу красит страница, а не стили: состояния и триггеры MAUI
	// не снимали цвет после щелчка, и неактивные кнопки оставались с цветом
	// активной вкладки. Наведение тоже наше — по событиям указателя.
	private void TieNav(Button button)
	{
		var pointer = new PointerGestureRecognizer();
		pointer.PointerEntered += (_, _) =>
		{
			_navHovered = button;
			PaintNav();
		};
		pointer.PointerExited += (_, _) =>
		{
			if (_navHovered == button)
				_navHovered = null;
			PaintNav();
		};
		button.GestureRecognizers.Add(pointer);
	}

	private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(MainViewModel.IsSettingsSelected)
			or nameof(MainViewModel.IsOrderSelected)
			or nameof(MainViewModel.IsProfileSelected))
			PaintNav();
	}

	private void PaintNav()
	{
		PaintNavButton(NavSettingsButton, _model.IsSettingsSelected);
		PaintNavButton(NavOrderButton, _model.IsOrderSelected);
		PaintNavButton(NavProfileButton, _model.IsProfileSelected);
	}

	private void PaintNavButton(Button button, bool selected)
	{
		bool dark = ThemeService.IsDark;
		bool hovered = _navHovered == button && !selected;

		if (selected)
		{
			button.BackgroundColor = NavColor(dark ? "SurfaceWarmDark" : "AccentLight");
			button.TextColor = NavColor(dark ? "AccentLight" : "TextPrimary");
			button.FontAttributes = FontAttributes.Bold;
		}
		else if (hovered)
		{
			button.BackgroundColor = NavColor(dark ? "SurfaceMutedDark" : "SurfaceMuted");
			button.TextColor = NavColor(dark ? "TextPrimaryDark" : "TextPrimary");
			button.FontAttributes = FontAttributes.None;
		}
		else
		{
			button.BackgroundColor = NavColor(dark ? "HeaderBackgroundDark" : "HeaderBackground");
			button.TextColor = NavColor(dark ? "MutedTextDark" : "MutedText");
			button.FontAttributes = FontAttributes.None;
		}
	}

	private static Color NavColor(string key)
		=> Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) is true && value is Color color
			? color
			: Colors.Transparent;

	// системные кнопки окна заменены своими, поэтому за разворотом следим сами
	private void UpdateMaximizeGlyph(bool maximized)
		=> MaximizeButton.Text = maximized ? "\uE923" : "\uE922";

	private void OnMinimizeClicked(object? sender, EventArgs e) => WindowChrome.Minimize();

	private void OnMaximizeClicked(object? sender, EventArgs e) => WindowChrome.ToggleMaximize();

	private void OnCloseClicked(object? sender, EventArgs e) => WindowChrome.Close();

	// Потянуть окно за шапку. Шапку рисует страница, поэтому система про неё
	// ничего не знает и перетаскивание тоже наше.
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

	// Двойной щелчок по шапке разворачивает окно, но по кнопкам окна — нет.
	private void OnHeaderDoubleTapped(object? sender, TappedEventArgs e)
	{
		var point = e.GetPosition(AppHeader);
		if (point is null || point.Value.X >= AppHeader.Width - WindowChrome.ButtonsWidth)
			return;

		WindowChrome.ToggleMaximize();
	}

	private async void ApplyMapTheme()
	{
		try
		{
			var dark = ThemeService.IsDark ? "true" : "false";
			await MapWebView.EvaluateJavaScriptAsync($"window.ihiksSetTheme && window.ihiksSetTheme({dark})");
		}
		catch (Exception ex)
		{
			// страница карты могла ещё не загрузиться — тему применит следующая попытка
			AppLog.Error("Не удалось переключить тему карты", ex);
		}
	}

	private void UpdateMapClip()
	{
		if (MapWebView.Width <= 0 || MapWebView.Height <= 0)
			return;

		MapWebView.Clip = new RoundRectangleGeometry
		{
			CornerRadius = new CornerRadius(12),
			Rect = new Rect(0, 0, MapWebView.Width, MapWebView.Height),
		};
	}
}
