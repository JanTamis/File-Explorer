﻿using System;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FileExplorerCore.Converters
{
	public class MinSizeConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			//if (value is double width && Double.TryParse(parameter.ToString(), out var minSize))
			//{
			//	return width > minSize;
			//}

			return true;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}