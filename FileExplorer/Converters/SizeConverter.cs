﻿using Avalonia.Data.Converters;
using System.Globalization;
using System.IO;
using FileExplorer.Interfaces;
using FileExplorer.Models;
using Humanizer;

namespace FileExplorer.Converters;

public sealed class SizeConverter : IValueConverter, ISingleton<SizeConverter>
{
	public static SizeConverter Instance { get; } = new();
	
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		switch (value)
		{
			case FileModel { Size: < 0, }:
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