using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace FileExplorerCore.Helpers
{
	public class FileSystemTreeItem : TreeItem<string>
	{
		static EnumerationOptions options = new EnumerationOptions
		{
			IgnoreInaccessible = true,
			RecurseSubdirectories = false,
			AttributesToSkip = FileAttributes.System
		};

		public FileSystemTreeItem(string path, FileSystemTreeItem parent) : base(Path.GetFileName(path), parent: parent)
		{
			if (Directory.Exists(path))
			{
				Children.AddRange(Directory.EnumerateFileSystemEntries(path, "*", options).Select(s => new FileSystemTreeItem(s, this)));
			}
		}

		public FileSystemTreeItem(string value, IEnumerable<TreeItem<string>>? children = null, TreeItem<string>? parent = null) : base(value, children, parent)
		{
		}
	}
}
