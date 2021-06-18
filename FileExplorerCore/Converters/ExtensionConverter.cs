using Avalonia.Data.Converters;
using FileExplorerCore.Models;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace FileExplorerCore.Converters
{
	public class ExtensionConverter : IValueConverter
	{
		readonly static bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is FileModel model)
			{
				if (IsWindows)
				{
					return model.ExtensionName ??= NativeMethods.GetShellFileType(model.Path);
				}
				else
				{
					if (File.Exists(model.Path))
					{
						return Path.GetExtension(model.Path);
					}
					else
					{
						return "System Folder";
					}
				}
			}

			return String.Empty;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return String.Empty;
		}
	}
}
