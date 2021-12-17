using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace FileExplorerCore.Converters
{
	public class ImageTransformConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool needsTranslation && needsTranslation)
			{
				return new ScaleTransform(1, -1);
			}

			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}
