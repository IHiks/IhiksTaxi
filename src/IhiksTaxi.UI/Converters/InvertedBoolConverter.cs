using System.Globalization;

namespace IhiksTaxi.UI.Converters;

// для кнопки «Оформить заказ»: активна, пока не идёт загрузка
public class InvertedBoolConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is bool flag ? !flag : value!;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is bool flag ? !flag : value!;
}
