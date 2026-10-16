using System.Security.Cryptography;

namespace IhiksTaxi.Application;

// пароль храним только в виде хеша PBKDF2 (соль + много итераций), открытый текст нигде не сохраняем
public static class PasswordHasher
{
	private const int Iterations = 100_000;
	private const int SaltSize = 16;
	private const int KeySize = 32;

	public static string Hash(string password)
	{
		var salt = RandomNumberGenerator.GetBytes(SaltSize);
		var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
		return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
	}

	public static bool Verify(string password, string? hash)
	{
		if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(hash))
			return false;

		var parts = hash.Split('.');
		if (parts.Length != 3)
			return false;

		try
		{
			var iterations = int.Parse(parts[0]);
			var salt = Convert.FromBase64String(parts[1]);
			var expected = Convert.FromBase64String(parts[2]);
			var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
			return CryptographicOperations.FixedTimeEquals(actual, expected);
		}
		catch (FormatException)
		{
			return false;
		}
	}
}
