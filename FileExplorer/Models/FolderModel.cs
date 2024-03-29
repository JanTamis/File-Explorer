﻿using Avalonia.Media;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;

namespace FileExplorer.Models;

public sealed class FolderModel : IPathSegment
{
	private readonly string? _name;
	private readonly IEnumerable<IPathSegment>? _folders;

	public FileSystemTreeItem? TreeItem { get; }

	public string Name => _name ?? TreeItem?.Value ?? String.Empty;

	public bool HasItems => TreeItem?.HasFolders ?? false;

	public Task<IImage?> Image => ThumbnailProvider.GetFileImage(TreeItem, 24);

	public IEnumerable<IPathSegment> SubSegments => _folders ?? TreeItem?
		.EnumerateChildren()
		.Where(w => w.IsFolder)
		.Select(s => new FolderModel(s)) ?? Enumerable.Empty<IPathSegment>();

	public FolderModel(FileSystemTreeItem item)
	{
		TreeItem = item;
	}

	public FolderModel(ReadOnlySpan<char> name, IEnumerable<FolderModel> children)
	{
		TreeItem = null;
		_name = name.ToString();
		_folders = children;
	}

	public FolderModel(FileSystemTreeItem item, string name, IEnumerable<FolderModel>? children)
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