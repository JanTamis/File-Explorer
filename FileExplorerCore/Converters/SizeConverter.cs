using System;
using Avalonia.Data.Converters;
using FileExplorerCore.Models;
using System.Globalization;
using System.IO;
using Humanizer;

namespace FileExplorerCore.Converters
{
	public class SizeConverter : IValueConverter
	{
		public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is FileModel model)
			{
				if (model.Size == -1)
				{
					return String.Empty;
				}

				return model.Size.Bytes().ToString();
			}
			else if (value is long size)
			{
				if (size == -1)
				{
					return String.Empty;
				}

				return size.Bytes().ToString();
			}
			else if (value is string path && File.Exists(path))
			{
				var fileSize = new FileInfo(path).Length;

				return fileSize.Bytes().ToString();
			}

			return String.Empty;
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			return String.Empty;
		}
	}
}
