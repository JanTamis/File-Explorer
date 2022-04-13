using System;
using Avalonia.Data.Converters;
using FileExplorerCore.Models;
using System.Globalization;
using System.IO;
using Humanizer;

namespace FileExplorerCore.Converters;

public class SizeConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		switch (value)
		{
			case FileModel { Size: < 0 }:
				return String.Empty;
				
			case FileModel model:
				return model.Size.Bytes().ToString();
				
			case long and < 0:
				return String.Empty;
				
			case long size:
				return size.Bytes().ToString();
				
			case string path when File.Exists(path):
			{
				var fileSize = new FileInfo(path).Length;

				return fileSize.Bytes().ToString();
			}
			default:
				return String.Empty;
		}
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return String.Empty;
	}
}