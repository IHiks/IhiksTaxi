namespace IhiksTaxi.Domain;

public class Passenger
{
	public int Id { get; set; }
	public string FullName { get; set; } = null!;
	public string Phone { get; set; } = null!;
	public string? Email { get; set; }
	// хеш с солью, открытый текст пароля не храним
	public string? PasswordHash { get; set; }

	public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
