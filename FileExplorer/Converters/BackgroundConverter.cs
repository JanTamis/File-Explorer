using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace FileExplorer.Converters;

public sealed class BackgroundConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is string text)
		{
			var hash = text.GetHashCode();
			var rng = new Random(hash);

			var red = (byte)rng.Next(100, Byte.MaxValue);
			var green = (byte)rng.Next(100, Byte.MaxValue);
			var blue = (byte)rng.Next(100, Byte.MaxValue);

			return new SolidColorBrush(new Color(255, red, green, blue)).ToImmutable();
		}

		return Brushes.Transparent;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return String.Empty;
	}
}