namespace IhiksTaxi.UI;

public partial class MainPage : ContentPage
{
	public MainPage(MainViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		SizeChanged += (_, _) => viewModel.ApplyWidth(Width);
	}
}
