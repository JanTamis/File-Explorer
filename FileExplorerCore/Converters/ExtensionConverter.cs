using System;
using Avalonia.Data.Converters;
using FileExplorerCore.Models;
using System.Globalization;
using System.IO;
using System.Threading;
using Humanizer;

namespace FileExplorerCore.Converters
{
	public class ExtensionConverter : IValueConverter
	{
		public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			return value switch
			{
				FileModel model when OperatingSystem.IsWindows() && !model.IsFolder => model.ExtensionName ??= model.TreeItem.GetPath(path => NativeMethods.GetShellFileType(path)),
				FileModel model => !model.IsFolder
					? model.TreeItem.GetPath(path =>
						{
							var extension = Path.GetExtension(path);

							if (extension.Length > 1)
							{
								return String.Concat(extension[1..], " file");
							}

							return "File";
						})
					: "System folder",
				_ => String.Empty,
			};
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			return String.Empty;
		}
	}
}