using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using FileExplorer.Models;

namespace FileExplorer.Converters;

public class IsHiddenConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is FileSystemTreeItem item)
		{
			var attributes = item.GetPath(path => File.GetAttributes(path.ToString()));

			return attributes.HasFlag(FileAttributes.Hidden)
				? 0.75
				: 1;
		}

		return 1;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}