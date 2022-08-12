using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using FileExplorer.Core.Interfaces;
using FileExplorer.Models;
using FileExplorer.Helpers;
using System.Threading;

namespace FileExplorer.Providers;

public class FileSystemProvider : IItemProvider
{
	public ValueTask<IEnumerable<IFileItem>> GetItemsAsync(string path, string filter, bool recursive, CancellationToken token)
	{
		var treeItem = PathHelper.FromPath(path);

		if (treeItem is null)
		{
			return ValueTask.FromResult(Enumerable.Empty<IFileItem>());
		}

		if (recursive)
		{
			return ValueTask.FromResult(GetFileSystemEntriesRecursive(treeItem, filter).Cast<IFileItem>());
		}

		return ValueTask.FromResult(GetFileSystemEntries(treeItem, filter).Cast<IFileItem>());
	}

	public IEnumerable<IPathSegment> GetPath(string path)
	{
		var treeItem = PathHelper.FromPath(path);

		return treeItem?
			.EnumerateToRoot()
			.Reverse()
			.Select(s => new FolderModel(s)) ?? Enumerable.Empty<FolderModel>();
	}

	private static IEnumerable<FileModel> GetFileSystemEntriesRecursive(FileSystemTreeItem path, string search)
	{
		if (search is "*" or "*.*")
		{
			return path
				.EnumerateChildren((ref FileSystemEntry file) => !file.IsHidden)
				.Select(s => new FileModel(s));
		}

		return path
			.EnumerateChildren((ref FileSystemEntry entry) => !entry.IsHidden)
			.Where(w => FileSystemName.MatchesSimpleExpression(search, w.Value))
			.Select(s => new FileModel(s));
	}

	private static IEnumerable<FileModel> GetFileSystemEntries(FileSystemTreeItem path, string search)
	{
		return path
			.EnumerateChildren((ref FileSystemEntry file) => !file.IsHidden, 0)
			.Where(w => FileSystemName.MatchesSimpleExpression(search, w.Value))
			.Select(s => new FileModel(s));

	}
}