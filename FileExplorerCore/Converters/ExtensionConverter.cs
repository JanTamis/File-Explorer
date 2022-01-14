using System;
using Avalonia.Data.Converters;
using FileExplorerCore.Models;
using System.Globalization;
using System.IO;

namespace FileExplorerCore.Converters
{
	public class ExtensionConverter : IValueConverter
	{
		private static readonly bool IsWindows = OperatingSystem.IsWindows();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value switch
			{
				FileModel model when IsWindows => model.ExtensionName ??= NativeMethods.GetShellFileType(model.Path),
				FileModel model => !model.IsFolder ? Path.GetExtension(model.Path) : "System Folder",
				string path => NativeMethods.GetShellFileType(path),
				_ => String.Empty
			};
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return String.Empty;
		}
	}
}
