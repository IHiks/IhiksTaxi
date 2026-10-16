namespace IhiksTaxi.Application;

public sealed class PassengerAccount
{
	public int Id { get; init; }
	public string FullName { get; init; } = null!;
	public string? PhoneNormalized { get; init; }
	public string? PasswordHash { get; init; }
}
