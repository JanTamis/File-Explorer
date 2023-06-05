using Avalonia.Data.Converters;
using System.Globalization;
using Avalonia.Media;
using FileExplorer.Helpers;
using FileExplorer.Interfaces;
using FileExplorer.Models;
using FileExplorer.Core.Interfaces;
using FileExplorer.ViewModels;

namespace FileExplorer.Converters;

public sealed class PathToImageConverter : IValueConverter, IMultiValueConverter, ISingleton<PathToImageConverter>
{
	public static readonly PathToImageConverter Instance = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		switch (value)
		{
			case Task<IImage?> imageTask:
				return new TaskCompletionNotifier<IImage?>(imageTask);
			case IFileItem item when parameter is IItemProvider provider:
				return new TaskCompletionNotifier<IImage?>(provider.GetThumbnailAsync(item, 100, CancellationToken.None));
			case FileModel item:
				return new TaskCompletionNotifier<IImage?>(ThumbnailProvider.GetFileImage(item.TreeItem, 100));
		}

		if (parameter is int size)
		{
			var task = value switch
			{
				// IFileItem model => ThumbnailProvider.GetFileImage(model, size, () => true),
				FileSystemTreeItem treeItem => ThumbnailProvider.GetFileImage(treeItem, size),
				TabItemViewModel model => ThumbnailProvider.GetFileImage(model.CurrentFolder, model.Provider, size),
				string path => ThumbnailProvider.GetFileImage(FileSystemTreeItem.FromPath(path), size),
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

	public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values is [IFileItem item, IItemProvider provider])
		{
			return new TaskCompletionNotifier<IImage?>(provider.GetThumbnailAsync(item, 100, CancellationToken.None));
		}

		return null;
	}
}