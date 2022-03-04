using Avalonia.Media;
using FileExplorerCore.Helpers;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileExplorerCore.Models
{
	public class FolderModel : ReactiveObject
	{
		public FileSystemTreeItem TreeItem { get; }

		public string Name => TreeItem.Value;

		public Task<IImage?> Image => ThumbnailProvider.GetFileImage(TreeItem, 24);

		public IEnumerable<FolderModel> SubFolders => TreeItem
			.EnumerateChildren(0)
			.Where(w => w.IsFolder)
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