using Avalonia.Data.Converters;
using System.Globalization;
using FileExplorer.Interfaces;

namespace FileExplorer.Converters;

public sealed class MoreThanConverter : IValueConverter, ISingleton<MoreThanConverter>
{
	public static MoreThanConverter Instance = new();
	
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is int amount && parameter is string number && Int32.TryParse(number, out var threshold))
		{
			return amount > threshold;
		}

		return false;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return String.Empty;
	}
}