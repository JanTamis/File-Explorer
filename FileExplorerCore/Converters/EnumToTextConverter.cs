using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FileExplorerCore.Interfaces;
using Humanizer;

namespace FileExplorerCore.Converters;

public class EnumToTextConverter : IValueConverter, ISingleton<EnumToTextConverter>
{
	public static readonly EnumToTextConverter Instance = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value?.ToString().Humanize() ?? String.Empty;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}