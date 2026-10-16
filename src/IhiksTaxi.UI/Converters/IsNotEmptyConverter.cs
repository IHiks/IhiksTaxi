using System.Globalization;

namespace IhiksTaxi.UI.Converters;

// показываем сообщение только когда оно не пустое
public class IsNotEmptyConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is string text && !string.IsNullOrWhiteSpace(text);

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
