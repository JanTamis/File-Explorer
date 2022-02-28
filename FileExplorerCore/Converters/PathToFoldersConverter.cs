using System;
using System.Collections.Generic;
using Avalonia.Data.Converters;
using FileExplorerCore.Models;
using System.Globalization;
using System.Linq;
using FileExplorerCore.Helpers;
using static FileExplorerCore.Helpers.SpanSplitExtensions;

namespace FileExplorerCore.Converters
{
	public class PathToFoldersConverter : IValueConverter
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
			Enumerable1<char> enumerable = new();

			if (OperatingSystem.IsMacOS())
			{
				item = new FileSystemTreeItem(path.AsSpan().Slice(0, 1), true);
				enumerable = new Enumerable1<char>(path.AsSpan(1), PathHelper.DirectorySeparator);
			}
			else if (OperatingSystem.IsWindows())
			{
				item = new FileSystemTreeItem(path.AsSpan().Slice(0, 3), true);
				enumerable = new Enumerable1<char>(path.AsSpan(3), PathHelper.DirectorySeparator);
			}

			yield return new FolderModel(item);

			foreach (var subPath in path[(OperatingSystem.IsWindows() ? 3 : 1)..].Split(PathHelper.DirectorySeparator))
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
}
