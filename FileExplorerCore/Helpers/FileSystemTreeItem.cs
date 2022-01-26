using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileExplorerCore.Models;

namespace FileExplorerCore.Helpers
{
	public class FileSystemTreeItem : TreeItem<string>
	{
		private static readonly EnumerationOptions options = new()
		{
			IgnoreInaccessible = true,
			RecurseSubdirectories = false,
			AttributesToSkip = FileAttributes.System,
		};

		public FileSystemTreeItem(string path, FileSystemTreeItem parent = null) : base(Path.GetFileName(path), parent: parent)
		{
			if (Directory.Exists(path))
			{
				Query = new FileSystemEnumerable<FileSystemTreeItem>(path, (ref FileSystemEntry x) => new FileSystemTreeItem(x.ToFullPath(), this), options);
			}
		}
	}
}