using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using IhiksTaxi.Domain;
using IhiksTaxi.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IhiksTaxi.UI;

public class MainViewModel : INotifyPropertyChanged
{
	private readonly IhiksTaxiDbContext _db;
	private bool _isBusy;

	public MainViewModel(IhiksTaxiDbContext db)
	{
		_db = db;
		RefreshCommand = new Command(async () => await LoadAsync());
		_ = LoadAsync();
	}

	public ICommand RefreshCommand { get; }

	public ObservableCollection<Tariff> Tariffs { get; } = new();

	private int _driversCount;
	public int DriversCount
	{
		get => _driversCount;
		set => Set(ref _driversCount, value);
	}

	private int _tripsCount;
	public int TripsCount
	{
		get => _tripsCount;
		set => Set(ref _tripsCount, value);
	}

	private string? _statusMessage;
	public string? StatusMessage
	{
		get => _statusMessage;
		set => Set(ref _statusMessage, value);
	}

	public bool IsBusy
	{
		get => _isBusy;
		set => Set(ref _isBusy, value);
	}

	public async Task LoadAsync()
	{
		if (IsBusy)
			return;

		IsBusy = true;
		try
		{
			DriversCount = await _db.Drivers.CountAsync();
			TripsCount = await _db.Trips.CountAsync();

			Tariffs.Clear();
			foreach (var tariff in await _db.Tariffs.OrderBy(t => t.Id).ToListAsync())
				Tariffs.Add(tariff);

			StatusMessage = "Данные загружены из PostgreSQL";
		}
		catch (Exception ex)
		{
			StatusMessage = "Ошибка подключения к БД: " + ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
			return;

		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
