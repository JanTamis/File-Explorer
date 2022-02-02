﻿using System;
using Avalonia.Data.Converters;
using FileExplorerCore.Models;
using System.Globalization;
using System.IO;

namespace FileExplorerCore.Converters
{
	public class ExtensionConverter : IValueConverter
	{

		public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			return value switch
			{
				FileModel model when OperatingSystem.IsWindows() => model.ExtensionName ??= model.TreeItem.GetPath(path => NativeMethods.GetShellFileType(path)),
				FileModel model => !model.IsFolder ? String.Intern(model.TreeItem.GetPath(path => Path.GetExtension(path).ToString())) : "System Folder",
				string path when OperatingSystem.IsWindows() => NativeMethods.GetShellFileType(path),
				_ => String.Empty
			};
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			return String.Empty;
		}
	}
}