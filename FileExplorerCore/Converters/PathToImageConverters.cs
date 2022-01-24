using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using FileExplorerCore.Helpers;
using FileTypeAndIcon;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

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