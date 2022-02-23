using System;
using FileExplorerCore.Helpers;
using ReactiveUI;
using System.Collections.Generic;
using System.IO.Enumeration;
using System.Linq;
using Microsoft.Toolkit.HighPerformance;

namespace FileExplorerCore.Models
{
	public class FolderModel : ReactiveObject
	{
		public FileSystemTreeItem TreeItem { get; }

		public string Name => TreeItem.Value;

		public IEnumerable<FolderModel> SubFolders => TreeItem
			.EnumerateChildren((ref FileSystemEntry entry) => entry.IsDirectory && !entry.IsHidden, 0)
			.OrderBy(o => o.Value)
			.Select(s => new FolderModel(s));

		public FolderModel(FileSystemTreeItem item)
		{
			TreeItem = item;
		}

		public override int GetHashCode()
		{
			return TreeItem.GetPath(path =>
			{
				var code = new HashCode();

				code.AddBytes(path.AsBytes());

				return code.ToHashCode();
			});
		}

		public override string ToString()
		{
			return Name;
		}
	}
}