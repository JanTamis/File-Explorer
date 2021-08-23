using Avalonia.Data.Converters;
using FileExplorerCore.Models;
using System.Globalization;

namespace FileExplorerCore.Converters
{
	public class FileNameConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is string path)
			{
				if (Directory.Exists(path))
				{
					var name = Path.GetFileName(path);

					if (String.IsNullOrEmpty(name))
					{
						return path;
					}
					else
					{
						return Path.GetFileName(path);
					}
				}
				else if (path is "")
				{
					return "Quick Start";
				}
				else
				{
					return Path.GetFileName(path).Split('.')[^2];
				}
			}
			else if (value is FileModel model)
			{
				return model.Name;
			}

			return String.Empty;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return String.Empty;
		}
	}
}
