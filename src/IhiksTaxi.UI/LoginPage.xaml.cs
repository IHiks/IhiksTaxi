namespace IhiksTaxi.UI;

public partial class LoginPage : ContentPage
{
	public LoginPage(AuthViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		SizeChanged += (_, _) => CenterForm();
		FormContent.SizeChanged += (_, _) => CenterForm();
	}

	// форма входа короткая: при достатке места держим её по центру, при нехватке — прокрутка
	private void CenterForm()
	{
		if (Height <= 0 || FormContent.Height <= 0)
			return;

		double extra = Height - FormContent.Height - 64;
		double pad = extra > 0 ? Math.Min(extra / 2, 160) : 0;
		FormContent.Padding = new Thickness(24, 32 + pad, 24, 32 + pad);
	}
}
