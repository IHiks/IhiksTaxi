using System;
using Microsoft.Maui.Controls.Shapes;

namespace IhiksTaxi.UI;

public partial class MainPage : ContentPage
{
	public MainPage(MainViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		SizeChanged += (_, _) => viewModel.ApplySize(Width, Height);
		MapWebView.SizeChanged += (_, _) => UpdateMapClip();

		// карта нарисована в WebView, поэтому о смене темы ей сообщаем сами
		MapWebView.Navigated += (_, _) => ApplyMapTheme();

		Loaded += (_, _) =>
		{
			WindowChrome.Attach(AppHeader, UpdateMaximizeGlyph);
			WindowChrome.TieWindowButtons(AppHeader, MinimizeButton, MaximizeButton, CloseButton);
			ThemeService.Changed += ApplyMapTheme;
			ApplyMapTheme();
		};

		Unloaded += (_, _) =>
		{
			ThemeService.Changed -= ApplyMapTheme;
			WindowChrome.UntieWindowButtons(AppHeader);
		};
	}

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
