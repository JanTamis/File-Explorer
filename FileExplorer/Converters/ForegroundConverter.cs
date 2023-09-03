using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace FileExplorer.Converters;

public sealed class ForegroundConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value switch
		{
			SolidColorBrush { Color: var brushColor, } => GetForeground(brushColor),
			Color color                                => GetForeground(color),
			_                                          => Brushes.Black
		};
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return String.Empty;
	}

	private static IImmutableSolidColorBrush GetForeground(Color color)
	{
		var y = 0.2126 * color.R + 
		        0.7152 * color.G + 
		        0.0722 * color.B;

		return y > 127
			? Brushes.Black
			: Brushes.White;
	}
}