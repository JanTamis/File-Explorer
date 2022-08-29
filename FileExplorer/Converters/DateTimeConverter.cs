using Avalonia.Data.Converters;
using Humanizer;
using System.Globalization;

namespace FileExplorer.Converters;

public class DateTimeConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is DateTime dateTime)
		{
			return dateTime.Humanize();
		}

		return String.Empty;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return String.Empty;
	}
}