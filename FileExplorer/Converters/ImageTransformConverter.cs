using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace FileExplorer.Converters;

public sealed class ImageTransformConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is true 
			? new ScaleTransform(1, -1) 
			: null;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return String.Empty;
	}
}