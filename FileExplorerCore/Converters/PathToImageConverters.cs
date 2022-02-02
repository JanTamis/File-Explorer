using System;
using Avalonia.Data.Converters;
using FileExplorerCore.Helpers;
using System.Globalization;

namespace FileExplorerCore.Converters
{
	public class PathToImageConverter : IValueConverter
	{
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is FileSystemTreeItem treeItem)
			{
				return ThumbnailProvider.GetFileImage(treeItem);
			}
			if (value is string str)
			{
				return ThumbnailProvider.GetFileImage(str);
			}
			
			return null;
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}