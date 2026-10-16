namespace IhiksTaxi.Application;

public sealed class AuthService
{
	private readonly ITaxiStore _store;

	public AuthService(ITaxiStore store)
	{
		_store = store;
	}

	public async Task<PassengerInfo> RegisterAsync(string fullName, string phone, string password, string confirmPassword,
		CancellationToken ct = default)
	{
		var name = (fullName ?? string.Empty).Trim();

		if (name.Length < 2)
			throw new InvalidOperationException("Укажите имя пассажира.");
		if (name.Length > 120)
			throw new InvalidOperationException("Имя слишком длинное.");

		var normalized = PhoneNumber.Normalize(phone);
		if (normalized.Length < 10)
			throw new InvalidOperationException("Введите корректный номер телефона.");

		if (string.IsNullOrEmpty(password) || password.Length < 6)
			throw new InvalidOperationException("Пароль должен быть не короче 6 символов.");
		if (password != confirmPassword)
			throw new InvalidOperationException("Пароли не совпадают.");

		if (await _store.PhoneExistsAsync(normalized, ct))
			throw new InvalidOperationException("Этот номер телефона уже зарегистрирован.");

		var hash = PasswordHasher.Hash(password);
		var id = await _store.CreatePassengerAsync(name, PhoneNumber.ToStored(normalized), normalized, hash, ct);

		return await _store.GetPassengerAsync(id, ct)
			?? throw new InvalidOperationException("Не удалось создать профиль.");
	}

	public async Task<PassengerInfo> LoginAsync(string phone, string password, CancellationToken ct = default)
	{
		var normalized = PhoneNumber.Normalize(phone);

		if (normalized.Length == 0 || string.IsNullOrEmpty(password))
			throw new InvalidOperationException("Введите номер телефона и пароль.");

		var account = await _store.GetAccountByPhoneAsync(normalized, ct);

		if (account is null || !PasswordHasher.Verify(password, account.PasswordHash))
			throw new InvalidOperationException("Неверный номер телефона или пароль.");

		return await _store.GetPassengerAsync(account.Id, ct)
			?? throw new InvalidOperationException("Профиль не найден.");
	}
}
