using System;
using System.Collections.Generic;
using Avalonia.Data.Converters;
using FileExplorerCore.Models;
using System.Globalization;
using System.IO;
using System.Linq;
using FileExplorerCore.ViewModels;

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
			var separator = OperatingSystem.IsWindows()
				? '\\'
				: '/';
			
			var names = path.Split(separator, StringSplitOptions.RemoveEmptyEntries);

			if (OperatingSystem.IsMacOS())
			{
				yield return new FolderModel(MainWindowViewModel.Tree.Children[0]);
			}

			for (var i = 0; i < names.Length; i++)
			{
				var folderPath = String.Join(separator, new ArraySegment<string>(names, 0, i + 1));
				var name = names[i];

				if (!String.IsNullOrEmpty(folderPath))
				{
					if (i is 0)
					{
						folderPath += separator;
						name += separator;
					}

					// yield return new FolderModel(folderPath, name, from directory in Directory.EnumerateDirectories(folderPath, "*", new EnumerationOptions())
					// 																							 select new FolderModel(directory, Path.GetFileName(directory)));
				}
			}
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			return String.Empty;
		}
	}
}
