using System.Threading.Tasks;
using System.Windows.Input;
using IhiksTaxi.Application;

namespace IhiksTaxi.UI;

public class AuthViewModel : ObservableObject
{
	private readonly AuthService _auth;
	private readonly SessionService _session;

	public AuthViewModel(AuthService auth, SessionService session)
	{
		_auth = auth;
		_session = session;
		LoginCommand = new Command(async () => await LoginAsync());
		RegisterCommand = new Command(async () => await RegisterAsync());
		ShowLoginCommand = new Command(() => IsRegisterMode = false);
		ShowRegisterCommand = new Command(() => IsRegisterMode = true);
	}

	public ICommand LoginCommand { get; }
	public ICommand RegisterCommand { get; }
	public ICommand ShowLoginCommand { get; }
	public ICommand ShowRegisterCommand { get; }

	private bool _isRegisterMode;
	public bool IsRegisterMode
	{
		get => _isRegisterMode;
		set
		{
			if (Set(ref _isRegisterMode, value))
			{
				ErrorMessage = null;
				OnPropertyChanged(nameof(ModeTitle));
				OnPropertyChanged(nameof(ActionLabel));
			}
		}
	}

	private string _fullName = string.Empty;
	public string FullName
	{
		get => _fullName;
		set => Set(ref _fullName, value);
	}

	private string _phone = string.Empty;
	public string Phone
	{
		get => _phone;
		set => Set(ref _phone, value);
	}

	private string _password = string.Empty;
	public string Password
	{
		get => _password;
		set => Set(ref _password, value);
	}

	private string _confirmPassword = string.Empty;
	public string ConfirmPassword
	{
		get => _confirmPassword;
		set => Set(ref _confirmPassword, value);
	}

	private bool _isBusy;
	public bool IsBusy
	{
		get => _isBusy;
		set
		{
			if (Set(ref _isBusy, value))
				OnPropertyChanged(nameof(CanSubmit));
		}
	}

	public bool CanSubmit => !IsBusy;

	private string? _errorMessage;
	public string? ErrorMessage
	{
		get => _errorMessage;
		set => Set(ref _errorMessage, value);
	}

	public string ModeTitle => IsRegisterMode ? "Регистрация" : "Вход";
	public string ActionLabel => IsRegisterMode ? "Зарегистрироваться" : "Войти";

	private async Task LoginAsync()
	{
		if (IsBusy)
			return;

		IsBusy = true;
		try
		{
			var passenger = await _auth.LoginAsync(Phone, Password);
			ErrorMessage = null;
			_session.SignIn(passenger.Id);
		}
		catch (Exception ex)
		{
			AppLog.Error("Вход не выполнен", ex);
			ErrorMessage = ErrorText.Describe(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task RegisterAsync()
	{
		if (IsBusy)
			return;

		IsBusy = true;
		try
		{
			var passenger = await _auth.RegisterAsync(FullName, Phone, Password, ConfirmPassword);
			ErrorMessage = null;
			_session.SignIn(passenger.Id);
		}
		catch (Exception ex)
		{
			AppLog.Error("Регистрация не выполнена", ex);
			ErrorMessage = ErrorText.Describe(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}
}
