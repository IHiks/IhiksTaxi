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
		Loaded += (_, _) => WindowChrome.Attach(AppHeader, UpdateMaximizeGlyph);
	}

	// системные кнопки окна заменены своими, поэтому за разворотом следим сами
	private void UpdateMaximizeGlyph(bool maximized)
		=> MaximizeButton.Text = maximized ? "\uE923" : "\uE922";

	private void OnMinimizeClicked(object? sender, EventArgs e) => WindowChrome.Minimize();

	private void OnMaximizeClicked(object? sender, EventArgs e) => WindowChrome.ToggleMaximize();

	private void OnCloseClicked(object? sender, EventArgs e) => WindowChrome.Close();

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
