﻿using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;
using FileExplorer.Interfaces;

namespace FileExplorer.Converters;

public sealed class BoolToBoldConverter : IValueConverter, ISingleton<BoolToBoldConverter>
{
	public static readonly BoolToBoldConverter Instance = new();

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