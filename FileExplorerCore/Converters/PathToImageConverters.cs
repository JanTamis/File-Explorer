using System;
using Avalonia.Data.Converters;
using FileExplorerCore.Helpers;
using System.Globalization;

// ReSharper disable InvertIf

namespace FileExplorerCore.Converters
{
	public class PathToImageConverter : IValueConverter
	{

		public PathToImageConverter()
		{
			if (!OperatingSystem.IsWindows())
			{
				
			}
		}

		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			return ThumbnailProvider.GetFileImage(value as string);
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		
	}
}