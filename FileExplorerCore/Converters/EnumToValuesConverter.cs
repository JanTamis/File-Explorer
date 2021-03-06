using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FileExplorerCore.Converters;

public class EnumToValuesConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (parameter is Type type)
		{
			return Enum.GetValues(type);
		}

		return null;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}