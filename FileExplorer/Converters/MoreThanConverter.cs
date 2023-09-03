using Avalonia.Data.Converters;
using System.Globalization;
using FileExplorer.Interfaces;

namespace FileExplorer.Converters;

public sealed class MoreThanConverter : IValueConverter, ISingleton<MoreThanConverter>
{
	public static MoreThanConverter Instance { get; } = new();
	
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is int amount && 
		       parameter is string number && 
		       Int32.TryParse(number, out var threshold) &&
		       amount > threshold;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return String.Empty;
	}
}