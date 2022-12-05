using Avalonia.Data.Converters;
using System.Globalization;
using FileExplorer.Helpers;
using FileExplorer.Models;

namespace FileExplorer.Converters;

public sealed class PathToFoldersConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is string path)
		{
			return GetFolders(path);
		}

		return Enumerable.Empty<FolderModel>();
	}

	public IEnumerable<FolderModel> GetFolders(string path)
	{
		if (OperatingSystem.IsMacOS())
		{
			yield return new FolderModel(new FileSystemTreeItem(PathHelper.DirectorySeparator.ToString(), true));
		}

		FileSystemTreeItem item = null!;

		if (OperatingSystem.IsMacOS())
		{
			item = new FileSystemTreeItem(path.AsSpan(0, 1), true);
		}
		else if (OperatingSystem.IsWindows())
		{
			item = new FileSystemTreeItem(path.AsSpan(0, 3), true);
		}

		yield return new FolderModel(item);

		foreach (var subPath in path.Substring(OperatingSystem.IsWindows() ? 3 : 1).Split(PathHelper.DirectorySeparator))
		{
			item = new FileSystemTreeItem(subPath, true, item);

			yield return new FolderModel(item);
		}
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return String.Empty;
	}
}