using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using FileExplorer.Models;

namespace FileExplorer.Converters;

public sealed class IsHiddenConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is FileSystemTreeItem item)
		{
			var attributes = item.GetPath(path => File.GetAttributes(path.ToString()));

			if (attributes.HasFlag(FileAttributes.Hidden))
			{
				return 0.75;
			}
		}

		return 1;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}