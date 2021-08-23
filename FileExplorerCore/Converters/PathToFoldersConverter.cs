using Avalonia.Data.Converters;
using FileExplorerCore.Models;
using System.Globalization;

namespace FileExplorerCore.Converters
{
	public class PathToFoldersConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is string path)
			{
				return GetFolders(path);
			}

			return Enumerable.Empty<FolderModel>();
		}

		public IEnumerable<FolderModel> GetFolders(string path)
		{
			path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

			var names = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

			for (int i = 0; i < names.Length; i++)
			{
				var folderPath = String.Join(Path.DirectorySeparatorChar, new ArraySegment<string>(names, 0, i + 1));
				var name = names[i];

				if (!String.IsNullOrEmpty(folderPath))
				{
					if (i is 0)
					{
						folderPath += Path.DirectorySeparatorChar;
						name += Path.DirectorySeparatorChar;
					}

					yield return new FolderModel(folderPath, name, from directory in Directory.EnumerateDirectories(folderPath, "*", new EnumerationOptions())
																												 select new FolderModel(directory, Path.GetFileName(directory)));
				}
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}
