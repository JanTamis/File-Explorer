using Avalonia.Data.Converters;
using System.Globalization;
using System.IO;
using FileExplorer.Models;

namespace FileExplorer.Converters;

public class FileNameConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		switch (value)
		{
			case string path when Directory.Exists(path):
			{
				var name = Path.GetFileName(path);

				return String.IsNullOrEmpty(name) ? path : Path.GetFileName(path);
			}
			case "":
				return "Quick Start";
			case string path:
				return Path.GetFileName(path).Split('.')[^2];
			case FileModel model:
				return model.Name;
			case FileSystemTreeItem treeItem:
				return treeItem.Value;
			case null:
				return "Quick Start";
			default:
				return String.Empty;
		}
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return String.Empty;
	}
}