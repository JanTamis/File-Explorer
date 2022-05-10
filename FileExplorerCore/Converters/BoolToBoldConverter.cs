using Avalonia.Data.Converters;
using Avalonia.Media;
using FileExplorerCore.Interfaces;
using System;
using System.Globalization;

namespace FileExplorerCore.Converters;

public class BoolToBoldConverter : IValueConverter, ISingleton<BoolToBoldConverter>
{
	public static readonly BoolToBoldConverter Instance = new BoolToBoldConverter();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is true
			? FontWeight.SemiBold
			: FontWeight.Normal;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}