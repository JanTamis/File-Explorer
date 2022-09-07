using Avalonia.Data.Converters;
using System.Globalization;
using Avalonia.Media;
using FileExplorer.Helpers;
using FileExplorer.Interfaces;
using FileExplorer.Models;
using FileExplorer.Core.Interfaces;

namespace FileExplorer.Converters;

public class PathToImageConverter : IValueConverter, ISingleton<PathToImageConverter>
{
	public static PathToImageConverter Instance = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is Task<IImage?> imageTask)
		{
			return new TaskCompletionNotifier<IImage?>(imageTask);
		}

		if (value is IFileItem item && parameter is IItemProvider provider)
		{
			return new TaskCompletionNotifier<IImage?>(provider.GetThumbnailAsync(item, 100, CancellationToken.None));
		}

		if (parameter is int size)
		{
			var task = value switch
			{
				//IFileItem model => ThumbnailProvider.GetFileImage(model, size, () => true),
				FileSystemTreeItem treeItem => ThumbnailProvider.GetFileImage(treeItem, size),
				string path => ThumbnailProvider.GetFileImage(PathHelper.FromPath(path), size),
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