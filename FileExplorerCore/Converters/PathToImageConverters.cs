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
			return value switch
			{
				FileSystemTreeItem treeItem => ThumbnailProvider.GetFileImage(treeItem),
				string str => ThumbnailProvider.GetFileImage(str),
				_ => null,
			};
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}