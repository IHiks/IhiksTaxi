using IhiksTaxi.Application;

namespace IhiksTaxi.Application.Tests;

public class AuthServiceTests
{
	private static AuthService CreateService(out FakeTaxiStore store)
	{
		store = new FakeTaxiStore();
		return new AuthService(store);
	}

	[Test]
	public async Task Register_creates_account_and_stores_only_a_hash()
	{
		var auth = CreateService(out var store);

		var passenger = await auth.RegisterAsync("Иван Петров", "8 900 123-45-67", "secret123", "secret123");

		Assert.Greater(passenger.Id, 0);
		Assert.IsTrue(await store.PhoneExistsAsync("79001234567"));
	}

	[Test]
	public async Task Register_rejects_a_phone_that_is_already_used()
	{
		var auth = CreateService(out _);
		await auth.RegisterAsync("Иван Петров", "89001234567", "secret123", "secret123");

		Assert.ThrowsAsync<InvalidOperationException>(() =>
			auth.RegisterAsync("Пётр Иванов", "+7 900 123-45-67", "secret123", "secret123"));
	}

	[Test]
	public async Task Login_accepts_the_correct_password()
	{
		var auth = CreateService(out _);
		await auth.RegisterAsync("Иван Петров", "89001234567", "secret123", "secret123");

		var passenger = await auth.LoginAsync("+7 900 123-45-67", "secret123");

		Assert.AreEqual("Иван Петров", passenger.FullName);
	}

	[Test]
	public async Task Login_rejects_a_wrong_password()
	{
		var auth = CreateService(out _);
		await auth.RegisterAsync("Иван Петров", "89001234567", "secret123", "secret123");

		Assert.ThrowsAsync<InvalidOperationException>(() =>
			auth.LoginAsync("89001234567", "wrong"));
	}
}
