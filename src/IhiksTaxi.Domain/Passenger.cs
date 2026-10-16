namespace IhiksTaxi.Domain;

public class Passenger
{
	public int Id { get; set; }
	public string FullName { get; set; } = null!;
	public string Phone { get; set; } = null!;
	// только цифры номера, по нему ищем и проверяем уникальность
	public string? PhoneNormalized { get; set; }
	public string? Email { get; set; }
	// хеш с солью, открытый текст пароля не храним
	public string? PasswordHash { get; set; }

	public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
