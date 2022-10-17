using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using FileExplorer.Models;

namespace FileExplorer.Helpers;

public static class PathHelper
{
	public static char DirectorySeparator { get; }
	public static byte[] DirectorySeparatorUTF8 { get; }

	static PathHelper()
	{
		DirectorySeparator = OperatingSystem.IsWindows()
			? '\\'
			: '/';

		DirectorySeparatorUTF8 = Encoding.UTF8.GetBytes(DirectorySeparator.ToString());
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