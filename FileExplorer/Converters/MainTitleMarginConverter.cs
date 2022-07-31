using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace FileExplorer.Converters;

public class MainTitleMarginConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return OperatingSystem.IsMacOS()
			? new Thickness(0, 30, 0, 0)
			: new Thickness(0);
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}