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
