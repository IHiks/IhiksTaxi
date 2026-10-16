using System;
using System.Windows.Input;
using IhiksTaxi.Application;

namespace IhiksTaxi.UI;

public class TariffOption : ObservableObject
{
	public TariffOption(TariffInfo info, Action<TariffOption> select)
	{
		Info = info;
		DisplayName = info.Name.ToUpperInvariant();
		SelectCommand = new Command(() => select(this));
	}

	public TariffInfo Info { get; }
	public int Id => Info.Id;
	public string DisplayName { get; }
	public ICommand SelectCommand { get; }

	private bool _isSelected;
	public bool IsSelected
	{
		get => _isSelected;
		set => Set(ref _isSelected, value);
	}
}
