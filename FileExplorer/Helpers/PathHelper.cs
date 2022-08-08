using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using FileExplorer.Models;

namespace FileExplorer.Helpers;

public static class PathHelper
{
	public static char DirectorySeparator { get; }

	static PathHelper()
	{
		DirectorySeparator = OperatingSystem.IsWindows()
			? '\\'
			: '/';
	}

	public static FileSystemTreeItem? FromPath([NotNullIfNotNull("path")]string? path)
	{
		if (path is null)
		{
			return null;
		}

		var directory = new DirectoryInfo(path);

		var result = new FileSystemTreeItem(directory.Name, true);
		var temp = result;

		while (directory.Parent is not null)
		{
			directory = directory.Parent;
			temp.Parent = new FileSystemTreeItem(directory.Name, true);
			temp = temp.Parent;
		}

		return result;
	}
}