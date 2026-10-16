using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;

namespace IhiksTaxi.UI;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly IServiceProvider _services;
	private readonly SessionService _session;

	public App(IServiceProvider services, SessionService session)
	{
		InitializeComponent();
		_services = services;
		_session = session;

		AppDomain.CurrentDomain.UnhandledException += (_, e) =>
			AppLog.Error("Необработанное исключение приложения", e.ExceptionObject as Exception);

		TaskScheduler.UnobservedTaskException += (_, e) =>
		{
			AppLog.Error("Необработанное исключение задачи", e.Exception);
			e.SetObserved();
		};

		AppLog.Info("Приложение запущено");
		_session.Changed += ShowRootPage;
		ShowRootPage();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = base.CreateWindow(activationState);
		window.Title = "IHIKS Taxi";
		window.Width = 720;
		window.Height = 960;
		window.MinimumWidth = 560;
		window.MinimumHeight = 640;
		return window;
	}

	private void ShowRootPage()
	{
		try
		{
			MainPage = _session.IsSignedIn
				? _services.GetRequiredService<MainPage>()
				: _services.GetRequiredService<LoginPage>();
		}
		catch (Exception ex)
		{
			AppLog.Error("Не удалось открыть стартовую страницу", ex);
		}
	}
}
