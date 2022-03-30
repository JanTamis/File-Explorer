using Avalonia.Media;
using FileExplorerCore.Helpers;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileExplorerCore.Models
{
	public class FolderModel
	{
		private readonly string? _name;
		private readonly IEnumerable<FolderModel>? _folders;

		public FileSystemTreeItem? TreeItem { get; }

		public string Name => _name ?? TreeItem?.Value ?? String.Empty;

		public Task<IImage?> Image => ThumbnailProvider.GetFileImage(TreeItem, 24);

		public IEnumerable<FolderModel> SubFolders => _folders is not null 
			? _folders
			: TreeItem?
				.EnumerateChildren(0)
				.Where(w => w.IsFolder)
				.Select(s => new FolderModel(s)) ?? Enumerable.Empty<FolderModel>();

		public FolderModel(FileSystemTreeItem item)
		{
			TreeItem = item;
		}

		public FolderModel(string name, IEnumerable<FolderModel> children)
		{
			TreeItem = null;
			_name = name;
			_folders = children;
		}

		public FolderModel(FileSystemTreeItem item, string? name, IEnumerable<FolderModel>? children)
		{
			_name = name;
			_folders = children;
			TreeItem = item;
		}

		public override int GetHashCode()
		{
			return TreeItem?.GetHashCode() ?? 0;
		}

		public override string ToString()
		{
			return Name;
		}
	}
}