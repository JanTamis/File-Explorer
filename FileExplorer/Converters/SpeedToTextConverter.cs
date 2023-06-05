using System.Globalization;
using Avalonia.Data.Converters;
using FileExplorer.Interfaces;
using Humanizer.Bytes;

namespace FileExplorer.Converters;

public class SpeedToTextConverter : IValueConverter, ISingleton<SpeedToTextConverter>
{
	public static SpeedToTextConverter Instance = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is long speed)
		{
			return $"{new ByteSize(speed).ToString()}/s";
		}

		return String.Empty;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}