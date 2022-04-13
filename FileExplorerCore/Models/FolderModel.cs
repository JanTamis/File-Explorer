using Avalonia.Media;
using FileExplorerCore.Helpers;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace FileExplorerCore.Models;

public class FolderModel
{
	private readonly DynamicString? _name;
	private readonly IEnumerable<FolderModel>? _folders;

	public FileSystemTreeItem? TreeItem { get; }

	public DynamicString Name => _name ?? TreeItem?.DynamicString ?? DynamicString.Empty;

	public bool HasFolders => TreeItem?.HasFolders ?? false;

	public Task<IImage?> Image => ThumbnailProvider.GetFileImage(TreeItem, 24);

	public IEnumerable<FolderModel> SubFolders => _folders ?? TreeItem?
		.EnumerateChildren(0)
		.Where(w => w.IsFolder)
		.Select(s => new FolderModel(s)) ?? Enumerable.Empty<FolderModel>();

	public FolderModel(FileSystemTreeItem item)
	{
		TreeItem = item;
	}

	public FolderModel(ReadOnlySpan<char> name, IEnumerable<FolderModel> children)
	{
		TreeItem = null;
		_name = new DynamicString(name);
		_folders = children;
	}

	public FolderModel(FileSystemTreeItem item, ReadOnlySpan<char> name, IEnumerable<FolderModel>? children)
	{
		_name = new DynamicString(name);
		_folders = children;
		TreeItem = item;
	}

	public override int GetHashCode()
	{
		return TreeItem?.GetHashCode() ?? 0;
	}

	public override string ToString()
	{
		return Name.ToString();
	}
}