using ReactiveUI;
using System.Collections.Generic;
using System.Linq;

namespace FileExplorerCore.Models
{
	public class FolderModel : ReactiveObject
	{
		public FileSystemTreeItem TreeItem { get; }

		public string Name => TreeItem.Value;

		public IEnumerable<FolderModel> SubFolders => TreeItem
			.EnumerateChildren(0)
			.Where(w => w.IsFolder)
			.OrderBy(o => o.Value)
			.Select(s => new FolderModel(s));

		public FolderModel(FileSystemTreeItem item)
		{
			TreeItem = item;
		}

		public override int GetHashCode()
		{
			return TreeItem.GetHashCode();
		}

		public override string ToString()
		{
			return Name;
		}
	}
}