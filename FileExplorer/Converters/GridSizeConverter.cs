using Avalonia.Data.Converters;
using FileExplorer.Interfaces;
using System.Globalization;

namespace FileExplorer.Converters
{
	public sealed class GridSizeConverter : IValueConverter, ISingleton<GridSizeConverter>
	{
		public static readonly GridSizeConverter Instance = new();

		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is double width && !Double.IsNaN(width) && Int32.TryParse(parameter as string, out var size))
			{
				return (int)(width / size);
			}

			return 1;
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}