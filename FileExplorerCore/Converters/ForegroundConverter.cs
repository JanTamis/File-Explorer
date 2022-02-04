using System;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace FileExplorerCore.Converters
{
	public class ForegroundConverter : IValueConverter
	{
		public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is SolidColorBrush brush)
			{
				var color = brush.Color;
				var y = 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;

				return y > 127 ? Brushes.Black : Brushes.White;
			}

			return Brushes.Black;
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			return String.Empty;
		}
	}
}