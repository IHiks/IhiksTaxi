using System.Globalization;

namespace IhiksTaxi.UI.Converters;

// для кнопки "Обновить": активна, пока не идёт загрузка
public class InvertedBoolConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		=> value is bool b ? !b : value;

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> value is bool b ? !b : value;
}