using System.Globalization;
using Avalonia.Data.Converters;
using FileExplorer.Interfaces;
using Humanizer;
using Humanizer.Localisation;

namespace FileExplorer.Converters;

public class TimeSpanToTextConverter : IValueConverter, ISingleton<TimeSpanToTextConverter>
{
	public static TimeSpanToTextConverter Instance { get; } = new();
	
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is TimeSpan timeSpan && timeSpan != TimeSpan.MaxValue && timeSpan != TimeSpan.MinValue && timeSpan != TimeSpan.Zero)
		{
			return timeSpan.Humanize(3, minUnit: TimeUnit.Second);
		}

		return String.Empty;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}