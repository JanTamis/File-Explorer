using System;
using Avalonia.Data.Converters;
using FileExplorerCore.Helpers;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Media;
using FileExplorerCore.Models;

namespace FileExplorerCore.Converters
{
	public class PathToImageConverter : IValueConverter
	{
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (parameter is int size)
			{
				var task = value switch
				{
					FileModel { IsVisible: true } model => ThumbnailProvider.GetFileImage(model.TreeItem, size),
					FileSystemTreeItem treeItem => ThumbnailProvider.GetFileImage(treeItem, size),
					_ => null,
				};

				if (task is not null)
				{
					return new TaskCompletionNotifier<IImage?>(task);
				}
			}

			return null;
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}